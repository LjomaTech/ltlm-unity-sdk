using System;
using System.Collections.Generic;

namespace LTLM.SDK.Core.Models
{
    [Serializable]
    public class SignedResponse<T>
    {
        public T data;
        public string signature;
        public string server_nonce;
        public long timestamp;
    }

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
        public string status; // active, VALID_NO_SEAT, suspended, expired, revoked, grace_period
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
        public string seatStatus; // OCCUPIED, NO_SEAT, RELEASED, N/A
        public bool? canReleaseSeat; // Whether user can release other seats
        public KickedNotice kickedNotice; // Present if this device was kicked
        
        // Enforcement settings (from server)
        public bool? seatsEnabled;      // Whether concurrent seats are enabled for this license
        public bool? tokensEnabled;     // Whether token consumption is enabled
        public bool? offlineEnabled;    // Whether offline mode is enabled
        public int? heartbeatIntervalSeconds; // Server-specified heartbeat interval
        public int? offlineGraceHours;   // Hours allowed offline before requiring revalidation
        
        public string subscriptionStatus;
        public string detectedCountry;

        public List<MachineData> machines;
        public PolicyData policy;
        public CustomerData customer;
        
        /// <summary>
        /// Resolved configuration with overrides applied (Policy + Project + License).
        /// Access limits, features, capabilities, projectSettings, enforcement settings.
        /// </summary>
        public LicenseConfig config;
        
        /// <summary>
        /// Custom org-defined settings merged in order: Project (base) → Policy → License.
        /// These are arbitrary key-value pairs defined by the organization for their own use
        /// (e.g., API endpoints, feature flags, environment URLs).
        /// </summary>
        public Dictionary<string, object> ProjectSettings => config?.projectSettings ?? new Dictionary<string, object>();
        
        /// <summary>
        /// Gets a specific project setting value with type conversion.
        /// </summary>
        public T GetProjectSetting<T>(string key, T defaultValue = default)
        {
            var settings = ProjectSettings;
            if (settings != null && settings.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is T typed) return typed;
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch { }
            }
            return defaultValue;
        }
        
        // User-defined settings (synced across devices)
        public Dictionary<string, object> userSettings;
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
    public class ReleaseSeatRequest
    {
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
        
        // Policy config - includes limits, features, etc. for store/marketplace display
        // Note: This is ONLY populated by buyable-policies endpoint, NOT by license validation
        // (license validation uses LicenseData.config for resolved settings)
        public PolicyConfig config;
        
        // Top-up options (policy-level only, not overrideable per license)
        public List<TopUpOption> topUpOptions;
    }

    #region License Config (Resolved/Overrideable Settings)
    
    /// <summary>
    /// Resolved license configuration with all overrides applied.
    /// Contains only overrideable items (not full policy config to avoid duplication).
    /// </summary>
    [Serializable]
    public class LicenseConfig
    {
        public PolicyLimits limits;
        public Dictionary<string, string> features;
        public List<string> capabilities;
        public Dictionary<string, object> projectSettings;
        public PolicyVersioning versioning;
        public GeofencingConfig geofencing;
        public SDKEnforcementConfig sdkEnforcement;
        public LicenseCustomerActions customerActions;
    }

    /// <summary>
    /// SDK enforcement settings (overrideable per license).
    /// </summary>
    [Serializable]
    public class SDKEnforcementConfig
    {
        public HeartbeatConfig heartbeat;
        public OfflineGracePeriodConfig offlineGracePeriod;
        public string mode; // standard, pro_only
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

    /// <summary>
    /// Geofencing configuration (region restrictions).
    /// </summary>
    [Serializable]
    public class GeofencingConfig
    {
        public bool enabled;
        public List<string> allowedCountries;
        public List<string> blockedCountries;
    }

    /// <summary>
    /// Customer actions configuration (without topUpOptions which are policy-level).
    /// </summary>
    [Serializable]
    public class LicenseCustomerActions
    {
        public bool allowHardwareRelease;
        public bool allowRemoteSeatRelease;
        public bool canBuyTopUps;
        // Note: topUpOptions are in PolicyData.topUpOptions (policy-level only)
    }
    
    #endregion

    [Serializable]
    public class PolicyConfig
    {
        public string environment;
        public PolicyLimits limits;
        public PolicyCustomerActions customerActions;
        public PolicyMetadata metadata;
        public PolicyVersioning versioning;
        public PolicyVisibility visibility;
        public Dictionary<string, string> features;
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
        public string mode; // duration, absolute, relative
        public int durationDays;
        public int gracePeriodDays;
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
        public bool hwidReqiured;
        public int maxDevices;
    }

    [Serializable]
    public class PolicyCustomerActions
    {
        public bool allowHardwareRelease;
        public bool allowRemoteSeatRelease;
        public bool canBuyTopUps;
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
        
        // Organization info
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
        public string role;   // Owner, Admin, Member
        public string status; // active, suspended, etc.
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
        public string mfaToken; // For MFA if required
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
        public string mfaToken; // Token for MFA verification
        public int userId; // For MFA flow
    }

    [Serializable]
    public class LTLMError
    {
        public string status; 
        public string message;
        public bool isOperational;
        public string error;
    }

    // Request Models
    [Serializable]
    public class ActivationRequest
    {
        public string key;
        public string hwid;
        public string version;      // SDK Version
        public string clientVersion; // Software/App Version
        public string nickname;
        public Dictionary<string, object> meta;
    }

    [Serializable]
    public class HeartbeatRequest
    {
        public string key;
        public string hwid;
        public bool isClosing; // If true, this seat is being released
    }

    [Serializable]
    public class ConsumptionRequest
    {
        public string key;
        public int amount;
        public string action;
        public string hwid;
        public string timestamp; // For offline usage tracking
        public Dictionary<string, object> meta;
    }

    /// <summary>
    /// Request for single token consumption (online mode).
    /// </summary>
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
    
    /// <summary>
    /// Result of a trial eligibility check.
    /// Use CheckTrialEligibility to verify before showing trial options.
    /// </summary>
    [Serializable]
    public class TrialEligibilityResult
    {
        /// <summary>Whether the customer is eligible for a trial.</summary>
        public bool eligible;
        
        /// <summary>Reason code if not eligible ("previous_trial", "duplicate_card", etc.).</summary>
        public string reason;
        
        /// <summary>User-facing message explaining eligibility status.</summary>
        public string message;
    }
    
    #endregion
}

