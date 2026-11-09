using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace LTLM.SDK.Core.Models
{
    #region Transport

    [Serializable]
    public class SignedResponse<T>
    {
        public T data;
        public string signature;
        public string server_nonce;
        public long timestamp;
    }

    /// <summary>
    /// Structured error produced by <c>LTLMClient</c>. Legacy string callbacks receive <see cref="Message"/>;
    /// use <c>LTLMManager.LastError</c> to inspect the typed version.
    /// </summary>
    public class LTLMRequestError
    {
        /// <summary>Human readable message (server message when available).</summary>
        public string Message;

        /// <summary>HTTP status code, 0 when the request never reached the server.</summary>
        public long HttpStatus;

        /// <summary>True for DNS/socket/timeout failures - the server was not reached. Offline grace may apply.</summary>
        public bool IsNetworkError;

        /// <summary>True when the local SDK failed to decrypt/verify the response (possible tampering).</summary>
        public bool IsIntegrityError;

        /// <summary>Optional machine readable code from the server (e.g. PRO_SDK_REQUIRED).</summary>
        public string Code;

        /// <summary>Raw response body, for diagnostics.</summary>
        public string RawBody;

        /// <summary>Server rejected the nonce chain - the SDK should re-activate to resync.</summary>
        public bool IsNonceMismatch =>
            HttpStatus == 401 && !string.IsNullOrEmpty(Message) &&
            Message.IndexOf("nonce", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>A server-side rejection (4xx/5xx) as opposed to a transport failure.</summary>
        public bool IsServerRejection => !IsNetworkError && !IsIntegrityError && HttpStatus > 0;

        public static LTLMRequestError Network(string message, string raw = null) =>
            new LTLMRequestError { Message = message, IsNetworkError = true, RawBody = raw };

        public static LTLMRequestError Integrity(string message, string raw = null) =>
            new LTLMRequestError { Message = message, IsIntegrityError = true, RawBody = raw };

        public static LTLMRequestError Local(string message) =>
            new LTLMRequestError { Message = message };

        public override string ToString() => Message;
    }

    /// <summary>Exception thrown by the Task-based (async) SDK API. Wraps <see cref="LTLMRequestError"/>.</summary>
    public class LTLMException : Exception
    {
        public LTLMRequestError Error { get; }

        public LTLMException(LTLMRequestError error)
            : base(error?.Message ?? "LTLM request failed")
        {
            Error = error ?? LTLMRequestError.Local("Unknown error");
        }

        public LTLMException(string message)
            : this(LTLMRequestError.Local(message))
        {
        }
    }

    #endregion

    #region License

    [Serializable]
    public class MachineData
    {
        public string hwid;
        public string nickname;
        public string registeredAt;
        public string lastCheckIn;
        public Dictionary<string, object> meta;
    }

    [Serializable]
    public class CustomerData
    {
        public string email;
        public Dictionary<string, object> metadata;
        public string lastDetectedCountry;
    }

    [Serializable]
    public class LicenseData
    {
        public int LicenseID;
        public int ProjectID;
        public string licenseKey;
        public string status; // active, VALID_NO_SEAT, KICKED, suspended, expired, revoked, grace_period, ...
        public string environment; // production, staging, development

        public string validUntil;
        public string lastCheckIn;
        public string nextBillingDate;

        public int? tokensLimit;
        public int? tokensConsumed;
        public int? tokensRemaining;
        public int? maxActivations; // Total allowed HWIDs
        public int? maxConcurrentSeats; // Max simultaneous users
        public int? activeSeats; // Current simultaneous users

        // Seat management fields
        public string seatStatus; // OCCUPIED, NO_SEAT, RELEASED, KICKED, N/A
        public bool? canReleaseSeat;
        public KickedNotice kickedNotice;

        // Enforcement settings (from server)
        public bool? seatsEnabled;
        public bool? tokensEnabled;
        public bool? offlineEnabled;
        public int? heartbeatIntervalSeconds;
        public int? offlineGraceHours;

        public string subscriptionStatus;
        public string detectedCountry;

        public List<MachineData> machines;
        public PolicyData policy;
        public CustomerData customer;

        /// <summary>Resolved configuration with overrides applied (Policy + Project + License).</summary>
        public LicenseConfig config;

        /// <summary>User-defined settings (synced across devices).</summary>
        public Dictionary<string, object> userSettings;

        /// <summary>Present only on offline (.ltlm) licenses.</summary>
        public string issuedAt;

        // ------------------------------------------------------------------
        // Convenience helpers (not serialized)
        // ------------------------------------------------------------------

        /// <summary>Org-defined settings merged Project → Policy → License.</summary>
        [JsonIgnore]
        public Dictionary<string, object> ProjectSettings => config?.projectSettings ?? new Dictionary<string, object>();

        /// <summary>Gets a project setting with type conversion.</summary>
        public T GetProjectSetting<T>(string key, T defaultValue = default)
        {
            var settings = config?.projectSettings;
            if (settings != null && settings.TryGetValue(key, out var value) && value != null)
            {
                try
                {
                    if (value is T typed) return typed;
                    if (value is Newtonsoft.Json.Linq.JToken token) return token.ToObject<T>();
                    return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
                }
                catch { }
            }
            return defaultValue;
        }

        /// <summary>True for perpetual policies or licenses without an expiry date.</summary>
        [JsonIgnore]
        public bool IsPerpetual =>
            (policy != null && policy.type == "perpetual") || string.IsNullOrEmpty(validUntil);

        /// <summary>Expiry as UTC DateTime, or null if perpetual/unknown.</summary>
        [JsonIgnore]
        public DateTime? ValidUntilUtc => LTLMDate.ParseUtc(validUntil);

        /// <summary>Next billing date as UTC DateTime, or null.</summary>
        [JsonIgnore]
        public DateTime? NextBillingDateUtc => LTLMDate.ParseUtc(nextBillingDate);

        /// <summary>True when the capability list contains <paramref name="name"/> or the feature flag is on.</summary>
        public bool HasCapability(string name)
        {
            if (string.IsNullOrEmpty(name) || config == null) return false;
            if (config.capabilities != null && config.capabilities.Contains(name)) return true;
            return config.IsFeatureEnabled(name);
        }
    }

    [Serializable]
    public class KickedNotice
    {
        public string kickedBy; // HWID of device that kicked this one
        public string kickedByNickname;
        public string timestamp;
        public string reason;
    }

    [Serializable]
    public class SeatInfo
    {
        public string hwid;
        public string nickname;
        public string lastSeen;
        public bool isCurrentDevice;
    }

    [Serializable]
    public class GetSeatsResponse
    {
        public List<SeatInfo> seats;
        public int activeSeats;
        public int maxSeats;
        public bool canReleaseSeat;
    }

    [Serializable]
    public class GetSeatsRequest
    {
        public string key;
    }

    [Serializable]
    public class ReleaseSeatRequest
    {
        public string key;
        public string targetHwid;
        public string callerHwid;
        public bool claimSeat;
    }

    [Serializable]
    public class ReleaseSeatResponse
    {
        public bool success;
        public string message;
        public string releasedHwid;
        public bool seatClaimed;
        public int activeSeats;
    }

    #endregion

    #region Policy

    [Serializable]
    public class PolicyData
    {
        public int PolicyID;
        public string name;
        public string type; // perpetual, subscription, trial, fixed_duration, usage-based
        public string description;
        public string shortDescription;
        public float? price;
        public string currency;

        public string recurringInterval; // day, week, month, year
        public int? recurringIntervalCount;
        public float? recurringPrice;

        public bool isAirGapped;
        public string environment;

        /// <summary>Only populated by buyable-policies, not by license validation (see LicenseData.config).</summary>
        public PolicyConfig config;

        /// <summary>Top-up options (policy-level only).</summary>
        public List<TopUpOption> topUpOptions;

        /// <summary>Top-ups from either the policy summary or the raw policy config.</summary>
        [JsonIgnore]
        public List<TopUpOption> ResolvedTopUpOptions =>
            topUpOptions ?? config?.customerActions?.topUpOptions ?? new List<TopUpOption>();
    }

    /// <summary>Resolved license configuration with all overrides applied.</summary>
    [Serializable]
    public class LicenseConfig
    {
        public PolicyLimits limits;
        public Dictionary<string, object> features;
        public List<string> capabilities;
        public Dictionary<string, object> projectSettings;
        public PolicyVersioning versioning;
        public GeofencingConfig geofencing;
        public SDKEnforcementConfig sdkEnforcement;
        public LicenseCustomerActions customerActions;

        /// <summary>Feature flags are booleans on the server; older policies used "true"/"1" strings.</summary>
        public bool IsFeatureEnabled(string name)
        {
            if (features == null || string.IsNullOrEmpty(name)) return false;
            if (!features.TryGetValue(name, out var v) || v == null) return false;
            return LTLMValue.IsTruthy(v);
        }
    }

    [Serializable]
    public class SDKEnforcementConfig
    {
        public HeartbeatConfig heartbeat;
        public OfflineGracePeriodConfig offlineGracePeriod;
        public string mode; // standard, sdk_only
        public int? heartbeatIntervalSeconds; // legacy
    }

    [Serializable]
    public class HeartbeatConfig
    {
        public int interval; // in minutes
        public bool enabled;
    }

    [Serializable]
    public class OfflineGracePeriodConfig
    {
        public bool enabled;
        public int duration; // in hours
    }

    [Serializable]
    public class GeofencingConfig
    {
        public bool enabled;
        public List<string> allowedCountries;
        public List<string> blockedCountries;
    }

    [Serializable]
    public class LicenseCustomerActions
    {
        public bool allowHardwareRelease;
        public bool allowRemoteSeatRelease;
        public bool canBuyTopUps;
        public int? seatReleaseCooldownSeconds;
    }

    [Serializable]
    public class PolicyConfig
    {
        public string environment;
        public PolicyLimits limits;
        public PolicyCustomerActions customerActions;
        public PolicyMetadata metadata;
        public PolicyVersioning versioning;
        public PolicyVisibility visibility;
        public GeofencingConfig geofencing;
        public SDKEnforcementConfig sdkEnforcement;
        public Dictionary<string, object> features;
        public List<string> capabilities;

        public bool IsFeatureEnabled(string name)
        {
            if (features == null || string.IsNullOrEmpty(name)) return false;
            if (!features.TryGetValue(name, out var v) || v == null) return false;
            return LTLMValue.IsTruthy(v);
        }

        /// <summary>Capabilities list plus any feature flag that is switched on.</summary>
        [JsonIgnore]
        public List<string> EnabledCapabilities
        {
            get
            {
                var list = new List<string>();
                if (capabilities != null) list.AddRange(capabilities);
                if (features != null)
                    foreach (var kv in features)
                        if (kv.Value != null && LTLMValue.IsTruthy(kv.Value) && !list.Contains(kv.Key))
                            list.Add(kv.Key);
                return list;
            }
        }
    }

    [Serializable]
    public class PolicyLimits
    {
        public PolicyTimeLimit time;
        public PolicySeatsLimit seats;
        public PolicyTokenLimit tokens;
        public PolicyActivationsLimit devices;
    }

    [Serializable]
    public class PolicyTimeLimit
    {
        public string mode; // duration, absolute, relative, expiry
        public int durationDays;
        public int gracePeriodDays;
        public string expiryDate;
    }

    [Serializable]
    public class PolicySeatsLimit
    {
        public bool enabled;
        public int maxSeats;
    }

    [Serializable]
    public class PolicyTokenLimit
    {
        public bool enabled;
        public int maxTokens;
    }

    [Serializable]
    public class PolicyActivationsLimit
    {
        public bool hwidRequired;
        public int maxDevices;
    }

    [Serializable]
    public class PolicyCustomerActions
    {
        public bool allowHardwareRelease;
        public bool allowRemoteSeatRelease;
        public bool canBuyTopUps;
        public int? seatReleaseCooldownSeconds;
        public List<TopUpOption> topUpOptions;
    }

    [Serializable]
    public class TopUpOption
    {
        public string packId;
        public int tokens;
        public int devices;
        public int seats;
        public string capability;
        public string name;
        public int days;
        public PriceData price;
    }

    [Serializable]
    public class PriceData
    {
        public float amount;
        public string currency;
    }

    [Serializable]
    public class PolicyVersioning
    {
        public bool enabled;
        public string maxVersion;
        public string minVersion;
        public List<string> allowedVersions;
    }

    [Serializable]
    public class PolicyMetadata
    {
        public List<string> tags;
        public int version;
    }

    [Serializable]
    public class PolicyVisibility
    {
        public bool storefront;
        public bool dashboard;
    }

    #endregion

    #region Developer (Editor) Models

    [Serializable]
    public class DevProjectData
    {
        public int id;
        public string name;
        public string publicId;
        public string description;
        public string status;
        public bool isPublic;
        public string category;
        public List<string> tags;

        public string publicKey;
        public string secretKey;

        public List<string> capabilities;
        public AnalyticsConfig analytics;
        public Dictionary<string, object> settings;

        public int orgId;
        public string orgName;
    }

    [Serializable]
    public class AnalyticsEvent
    {
        public string id;
        public string type;
        public string description;
    }

    [Serializable]
    public class AnalyticsConfig
    {
        public List<AnalyticsEvent> events;
    }

    [Serializable]
    public class DevOrgData
    {
        public int id;
        public string name;
        public string role;
        public string status;
    }

    [Serializable]
    public class DevUserData
    {
        public int userId;
        public string username;
        public string fullName;
    }

    [Serializable]
    public class DevLoginRequest
    {
        public string username;
        public string password;
        public string mfaToken;
    }

    [Serializable]
    public class DevLoginResponse
    {
        public DevUserData user;
        public List<DevOrgData> organizations;
        public List<DevProjectData> projects;
        public string accessToken;
        public bool mfaRequired;
        public string mfaMethod;
        public string mfaToken;
        public int userId;
    }

    #endregion

    #region Errors

    [Serializable]
    public class LTLMError
    {
        public string status;
        public string message;
        public bool isOperational;
        public string error;
        public string code;
    }

    /// <summary>Machine readable error codes the server may return in <c>code</c>.</summary>
    public static class LTLMErrorCodes
    {
        public const string NETWORK_ERROR = "NETWORK_ERROR";
        public const string TIMEOUT = "TIMEOUT";

        public const string LICENSE_NOT_FOUND = "LICENSE_NOT_FOUND";
        public const string LICENSE_DISABLED = "LICENSE_DISABLED";
        public const string INVALID_PROJECT_SECRET = "INVALID_PROJECT_SECRET";
        public const string PRO_SDK_REQUIRED = "PRO_SDK_REQUIRED";
        public const string OFFLINE_ACTIVATION_REQUIRED = "OFFLINE_ACTIVATION_REQUIRED";
        public const string VERSION_NOT_SUPPORTED = "VERSION_NOT_SUPPORTED";
        public const string REGION_BLOCKED = "REGION_BLOCKED";
        public const string DEVICE_LIMIT_REACHED = "DEVICE_LIMIT_REACHED";
        public const string SEAT_LIMIT_REACHED = "SEAT_LIMIT_REACHED";
        public const string SEAT_RELEASE_COOLDOWN = "SEAT_RELEASE_COOLDOWN";
        public const string SEATS_DISABLED = "SEATS_DISABLED";
        public const string REMOTE_RELEASE_DISABLED = "REMOTE_RELEASE_DISABLED";
        public const string INSUFFICIENT_TOKENS = "INSUFFICIENT_TOKENS";
        public const string TOKENS_DISABLED = "TOKENS_DISABLED";
        public const string NONCE_MISMATCH = "NONCE_MISMATCH";
        public const string DECRYPT_FAILED = "DECRYPT_FAILED";
        public const string RESPONSE_TAMPERED = "RESPONSE_TAMPERED";
        public const string SERVER_ERROR = "SERVER_ERROR";
    }

    #endregion

    #region Request Models

    [Serializable]
    public class ActivationRequest
    {
        public string key;
        public string hwid;
        public string version; // SDK Version
        public string clientVersion; // Software/App Version
        public string nickname;
        public Dictionary<string, object> meta;
    }

    [Serializable]
    public class HeartbeatRequest
    {
        public string key;
        public string hwid;
        public bool isClosing;
    }

    [Serializable]
    public class ConsumptionRequest
    {
        public string key;
        public int amount;
        public string action;
        public string hwid;
        public string timestamp; // ISO 8601, for offline usage tracking
        public Dictionary<string, object> meta;

        /// <summary>Client-generated id so a queued consumption is never double-counted.</summary>
        public string clientId;
    }

    [Serializable]
    public class SingleConsumptionRequest
    {
        public string key;
        public int amount;
        public string action;
        public string hwid;
        public Dictionary<string, object> meta;
    }

    [Serializable]
    public class BatchConsumptionRequest
    {
        public string key;
        public string hwid;
        public List<ConsumptionRequest> usages;
    }

    [Serializable]
    public class EventRequest
    {
        public string key;
        public string type;
        public Dictionary<string, object> payload;
    }

    [Serializable]
    public class CheckoutRequest
    {
        public string policyId;
        public string customerEmail;
        public string redirectUrl;
    }

    [Serializable]
    public class TopUpRequest
    {
        public string key;
        public string packId;
        public string redirectUrl;
    }

    [Serializable]
    public class CheckoutResponse
    {
        public string checkoutUrl;
        public string sessionId;
        public string message;
    }

    [Serializable]
    public class PortfolioResponse
    {
        public string email;
        public List<LicenseData> licenses;
    }

    [Serializable]
    public class OtpRequest
    {
        public string email;
    }

    [Serializable]
    public class OtpVerifyRequest
    {
        public string email;
        public string otp;
    }

    [Serializable]
    public class BuyablePoliciesRequest
    {
        public string hwid;
    }

    #endregion

    #region User Settings (Cloud Sync)

    [Serializable]
    public class UserSettingsRequest
    {
        public string key;
        public string hwid;
    }

    [Serializable]
    public class SaveUserSettingsRequest
    {
        public string key;
        public string hwid;
        public Dictionary<string, object> settings;
    }

    [Serializable]
    public class UserSettingsResponse
    {
        public bool success;
        public string message;
        public Dictionary<string, object> settings;
        public string updatedAt;
    }

    #endregion

    #region Trial Eligibility

    [Serializable]
    public class TrialEligibilityResult
    {
        public bool eligible;
        public string reason;
        public string message;
    }

    #endregion

    #region Helpers

    /// <summary>Culture-invariant, UTC date parsing for server timestamps.</summary>
    public static class LTLMDate
    {
        public static DateTime? ParseUtc(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
                return dt;
            return null;
        }

        public static string ToIso(DateTime utc) => utc.ToString("o", CultureInfo.InvariantCulture);
    }

    internal static class LTLMValue
    {
        public static bool IsTruthy(object v)
        {
            switch (v)
            {
                case bool b: return b;
                case string s:
                    s = s.Trim().ToLowerInvariant();
                    return s == "true" || s == "1" || s == "yes" || s == "on";
                case int i: return i != 0;
                case long l: return l != 0;
                case double d: return Math.Abs(d) > double.Epsilon;
                case Newtonsoft.Json.Linq.JValue jv: return IsTruthy(jv.Value);
                default: return false;
            }
        }
    }

    #endregion
}
