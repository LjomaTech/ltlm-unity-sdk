using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LTLM.SDK.Core;
using LTLM.SDK.Core.Communication;
using LTLM.SDK.Core.Models;
using LTLM.SDK.Core.Storage;
using LTLM.SDK.Core.Security;
using LTLM.SDK.Unity.Hardware;
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
        Terminated
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

        public void TryLoadStoredLicense(Action<LicenseData, LicenseStatus> onSuccess = null, Action<string> onError = null)
        {
            if (_isValidating) return;
            
            string storedKey = SecureStorage.Load("license_key_" + projectId, DeviceID.GetHWID());
            if (!string.IsNullOrEmpty(storedKey))
            {
                ValidateLicense(storedKey, onSuccess, onError);
            }
            else
            {
                onError?.Invoke("No stored license found.");
            }
        }

        public void ActivateLicense(string licenseKey, Action<LicenseData, LicenseStatus> onSuccess = null, Action<string> onError = null)
        {
            if (_isValidating) return;
            _isValidating = true;

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
                    CacheLicense(license);
                    SecureStorage.Save("license_key_" + projectId, licenseKey, DeviceID.GetHWID());
                    SecureStorage.Save("last_successful_sync_" + projectId, SecureClock.GetEffectiveTime(projectId).Ticks.ToString(), DeviceID.GetHWID());
                    ProcessEnforcement(license);
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
                    onError?.Invoke(err);
                }
            ));
        }

        public void ValidateLicense(string licenseKey, Action<LicenseData, LicenseStatus> onSuccess = null, Action<string> onError = null)
        {
            if (_isValidating) return;
            _isValidating = true;
            
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
                    CacheLicense(license);
                    SecureStorage.Save("last_successful_sync_" + projectId, SecureClock.GetEffectiveTime(projectId).Ticks.ToString(), DeviceID.GetHWID());
                    ProcessEnforcement(license);
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
                    Debug.LogWarning("[LTLM] Network Validation Failed. Checking offline grace...");
                    if (CheckOfflineGraceTimeout())
                    {
                        onSuccess?.Invoke(_activeLicense, GetLicenseStatus());
                    }
                    else
                    {
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
                yield return new WaitForSeconds(heartbeatIntervalSeconds);
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
                    
                    // Trigger immediate validation to refresh license state
                    if (!string.IsNullOrEmpty(_activeLicense.licenseKey))
                    {
                        ValidateLicense(_activeLicense.licenseKey);
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
        /// <para><b>Example:</b></para>
        /// <code>
        /// LTLMManager.Instance.DeactivateSeat();
        /// </code>
        /// </summary>
        public void DeactivateSeat()
        {
            if (_activeLicense == null) return;

            var request = new HeartbeatRequest
            {
                key = _activeLicense.licenseKey,
                hwid = DeviceID.GetHWID(),
                isClosing = true
            };

            // Use coroutine for normal deactivation (async)
            StartCoroutine(_client.PostEncrypted<HeartbeatRequest, LicenseData>(
                "/v1/sdk/pro/license/heartbeat",
                request,
                _ => Debug.Log("[LTLM] Seat released successfully."),
                _ => { }
            ));
        }

        /// <summary>
        /// Synchronous/fire-and-forget seat release for application quit scenarios.
        /// Uses direct UnityWebRequest to attempt seat release before app terminates.
        /// </summary>
        private void DeactivateSeatSync()
        {
            if (_activeLicense == null) return;

            try
            {
                // Fire-and-forget: We attempt the request but don't wait for completion
                // This is the best we can do during OnApplicationQuit as coroutines won't complete
                var request = new HeartbeatRequest
                {
                    key = _activeLicense.licenseKey,
                    hwid = DeviceID.GetHWID(),
                    isClosing = true
                };

                // Log the attempt - actual release may or may not complete
                Debug.Log("[LTLM] Attempting seat release on quit...");
                
                // Start coroutine anyway - it will send the request even if callback doesn't fire
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

            string rawLastSync = SecureStorage.Load("last_successful_sync_" + projectId, DeviceID.GetHWID());
            if (!long.TryParse(rawLastSync, out long ticks)) return false;

            DateTime lastSync = new DateTime(ticks, DateTimeKind.Utc);
            DateTime now = SecureClock.GetEffectiveTime(projectId);
            TimeSpan offlineDuration = now - lastSync;

            // Resolve offline grace period 
            float graceHours = 24f; // Default if not found in policy
            
            if (_activeLicense.config != null && _activeLicense.config.ContainsKey("limits"))
            {
                try {
                    var limits = JsonConvert.DeserializeObject<PolicyLimits>(JsonConvert.SerializeObject(_activeLicense.config["limits"]));
                    if (limits.time != null) {
                        graceHours = limits.time.gracePeriodDays * 24f;
                    }
                } catch {}
            }

            if (offlineDuration.TotalHours > graceHours)
            {
                Debug.LogError($"[LTLM] OFFLINE GRACE PERIOD EXCEEDED ({graceHours} hours). Connection required.");
                LogEvent("OfflineGraceExceeded", new Dictionary<string, object> { { "duration_hours", offlineDuration.TotalHours } });
                
                // Lockdown the SDK
                _activeLicense.status = "connection_required";
                ProcessEnforcement(_activeLicense);
                return false;
            }
            else
            {
                Debug.LogWarning($"[LTLM] Running in offline grace mode. {graceHours - offlineDuration.TotalHours:F1} hours remaining.");
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
            // HWID Check: Verify if this device is still registered to the license
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
            if (_activeLicense == null || _activeLicense.features == null) return false;
            if (_activeLicense.features.TryGetValue(featureName, out object val)) {
                if (val == null) return false;
                string sVal = val.ToString().ToLower();
                return sVal == "true" || sVal == "1";
            }
            return false;
        }

        public object GetMetadata(string key)
        {
            if (_activeLicense == null || _activeLicense.metadata == null) return null;
            if (_activeLicense.metadata.TryGetValue(key, out object val)) return val;
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
                case "grace_period": return LicenseStatus.GracePeriod;
                case "expired": return LicenseStatus.Expired;
                case "suspended": return LicenseStatus.Suspended;
                case "revoked": return LicenseStatus.Revoked;
                case "terminated": return LicenseStatus.Terminated;
                case "connection_required": return LicenseStatus.ConnectionRequired;
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

            // 1. Create the consumption log
            var usage = new ConsumptionRequest
            {
                key = _activeLicense.licenseKey,
                amount = amount,
                action = action,
                hwid = DeviceID.GetHWID(),
                timestamp = SecureClock.GetEffectiveTime(projectId).ToString("o") // ISO 8601
            };

            // 2. Add to pending queue and save immediately
            _pendingConsumptions.Add(usage);
            SavePendingConsumptions();

            // 3. Optimistic Update: Reflect the "truth" immediately for the game layer
            if (_activeLicense.tokensConsumed.HasValue) _activeLicense.tokensConsumed += amount;
            if (_activeLicense.tokensRemaining.HasValue) _activeLicense.tokensRemaining -= amount;
            
            SecureStorage.Save("cached_tokens_" + projectId, (_activeLicense.tokensRemaining ?? 0).ToString(), DeviceID.GetHWID());
            
            onConsumed?.Invoke(_activeLicense);

            // 4. Try to sync with server
            SyncPendingConsumptions();
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
