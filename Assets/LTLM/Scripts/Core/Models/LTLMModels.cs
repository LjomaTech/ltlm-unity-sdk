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
        
        // Resolved config (access config["features"], config["metadata"] for custom data)
        public Dictionary<string, object> config;
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
        
        public PolicyConfig config; // Policy config for limits display
    }

    [Serializable]
    public class PolicyConfig
    {
        public PolicyLimits limits;
        public PolicyCustomerActions customerActions;
        public PolicySdkEnforcement sdkEnforcement;
        public PolicyMetadata metadata;
        public PolicyVisibility visibility;
    }

    [Serializable]
    public class PolicyLimits
    {
        public int maxMachines;
        public int maxActivations;
        public bool allowOffline;
        public PolicyTimeLimit time;
        public PolicySeatsLimit seats;
        public PolicyActivationsLimit activations;
    }

    [Serializable]
    public class PolicyTimeLimit
    {
        public string mode; // duration, absolute, relative
        public int durationDays;
        public int gracePeriodDays;
        public string value; // for relative
        public string unit;  // for relative
    }

    [Serializable]
    public class PolicySeatsLimit
    {
        public bool enabled;
        public int maxSeats;
        public string mode;
    }

    [Serializable]
    public class PolicyActivationsLimit
    {
        public bool enabled;
        public int maxTotal;
    }

    [Serializable]
    public class PolicyCustomerActions
    {
        public bool canViewLicenses;
        public bool canReleaseDevice;
        public bool canBuyTopUps;
        public List<TopUpOption> topUpOptions;
    }

    [Serializable]
    public class TopUpOption
    {
        public string packId;
        public int activations;
        public string displayName;
        public PriceData price;
    }

    [Serializable]
    public class PriceData
    {
        public float amount;
        public string currency;
    }

    [Serializable]
    public class PolicySdkEnforcement
    {
        public string mode; // heartbeat, validation, strict
        public int heartbeatIntervalSeconds;
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
}
