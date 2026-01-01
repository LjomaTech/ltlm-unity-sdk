using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        /// <summary>License is expired but within the configured grace period.</summary>
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
        Kicked
    }

    /// <summary>
    /// The main singleton manager for LTLM (License &amp; Token Management) integration in Unity.
    /// Handles license activation, validation, heartbeats, token consumption, and enforcement.
    /// 
    /// <para><b>Quick Start:</b></para>
    /// <code>
    /// // Activate a license
    /// LTLMManager.Instance.ActivateLicense("YOUR-LICENSE-KEY",
    ///     (license, status) => Debug.Log($"Activated! Status: {status}"),
    ///     error => Debug.LogError(error)
    /// );
    /// 
    /// // Check entitlement
    /// if (LTLMManager.Instance.IsEntitled("premium_feature", requiredTokens: 5))
    /// {
    ///     // Enable feature
    /// }
    /// 
    /// // Consume tokens
    /// LTLMManager.Instance.ConsumeTokens(1, "export_model",
    ///     license => Debug.Log($"Remaining: {license.tokensRemaining}"),
    ///     error => Debug.LogError(error)
    /// );
    /// </code>
    /// 
    /// <para><b>Lifecycle:</b></para>
    /// <list type="bullet">
    ///   <item>Automatically persists across scene loads (DontDestroyOnLoad)</item>
    ///   <item>Pauses heartbeat when app is backgrounded (mobile)</item>
    ///   <item>Releases concurrent seat on application quit</item>
    ///   <item>Supports offline usage with configurable grace period</item>
    /// </list>
    /// 
    /// <para><b>Platforms:</b></para>
    /// All platforms supported (Windows, macOS, Linux, Android, iOS, WebGL).
    /// See <see cref="LTLM.SDK.Core.Storage.PlatformStorage"/> for platform-specific notes.
    /// </summary>
    public class LTLMManager : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance of the LTLMManager.
        /// Access via <c>LTLMManager.Instance</c> after initialization.
        /// </summary>
        public static LTLMManager Instance { get; private set; }

        [Header("Project Configuration")]
        [Tooltip("Your LTLM Project ID. Obtained from the dashboard or Editor tools.")]
        public string projectId;
        
        [Tooltip("Ed25519 Public Key for signature verification. PEM format.")]
        public string publicKey;
        
        [Tooltip("AES-256 Secret Key for encryption. 64-character hex string. Keep this secret!")]
        public string secretKey;

        [Header("Settings")]
        [Tooltip("Interval between heartbeat requests in seconds. Default: 300 (5 minutes).")]
        public float heartbeatIntervalSeconds = 300f;
        
        [Tooltip("If true, automatically attempts to load and validate a stored license on Start().")]
        public bool autoValidateOnStart = true;
        
        [Tooltip("Your application/software version. If empty, uses Application.version.")]
        public string softwareVersion;

        private LTLMClient _client;
        private LicenseData _activeLicense;
        private Coroutine _heartbeatRoutine;
        private bool _isValidating = false;
        private bool _isSyncingTokens = false;
        private List<ConsumptionRequest> _pendingConsumptions = new List<ConsumptionRequest>();

        /// <summary>
        /// Returns true if the current license is active and valid.
        /// </summary>
        public bool IsAuthenticated => _activeLicense != null && _activeLicense.status == "active";
        
        /// <summary>
        /// Returns true if a validation request is currently in progress.
        /// </summary>
        public bool IsValidating => _isValidating;
        
        /// <summary>
        /// The currently loaded license data, or null if not authenticated.
        /// </summary>
        public LicenseData ActiveLicense => _activeLicense;

        // ============================================================
        // EVENTS - Subscribe to these to be notified of SDK state changes
        // ============================================================

        /// <summary>
        /// Fired when validation starts (including auto-validation on startup).
        /// Use this to show a loading indicator.
        /// </summary>
        public static event Action OnValidationStarted;

        /// <summary>
        /// Fired when validation completes (success or failure).
        /// Parameters: (bool success, LicenseStatus status)
        /// Use this to hide loading and update UI.
        /// </summary>
        public static event Action<bool, LicenseStatus> OnValidationCompleted;

        /// <summary>
        /// Fired when license status changes.
        /// </summary>
        public static event Action<LicenseStatus> OnLicenseStatusChanged;

        /// <summary>
        /// Fired after tokens are consumed.
        /// </summary>
        public static event Action<LicenseData> OnTokensConsumed;

        /// <summary>
        /// Fired when seat status changes (OCCUPIED, NO_SEAT, RELEASED).
        /// Parameters: (string seatStatus, int activeSeats, int maxSeats)
        /// </summary>
        public static event Action<string, int, int> OnSeatStatusChanged;

        /// <summary>
        /// Fired when this device is kicked by another device claiming its seat.
        /// Parameter: (KickedNotice notice)
        /// </summary>
        public static event Action<KickedNotice> OnKicked;

        /// <summary>
        /// Returns true if license is valid but waiting for a seat.
        /// </summary>
        public bool IsWaitingForSeat => _activeLicense != null && _activeLicense.status == "VALID_NO_SEAT";

        /// <summary>
        /// Returns true if concurrent seats are enabled for the current license.
        /// </summary>
        public bool IsSeatsEnabled => _activeLicense != null && (_activeLicense.seatsEnabled ?? false);

        /// <summary>
        /// Returns true if token consumption is enabled for the current license.
        /// </summary>
        public bool IsTokensEnabled => _activeLicense != null && (_activeLicense.tokensEnabled ?? false);

        /// <summary>
        /// Returns true if offline mode (grace period) is allowed for the current license.
        /// </summary>
        public bool IsOfflineEnabled => _activeLicense != null && (_activeLicense.offlineEnabled ?? true);

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Load settings if not manually set in inspector
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(publicKey))
            {
                var settings = LTLMSettings.Load();
                if (settings != null)
                {
                    projectId = settings.projectId;
                    publicKey = settings.publicKey;
                    secretKey = settings.secretKey;
                }
            }

            _client = new LTLMClient(LTLMConstants.BackendUrl, projectId, secretKey, publicKey);
            if (string.IsNullOrEmpty(softwareVersion)) softwareVersion = Application.version;
            LoadPendingConsumptions();
        }

        public LTLMClient GetClient() => _client;

        private void Start()
        {
            if (autoValidateOnStart)
            {
                TryLoadStoredLicense();
            }
        }

        /// <summary>
        /// Attempts to load and validate a previously stored license.
        /// Uses ValidateLicense to check status without re-registering the HWID.
        /// If nonce desync occurs (from crash/power outage), auto-recovery will handle it.
        /// </summary>
        public void TryLoadStoredLicense(Action<LicenseData, LicenseStatus> onSuccess = null, Action<string> onError = null)
        {
            if (_isValidating) return;
            
            string storedKey = SecureStorage.Load("license_key_" + projectId, DeviceID.GetHWID());
            if (!string.IsNullOrEmpty(storedKey))
            {
                // Use ValidateLicense to check status without re-registering HWID.
                // This respects admin actions like HWID removal from portal.
                // Auto-recovery handles nonce desync from crashes.
                ValidateLicense(storedKey, onSuccess, onError);
            }
            else
            {
                // Fire events so UI knows there's no stored license
                OnValidationCompleted?.Invoke(false, LicenseStatus.Unauthenticated);
                onError?.Invoke("No stored license found.");
            }
        }

        public void ActivateLicense(string licenseKey, Action<LicenseData, LicenseStatus> onSuccess = null, Action<string> onError = null)
        {
            if (_isValidating) return;
            _isValidating = true;
            
            // Fire event so UI can show loading state
            OnValidationStarted?.Invoke();

            var request = new ActivationRequest
            {
                key = licenseKey,
                hwid = DeviceID.GetHWID(),
                version = LTLMConstants.Version,
                clientVersion = softwareVersion,
                meta = new Dictionary<string, object>
                {
                    { "hostname", DeviceID.GetDeviceName() },
                    { "os", DeviceID.GetOS() }
                }
            };

            StartCoroutine(_client.PostEncrypted<ActivationRequest, LicenseData>(
                "/v1/sdk/pro/license/validate",
                request,
                license =>
                {
                    _isValidating = false;
                    _activeLicense = license;
                    var status = GetLicenseStatus();
                    OnValidationCompleted?.Invoke(true, status);
                    OnLicenseStatusChanged?.Invoke(status);
                    CacheLicense(license);
                    SecureStorage.Save("license_key_" + projectId, licenseKey, DeviceID.GetHWID());
                    SecureStorage.Save("last_successful_sync_" + projectId, SecureClock.GetEffectiveTime(projectId).Ticks.ToString(), DeviceID.GetHWID());
                    ProcessEnforcement(license);
                    ProcessSeatStatus(license);
                    if (_activeLicense == null) {
                        onError?.Invoke("This device is not authorized for this license.");
                        return;
                    }
                    StartHeartbeat();
                    onSuccess?.Invoke(license, GetLicenseStatus());
                    SyncPendingConsumptions();
                },
                err => {
                    _isValidating = false;
                    OnValidationCompleted?.Invoke(false, LicenseStatus.Unauthenticated);
                    onError?.Invoke(err);
                }
            ));
        }

        public void ValidateLicense(string licenseKey, Action<LicenseData, LicenseStatus> onSuccess = null, Action<string> onError = null)
        {
            if (_isValidating) return;
            _isValidating = true;
            
            // Fire event so UI can show loading state
            OnValidationStarted?.Invoke();
            
            var request = new ActivationRequest
            {
                key = licenseKey,
                hwid = DeviceID.GetHWID(),
                version = LTLMConstants.Version,
                clientVersion = softwareVersion,
                meta = new Dictionary<string, object>
                {
                    { "hostname", DeviceID.GetDeviceName() },
                    { "os", DeviceID.GetOS() }
                }
            };
 
            StartCoroutine(_client.PostEncrypted<ActivationRequest, LicenseData>(
                "/v1/sdk/pro/license/status",  
                request,
                license =>
                {
                    _isValidating = false;
                    _activeLicense = license;
                    var status = GetLicenseStatus();
                    OnValidationCompleted?.Invoke(true, status);
                    OnLicenseStatusChanged?.Invoke(status);
                    CacheLicense(license);
                    SecureStorage.Save("last_successful_sync_" + projectId, SecureClock.GetEffectiveTime(projectId).Ticks.ToString(), DeviceID.GetHWID());
                    ProcessEnforcement(license);
                    ProcessSeatStatus(license);
                    if (_activeLicense == null) {
                        onError?.Invoke("This device is not authorized for this license.");
                        return;
                    }
                    StartHeartbeat();
                    onSuccess?.Invoke(license, status);
                    SyncPendingConsumptions();
                },
                err => {
                    _isValidating = false;
                    Debug.LogWarning("[LTLM] Network Validation Failed. Checking offline grace...");
                    if (CheckOfflineGraceTimeout())
                    {
                        var status = GetLicenseStatus();
                        OnValidationCompleted?.Invoke(true, status);
                        onSuccess?.Invoke(_activeLicense, status);
                    }
                    else
                    {
                        OnValidationCompleted?.Invoke(false, LicenseStatus.ConnectionRequired);
                        onError?.Invoke("Offline grace period exceeded or no cached license found.");
                    }
                }
            ));
        }

        /// <summary>
        /// Activates a license using an offline .ltlm file blob.
        /// </summary>
        public void ActivateOffline(string encryptedBlob, Action<LicenseData, LicenseStatus> onSuccess = null, Action<string> onError = null)
        {
            try
            {
                string decrypted = _client.GetCrypto().Decrypt(encryptedBlob);
                
                // Parse with Newtonsoft for stable verification
                var settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
                var envelope = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(decrypted, settings);
                string signature = envelope["signature"]?.ToString();
                
                if (string.IsNullOrEmpty(signature))
                {
                    onError?.Invoke("Invalid offline license: missing signature.");
                    return;
                }

                envelope.Remove("signature");
                string verificationJson = LTLMClient.ToStableJson(envelope);

                if (_client.GetCrypto().VerifySignature(verificationJson, signature))
                {
                    var signed = JsonConvert.DeserializeObject<SignedResponse<LicenseData>>(decrypted);
                    var license = signed.data;
                    
                    // Immediate Expiry Check with Monotonic Clock
                    DateTime now = SecureClock.GetEffectiveTime(projectId);
                    if (!string.IsNullOrEmpty(license.validUntil) && DateTime.TryParse(license.validUntil, out DateTime expiry))
                    {
                        bool isPerp = license.policy != null && license.policy.type == "perpetual";
                        if (now > expiry && !isPerp)
                        {
                            onError?.Invoke("The offline license has expired.");
                            return;
                        }
                    }

                    _activeLicense = license;
                    SecureStorage.Save("license_key_" + projectId, _activeLicense.licenseKey, DeviceID.GetHWID());
                    ProcessEnforcement(_activeLicense);
                    if (_activeLicense == null) {
                        onError?.Invoke("This device is not authorized in the offline license blob.");
                        return;
                    }
                    onSuccess?.Invoke(_activeLicense, GetLicenseStatus());
                }
                else
                {
                    onError?.Invoke("Invalid signature on offline license.");
                }
            }
            catch (Exception ex)
            {
                onError?.Invoke("Failed to process offline license: " + ex.Message);
            }
        }

        private void StartHeartbeat()
        {
            if (_heartbeatRoutine != null) StopCoroutine(_heartbeatRoutine);
            _heartbeatRoutine = StartCoroutine(HeartbeatRoutine());
        }

        private IEnumerator HeartbeatRoutine()
        {
            while (true)
            {
                if (_activeLicense == null) break;

                var request = new HeartbeatRequest
                {
                    key = _activeLicense.licenseKey,
                    hwid = DeviceID.GetHWID(),
                    isClosing = false
                };

                yield return _client.PostEncrypted<HeartbeatRequest, LicenseData>(
                    "/v1/sdk/pro/license/heartbeat",
                    request,
                    license => {
                        _activeLicense = license;
                        CacheLicense(license);
                        SecureStorage.Save("last_successful_sync_" + projectId, SecureClock.GetEffectiveTime(projectId).Ticks.ToString(), DeviceID.GetHWID());
                        ProcessEnforcement(license);
                        ProcessSeatStatus(license);
                        SyncPendingConsumptions();
                    },
                    err => {
                        Debug.LogWarning("[LTLM] Heartbeat failed: " + err);
                        
                        // Automatic recovery for Nonce Mismatch or Tamper signals
                        if (err.Contains("Nonce Mismatch") || err.Contains("TAMPERED"))
                        {
                            Debug.Log("[LTLM] Security out-of-sync. Triggering re-activation...");
                            ActivateLicense(_activeLicense.licenseKey);
                        }
                        else
                        {
                            CheckOfflineGraceTimeout();
                        }
                    }
                );
                yield return new WaitForSeconds(heartbeatIntervalSeconds);
            }
        }

        /// <summary>
        /// Handles application pause/resume events (mobile platforms).
        /// Pauses heartbeat when app is backgrounded to save battery.
        /// </summary>
        /// <param name="isPaused">True if app is paused/backgrounded, false when resumed.</param>
        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                // App is being backgrounded - pause heartbeat
                if (_heartbeatRoutine != null)
                {
                    StopCoroutine(_heartbeatRoutine);
                    _heartbeatRoutine = null;
                    Debug.Log("[LTLM] Heartbeat paused (app backgrounded).");
                }
            }
            else
            {
                // App is resuming - restart heartbeat if we have an active license
                if (_activeLicense != null && _heartbeatRoutine == null)
                {
                    StartHeartbeat();
                    Debug.Log("[LTLM] Heartbeat resumed (app foregrounded).");
                    
                    // Sync any pending token consumptions that were queued while offline
                    if (_pendingConsumptions.Count > 0 && Application.internetReachability != NetworkReachability.NotReachable)
                    {
                        Debug.Log($"[LTLM] Syncing {_pendingConsumptions.Count} offline token consumptions...");
                        SyncPendingConsumptions();
                    }
                }
            }
        }

        /// <summary>
        /// Called when application is quitting. Attempts to release the concurrent seat.
        /// Uses fire-and-forget pattern as coroutines won't complete during quit.
        /// </summary>
        private void OnApplicationQuit()
        {
            DeactivateSeatSync();
        }

        /// <summary>
        /// Cleanup when the manager is destroyed (scene unload, etc.).
        /// Stops all active coroutines to prevent orphaned callbacks.
        /// </summary>
        private void OnDestroy()
        {
            if (_heartbeatRoutine != null)
            {
                StopCoroutine(_heartbeatRoutine);
                _heartbeatRoutine = null;
            }
            
            // Clear singleton reference if this instance is being destroyed
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Explicitly releases the concurrent seat for this machine.
        /// Use this for 'Sign Out' functionality or when switching users.
        /// 
        /// <para><b>Note:</b> This method requires network connectivity.
        /// Signout is blocked when offline to prevent license abuse.</para>
        /// 
        /// <para><b>Example:</b></para>
        /// <code>
        /// LTLMManager.Instance.DeactivateSeat();
        /// </code>
        /// </summary>
        /// <returns>True if deactivation was initiated, false if blocked (offline or no license)</returns>
        public bool DeactivateSeat()
        {
            if (_activeLicense == null) return false;

            // Block signout when offline to prevent abuse
            // (User could go offline, signout, share license, then claim they were offline)
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.LogWarning("[LTLM] Cannot sign out while offline. Please connect to the internet.");
                return false;
            }

            var request = new HeartbeatRequest
            {
                key = _activeLicense.licenseKey,
                hwid = DeviceID.GetHWID(),
                isClosing = true
            };

            // Use skipNonce=true to prevent nonce desync on app close
            StartCoroutine(_client.PostEncrypted<HeartbeatRequest, LicenseData>(
                "/v1/sdk/pro/license/heartbeat",
                request,
                _ => Debug.Log("[LTLM] Seat released successfully."),
                _ => { },
                skipNonce: true
            ));

            return true;
        }

        // ============================================================
        // SEAT MANAGEMENT METHODS
        // ============================================================

        /// <summary>
        /// Gets a list of all devices currently holding seats on this license.
        /// Use when status is VALID_NO_SEAT to show seat management UI.
        /// </summary>
        public void GetActiveSeats(Action<GetSeatsResponse> onSuccess, Action<string> onError = null)
        {
            if (_activeLicense == null)
            {
                onError?.Invoke("No active license");
                return;
            }

            if (_activeLicense.seatsEnabled == false)
            {
                Debug.LogWarning("[LTLM] Seat management is disabled for this license. GetActiveSeats will fail or return empty.");
            }

            string hwid = DeviceID.GetHWID();
            string endpoint = $"/v1/sdk/license/{_activeLicense.licenseKey}/seats?hwid={hwid}";

            StartCoroutine(_client.GetRequest<GetSeatsResponse>(
                endpoint,
                onSuccess,
                onError
            ));
        }

        /// <summary>
        /// Releases another device's seat so this device can claim it.
        /// </summary>
        /// <param name="targetHwid">The HWID of the device to disconnect</param>
        /// <param name="claimSeat">If true, this device will claim the released seat</param>
        /// <param name="onSuccess">Called on successful release</param>
        /// <param name="onError">Called on error</param>
        public void ReleaseSeat(string targetHwid, bool claimSeat, Action<ReleaseSeatResponse> onSuccess, Action<string> onError = null)
        {
            if (_activeLicense == null)
            {
                onError?.Invoke("No active license");
                return;
            }

            if (_activeLicense.seatsEnabled == false)
            {
                onError?.Invoke("Seat management is disabled for this license.");
                return;
            }

            var request = new ReleaseSeatRequest
            {
                targetHwid = targetHwid,
                callerHwid = DeviceID.GetHWID(),
                claimSeat = claimSeat
            };

            StartCoroutine(_client.PostEncrypted<ReleaseSeatRequest, ReleaseSeatResponse>(
                $"/v1/sdk/license/{_activeLicense.licenseKey}/release-seat",
                request,
                response => {
                    if (response.seatClaimed)
                    {
                        Debug.Log("[LTLM] Seat claimed successfully!");
                        // Re-validate to get updated license state
                        ValidateLicense(_activeLicense.licenseKey);
                    }
                    onSuccess?.Invoke(response);
                },
                onError
            ));
        }

        /// <summary>
        /// Attempts to reactivate a seat after being kicked.
        /// <para>Call this after receiving a Kicked status or OnKicked event.</para>
        /// <para>If a seat is available, it will be claimed and the heartbeat resumed.</para>
        /// </summary>
        /// <param name="onSuccess">Called with updated license data if reactivation succeeds</param>
        /// <param name="onError">Called if reactivation fails (e.g., no seats available)</param>
        public void ReactivateSeat(Action<LicenseData, LicenseStatus> onSuccess = null, Action<string> onError = null)
        {
            if (_activeLicense == null)
            {
                onError?.Invoke("No active license to reactivate.");
                return;
            }

            Debug.Log("[LTLM] Attempting seat reactivation...");

            // Re-validate the license - this will attempt to claim a seat
            ValidateLicense(_activeLicense.licenseKey, 
                (license, status) => {
                    if (status == LicenseStatus.Active)
                    {
                        Debug.Log("[LTLM] Seat reactivation successful!");
                        onSuccess?.Invoke(license, status);
                    }
                    else if (status == LicenseStatus.ValidNoSeat)
                    {
                        Debug.LogWarning("[LTLM] Reactivation failed: No seats available.");
                        onError?.Invoke("No seats available. All concurrent seats are in use.");
                    }
                    else
                    {
                        Debug.LogWarning($"[LTLM] Reactivation returned status: {status}");
                        onSuccess?.Invoke(license, status);
                    }
                },
                onError
            );
        }

        #region User Settings (Cloud Sync)
        
        /// <summary>
        /// Gets user settings from the server (synced across devices).
        /// </summary>
        /// <param name="onSuccess">Called with the settings dictionary</param>
        /// <param name="onError">Called on error</param>
        public void GetUserSettings(Action<Dictionary<string, object>> onSuccess, Action<string> onError = null)
        {
            if (_activeLicense == null)
            {
                onError?.Invoke("No active license. Activate first.");
                return;
            }

            Debug.Log("[LTLM] Fetching user settings from server...");

            var request = new UserSettingsRequest
            {
                key = _activeLicense.licenseKey,
                hwid = DeviceID.GetHWID()
            };

            StartCoroutine(_client.PostEncrypted<UserSettingsRequest, UserSettingsResponse>(
                "/v1/sdk/pro/license/settings/get",
                request,
                response => {
                    Debug.Log("[LTLM] User settings retrieved successfully.");
                    onSuccess?.Invoke(response.settings ?? new Dictionary<string, object>());
                },
                onError
            ));
        }

        /// <summary>
        /// Saves user settings to the server (synced across devices).
        /// Maximum size: 64KB.
        /// </summary>
        /// <param name="settings">Dictionary of settings to save</param>
        /// <param name="onSuccess">Called on successful save</param>
        /// <param name="onError">Called on error</param>
        public void SaveUserSettings(Dictionary<string, object> settings, Action onSuccess = null, Action<string> onError = null)
        {
            if (_activeLicense == null)
            {
                onError?.Invoke("No active license. Activate first.");
                return;
            }

            if (settings == null)
            {
                onError?.Invoke("Settings cannot be null.");
                return;
            }

            // Check size limit locally before sending
            string jsonCheck = JsonConvert.SerializeObject(settings);
            if (jsonCheck.Length > 65536)
            {
                onError?.Invoke($"Settings exceed maximum size of 64KB. Current size: {jsonCheck.Length} bytes.");
                return;
            }

            Debug.Log("[LTLM] Saving user settings to server...");

            var request = new SaveUserSettingsRequest
            {
                key = _activeLicense.licenseKey,
                hwid = DeviceID.GetHWID(),
                settings = settings
            };

            StartCoroutine(_client.PostEncrypted<SaveUserSettingsRequest, UserSettingsResponse>(
                "/v1/sdk/pro/license/settings/save",
                request,
                response => {
                    Debug.Log("[LTLM] User settings saved successfully.");
                    // Update local cache
                    if (_activeLicense != null)
                    {
                        _activeLicense.userSettings = settings;
                        CacheLicense(_activeLicense);
                    }
                    onSuccess?.Invoke();
                },
                onError
            ));
        }

        /// <summary>
        /// Gets user settings from the local license cache (no network call).
        /// Returns empty dictionary if no settings cached.
        /// </summary>
        public Dictionary<string, object> GetLocalUserSettings()
        {
            return _activeLicense?.userSettings ?? new Dictionary<string, object>();
        }

        #endregion

        /// <summary>
        /// Helper method to process seat status and fire events.
        /// Called after heartbeats and validations.
        /// </summary>
        private void ProcessSeatStatus(LicenseData license)
        {
            if (license == null) return;

            // Check if we were kicked - handle this FIRST before seat status
            if (license.status == "KICKED" || license.seatStatus == "KICKED" || license.kickedNotice != null)
            {
                string kickedBy = license.kickedNotice?.kickedByNickname ?? license.kickedNotice?.kickedBy ?? "another device";
                Debug.LogWarning($"[LTLM] KICKED: Your session was terminated by {kickedBy}. You must reactivate to continue.");
                
                // Stop heartbeat - don't keep trying to auto-register
                if (_heartbeatRoutine != null)
                {
                    StopCoroutine(_heartbeatRoutine);
                    _heartbeatRoutine = null;
                }
                
                // Fire events
                OnSeatStatusChanged?.Invoke("KICKED", license.activeSeats ?? 0, license.maxConcurrentSeats ?? 0);
                if (license.kickedNotice != null)
                {
                    OnKicked?.Invoke(license.kickedNotice);
                }
                
                // Fire validation event as invalid (forces UI to show reactivation prompt)
                OnValidationCompleted?.Invoke(false, LicenseStatus.Kicked);
                OnLicenseStatusChanged?.Invoke(LicenseStatus.Kicked);
                return;
            }

            // Check for VALID_NO_SEAT status - seat was lost during heartbeat
            if (license.status == "VALID_NO_SEAT" || license.seatStatus == "NO_SEAT")
            {
                Debug.LogWarning("[LTLM] VALID_NO_SEAT: License is valid but no seat is available.");
                
                // Fire events to notify UI of seat loss
                OnSeatStatusChanged?.Invoke("NO_SEAT", license.activeSeats ?? 0, license.maxConcurrentSeats ?? 0);
                OnValidationCompleted?.Invoke(true, LicenseStatus.ValidNoSeat);
                OnLicenseStatusChanged?.Invoke(LicenseStatus.ValidNoSeat);
                return;
            }

            // Fire seat status changed event for normal status
            if (!string.IsNullOrEmpty(license.seatStatus))
            {
                OnSeatStatusChanged?.Invoke(
                    license.seatStatus,
                    license.activeSeats ?? 0,
                    license.maxConcurrentSeats ?? 0
                );
                
                // If status is OCCUPIED (has seat), fire active status
                if (license.seatStatus == "OCCUPIED")
                {
                    var status = GetLicenseStatus();
                    OnLicenseStatusChanged?.Invoke(status);
                }
            }
        }

        /// <summary>
        /// Clears the stored nonce to force a fresh nonce chain on next activation.
        /// </summary>
        private void ClearStoredNonce()
        {
            SecureStorage.Delete("nonce_" + projectId, DeviceID.GetHWID());
        }

        /// <summary>
        /// Synchronous/fire-and-forget seat release for application quit scenarios.
        /// Clears the stored nonce to prevent desync issues on next startup.
        /// </summary>
        private void DeactivateSeatSync()
        {
            if (_activeLicense == null) return;

            try
            {
                // Clear stored nonce FIRST to prevent desync if the request doesn't complete
                ClearStoredNonce();

                var request = new HeartbeatRequest
                {
                    key = _activeLicense.licenseKey,
                    hwid = DeviceID.GetHWID(),
                    isClosing = true
                };

                Debug.Log("[LTLM] Attempting seat release on quit...");
                
                // Fire-and-forget: start request but it may not complete before app terminates
                StartCoroutine(_client.PostEncrypted<HeartbeatRequest, LicenseData>(
                    "/v1/sdk/pro/license/heartbeat",
                    request,
                    _ => { },
                    _ => { }
                ));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LTLM] Seat release on quit failed: {ex.Message}");
            }
        }

        private bool CheckOfflineGraceTimeout()
        {
            if (_activeLicense == null)
            {
                _activeLicense = LoadLicenseFromCache();
            }

            if (_activeLicense == null) return false;

            // Check if offline mode is enabled by the policy
            if (_activeLicense.offlineEnabled.HasValue && !_activeLicense.offlineEnabled.Value)
            {
                Debug.LogError("[LTLM] Offline mode is DISABLED for this license. Connection required.");
                _activeLicense.status = "connection_required";
                ProcessEnforcement(_activeLicense);
                return false;
            }

            string rawLastSync = SecureStorage.Load("last_successful_sync_" + projectId, DeviceID.GetHWID());
            if (!long.TryParse(rawLastSync, out long ticks)) return false;

            DateTime lastSync = new DateTime(ticks, DateTimeKind.Utc);
            DateTime now = SecureClock.GetEffectiveTime(projectId);
            TimeSpan offlineDuration = now - lastSync;

            // Use server-specified offline grace hours (default 0 = no grace period)
            float graceHours = 0f; // Default: no offline grace (require connection immediately)
            if (_activeLicense.offlineGraceHours.HasValue && _activeLicense.offlineGraceHours.Value > 0)
            {
                graceHours = _activeLicense.offlineGraceHours.Value;
            }

            // If graceHours is 0, offline is not allowed at all
            if (graceHours <= 0)
            {
                Debug.LogError("[LTLM] No offline grace period configured. Connection required.");
                _activeLicense.status = "connection_required";
                ProcessEnforcement(_activeLicense);
                OnLicenseStatusChanged?.Invoke(LicenseStatus.ConnectionRequired);
                return false;
            }

            if (offlineDuration.TotalHours > graceHours)
            {
                Debug.LogError($"[LTLM] OFFLINE GRACE PERIOD EXCEEDED ({graceHours} hours). Connection required.");
                LogEvent("OfflineGraceExceeded", new Dictionary<string, object> { { "duration_hours", offlineDuration.TotalHours } });
                
                // Lockdown the SDK
                _activeLicense.status = "connection_required";
                ProcessEnforcement(_activeLicense);
                OnLicenseStatusChanged?.Invoke(LicenseStatus.ConnectionRequired);
                return false;
            }
            else
            {
                Debug.LogWarning($"[LTLM] Running in offline grace mode. {graceHours - offlineDuration.TotalHours:F1} hours remaining.");
                // Fire GracePeriod status so UI knows we're in grace mode
                OnLicenseStatusChanged?.Invoke(LicenseStatus.GracePeriod);
                return true;
            }
        }

        private void CacheLicense(LicenseData license)
        {
            try {
                string json = JsonConvert.SerializeObject(license);
                SecureStorage.Save("license_cache_" + projectId, json, DeviceID.GetHWID());
                SecureStorage.Save("cached_tokens_" + projectId, (license.tokensRemaining ?? 0).ToString(), DeviceID.GetHWID());
            } catch (Exception e) {
                Debug.LogError("[LTLM] Failed to cache license: " + e.Message);
            }
        }

        private LicenseData LoadLicenseFromCache()
        {
            try {
                string json = SecureStorage.Load("license_cache_" + projectId, DeviceID.GetHWID());
                if (string.IsNullOrEmpty(json)) return null;
                return JsonConvert.DeserializeObject<LicenseData>(json);
            } catch {
                return null;
            }
        }

        private void ProcessEnforcement(LicenseData license)
        {
            if (license == null) return;

            // 1. Force Heartbeat Interval from server settings
            // If server specifies an interval, it MUST be used, overriding inspector settings.
            if (license.heartbeatIntervalSeconds.HasValue && license.heartbeatIntervalSeconds.Value > 0)
            {
                float serverInterval = license.heartbeatIntervalSeconds.Value;
                if (Mathf.Abs(heartbeatIntervalSeconds - serverInterval) > 0.1f)
                {
                    Debug.Log($"[LTLM] Server forced heartbeat interval change: {heartbeatIntervalSeconds}s -> {serverInterval}s");
                    heartbeatIntervalSeconds = serverInterval;
                    
                    // Restart heartbeat routine to apply the new interval immediately
                    if (_heartbeatRoutine != null)
                    {
                        StopCoroutine(_heartbeatRoutine);
                        _heartbeatRoutine = StartCoroutine(HeartbeatRoutine());
                    }
                }
            }

            // 2. Feature Enforcement (Hard-blocking or warning based on license capabilities)
            
            // Token Enforcement
            if (license.tokensEnabled == false)
            {
                // If developer tries to consume tokens but they are disabled
                if (_pendingConsumptions.Count > 0)
                {
                    Debug.LogError("[LTLM] ENFORCEMENT ERROR: Application is attempting to use tokens, but Token Consumption is DISABLED for this license. Please check your policy settings in the LTLM Portal.");
                }
            }

            // Seat Enforcement
            if (license.seatsEnabled == false)
            {
                // If seats are disabled but status is VALID_NO_SEAT, something is wrong on server or local state
                if (license.status == "VALID_NO_SEAT")
                {
                    Debug.LogError("[LTLM] ENFORCEMENT ERROR: License status is VALID_NO_SEAT but Seats are DISABLED. This license does not support concurrent seats.");
                }
            }

            // Offline Mode Enforcement
            if (license.offlineEnabled == false)
            {
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    Debug.LogError("[LTLM] ENFORCEMENT ERROR: Offline mode is DISABLED for this license. An active internet connection is required to continue using this software.");
                    // In a production app, you would fire an event here to show a "Please Connect" overlay
                }
            }

            // 3. HWID Check: Verify if this device is still registered to the license
            string currentHwid = DeviceID.GetHWID();
            bool isDeviceAuthorized = false;
            
            if (license.machines != null)
            {
                foreach (var machine in license.machines)
                {
                    if (machine.hwid == currentHwid)
                    {
                        isDeviceAuthorized = true;
                        break;
                    }
                }
            }

            if (!isDeviceAuthorized)
            {
                Debug.LogError("[LTLM] Device Authorization Failed: This hardware ID is not registered with the current license. De-activating...");
                ClearLicenseCache();
                return;
            }

            if (license.status == "terminated")
            {
                Debug.LogError("[LTLM] LICENSE TERMINATED BY SERVER. EXITING.");
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
                return;
            }

            if (license.status == "connection_required")
            {
                Debug.LogError("[LTLM] HEARTBEAT TIMEOUT. INTERNET CONNECTION REQUIRED TO CONTINUE.");
                return;
            }
            
            if (SecureClock.IsClockTampered(projectId))
            {
                Debug.LogError("[LTLM] CLOCK TAMPERING DETECTED. Access to features is restricted until the system clock is corrected.");
            }

            DateTime now = SecureClock.GetEffectiveTime(projectId);
            if (!string.IsNullOrEmpty(license.validUntil))
            {
                if (DateTime.TryParse(license.validUntil, out DateTime expiry))
                {
                    bool isPerp = license.policy != null && license.policy.type == "perpetual";
                    if (now > expiry && !isPerp)
                    {
                        if (license.status != "grace_period")
                        {
                            Debug.LogWarning($"[LTLM] License expired at {expiry}. Effective current time: {now}");
                            license.status = "expired";
                        }
                    }
                }
            }

            if (license.status == "expired")
            {
                Debug.LogWarning("[LTLM] License expired. Features may be limited.");
            }
        }

        #region Helper Functions

        /// <summary>
        /// Checks if the user has enough tokens.
        /// If forceRefresh is true, it calls the backend to get the latest count.
        /// </summary>
        public void DoesHaveTokens(int required, Action<bool> onResult, bool forceRefresh = false)
        {
            if (forceRefresh && _activeLicense != null)
            {
                ValidateLicense(_activeLicense.licenseKey);
                StartCoroutine(WaitForValidationAndCheck(required, onResult));
                return;
            }

            int currentTokens = 0;
            if (_activeLicense != null)
            {
                currentTokens = _activeLicense.tokensRemaining ?? 0;
            }
            else
            {
                string cached = SecureStorage.Load("cached_tokens_" + projectId, DeviceID.GetHWID());
                if (!int.TryParse(cached, out currentTokens)) currentTokens = 0;
            }

            onResult?.Invoke(currentTokens >= required);
        }

        private IEnumerator WaitForValidationAndCheck(int required, Action<bool> onResult)
        {
            yield return new WaitForSeconds(1f);
            DoesHaveTokens(required, onResult, false);
        }

        public bool HasCapability(string featureName)
        {
            if (_activeLicense == null || _activeLicense.config == null) return false;
            
            // Access features from config
            if (!_activeLicense.config.TryGetValue("features", out object featuresObj) || featuresObj == null) return false;
            
            var features = featuresObj as Dictionary<string, object>;
            if (features == null) return false;
            
            if (features.TryGetValue(featureName, out object val)) {
                if (val == null) return false;
                string sVal = val.ToString().ToLower();
                return sVal == "true" || sVal == "1";
            }
            return false;
        }

        public object GetMetadata(string key)
        {
            if (_activeLicense == null || _activeLicense.config == null) return null;
            
            // Access metadata from config
            if (!_activeLicense.config.TryGetValue("metadata", out object metadataObj) || metadataObj == null) return null;
            
            var metadata = metadataObj as Dictionary<string, object>;
            if (metadata == null) return null;
            
            if (metadata.TryGetValue(key, out object val)) return val;
            return null;
        }

        /// <summary>
        /// Universal Entitlement Check.
        /// </summary>
        public bool IsEntitled(string capability = null, int requiredTokens = 0)
        {
            var status = GetLicenseStatus();
            
            // Entitlements are only available for Active or GracePeriod licenses
            if (status != LicenseStatus.Active && status != LicenseStatus.GracePeriod) 
                return false;
            
            if (_activeLicense == null) return false;

            // Check specific capability if provided
            if (!string.IsNullOrEmpty(capability))
            {
                if (!HasCapability(capability)) return false;
            }

            // Check tokens
            if (requiredTokens > 0)
            {
                if ((_activeLicense.tokensRemaining ?? 0) < requiredTokens) return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the resolved status of the current license.
        /// </summary>
        public LicenseStatus GetLicenseStatus()
        {
            if (_activeLicense == null) return LicenseStatus.Unauthenticated;

            // 1. Clock Tampering high priority
            if (SecureClock.IsClockTampered(projectId)) return LicenseStatus.Tampered;

            // 2. Integrity Checks (File Deletion/Modification)
            if (SecureStorage.Load("license_key_" + projectId, DeviceID.GetHWID(), projectId) == "TAMPERED") return LicenseStatus.Tampered;
            if (SecureStorage.Load("cached_tokens_" + projectId, DeviceID.GetHWID(), projectId) == "TAMPERED") return LicenseStatus.Tampered;

            // 3. Map Backend Statuses
            switch (_activeLicense.status?.ToLower())
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
            }

            return LicenseStatus.Active;
        }

        /// <summary>
        /// Returns the number of tokens currently available (cached or live).
        /// </summary>
        public int GetTokenBalance()
        {
            if (_activeLicense != null) return _activeLicense.tokensRemaining ?? 0;
            
            string cached = SecureStorage.Load("cached_tokens_" + projectId, DeviceID.GetHWID());
            if (int.TryParse(cached, out int tokens)) return tokens;
            
            return 0;
        }

        /// <summary>
        /// Returns human-readable days remaining until license expiry.
        /// Returns -1 for perpetual or error.
        /// </summary>
        public int GetDaysRemaining()
        {
            bool isPerp = _activeLicense != null && _activeLicense.policy != null && _activeLicense.policy.type == "perpetual";
            if (_activeLicense == null || isPerp) return -1;
            if (string.IsNullOrEmpty(_activeLicense.validUntil)) return -1;

            if (DateTime.TryParse(_activeLicense.validUntil, out DateTime expiry))
            {
                TimeSpan remaining = expiry - SecureClock.GetEffectiveTime(projectId);
                return Math.Max(0, (int)remaining.TotalDays);
            }

            return -1;
        }

        /// <summary>
        /// Logs a custom analytic event to the LTLM server.
        /// </summary>
        public void LogEvent(string eventType, Dictionary<string, object> payload = null)
        {
            if (_activeLicense == null) return;

            var request = new EventRequest
            {
                key = _activeLicense.licenseKey,
                type = eventType,
                payload = payload ?? new Dictionary<string, object>()
            };

            StartCoroutine(_client.PostEncrypted<EventRequest, object>(
                "/v1/sdk/pro/license/events",
                request,
                res => Debug.Log($"[LTLM] Event '{eventType}' logged."),
                err => Debug.LogWarning($"[LTLM] Event logging failed: {err}")
            ));
        }

        /// <summary>
        /// Clears all local license data and de-authenticates.
        /// </summary>
        public void ClearLicenseCache()
        {
            _activeLicense = null;
            SecureStorage.Delete("license_cache_" + projectId);
            SecureStorage.Delete("license_key_" + projectId);
            SecureStorage.Delete("cached_tokens_" + projectId);
            SecureStorage.Delete("nonce_" + projectId);
            
            if (_heartbeatRoutine != null)
            {
                StopCoroutine(_heartbeatRoutine);
                _heartbeatRoutine = null;
            }
        }

        #endregion

        #region Commerce / Payments

        /// <summary>
        /// Fetches a list of active policies with a price > 0 for this project.
        /// </summary>
        public void GetBuyablePolicies(Action<List<PolicyData>> onSuccess, Action<string> onError = null)
        {
            // POST /v1/sdk/pro/project/buyable-policies – Encrypted
            StartCoroutine(_client.PostEncrypted<object, List<PolicyData>>(
                "/v1/sdk/pro/project/buyable-policies",
                new { hwid = DeviceID.GetHWID() },
                res => onSuccess?.Invoke(res),
                err => onError?.Invoke(err)
            ));
        }

        /// <summary>
        /// Generates a hosted checkout URL for a new license.
        /// </summary>
        public void CreateCheckoutSession(string policyId, string customerEmail, string redirectUrl, Action<string> onUrlReceived, Action<string> onError = null)
        {
            var request = new CheckoutRequest
            {
                policyId = policyId,
                customerEmail = customerEmail,
                redirectUrl = redirectUrl
            };

            StartCoroutine(_client.PostEncrypted<CheckoutRequest, CheckoutResponse>(
                "/v1/sdk/pro/project/checkout-link",
                request,
                res => onUrlReceived?.Invoke(res.checkoutUrl),
                err => onError?.Invoke(err)
            ));
        }

        /// <summary>
        /// Generates a hosted checkout URL for a token top-up.
        /// </summary>
        public void CreateTopUpSession(string packId, string redirectUrl, Action<string> onUrlReceived, Action<string> onError = null)
        {
            if (_activeLicense == null)
            {
                onError?.Invoke("Active license required for top-up.");
                return;
            }

            var request = new TopUpRequest
            {
                key = _activeLicense.licenseKey,
                packId = packId,
                redirectUrl = redirectUrl
            };

            StartCoroutine(_client.PostEncrypted<TopUpRequest, CheckoutResponse>(
                "/v1/sdk/pro/license/topup-link",
                request,
                res => onUrlReceived?.Invoke(res.checkoutUrl),
                err => onError?.Invoke(err)
            ));
        }

        #endregion

        public void ConsumeTokens(int amount, string action, Action<LicenseData> onConsumed = null, Action<string> onError = null)
        {
            if (_activeLicense == null) return;

            if (_activeLicense.tokensEnabled == false)
            {
                Debug.LogWarning("[LTLM] Token consumption is disabled for this license. Usage will be recorded on device but may be rejected by server.");
            }

            // 1. Create the consumption log
            var usage = new ConsumptionRequest
            {
                key = _activeLicense.licenseKey,
                amount = amount,
                action = action,
                hwid = DeviceID.GetHWID(),
                timestamp = SecureClock.GetEffectiveTime(projectId).ToString("o") // ISO 8601
            };

            // 2. Optimistic Update: Reflect the "truth" immediately for the game layer
            if (_activeLicense.tokensConsumed.HasValue) _activeLicense.tokensConsumed += amount;
            if (_activeLicense.tokensRemaining.HasValue) _activeLicense.tokensRemaining -= amount;
            
            SecureStorage.Save("cached_tokens_" + projectId, (_activeLicense.tokensRemaining ?? 0).ToString(), DeviceID.GetHWID());
            
            onConsumed?.Invoke(_activeLicense);
            
            // Fire the static event for global subscribers (optimistic)
            OnTokensConsumed?.Invoke(_activeLicense);

            // 3. Sync with server using appropriate API based on connectivity
            if (Application.internetReachability != NetworkReachability.NotReachable)
            {
                // ONLINE: Send immediately via single consumption API
                SendSingleConsumption(usage);
            }
            else
            {
                // OFFLINE: Queue for batch sync later
                _pendingConsumptions.Add(usage);
                SavePendingConsumptions();
                Debug.Log($"[LTLM] Offline - token consumption queued for sync when online ({_pendingConsumptions.Count} pending)");
            }
        }

        /// <summary>
        /// Sends a single token consumption to the server (used when online).
        /// </summary>
        private void SendSingleConsumption(ConsumptionRequest usage)
        {
            var request = new SingleConsumptionRequest
            {
                key = usage.key,
                amount = usage.amount,
                action = usage.action,
                hwid = usage.hwid
            };

            StartCoroutine(_client.PostEncrypted<SingleConsumptionRequest, LicenseData>(
                "/v1/sdk/pro/license/consume",
                request,
                fullLicense => {
                    // Update local license with absolute truth from server
                    _activeLicense = fullLicense;
                    CacheLicense(fullLicense);
                    Debug.Log("[LTLM] Token consumed. Server balance: " + fullLicense.tokensRemaining);
                    
                    // Fire event with server-confirmed data
                    OnTokensConsumed?.Invoke(fullLicense);
                },
                err => {
                    // If server sync fails, queue it for batch sync later
                    Debug.LogWarning("[LTLM] Token sync failed, queuing for later: " + err);
                    _pendingConsumptions.Add(usage);
                    SavePendingConsumptions();
                }
            ));
        }

        private void SyncPendingConsumptions()
        {
            if (_activeLicense == null || _isSyncingTokens || _pendingConsumptions.Count == 0) return;

            _isSyncingTokens = true;
            
            var batch = new BatchConsumptionRequest
            {
                key = _activeLicense.licenseKey,
                hwid = DeviceID.GetHWID(),
                usages = new List<ConsumptionRequest>(_pendingConsumptions)
            };

            Debug.Log($"[LTLM] Syncing {batch.usages.Count} pending token consumptions...");

            StartCoroutine(_client.PostEncrypted<BatchConsumptionRequest, LicenseData>(
                "/v1/sdk/pro/license/consume-batch",
                batch,
                fullLicense => {
                    _isSyncingTokens = false;
                    
                    // Remove the synced items (in case more were added while we were syncing)
                    _pendingConsumptions.RemoveAll(p => batch.usages.Contains(p));
                    SavePendingConsumptions();

                    // Update local license with absolute truth from server
                    _activeLicense = fullLicense;
                    CacheLicense(fullLicense);

                    Debug.Log("[LTLM] Token sync successful. Server balance: " + fullLicense.tokensRemaining);
                    
                    // Fire event for global subscribers after server sync confirms
                    OnTokensConsumed?.Invoke(fullLicense);
                },
                err => {
                    _isSyncingTokens = false;
                    Debug.LogWarning("[LTLM] Token sync failed (will retry later): " + err);
                }
            ));
        }

        private void LoadPendingConsumptions()
        {
            try {
                string json = SecureStorage.Load("pending_usages_" + projectId, DeviceID.GetHWID());
                if (!string.IsNullOrEmpty(json)) {
                    _pendingConsumptions = JsonConvert.DeserializeObject<List<ConsumptionRequest>>(json);
                }
            } catch {
                _pendingConsumptions = new List<ConsumptionRequest>();
            }
        }

        private void SavePendingConsumptions()
        {
            try {
                string json = JsonConvert.SerializeObject(_pendingConsumptions);
                SecureStorage.Save("pending_usages_" + projectId, json, DeviceID.GetHWID());
            } catch (Exception e) {
                Debug.LogError("[LTLM] Failed to save pending consumptions: " + e.Message);
            }
        }
    }
}
