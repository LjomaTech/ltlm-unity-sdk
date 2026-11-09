using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using LTLM.SDK.Core;
using LTLM.SDK.Core.Communication;
using LTLM.SDK.Core.Models;
using LTLM.SDK.Core.Storage;
using LTLM.SDK.Core.Security;
using LTLM.SDK.Core.Hardware;
using Newtonsoft.Json;

namespace LTLM.SDK.Unity
{
    /// <summary>
    /// Represents the current state of a license in the LTLM system.
    /// Used by <see cref="LTLMManager.GetLicenseStatus"/> and callbacks.
    /// </summary>
    public enum LicenseStatus
    {
        /// <summary>No license is currently loaded or validated.</summary>
        Unauthenticated,

        /// <summary>License is valid and active.</summary>
        Active,

        /// <summary>License is valid but all concurrent seats are occupied.</summary>
        ValidNoSeat,

        /// <summary>License has passed its expiration date.</summary>
        Expired,

        /// <summary>License is expired but within the configured (server-side) grace period.</summary>
        GracePeriod,

        /// <summary>Offline grace period exceeded; internet connection required.</summary>
        ConnectionRequired,

        /// <summary>Clock manipulation or file tampering detected.</summary>
        Tampered,

        /// <summary>License has been suspended by administrator.</summary>
        Suspended,

        /// <summary>License has been revoked and cannot be used.</summary>
        Revoked,

        /// <summary>License has been terminated; application will exit.</summary>
        Terminated,

        /// <summary>Session was kicked by another device. Must reactivate to continue.</summary>
        Kicked,

        /// <summary>
        /// The server is unreachable but the cached license is valid and the policy allows offline use.
        /// Treat as <see cref="Active"/> for gating; check <see cref="LTLMManager.OfflineGraceRemaining"/> to warn the user.
        /// </summary>
        OfflineGrace
    }

    /// <summary>
    /// The main singleton manager for LTLM (License &amp; Token Management) integration in Unity.
    /// Handles license activation, validation, heartbeats, token consumption, and enforcement.
    ///
    /// <para><b>Quick Start:</b></para>
    /// <code>
    /// LTLMManager.Instance.ActivateLicense("YOUR-LICENSE-KEY",
    ///     (license, status) => Debug.Log($"Activated! Status: {status}"),
    ///     error => Debug.LogError(error));
    ///
    /// // or with async/await
    /// var license = await LTLMManager.Instance.ActivateLicenseAsync("YOUR-LICENSE-KEY");
    /// </code>
    ///
    /// <para><b>Events (all fired on the main thread):</b></para>
    /// <list type="bullet">
    ///   <item><see cref="OnValidationStarted"/> / <see cref="OnValidationCompleted"/> - once per explicit validation call.</item>
    ///   <item><see cref="OnLicenseUpdated"/> - every time fresh license data is applied (validation, heartbeat, consumption, settings).</item>
    ///   <item><see cref="OnLicenseStatusChanged"/> - only when the resolved <see cref="LicenseStatus"/> actually changes.</item>
    ///   <item><see cref="OnSeatStatusChanged"/>, <see cref="OnKicked"/>, <see cref="OnTokensConsumed"/>, <see cref="OnError"/>.</item>
    /// </list>
    /// </summary>
    public class LTLMManager : MonoBehaviour
    {
        /// <summary>Singleton instance. Null until a manager exists in the scene (see <see cref="LTLMBootstrap"/>).</summary>
        public static LTLMManager Instance { get; private set; }

        [Header("Project Configuration")]
        [Tooltip("Your LTLM Project ID. Obtained from the dashboard or Editor tools.")]
        public string projectId;

        [Tooltip("Ed25519 Public Key for signature verification. PEM format.")]
        public string publicKey;

        [Tooltip("AES-256 Secret Key for encryption. 64-character hex string. Keep this secret!")]
        public string secretKey;

        [Header("Settings")]
        [Tooltip("Interval between heartbeat requests in seconds. The server value always overrides this.")]
        public float heartbeatIntervalSeconds = 300f;

        [Tooltip("If true, automatically attempts to load and validate a stored license on Start().")]
        public bool autoValidateOnStart = true;

        [Tooltip("Your application/software version. If empty, uses Application.version.")]
        public string softwareVersion;

        [Tooltip("If true, allows token consumption while offline (queued for sync when online).")]
        public bool allowOfflineTokenConsumption = true;

        [Tooltip("Quit the application when the server reports the license as terminated.")]
        public bool quitOnTerminated = true;

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        private LTLMClient _client;
        private LTLMSettings _settings;
        private LicenseData _activeLicense;
        private LicenseStatus _cachedStatus = LicenseStatus.Unauthenticated;
        private Coroutine _heartbeatRoutine;
        private bool _isValidating;
        private bool _isSyncingTokens;
        private bool _vaultTampered;
        private bool _isInOfflineGrace;
        private DateTime _offlineGraceDeadlineUtc = DateTime.MinValue;
        private string _lastSeatSignature;
        private List<ConsumptionRequest> _pendingConsumptions = new List<ConsumptionRequest>();

        // ------------------------------------------------------------------
        // Public state
        // ------------------------------------------------------------------

        /// <summary>True when the license grants access right now (active, in server grace, or in offline grace).</summary>
        public bool IsAuthenticated =>
            _activeLicense != null &&
            (ComputeStatus() == LicenseStatus.Active ||
             ComputeStatus() == LicenseStatus.GracePeriod ||
             ComputeStatus() == LicenseStatus.OfflineGrace);

        /// <summary>True while a validation/activation request is in progress.</summary>
        public bool IsValidating => _isValidating;

        /// <summary>The currently loaded license data, or null.</summary>
        public LicenseData ActiveLicense => _activeLicense;

        /// <summary>The last error returned by any request (typed). Null after a successful request.</summary>
        public LTLMRequestError LastError { get; private set; }

        /// <summary>True when the SDK is running on cached data because the server could not be reached.</summary>
        public bool IsInOfflineGrace => _isInOfflineGrace;

        /// <summary>Time left in offline grace, or null when online / grace not applicable.</summary>
        public TimeSpan? OfflineGraceRemaining
        {
            get
            {
                if (!_isInOfflineGrace || _offlineGraceDeadlineUtc == DateTime.MinValue) return null;
                var remaining = _offlineGraceDeadlineUtc - SecureClock.GetEffectiveTime(projectId);
                return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
            }
        }

        /// <summary>Returns true if license is valid but waiting for a seat.</summary>
        public bool IsWaitingForSeat => _cachedStatus == LicenseStatus.ValidNoSeat;

        public bool IsSeatsEnabled => _activeLicense != null && (_activeLicense.seatsEnabled ?? false);
        public bool IsTokensEnabled => _activeLicense != null && (_activeLicense.tokensEnabled ?? false);
        public bool IsOfflineEnabled => _activeLicense != null && (_activeLicense.offlineEnabled ?? false);

        /// <summary>Number of token consumptions queued for sync.</summary>
        public int PendingConsumptionCount => _pendingConsumptions.Count;

        /// <summary>The hardware id this device reports to the server.</summary>
        public string DeviceHwid => DeviceID.GetHWID();

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        /// <summary>Fired when an explicit validation/activation starts. Show a loading indicator.</summary>
        public static event Action OnValidationStarted;

        /// <summary>Fired when validation completes: (success, status).</summary>
        public static event Action<bool, LicenseStatus> OnValidationCompleted;

        /// <summary>Fired only when the resolved license status changes.</summary>
        public static event Action<LicenseStatus> OnLicenseStatusChanged;

        /// <summary>Fired every time license data is refreshed. Null when the license was cleared.</summary>
        public static event Action<LicenseData> OnLicenseUpdated;

        /// <summary>Fired after tokens are consumed (optimistic and server-confirmed).</summary>
        public static event Action<LicenseData> OnTokensConsumed;

        /// <summary>Fired when the seat situation changes: (seatStatus, activeSeats, maxSeats).</summary>
        public static event Action<string, int, int> OnSeatStatusChanged;

        /// <summary>Fired when this device is kicked by another device claiming its seat.</summary>
        public static event Action<KickedNotice> OnKicked;

        /// <summary>Fired for every failed request, after the callback error has been delivered.</summary>
        public static event Action<LTLMRequestError> OnError;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _settings = LTLMSettings.Load();
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(secretKey))
            {
                if (_settings != null)
                {
                    if (string.IsNullOrEmpty(projectId)) projectId = _settings.projectId;
                    if (string.IsNullOrEmpty(publicKey)) publicKey = _settings.publicKey;
                    if (string.IsNullOrEmpty(secretKey)) secretKey = _settings.secretKey;
                }
            }

            if (_settings != null) LTLMLog.Verbose = _settings.verboseLogging;

            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(publicKey))
            {
                LTLMLog.Error("SDK is not configured (projectId / publicKey / secretKey missing). Open LTLM -> Project Settings.");
                return;
            }

            MigrateHwidIfNeeded();

            string baseUrl = _settings != null ? _settings.ResolvedBackendUrl : LTLMConstants.BackendUrl;
            try
            {
                _client = new LTLMClient(baseUrl, projectId, secretKey, publicKey)
                {
                    TimeoutSeconds = _settings != null ? _settings.requestTimeoutSeconds : LTLMConstants.DefaultRequestTimeoutSeconds,
                    MaxResponseAgeSeconds = _settings != null ? _settings.maxResponseAgeSeconds : 0
                };
            }
            catch (Exception ex)
            {
                LTLMLog.Error("Failed to initialise crypto client: " + ex.Message);
                return;
            }

            if (string.IsNullOrEmpty(softwareVersion)) softwareVersion = Application.version;

            LoadLocalState();
        }

        private void Start()
        {
            if (autoValidateOnStart && _client != null)
            {
                TryLoadStoredLicense();
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                StopHeartbeat();
                SecureClock.Flush(projectId);
                SavePendingConsumptions();
            }
            else if (_activeLicense != null && _heartbeatRoutine == null && _client != null)
            {
                // Immediate heartbeat on resume: re-claims the seat and syncs queued usage.
                StartHeartbeat(immediate: true);
            }
        }

        private void OnApplicationQuit()
        {
            SecureClock.Flush(projectId);
            SavePendingConsumptions();
            ReleaseSeatOnQuit();
        }

        private void OnDestroy()
        {
            StopHeartbeat();
            if (Instance == this) Instance = null;
        }

        public LTLMClient GetClient() => _client;

        // ------------------------------------------------------------------
        // Local state / vault
        // ------------------------------------------------------------------

        private string Hwid => DeviceID.GetHWID();

        private string VaultFile(string name) => name + "_" + projectId;

        /// <summary>Detects vaults written by SDK &lt; 1.3 (v1 HWID formula) and keeps using that HWID for them.</summary>
        private void MigrateHwidIfNeeded()
        {
            const string marker = "hwid_mode";
            string mode = PlatformStorage.GetMarker(projectId, marker);

            if (mode == "v1")
            {
                DeviceID.UseLegacyFormula = true;
                return;
            }
            if (mode == "v2" || mode == LTLMConstants.TamperedMarker) return;

            // No marker: fresh install or upgrade from < 1.3
            if (SecureStorage.Exists(VaultFile("license_key")))
            {
                DeviceID.UseLegacyFormula = true;
                string key = SecureStorage.Load(VaultFile("license_key"), DeviceID.GetLegacyHWID(), projectId);
                if (!string.IsNullOrEmpty(key) && key != LTLMConstants.TamperedMarker)
                {
                    LTLMLog.Info("Existing activation found; keeping legacy hardware id for this install.");
                    PlatformStorage.SetMarker(projectId, marker, "v1");
                    return;
                }
                DeviceID.UseLegacyFormula = false;
                SecureStorage.InvalidateCache();
            }

            PlatformStorage.SetMarker(projectId, marker, "v2");
        }

        private void LoadLocalState()
        {
            _vaultTampered = false;
            bool t;

            SecureStorage.TryLoad(VaultFile("license_key"), Hwid, projectId, out t);
            _vaultTampered |= t;
            SecureStorage.TryLoad(VaultFile("license_cache"), Hwid, projectId, out t);
            _vaultTampered |= t;
            SecureStorage.TryLoad(VaultFile("cached_tokens"), Hwid, projectId, out t);
            _vaultTampered |= t;

            if (_vaultTampered)
                LTLMLog.Error("Local license vault failed integrity checks. The license must be re-activated online.");

            LoadPendingConsumptions();
        }

        private string GetStoredLicenseKey()
        {
            string key = SecureStorage.TryLoad(VaultFile("license_key"), Hwid, projectId, out bool t);
            if (t) _vaultTampered = true;
            return key;
        }

        private void CacheLicense(LicenseData license)
        {
            if (license == null) return;
            try
            {
                SecureStorage.Save(VaultFile("license_cache"), JsonConvert.SerializeObject(license), Hwid, projectId);
                SecureStorage.Save(VaultFile("cached_tokens"), (license.tokensRemaining ?? 0).ToString(), Hwid, projectId);
            }
            catch (Exception e)
            {
                LTLMLog.Error("Failed to cache license: " + e.Message);
            }
        }

        private LicenseData LoadLicenseFromCache()
        {
            try
            {
                string json = SecureStorage.TryLoad(VaultFile("license_cache"), Hwid, projectId, out bool t);
                if (t) { _vaultTampered = true; return null; }
                if (string.IsNullOrEmpty(json)) return null;
                return JsonConvert.DeserializeObject<LicenseData>(json);
            }
            catch
            {
                return null;
            }
        }

        private void SaveLastSync()
        {
            SecureStorage.Save(VaultFile("last_successful_sync"),
                SecureClock.GetEffectiveTime(projectId).Ticks.ToString(), Hwid, projectId);
        }

        private DateTime? GetLastSync()
        {
            string raw = SecureStorage.TryLoad(VaultFile("last_successful_sync"), Hwid, projectId, out _);
            if (long.TryParse(raw, out long ticks)) return new DateTime(ticks, DateTimeKind.Utc);
            return null;
        }

        /// <summary>Clears all local license data and de-authenticates (does not contact the server).</summary>
        public void ClearLicenseCache()
        {
            StopHeartbeat();
            _activeLicense = null;
            _isInOfflineGrace = false;
            _offlineGraceDeadlineUtc = DateTime.MinValue;
            _lastSeatSignature = null;

            SecureStorage.Delete(VaultFile("license_cache"), projectId);
            SecureStorage.Delete(VaultFile("license_key"), projectId);
            SecureStorage.Delete(VaultFile("cached_tokens"), projectId);
            SecureStorage.Delete(VaultFile("last_successful_sync"), projectId);
            _client?.ClearNonce();
            _vaultTampered = false;

            SetStatus(LicenseStatus.Unauthenticated);
            OnLicenseUpdated?.Invoke(null);
        }

        // ------------------------------------------------------------------
        // Status pipeline
        // ------------------------------------------------------------------

        private LicenseStatus ComputeStatus()
        {
            if (_activeLicense == null) return LicenseStatus.Unauthenticated;
            if (_vaultTampered || SecureClock.IsClockTampered(projectId)) return LicenseStatus.Tampered;
            var now = SecureClock.GetEffectiveTime(projectId);
            if (_isInOfflineGrace && now >= _offlineGraceDeadlineUtc) return LicenseStatus.ConnectionRequired;
            var expiry = _activeLicense.ValidUntilUtc;
            if (!_activeLicense.IsPerpetual && expiry.HasValue &&
                now > expiry.Value.AddDays(_activeLicense.config?.limits?.time?.gracePeriodDays ?? 0))
                return LicenseStatus.Expired;

            var mapped = MapStatus(_activeLicense.status);
            if (_isInOfflineGrace && (mapped == LicenseStatus.Active || mapped == LicenseStatus.GracePeriod))
                return LicenseStatus.OfflineGrace;
            return mapped;
        }

        private static LicenseStatus MapStatus(string status)
        {
            switch (status?.ToLowerInvariant())
            {
                case "active": return LicenseStatus.Active;
                case "valid_no_seat": return LicenseStatus.ValidNoSeat;
                case "grace_period": return LicenseStatus.GracePeriod;
                case "expired": return LicenseStatus.Expired;
                case "suspended": return LicenseStatus.Suspended;
                case "revoked": return LicenseStatus.Revoked;
                case "terminated": return LicenseStatus.Terminated;
                case "connection_required": return LicenseStatus.ConnectionRequired;
                case "kicked": return LicenseStatus.Kicked;
                case "pending":
                case "inactive":
                case "disabled": return LicenseStatus.Suspended;
                default: return LicenseStatus.Unauthenticated;
            }
        }

        private void SetStatus(LicenseStatus status)
        {
            if (status == _cachedStatus) return;
            _cachedStatus = status;
            OnLicenseStatusChanged?.Invoke(status);
        }

        private void RefreshStatus() => SetStatus(ComputeStatus());

        /// <summary>Gets the resolved status of the current license (cached; cheap to call every frame).</summary>
        public LicenseStatus GetLicenseStatus()
        {
            RefreshStatus();
            return _cachedStatus;
        }

        /// <summary>
        /// Central entry point for fresh license data from the server (or the cache when offline).
        /// Applies enforcement, recomputes status and raises events in a deterministic order.
        /// </summary>
        private void ApplyLicense(LicenseData license, bool fromServer)
        {
            if (license == null) return;

            if (fromServer)
            {
                _isInOfflineGrace = false;
                _offlineGraceDeadlineUtc = DateTime.MinValue;
            }

            _activeLicense = license;

            ProcessEnforcement(license);
            if (_activeLicense == null) return; // de-authorized by enforcement

            if (fromServer)
            {
                CacheLicense(_activeLicense);
                SaveLastSync();
            }

            RefreshStatus();
            OnLicenseUpdated?.Invoke(_activeLicense);
            ProcessSeatStatus(_activeLicense);
        }

        private void ProcessEnforcement(LicenseData license)
        {
            // 1. Heartbeat interval comes from the server.
            if (license.heartbeatIntervalSeconds.HasValue && license.heartbeatIntervalSeconds.Value > 0)
            {
                float serverInterval = license.heartbeatIntervalSeconds.Value;
                if (Mathf.Abs(heartbeatIntervalSeconds - serverInterval) > 0.1f)
                {
                    LTLMLog.Info($"Server heartbeat interval: {serverInterval}s (was {heartbeatIntervalSeconds}s)");
                    heartbeatIntervalSeconds = serverInterval;
                }
            }

            // 2. Device authorization: this HWID must be registered, except for air-gapped licenses
            //    (offline files are not HWID-bound server-side) and responses that omit the machine list.
            bool airGapped = license.policy != null && license.policy.isAirGapped;
            if (!airGapped && license.machines != null && license.machines.Count > 0)
            {
                string hwid = Hwid;
                bool authorized = false;
                foreach (var m in license.machines)
                {
                    if (m != null && m.hwid == hwid) { authorized = true; break; }
                }

                if (!authorized)
                {
                    LTLMLog.Error("Device authorization failed: this hardware id is not registered with the license. De-activating.");
                    var err = LTLMRequestError.Local("This device is not authorized for this license.");
                    err.Code = LTLMErrorCodes.DEVICE_LIMIT_REACHED;
                    LastError = err;
                    ClearLicenseCache();
                    return;
                }
            }

            // 3. Terminated: hard stop.
            if (string.Equals(license.status, "terminated", StringComparison.OrdinalIgnoreCase))
            {
                LTLMLog.Error("LICENSE TERMINATED BY SERVER.");
                SetStatus(LicenseStatus.Terminated);
                if (quitOnTerminated)
                {
                    Application.Quit();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                }
                return;
            }

            // 4. Local expiry check with the monotonic clock (server is authoritative, this catches stale caches).
            DateTime? expiry = license.ValidUntilUtc;
            if (expiry.HasValue && !license.IsPerpetual)
            {
                DateTime now = SecureClock.GetEffectiveTime(projectId);
                if (now > expiry.Value && string.Equals(license.status, "active", StringComparison.OrdinalIgnoreCase))
                {
                    int graceDays = license.config?.limits?.time?.gracePeriodDays ?? 0;
                    license.status = now <= expiry.Value.AddDays(graceDays) ? "grace_period" : "expired";
                    LTLMLog.Warn($"License expired at {expiry.Value:u}; local status is now '{license.status}'.");
                }
            }
        }

        private void ProcessSeatStatus(LicenseData license)
        {
            bool kicked = string.Equals(license.status, "KICKED", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(license.seatStatus, "KICKED", StringComparison.OrdinalIgnoreCase) ||
                          license.kickedNotice != null;

            if (kicked)
            {
                string kickedBy = license.kickedNotice?.kickedByNickname ?? license.kickedNotice?.kickedBy ?? "another device";
                LTLMLog.Warn($"KICKED: this session was terminated by {kickedBy}. Reactivate to continue.");

                StopHeartbeat();
                license.status = "KICKED";
                SetStatus(LicenseStatus.Kicked);
                _lastSeatSignature = "KICKED";
                OnSeatStatusChanged?.Invoke("KICKED", license.activeSeats ?? 0, license.maxConcurrentSeats ?? 0);
                if (license.kickedNotice != null) OnKicked?.Invoke(license.kickedNotice);
                return;
            }

            string seatStatus = license.seatStatus;
            if (string.Equals(license.status, "VALID_NO_SEAT", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(seatStatus))
                seatStatus = "NO_SEAT";
            if (string.IsNullOrEmpty(seatStatus)) return;

            string signature = $"{seatStatus}|{license.activeSeats ?? 0}|{license.maxConcurrentSeats ?? 0}";
            if (signature == _lastSeatSignature) return;
            _lastSeatSignature = signature;

            OnSeatStatusChanged?.Invoke(seatStatus, license.activeSeats ?? 0, license.maxConcurrentSeats ?? 0);
        }

        // ------------------------------------------------------------------
        // Validation / activation
        // ------------------------------------------------------------------

        /// <summary>Attempts to load and validate a previously stored license (offline grace applies).</summary>
        public void TryLoadStoredLicense(Action<LicenseData, LicenseStatus> onSuccess = null, Action<string> onError = null)
        {
            if (_client == null) { onError?.Invoke("SDK not configured."); return; }

            string storedKey = GetStoredLicenseKey();
            if (string.IsNullOrEmpty(storedKey))
            {
                if (_vaultTampered) SetStatus(LicenseStatus.Tampered);
                OnValidationCompleted?.Invoke(false, _vaultTampered ? LicenseStatus.Tampered : LicenseStatus.Unauthenticated);
                onError?.Invoke(_vaultTampered ? "Local license data was tampered with." : "No stored license found.");
                return;
            }

            RunValidation("/v1/sdk/pro/license/status", storedKey, isActivation: false, showLoading: true, onSuccess, onError);
        }

        /// <summary>Activates a license key on this device (registers the HWID) and starts the session.</summary>
        public void ActivateLicense(string licenseKey, Action<LicenseData, LicenseStatus> onSuccess = null,
            Action<string> onError = null, bool DoNotShowLoading = false)
        {
            if (_client == null) { onError?.Invoke("SDK not configured."); return; }
            licenseKey = licenseKey?.Trim();
            if (string.IsNullOrEmpty(licenseKey)) { onError?.Invoke("License key is required."); return; }

            RunValidation("/v1/sdk/pro/license/validate", licenseKey, isActivation: true, showLoading: !DoNotShowLoading, onSuccess, onError);
        }

        /// <summary>Validates an already-registered license without registering the HWID again.</summary>
        public void ValidateLicense(string licenseKey, Action<LicenseData, LicenseStatus> onSuccess = null, Action<string> onError = null)
        {
            if (_client == null) { onError?.Invoke("SDK not configured."); return; }
            licenseKey = licenseKey?.Trim();
            if (string.IsNullOrEmpty(licenseKey)) { onError?.Invoke("License key is required."); return; }

            RunValidation("/v1/sdk/pro/license/status", licenseKey, isActivation: false, showLoading: true, onSuccess, onError);
        }

        /// <summary>Re-validates the current license with the server (e.g. after a purchase or top-up).</summary>
        public void Refresh(Action<LicenseData, LicenseStatus> onSuccess = null, Action<string> onError = null)
        {
            string key = _activeLicense?.licenseKey ?? GetStoredLicenseKey();
            if (string.IsNullOrEmpty(key)) { onError?.Invoke("No license to refresh."); return; }
            RunValidation("/v1/sdk/pro/license/status", key, isActivation: false, showLoading: false, onSuccess, onError);
        }

        private ActivationRequest BuildActivationRequest(string licenseKey)
        {
            return new ActivationRequest
            {
                key = licenseKey,
                hwid = Hwid,
                version = softwareVersion,
                nickname = DeviceID.GetDeviceName(),
                clientVersion = softwareVersion,
                meta = new Dictionary<string, object>
                {
                    { "os", DeviceID.GetOS() },
                    { "platform", DeviceID.GetPlatformTag() },
                    { "sdk", "unity" },
                    { "unityVersion", Application.unityVersion }
                }
            };
        }

        private void RunValidation(string endpoint, string licenseKey, bool isActivation, bool showLoading,
            Action<LicenseData, LicenseStatus> onSuccess, Action<string> onError, bool isRetry = false)
        {
            if (_client == null) { onError?.Invoke("SDK not configured."); return; }
            if (_isValidating)
            {
                onError?.Invoke("A validation is already in progress.");
                return;
            }

            _isValidating = true;
            if (showLoading && !isRetry) OnValidationStarted?.Invoke();

            StartCoroutine(_client.PostEncrypted<ActivationRequest, LicenseData>(
                endpoint,
                BuildActivationRequest(licenseKey),
                license =>
                {
                    _isValidating = false;
                    LastError = null;
                    _vaultTampered = false; // the vault is rewritten from authoritative server data below

                    ApplyLicense(license, fromServer: true);

                    if (_activeLicense == null)
                    {
                        // Enforcement removed the license (device not authorized).
                        string msg = LastError?.Message ?? "This device is not authorized for this license.";
                        OnValidationCompleted?.Invoke(false, LicenseStatus.Unauthenticated);
                        onError?.Invoke(msg);
                        return;
                    }

                    SecureStorage.Save(VaultFile("license_key"), licenseKey, Hwid, projectId);

                    if (_cachedStatus != LicenseStatus.Kicked && _cachedStatus != LicenseStatus.Terminated)
                        StartHeartbeat(immediate: false);

                    var status = _cachedStatus;
                    OnValidationCompleted?.Invoke(true, status);
                    onSuccess?.Invoke(_activeLicense, status);
                    SyncPendingConsumptions();
                },
                err =>
                {
                    _isValidating = false;
                    LastError = err;

                    // Nonce chain out of sync (crash, restore, cleared server cache): the validate endpoint resets it.
                    if (err.IsNonceMismatch && !isRetry)
                    {
                        LTLMLog.Warn("Nonce out of sync; re-activating to resynchronise.");
                        RunValidation("/v1/sdk/pro/license/validate", licenseKey, true, false, onSuccess, onError, isRetry: true);
                        return;
                    }

                    if (err.IsNetworkError)
                    {
                        string cachedKey = _activeLicense?.licenseKey ?? GetStoredLicenseKey();
                        if (cachedKey == licenseKey && TryEnterOfflineGrace())
                        {
                            var status = _cachedStatus;
                            StartHeartbeat(immediate: false);
                            OnValidationCompleted?.Invoke(true, status);
                            onSuccess?.Invoke(_activeLicense, status);
                            return;
                        }

                        LTLMLog.Warn("Server unreachable and no offline grace available: " + err.Message);
                        OnValidationCompleted?.Invoke(false, LicenseStatus.ConnectionRequired);
                        onError?.Invoke(err.Message);
                        OnError?.Invoke(err);
                        return;
                    }

                    LTLMLog.Error("Server rejected validation: " + err.Message);
                    OnValidationCompleted?.Invoke(false, LicenseStatus.Unauthenticated);
                    onError?.Invoke(err.Message);
                    OnError?.Invoke(err);
                }
            ));
        }

        /// <summary>Activates a license using an offline .ltlm file blob (air-gapped licenses).</summary>
        public void ActivateOffline(string encryptedBlob, Action<LicenseData, LicenseStatus> onSuccess = null, Action<string> onError = null)
        {
            if (_client == null) { onError?.Invoke("SDK not configured."); return; }

            if (!_client.TryUnwrap<LicenseData>(encryptedBlob?.Trim(), out var signed, out var error))
            {
                LastError = error;
                onError?.Invoke("Invalid offline license: " + error.Message);
                OnError?.Invoke(error);
                return;
            }

            var license = signed.data;
            if (license == null || string.IsNullOrEmpty(license.licenseKey))
            {
                onError?.Invoke("Offline license file contains no license data.");
                return;
            }

            DateTime now = SecureClock.GetEffectiveTime(projectId);
            DateTime? expiry = license.ValidUntilUtc;
            if (expiry.HasValue && !license.IsPerpetual && now > expiry.Value)
            {
                onError?.Invoke("The offline license has expired.");
                return;
            }

            if (license.policy == null) license.policy = new PolicyData();
            license.policy.isAirGapped = true;
            if (string.IsNullOrEmpty(license.status)) license.status = "active";

            StopHeartbeat();
            _activeLicense = license;
            ProcessEnforcement(license);
            if (_activeLicense == null)
            {
                onError?.Invoke("This device is not authorized in the offline license.");
                return;
            }

            SecureStorage.Save(VaultFile("license_key"), license.licenseKey, Hwid, projectId);
            SecureStorage.Save(VaultFile("offline_blob"), encryptedBlob.Trim(), Hwid, projectId);
            CacheLicense(license);

            RefreshStatus();
            OnLicenseUpdated?.Invoke(license);
            OnValidationCompleted?.Invoke(true, _cachedStatus);
            onSuccess?.Invoke(license, _cachedStatus);
        }

        // ------------------------------------------------------------------
        // Offline grace
        // ------------------------------------------------------------------

        private bool TryEnterOfflineGrace()
        {
            if (_activeLicense == null) _activeLicense = LoadLicenseFromCache();
            if (_activeLicense == null) return false;

            if (_activeLicense.policy != null && _activeLicense.policy.isAirGapped)
            {
                // Air-gapped licenses never need the server.
                RefreshStatus();
                return IsAuthenticated;
            }

            bool offlineEnabled = _activeLicense.offlineEnabled ?? false;
            int graceHours = _activeLicense.offlineGraceHours ?? 0;
            if (!offlineEnabled || graceHours <= 0)
            {
                LTLMLog.Error("Offline use is not allowed for this license. Connection required.");
                _activeLicense.status = "connection_required";
                _isInOfflineGrace = false;
                RefreshStatus();
                return false;
            }

            DateTime? lastSync = GetLastSync();
            if (!lastSync.HasValue)
            {
                _activeLicense.status = "connection_required";
                RefreshStatus();
                return false;
            }

            DateTime now = SecureClock.GetEffectiveTime(projectId);
            _offlineGraceDeadlineUtc = lastSync.Value.AddHours(graceHours);

            if (now > _offlineGraceDeadlineUtc)
            {
                LTLMLog.Error($"Offline grace period of {graceHours}h exceeded. Connection required.");
                _activeLicense.status = "connection_required";
                _isInOfflineGrace = false;
                RefreshStatus();
                return false;
            }

            _isInOfflineGrace = true;
            LTLMLog.Warn($"Running in offline grace mode. {(_offlineGraceDeadlineUtc - now).TotalHours:F1} hours remaining.");
            RefreshStatus();
            OnLicenseUpdated?.Invoke(_activeLicense);
            return IsAuthenticated;
        }

        // ------------------------------------------------------------------
        // Heartbeat
        // ------------------------------------------------------------------

        private void StartHeartbeat(bool immediate)
        {
            StopHeartbeat();
            if (_activeLicense == null || _client == null) return;
            if (_activeLicense.policy != null && _activeLicense.policy.isAirGapped) return;
            _heartbeatRoutine = StartCoroutine(HeartbeatRoutine(immediate));
        }

        private void StopHeartbeat()
        {
            if (_heartbeatRoutine == null) return;
            StopCoroutine(_heartbeatRoutine);
            _heartbeatRoutine = null;
        }

        private IEnumerator HeartbeatRoutine(bool immediate)
        {
            if (!immediate)
                yield return new WaitForSecondsRealtime(NextHeartbeatDelay());

            while (_activeLicense != null)
            {
                if (_isInOfflineGrace && _offlineGraceDeadlineUtc != DateTime.MinValue &&
                    SecureClock.GetEffectiveTime(projectId) > _offlineGraceDeadlineUtc)
                {
                    LTLMLog.Error("Offline grace period exceeded. Connection required.");
                    _activeLicense.status = "connection_required";
                    _isInOfflineGrace = false;
                    RefreshStatus();
                }

                var request = new HeartbeatRequest { key = _activeLicense.licenseKey, hwid = Hwid, isClosing = false };
                bool reactivate = false;

                yield return _client.PostEncrypted<HeartbeatRequest, LicenseData>(
                    "/v1/sdk/pro/license/heartbeat",
                    request,
                    license =>
                    {
                        LastError = null;
                        if (_isInOfflineGrace) LTLMLog.Info("Connection restored; leaving offline grace mode.");
                        ApplyLicense(license, fromServer: true);
                        SyncPendingConsumptions();
                    },
                    err =>
                    {
                        LastError = err;
                        if (err.IsNonceMismatch || err.IsIntegrityError)
                        {
                            LTLMLog.Warn("Heartbeat security state out of sync; re-activating. " + err.Message);
                            reactivate = true;
                        }
                        else if (err.IsNetworkError)
                        {
                            if (!_isInOfflineGrace) LTLMLog.Warn("Heartbeat failed (network). Entering offline grace check.");
                            TryEnterOfflineGrace();
                        }
                        else if (err.HttpStatus == 404)
                        {
                            LTLMLog.Error("License no longer exists on the server. Clearing local data.");
                            OnError?.Invoke(err);
                            ClearLicenseCache();
                        }
                        else
                        {
                            LTLMLog.Error("Server rejected heartbeat: " + err.Message);
                            OnError?.Invoke(err);
                        }
                    }
                );

                if (_activeLicense == null) yield break;

                if (reactivate)
                {
                    // Runs its own heartbeat on success; this loop ends.
                    _heartbeatRoutine = null;
                    RunValidation("/v1/sdk/pro/license/validate", _activeLicense.licenseKey, true, false, null, null, isRetry: true);
                    yield break;
                }

                if (_cachedStatus == LicenseStatus.Kicked) yield break;

                yield return new WaitForSecondsRealtime(NextHeartbeatDelay());
            }

            _heartbeatRoutine = null;
        }

        private float NextHeartbeatDelay()
        {
            if (_isInOfflineGrace || _cachedStatus == LicenseStatus.ConnectionRequired)
                return LTLMConstants.OfflineRetryIntervalSeconds;
            return Mathf.Max(15f, heartbeatIntervalSeconds);
        }

        // ------------------------------------------------------------------
        // Seats
        // ------------------------------------------------------------------

        /// <summary>
        /// Releases this device's concurrent seat and stops the heartbeat ("sign out of this device").
        /// Requires connectivity. The license stays cached; call <see cref="ReactivateSeat"/> to resume.
        /// </summary>
        /// <returns>True if the release was sent, false if blocked (offline / no license).</returns>
        public bool DeactivateSeat(Action onDone = null, Action<string> onError = null)
        {
            if (_activeLicense == null || _client == null) { onError?.Invoke("No active license."); return false; }

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                LTLMLog.Warn("Cannot release the seat while offline.");
                onError?.Invoke("Cannot sign out while offline. Please connect to the internet.");
                return false;
            }

            StopHeartbeat();
            var request = new HeartbeatRequest { key = _activeLicense.licenseKey, hwid = Hwid, isClosing = true };

            StartCoroutine(_client.PostEncrypted<HeartbeatRequest, LicenseData>(
                "/v1/sdk/pro/license/heartbeat",
                request,
                _ =>
                {
                    LTLMLog.Info("Seat released.");
                    if (_activeLicense != null)
                    {
                        _activeLicense.seatStatus = "RELEASED";
                        if (_activeLicense.seatsEnabled == true) _activeLicense.status = "VALID_NO_SEAT";
                        _activeLicense.activeSeats = Math.Max(0, (_activeLicense.activeSeats ?? 1) - 1);
                        RefreshStatus();
                        _lastSeatSignature = null;
                        OnSeatStatusChanged?.Invoke("RELEASED", _activeLicense.activeSeats ?? 0, _activeLicense.maxConcurrentSeats ?? 0);
                    }
                    onDone?.Invoke();
                },
                err =>
                {
                    LastError = err;
                    LTLMLog.Warn("Seat release failed: " + err.Message);
                    onError?.Invoke(err.Message);
                    OnError?.Invoke(err);
                },
                skipNonce: true
            ));

            return true;
        }

        /// <summary>Releases the seat (if online), clears local license data and returns to the unauthenticated state.</summary>
        public void SignOut(Action onDone = null)
        {
            if (_activeLicense == null || _client == null || Application.internetReachability == NetworkReachability.NotReachable)
            {
                ClearLicenseCache();
                onDone?.Invoke();
                return;
            }

            var request = new HeartbeatRequest { key = _activeLicense.licenseKey, hwid = Hwid, isClosing = true };
            StopHeartbeat();
            StartCoroutine(_client.PostEncrypted<HeartbeatRequest, LicenseData>(
                "/v1/sdk/pro/license/heartbeat", request,
                _ => { ClearLicenseCache(); onDone?.Invoke(); },
                err => { LastError = err; ClearLicenseCache(); onDone?.Invoke(); },
                skipNonce: true));
        }

        private void ReleaseSeatOnQuit()
        {
            if (_activeLicense == null || _client == null || _isInOfflineGrace) return;
            if (_activeLicense.policy != null && _activeLicense.policy.isAirGapped) return;
            try
            {
                var request = new HeartbeatRequest { key = _activeLicense.licenseKey, hwid = Hwid, isClosing = true };
                StartCoroutine(_client.PostEncryptedUnlocked<HeartbeatRequest, LicenseData>(
                    "/v1/sdk/pro/license/heartbeat", request, _ => { }, _ => { }));
            }
            catch (Exception ex)
            {
                LTLMLog.Warn($"Seat release on quit failed: {ex.Message}");
            }
        }

        /// <summary>Gets all devices currently holding seats on this license.</summary>
        public void GetActiveSeats(Action<GetSeatsResponse> onSuccess, Action<string> onError = null)
        {
            if (_activeLicense == null || _client == null) { onError?.Invoke("No active license"); return; }

            StartCoroutine(_client.PostEncrypted<GetSeatsRequest, GetSeatsResponse>(
                "/v1/sdk/pro/license/seats",
                new GetSeatsRequest { key = _activeLicense.licenseKey },
                res => { LastError = null; onSuccess?.Invoke(res); },
                Fail(onError),
                rotatesNonce: false
            ));
        }

        /// <summary>Releases another device's seat so this device can claim it.</summary>
        public void ReleaseSeat(string targetHwid, bool claimSeat, Action<ReleaseSeatResponse> onSuccess, Action<string> onError = null)
        {
            if (_activeLicense == null || _client == null) { onError?.Invoke("No active license"); return; }
            if (_activeLicense.seatsEnabled == false) { onError?.Invoke("Seat management is disabled for this license."); return; }
            if (string.IsNullOrEmpty(targetHwid)) { onError?.Invoke("targetHwid is required."); return; }

            var request = new ReleaseSeatRequest
            {
                key = _activeLicense.licenseKey,
                targetHwid = targetHwid,
                callerHwid = Hwid,
                claimSeat = claimSeat
            };

            StartCoroutine(_client.PostEncrypted<ReleaseSeatRequest, ReleaseSeatResponse>(
                "/v1/sdk/pro/license/release-seat",
                request,
                response =>
                {
                    LastError = null;
                    if (response.seatClaimed)
                    {
                        LTLMLog.Info("Seat claimed. Refreshing license state.");
                        Refresh();
                    }
                    onSuccess?.Invoke(response);
                },
                Fail(onError)
            ));
        }

        /// <summary>Attempts to reactivate a seat after being kicked or releasing it.</summary>
        public void ReactivateSeat(Action<LicenseData, LicenseStatus> onSuccess = null, Action<string> onError = null)
        {
            string key = _activeLicense?.licenseKey ?? GetStoredLicenseKey();
            if (string.IsNullOrEmpty(key)) { onError?.Invoke("No license to reactivate."); return; }

            _lastSeatSignature = null;
            RunValidation("/v1/sdk/pro/license/status", key, isActivation: false, showLoading: true,
                (license, status) =>
                {
                    if (status == LicenseStatus.ValidNoSeat)
                        onError?.Invoke("No seats available. All concurrent seats are in use.");
                    else
                        onSuccess?.Invoke(license, status);
                },
                onError);
        }

        // ------------------------------------------------------------------
        // User settings (cloud sync)
        // ------------------------------------------------------------------

        public void GetUserSettings(Action<Dictionary<string, object>> onSuccess, Action<string> onError = null)
        {
            if (_activeLicense == null || _client == null) { onError?.Invoke("No active license. Activate first."); return; }

            StartCoroutine(_client.PostEncrypted<UserSettingsRequest, UserSettingsResponse>(
                "/v1/sdk/pro/license/settings/get",
                new UserSettingsRequest { key = _activeLicense.licenseKey, hwid = Hwid },
                response =>
                {
                    LastError = null;
                    var settings = response.settings ?? new Dictionary<string, object>();
                    if (_activeLicense != null) { _activeLicense.userSettings = settings; CacheLicense(_activeLicense); }
                    onSuccess?.Invoke(settings);
                },
                Fail(onError)
            ));
        }

        public void SaveUserSettings(Dictionary<string, object> settings, Action onSuccess = null, Action<string> onError = null)
        {
            if (_activeLicense == null || _client == null) { onError?.Invoke("No active license. Activate first."); return; }
            if (settings == null) { onError?.Invoke("Settings cannot be null."); return; }

            string json = JsonConvert.SerializeObject(settings);
            if (json.Length > LTLMConstants.MaxUserSettingsBytes)
            {
                onError?.Invoke($"Settings exceed maximum size of 64KB. Current size: {json.Length} bytes.");
                return;
            }

            StartCoroutine(_client.PostEncrypted<SaveUserSettingsRequest, UserSettingsResponse>(
                "/v1/sdk/pro/license/settings/save",
                new SaveUserSettingsRequest { key = _activeLicense.licenseKey, hwid = Hwid, settings = settings },
                _ =>
                {
                    LastError = null;
                    if (_activeLicense != null)
                    {
                        _activeLicense.userSettings = settings;
                        CacheLicense(_activeLicense);
                        OnLicenseUpdated?.Invoke(_activeLicense);
                    }
                    onSuccess?.Invoke();
                },
                Fail(onError)
            ));
        }

        /// <summary>User settings from the local cache (no network).</summary>
        public Dictionary<string, object> GetLocalUserSettings() =>
            _activeLicense?.userSettings ?? new Dictionary<string, object>();

        // ------------------------------------------------------------------
        // Customer login (OTP) / portfolio
        // ------------------------------------------------------------------

        public void RequestOTP(string email, Action onSuccess, Action<string> onError)
        {
            if (_client == null) { onError?.Invoke("SDK not configured."); return; }
            if (string.IsNullOrWhiteSpace(email)) { onError?.Invoke("Email is required."); return; }

            StartCoroutine(_client.PostEncrypted<OtpRequest, object>(
                "/v1/sdk/pro/auth/customer-login-request",
                new OtpRequest { email = email.Trim() },
                _ => { LastError = null; onSuccess?.Invoke(); },
                Fail(onError),
                rotatesNonce: false
            ));
        }

        public void VerifyOTP(string email, string otp, Action<List<LicenseData>> onPortfolioReceived, Action<string> onError)
        {
            if (_client == null) { onError?.Invoke("SDK not configured."); return; }
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp)) { onError?.Invoke("Email and code are required."); return; }

            StartCoroutine(_client.PostEncrypted<OtpVerifyRequest, PortfolioResponse>(
                "/v1/sdk/pro/auth/customer-portfolio",
                new OtpVerifyRequest { email = email.Trim(), otp = otp.Trim() },
                portfolio => { LastError = null; onPortfolioReceived?.Invoke(portfolio?.licenses ?? new List<LicenseData>()); },
                Fail(onError),
                rotatesNonce: false
            ));
        }

        // ------------------------------------------------------------------
        // Entitlements / helpers
        // ------------------------------------------------------------------

        /// <summary>Checks the token balance; with forceRefresh the server is asked first.</summary>
        public void DoesHaveTokens(int required, Action<bool> onResult, bool forceRefresh = false)
        {
            if (forceRefresh && _activeLicense != null)
            {
                Refresh((l, s) => onResult?.Invoke(GetTokenBalance() >= required),
                    _ => onResult?.Invoke(GetTokenBalance() >= required));
                return;
            }
            onResult?.Invoke(GetTokenBalance() >= required);
        }

        /// <summary>True if the license has the capability (or feature flag) enabled.</summary>
        public bool HasCapability(string featureName) => _activeLicense != null && _activeLicense.HasCapability(featureName);

        /// <summary>Gets a project setting (org-defined key/value) or null.</summary>
        public object GetProjectSetting(string key) => _activeLicense?.GetProjectSetting<object>(key, null);

        /// <summary>Typed project setting accessor.</summary>
        public T GetProjectSetting<T>(string key, T defaultValue = default) =>
            _activeLicense == null ? defaultValue : _activeLicense.GetProjectSetting(key, defaultValue);

        /// <summary>Universal entitlement check: authenticated + optional capability + optional token balance.</summary>
        public bool IsEntitled(string capability = null, int requiredTokens = 0)
        {
            if (!IsAuthenticated) return false;
            if (!string.IsNullOrEmpty(capability) && !HasCapability(capability)) return false;
            if (requiredTokens > 0 && GetTokenBalance() < requiredTokens) return false;
            return true;
        }

        /// <summary>All enabled capabilities for the given (or active) license.</summary>
        public List<string> GetEntitledCapabilites(LicenseData _license = null)
        {
            _license = _license ?? _activeLicense;
            var result = new List<string>();
            if (_license?.config == null) return result;
            if (_license.config.capabilities != null) result.AddRange(_license.config.capabilities);
            if (_license.config.features != null)
                foreach (var kv in _license.config.features)
                    if (_license.config.IsFeatureEnabled(kv.Key) && !result.Contains(kv.Key)) result.Add(kv.Key);
            return result;
        }

        /// <summary>Alias with correct spelling.</summary>
        public List<string> GetEntitledCapabilities(LicenseData license = null) => GetEntitledCapabilites(license);

        /// <summary>Tokens currently available (live or cached).</summary>
        public int GetTokenBalance()
        {
            if (_activeLicense != null) return _activeLicense.tokensRemaining ?? 0;
            string cached = SecureStorage.TryLoad(VaultFile("cached_tokens"), Hwid, projectId, out _);
            return int.TryParse(cached, out int tokens) ? tokens : 0;
        }

        /// <summary>Days until expiry, or -1 for perpetual/unknown.</summary>
        public int GetDaysRemaining()
        {
            if (_activeLicense == null || _activeLicense.IsPerpetual) return -1;
            DateTime? expiry = _activeLicense.ValidUntilUtc;
            if (!expiry.HasValue) return -1;
            TimeSpan remaining = expiry.Value - SecureClock.GetEffectiveTime(projectId);
            return Math.Max(0, (int)remaining.TotalDays);
        }

        /// <summary>Logs a custom analytics event to the LTLM server.</summary>
        public void LogEvent(string eventType, Dictionary<string, object> payload = null)
        {
            if (_activeLicense == null || _client == null || _isInOfflineGrace) return;

            var request = new EventRequest
            {
                key = _activeLicense.licenseKey,
                type = eventType,
                payload = payload ?? new Dictionary<string, object>()
            };

            StartCoroutine(_client.PostEncrypted<EventRequest, object>(
                "/v1/sdk/pro/license/events",
                request,
                _ => LTLMLog.Trace($"Event '{eventType}' logged."),
                err => LTLMLog.Warn($"Event logging failed: {err.Message}")
            ));
        }

        /// <summary>Adapts a typed error into the legacy string callback and the OnError event.</summary>
        private Action<LTLMRequestError> Fail(Action<string> onError)
        {
            return err =>
            {
                LastError = err;
                onError?.Invoke(err.Message);
                OnError?.Invoke(err);
            };
        }

        // ------------------------------------------------------------------
        // Commerce
        // ------------------------------------------------------------------

        /// <summary>Fetches the policies that can be purchased for this project (including free trials).</summary>
        public void GetBuyablePolicies(Action<List<PolicyData>> onSuccess, Action<string> onError = null)
        {
            if (_client == null) { onError?.Invoke("SDK not configured."); return; }
            StartCoroutine(_client.PostEncrypted<BuyablePoliciesRequest, List<PolicyData>>(
                "/v1/sdk/pro/project/buyable-policies",
                new BuyablePoliciesRequest { hwid = Hwid },
                res => { LastError = null; onSuccess?.Invoke(res ?? new List<PolicyData>()); },
                Fail(onError),
                rotatesNonce: false
            ));
        }

        /// <summary>Generates a hosted checkout URL for a new license.</summary>
        public void CreateCheckoutSession(string policyId, string customerEmail, string redirectUrl,
            Action<string> onUrlReceived, Action<string> onError = null)
        {
            if (_client == null) { onError?.Invoke("SDK not configured."); return; }
            var request = new CheckoutRequest { policyId = policyId, customerEmail = customerEmail, redirectUrl = redirectUrl };

            StartCoroutine(_client.PostEncrypted<CheckoutRequest, CheckoutResponse>(
                "/v1/sdk/pro/project/checkout-link",
                request,
                res => { LastError = null; onUrlReceived?.Invoke(res.checkoutUrl); },
                Fail(onError),
                rotatesNonce: false
            ));
        }

        /// <summary>Generates a hosted checkout URL for a token/seat/time top-up.</summary>
        public void CreateTopUpSession(string packId, string redirectUrl, Action<string> onUrlReceived, Action<string> onError = null)
        {
            if (_activeLicense == null || _client == null) { onError?.Invoke("Active license required for top-up."); return; }
            var request = new TopUpRequest { key = _activeLicense.licenseKey, packId = packId, redirectUrl = redirectUrl };

            StartCoroutine(_client.PostEncrypted<TopUpRequest, CheckoutResponse>(
                "/v1/sdk/pro/license/topup-link",
                request,
                res => { LastError = null; onUrlReceived?.Invoke(res.checkoutUrl); },
                Fail(onError)
            ));
        }

        /// <summary>Checks if a customer email is eligible for a trial on this project.</summary>
        public void CheckTrialEligibility(string email, Action<TrialEligibilityResult> onSuccess, Action<string> onError = null)
        {
            if (_client == null) { onError?.Invoke("SDK not configured."); return; }
            if (string.IsNullOrWhiteSpace(email)) { onError?.Invoke("Email is required for trial eligibility check."); return; }

            StartCoroutine(_client.PostEncrypted<object, TrialEligibilityResult>(
                "/v1/sdk/pro/project/trial-eligibility",
                new { email = email.Trim() },
                res => { LastError = null; onSuccess?.Invoke(res); },
                Fail(onError), rotatesNonce: false
            ));
        }

        // ------------------------------------------------------------------
        // Tokens
        // ------------------------------------------------------------------

        /// <summary>
        /// Consumes tokens. Online: server-confirmed. Offline (if allowed): optimistic and queued for sync.
        /// </summary>
        public void ConsumeTokens(int amount, string action, Action<LicenseData> onConsumed = null, Action<string> onError = null)
        {
            if (_activeLicense == null || _client == null) { onError?.Invoke("No active license."); return; }
            if (!IsAuthenticated) { onError?.Invoke("License does not currently grant access."); return; }
            if (amount <= 0) { onError?.Invoke("Amount must be positive."); return; }

            if (_activeLicense.tokensEnabled == false)
            {
                onError?.Invoke("Token consumption is disabled for this license.");
                return;
            }

            var usage = new ConsumptionRequest
            {
                key = _activeLicense.licenseKey,
                amount = amount,
                action = action,
                hwid = Hwid,
                timestamp = LTLMDate.ToIso(SecureClock.GetEffectiveTime(projectId)),
                clientId = Guid.NewGuid().ToString("N")
            };

            bool reachable = Application.internetReachability != NetworkReachability.NotReachable && !_isInOfflineGrace;
            if (reachable)
            {
                SendSingleConsumption(usage, onConsumed, onError);
                return;
            }

            if (!allowOfflineTokenConsumption)
            {
                onError?.Invoke("Offline token consumption is disabled. Please connect to the internet.");
                return;
            }

            QueueConsumptionOptimistically(usage, onConsumed, onError);
        }

        private void QueueConsumptionOptimistically(ConsumptionRequest usage, Action<LicenseData> onConsumed, Action<string> onError)
        {
            if (!TryEnterOfflineGrace() || !IsAuthenticated)
            {
                onError?.Invoke("Offline consumption requires a valid license within its offline allowance.");
                return;
            }
            if (_activeLicense.tokensRemaining.HasValue)
            {
                if (_activeLicense.tokensRemaining.Value < usage.amount)
                {
                    onError?.Invoke("Not enough tokens");
                    return;
                }
                _activeLicense.tokensRemaining -= usage.amount;
            }
            if (_activeLicense.tokensConsumed.HasValue) _activeLicense.tokensConsumed += usage.amount;

            _pendingConsumptions.Add(usage);
            SavePendingConsumptions();
            CacheLicense(_activeLicense);

            OnTokensConsumed?.Invoke(_activeLicense);
            OnLicenseUpdated?.Invoke(_activeLicense);
            onConsumed?.Invoke(_activeLicense);
            LTLMLog.Info($"Offline - token consumption queued ({_pendingConsumptions.Count} pending).");
        }

        private void SendSingleConsumption(ConsumptionRequest usage, Action<LicenseData> onConsumed, Action<string> onError)
        {
            var request = new SingleConsumptionRequest
            {
                key = usage.key,
                amount = usage.amount,
                action = usage.action,
                hwid = usage.hwid,
                meta = new Dictionary<string, object> { { "action", usage.action }, { "clientId", usage.clientId } }
            };

            StartCoroutine(_client.PostEncrypted<SingleConsumptionRequest, LicenseData>(
                "/v1/sdk/pro/license/consume",
                request,
                fullLicense =>
                {
                    LastError = null;
                    ApplyLicense(fullLicense, fromServer: true);
                    if (_activeLicense == null) { onError?.Invoke("Device no longer authorized."); return; }
                    OnTokensConsumed?.Invoke(_activeLicense);
                    onConsumed?.Invoke(_activeLicense);
                },
                err =>
                {
                    LastError = err;
                    if (err.IsNetworkError && allowOfflineTokenConsumption)
                    {
                        LTLMLog.Warn("Token sync failed (network); queuing for later.");
                        QueueConsumptionOptimistically(usage, onConsumed, onError);
                        return;
                    }
                    LTLMLog.Error("Token consumption denied: " + err.Message);
                    onError?.Invoke(err.Message);
                    OnError?.Invoke(err);
                }
            ));
        }

        private void SyncPendingConsumptions()
        {
            if (_activeLicense == null || _client == null || _isSyncingTokens || _pendingConsumptions.Count == 0 || _isInOfflineGrace) return;

            _isSyncingTokens = true;
            var batch = new BatchConsumptionRequest
            {
                key = _activeLicense.licenseKey,
                hwid = Hwid,
                usages = _pendingConsumptions.FindAll(p => p.key == _activeLicense.licenseKey)
            };
            if (batch.usages.Count == 0) { _isSyncingTokens = false; return; }

            LTLMLog.Info($"Syncing {batch.usages.Count} pending token consumptions...");

            StartCoroutine(_client.PostEncrypted<BatchConsumptionRequest, LicenseData>(
                "/v1/sdk/pro/license/consume-batch",
                batch,
                fullLicense =>
                {
                    _isSyncingTokens = false;
                    _pendingConsumptions.RemoveAll(p => batch.usages.Contains(p));
                    SavePendingConsumptions();
                    ApplyLicense(fullLicense, fromServer: true);
                    if (_activeLicense != null) OnTokensConsumed?.Invoke(_activeLicense);
                },
                err =>
                {
                    _isSyncingTokens = false;
                    LastError = err;
                    if (err.IsNetworkError || err.IsNonceMismatch)
                    {
                        LTLMLog.Warn("Token sync deferred: " + err.Message);
                        return;
                    }
                    LTLMLog.Error("Token sync rejected; retaining usage for reconciliation: " + err.Message);
                    OnError?.Invoke(err);
                }
            ));
        }

        private void LoadPendingConsumptions()
        {
            try
            {
                string json = SecureStorage.TryLoad(VaultFile("pending_usages"), Hwid, projectId, out _);
                _pendingConsumptions = string.IsNullOrEmpty(json)
                    ? new List<ConsumptionRequest>()
                    : JsonConvert.DeserializeObject<List<ConsumptionRequest>>(json) ?? new List<ConsumptionRequest>();
            }
            catch
            {
                _pendingConsumptions = new List<ConsumptionRequest>();
            }
        }

        private void SavePendingConsumptions()
        {
            if (_client == null) return;
            try
            {
                SecureStorage.Save(VaultFile("pending_usages"), JsonConvert.SerializeObject(_pendingConsumptions), Hwid, projectId);
            }
            catch (Exception e)
            {
                LTLMLog.Error("Failed to save pending consumptions: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Task-based API
        // ------------------------------------------------------------------

        private Task<T> Await<T>(Action<Action<T>, Action<string>> start)
        {
            var tcs = new TaskCompletionSource<T>();
            var before = LastError;
            try
            {
                start(v => tcs.TrySetResult(v), msg =>
                {
                    var typed = LastError != null && !ReferenceEquals(LastError, before) ? LastError : LTLMRequestError.Local(msg);
                    tcs.TrySetException(new LTLMException(typed));
                });
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            return tcs.Task;
        }

        public Task<LicenseData> ActivateLicenseAsync(string licenseKey) =>
            Await<LicenseData>((ok, fail) => ActivateLicense(licenseKey, (l, s) => ok(l), fail));

        public Task<LicenseData> ValidateLicenseAsync(string licenseKey) =>
            Await<LicenseData>((ok, fail) => ValidateLicense(licenseKey, (l, s) => ok(l), fail));

        public Task<LicenseData> TryLoadStoredLicenseAsync() =>
            Await<LicenseData>((ok, fail) => TryLoadStoredLicense((l, s) => ok(l), fail));

        public Task<LicenseData> RefreshAsync() =>
            Await<LicenseData>((ok, fail) => Refresh((l, s) => ok(l), fail));

        public Task<LicenseData> ReactivateSeatAsync() =>
            Await<LicenseData>((ok, fail) => ReactivateSeat((l, s) => ok(l), fail));

        public Task<LicenseData> ConsumeTokensAsync(int amount, string action) =>
            Await<LicenseData>((ok, fail) => ConsumeTokens(amount, action, ok, fail));

        public Task<GetSeatsResponse> GetActiveSeatsAsync() =>
            Await<GetSeatsResponse>((ok, fail) => GetActiveSeats(ok, fail));

        public Task<ReleaseSeatResponse> ReleaseSeatAsync(string targetHwid, bool claimSeat = true) =>
            Await<ReleaseSeatResponse>((ok, fail) => ReleaseSeat(targetHwid, claimSeat, ok, fail));

        public Task<Dictionary<string, object>> GetUserSettingsAsync() =>
            Await<Dictionary<string, object>>((ok, fail) => GetUserSettings(ok, fail));

        public Task SaveUserSettingsAsync(Dictionary<string, object> settings) =>
            Await<bool>((ok, fail) => SaveUserSettings(settings, () => ok(true), fail));

        public Task<List<PolicyData>> GetBuyablePoliciesAsync() =>
            Await<List<PolicyData>>((ok, fail) => GetBuyablePolicies(ok, fail));

        public Task<string> CreateCheckoutSessionAsync(string policyId, string customerEmail, string redirectUrl) =>
            Await<string>((ok, fail) => CreateCheckoutSession(policyId, customerEmail, redirectUrl, ok, fail));

        public Task<string> CreateTopUpSessionAsync(string packId, string redirectUrl) =>
            Await<string>((ok, fail) => CreateTopUpSession(packId, redirectUrl, ok, fail));

        public Task<TrialEligibilityResult> CheckTrialEligibilityAsync(string email) =>
            Await<TrialEligibilityResult>((ok, fail) => CheckTrialEligibility(email, ok, fail));

        public Task RequestOTPAsync(string email) =>
            Await<bool>((ok, fail) => RequestOTP(email, () => ok(true), fail));

        public Task<List<LicenseData>> VerifyOTPAsync(string email, string otp) =>
            Await<List<LicenseData>>((ok, fail) => VerifyOTP(email, otp, ok, fail));

        public Task<bool> DeactivateSeatAsync() =>
            Await<bool>((ok, fail) => { if (!DeactivateSeat(() => ok(true), fail)) fail("Seat release was not sent."); });

        public Task SignOutAsync() =>
            Await<bool>((ok, fail) => SignOut(() => ok(true)));
    }
}
