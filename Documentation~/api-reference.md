# API Reference

Complete reference for all LTLMManager methods and properties.

---

## Accessing the Manager

```csharp
using LTLM.SDK.Unity;

// Access anywhere in your code
LTLMManager manager = LTLMManager.Instance;
```

---

## Properties

### IsAuthenticated

Returns `true` if a valid active license is loaded.

```csharp
if (LTLMManager.Instance.IsAuthenticated)
{
    // User has a valid license
    ShowMainApp();
}
else
{
    // No license - show activation screen
    ShowActivation();
}
```

---

### IsValidating

Returns `true` if a validation request is currently in progress.

```csharp
if (LTLMManager.Instance.IsValidating)
{
    // Show loading indicator
    loadingSpinner.SetActive(true);
}
```

---

### ActiveLicense

Returns the current license data, or `null` if not authenticated.

```csharp
LicenseData license = LTLMManager.Instance.ActiveLicense;

if (license != null)
{
    Debug.Log("Key: " + license.licenseKey);
    Debug.Log("Status: " + license.status);
    Debug.Log("Expires: " + license.validUntil);
    Debug.Log("Tokens: " + license.tokensRemaining);
}
```

---

## License Methods

### ActivateLicense

Activates a license key with the server.

```csharp
LTLMManager.Instance.ActivateLicense(
    licenseKey,      // string: The license key
    onSuccess,       // Action<LicenseData, LicenseStatus>: Success callback
    onError          // Action<string>: Error callback
);
```

**Example:**
```csharp
LTLMManager.Instance.ActivateLicense("XXXX-XXXX-XXXX-XXXX",
    (license, status) => {
        Debug.Log("Activated! Status: " + status);
    },
    error => {
        Debug.LogError("Failed: " + error);
    }
);
```

---

### ValidateLicense

Validates an existing license with the server.

```csharp
LTLMManager.Instance.ValidateLicense(
    licenseKey,      // string: The license key
    onSuccess,       // Action<LicenseData, LicenseStatus>: Success callback
    onError          // Action<string>: Error callback
);
```

**Example:**
```csharp
string key = LTLMManager.Instance.ActiveLicense.licenseKey;

LTLMManager.Instance.ValidateLicense(key,
    (license, status) => {
        Debug.Log("Fresh validation: " + status);
    },
    error => {
        Debug.LogError("Validation failed: " + error);
    }
);
```

---

### TryLoadStoredLicense

Attempts to load and validate a previously stored license. Called automatically on startup if `autoValidateOnStart` is enabled.

```csharp
LTLMManager.Instance.TryLoadStoredLicense(
    onSuccess,       // Action<LicenseData, LicenseStatus>: Success callback
    onError          // Action<string>: Error callback
);
```

**Example:**
```csharp
LTLMManager.Instance.TryLoadStoredLicense(
    (license, status) => {
        Debug.Log("Loaded stored license: " + status);
        if (status == LicenseStatus.Active)
        {
            ShowMainScreen();
        }
    },
    error => {
        Debug.Log("No stored license or error: " + error);
        ShowActivationScreen();
    }
);
```

---

### ClearLicenseCache

Clears all stored license data.

```csharp
LTLMManager.Instance.ClearLicenseCache();
```

**Example:**
```csharp
public void OnSignOutClicked()
{
    LTLMManager.Instance.DeactivateSeat();
    LTLMManager.Instance.ClearLicenseCache();
    ShowLoginScreen();
}
```

---

### DeactivateSeat

Releases the seat on the server. Use when user signs out.

```csharp
bool success = LTLMManager.Instance.DeactivateSeat();
```

**Returns:** `true` if deactivation was initiated, `false` if blocked.

**Important:** This method requires network connectivity. Signout is blocked when offline to prevent license abuse (sharing license by going offline and signing out).

**Example:**
```csharp
public void OnSignOutClicked()
{
    if (!LTLMManager.Instance.DeactivateSeat())
    {
        ShowToast("Cannot sign out while offline. Please connect to the internet.");
        return;
    }
    
    LTLMManager.Instance.ClearLicenseCache();
    ShowLoginScreen();
}
```

---

## Seat Management Methods

### IsWaitingForSeat

Returns `true` if the license is valid but waiting for an available seat.

```csharp
if (LTLMManager.Instance.IsWaitingForSeat)
{
    ShowSeatManagementUI();
}
```

---

### GetActiveSeats

Gets a list of all devices currently holding seats on this license.

```csharp
LTLMManager.Instance.GetActiveSeats(
    onSuccess,       // Action<GetSeatsResponse>: Success callback
    onError          // Action<string>: Error callback
);
```

**Example:**
```csharp
LTLMManager.Instance.GetActiveSeats(
    response => {
        Debug.Log($"Seats: {response.activeSeats}/{response.maxSeats}");
        foreach (var seat in response.seats)
        {
            Debug.Log($"Device: {seat.nickname ?? seat.hwid}");
        }
    },
    error => Debug.LogError(error)
);
```

---

### ReleaseSeat

Releases another device's seat so this device can claim it.

```csharp
LTLMManager.Instance.ReleaseSeat(
    targetHwid,      // string: HWID of device to disconnect
    claimSeat,       // bool: Claim the seat after release
    onSuccess,       // Action<ReleaseSeatResponse>: Success callback
    onError          // Action<string>: Error callback
);
```

**Example:**
```csharp
LTLMManager.Instance.ReleaseSeat("abc123...", claimSeat: true,
    response => {
        if (response.seatClaimed)
        {
            Debug.Log("Seat claimed!");
        }
    },
    error => {
        if (error.Contains("wait"))
        {
            // Cooldown active
        }
    }
);
```

---

## Token Methods

### GetTokenBalance

Returns the current token balance.

```csharp
int balance = LTLMManager.Instance.GetTokenBalance();
Debug.Log("You have " + balance + " credits");
```

---

### ConsumeTokens

Consumes tokens for an action.

```csharp
LTLMManager.Instance.ConsumeTokens(
    amount,          // int: Number of tokens to consume
    action,          // string: Action identifier for tracking
    onConsumed,      // Action<LicenseData>: Success callback
    onError          // Action<string>: Error callback
);
```

**Example:**
```csharp
LTLMManager.Instance.ConsumeTokens(5, "export_high_res",
    license => {
        Debug.Log("Consumed! Remaining: " + license.tokensRemaining);
        PerformExport();
    },
    error => {
        Debug.LogError("Consumption failed: " + error);
    }
);
```

---

### DoesHaveTokens

Checks if user has enough tokens.

```csharp
LTLMManager.Instance.DoesHaveTokens(
    amount,          // int: Required tokens
    callback         // Action<bool>: Result callback
);
```

**Example:**
```csharp
LTLMManager.Instance.DoesHaveTokens(10, hasTokens => {
    exportButton.interactable = hasTokens;
});
```

---

## Entitlement Methods

### HasCapability

Checks if license includes a capability.

```csharp
bool hasPro = LTLMManager.Instance.HasCapability("pro_features");
```

**Example:**
```csharp
if (LTLMManager.Instance.HasCapability("advanced_editor"))
{
    EnableAdvancedEditor();
}
else
{
    ShowUpgradePrompt();
}
```

---

### IsEntitled

Combined check for capability AND tokens.

```csharp
bool isEntitled = LTLMManager.Instance.IsEntitled(
    feature,         // string: Capability name
    requiredTokens   // int: Minimum tokens needed
);
```

**Example:**
```csharp
if (LTLMManager.Instance.IsEntitled("pro_export", 10))
{
    // Has "pro_export" capability AND at least 10 tokens
    EnableExportButton();
}
```

---

### GetMetadata

Gets custom metadata from the license.

```csharp
object value = LTLMManager.Instance.GetMetadata("custom_field");
```

**Example:**
```csharp
object maxProjects = LTLMManager.Instance.GetMetadata("max_projects");
if (maxProjects != null)
{
    int limit = Convert.ToInt32(maxProjects);
    Debug.Log("Project limit: " + limit);
}
```

---

## User Settings (Cloud Sync)

User settings are synced across all devices using the same license. Maximum size: 64KB.

### GetUserSettings

Fetches user settings from the server.

```csharp
LTLMManager.Instance.GetUserSettings(
    settings => { /* Dictionary<string, object> */ },
    error => { /* string error message */ }
);
```

**Example:**
```csharp
LTLMManager.Instance.GetUserSettings(
    settings => {
        if (settings.TryGetValue("theme", out object theme))
        {
            ApplyTheme(theme.ToString());
        }
    },
    error => Debug.LogError(error)
);
```

---

### SaveUserSettings

Saves user settings to the server. Settings sync across all devices.

```csharp
LTLMManager.Instance.SaveUserSettings(
    settings,    // Dictionary<string, object>: Settings to save
    onSuccess,   // Action: Called on success
    onError      // Action<string>: Called on error
);
```

**Example:**
```csharp
var settings = new Dictionary<string, object> {
    { "theme", "dark" },
    { "language", "en" },
    { "notifications", true },
    { "preferences", new Dictionary<string, object> {
        { "autoSave", true },
        { "fontSize", 14 }
    }}
};

LTLMManager.Instance.SaveUserSettings(settings, 
    () => Debug.Log("Settings saved!"),
    error => Debug.LogError($"Failed to save: {error}")
);
```

> [!NOTE]
> Settings are automatically cached locally when saved and restored on next login.

---

### GetLocalUserSettings

Gets settings from local cache (no network call).

```csharp
Dictionary<string, object> settings = LTLMManager.Instance.GetLocalUserSettings();
```

**Example:**
```csharp
// Fast: Load from cache on startup
var cached = LTLMManager.Instance.GetLocalUserSettings();
ApplySettings(cached);

// Then sync with server in background
LTLMManager.Instance.GetUserSettings(
    serverSettings => ApplySettings(serverSettings),
    error => { /* Use cached settings */ }
);
```

---

## Analytics Methods

### LogEvent

Logs a custom analytics event to the LTLM server. Use for tracking feature usage, user behavior, or custom metrics.

```csharp
LTLMManager.Instance.LogEvent(
    eventType,       // string: Event type identifier
    payload          // Dictionary<string, object>: Optional event data
);
```

**Example:**
```csharp
// Track feature usage
LTLMManager.Instance.LogEvent("feature_used", new Dictionary<string, object> {
    { "feature", "ai_upscale" },
    { "duration_ms", 4500 },
    { "input_size", "1080p" }
});

// Track export
LTLMManager.Instance.LogEvent("export_completed", new Dictionary<string, object> {
    { "format", "mp4" },
    { "quality", "4K" },
    { "file_size_mb", 245 }
});

// Track session start
LTLMManager.Instance.LogEvent("session_started", new Dictionary<string, object> {
    { "version", Application.version },
    { "platform", Application.platform.ToString() }
});
```

**Use Cases:**
- Track which features are most used
- Monitor performance metrics
- Log errors or warnings
- Measure user engagement
- A/B testing data

---

## Status Methods

### GetLicenseStatus

Returns the current license status as an enum.

```csharp
LicenseStatus status = LTLMManager.Instance.GetLicenseStatus();
```

**Example:**
```csharp
switch (LTLMManager.Instance.GetLicenseStatus())
{
    case LicenseStatus.Active:
        // Full access
        break;
    case LicenseStatus.Expired:
        ShowRenewalPrompt();
        break;
    case LicenseStatus.GracePeriod:
        ShowGraceWarning();
        break;
}
```

---

## Purchase Methods

### GetBuyablePolicies

Fetches available products for purchase.

```csharp
LTLMManager.Instance.GetBuyablePolicies(
    onSuccess,       // Action<List<PolicyData>>: Success callback
    onError          // Action<string>: Error callback
);
```

---

### CreateCheckoutSession

Creates a checkout URL for new purchase.

```csharp
LTLMManager.Instance.CreateCheckoutSession(
    policyId,        // string: Policy to purchase
    customerEmail,   // string: Customer email
    redirectUrl,     // string: URL after checkout
    onSuccess,       // Action<string>: Returns checkout URL
    onError          // Action<string>: Error callback
);
```

---

### CreateTopUpSession

Creates a checkout URL for token top-up.

```csharp
LTLMManager.Instance.CreateTopUpSession(
    packId,          // string: Token pack ID
    redirectUrl,     // string: URL after checkout
    onSuccess,       // Action<string>: Returns checkout URL
    onError          // Action<string>: Error callback
);
```

---

## Events

### OnLicenseStatusChanged

Fired when license status changes.

```csharp
void Start()
{
    LTLMManager.OnLicenseStatusChanged += OnStatusChanged;
}

void OnStatusChanged(LicenseStatus status)
{
    Debug.Log("Status changed to: " + status);
}

void OnDestroy()
{
    LTLMManager.OnLicenseStatusChanged -= OnStatusChanged;
}
```

---

### OnTokensConsumed

Fired after tokens are consumed.

```csharp
void Start()
{
    LTLMManager.OnTokensConsumed += OnTokensUpdated;
}

void OnTokensUpdated(LicenseData license)
{
    tokenLabel.text = license.tokensRemaining.ToString();
}
```

---

### OnValidationStarted

Fired when validation begins.

```csharp
LTLMManager.OnValidationStarted += () => {
    loadingUI.SetActive(true);
};
```

---

### OnValidationCompleted

Fired when validation finishes (success or failure).

```csharp
LTLMManager.OnValidationCompleted += (success, status) => {
    loadingUI.SetActive(false);
    if (success)
    {
        ShowMainScreen();
    }
    else
    {
        ShowActivationScreen();
    }
};
```

---

### OnSeatStatusChanged

Fired when seat status changes (for concurrent licensing).

```csharp
void OnEnable()
{
    LTLMManager.OnSeatStatusChanged += OnSeatChanged;
}

void OnSeatChanged(string seatStatus, int activeSeats, int maxSeats)
{
    // seatStatus: "OCCUPIED", "NO_SEAT", or "RELEASED"
    seatLabel.text = $"Seats: {activeSeats}/{maxSeats}";
    
    if (seatStatus == "NO_SEAT")
    {
        ShowWaitingForSeatUI();
    }
}

void OnDisable()
{
    LTLMManager.OnSeatStatusChanged -= OnSeatChanged;
}
```

---

### OnKicked

Fired when another device releases your seat.

```csharp
void OnEnable()
{
    LTLMManager.OnKicked += HandleKicked;
}

void HandleKicked(KickedNotice notice)
{
    string message = notice.kickedByNickname != null
        ? $"Session ended by '{notice.kickedByNickname}'"
        : "Session ended by another device";
    
    ShowToast(message);
    TransitionToWaitingMode();
}

void OnDisable()
{
    LTLMManager.OnKicked -= HandleKicked;
}
```

---

### OnLicenseStatusChanged

Fired whenever the license status changes (including during offline grace checks).

```csharp
void OnEnable()
{
    LTLMManager.OnLicenseStatusChanged += HandleStatusChange;
}

void HandleStatusChange(LicenseStatus status)
{
    switch (status)
    {
        case LicenseStatus.Active:
            HideAllWarnings();
            break;
        case LicenseStatus.GracePeriod:
            ShowGraceWarning();
            break;
        case LicenseStatus.Expired:
            ShowRenewalRequired();
            break;
        case LicenseStatus.ConnectionRequired:
            ShowOfflineExpiredMessage();
            break;
        case LicenseStatus.Kicked:
            ShowKickedMessage();
            break;
        case LicenseStatus.WaitingForSeat:
            ShowSeatManagementUI();
            break;
    }
}

void OnDisable()
{
    LTLMManager.OnLicenseStatusChanged -= HandleStatusChange;
}
```

**Use Cases:**
- Update UI when license state changes
- Show appropriate banners/popups
- Handle offline grace period expiration
- React to being kicked from seat

---

## Events Summary

| Event | Parameters | When Fired |
|-------|------------|------------|
| `OnValidationStarted` | none | Validation request begins |
| `OnValidationCompleted` | `bool success, LicenseStatus status` | Validation finishes |
| `OnLicenseStatusChanged` | `LicenseStatus status` | Any license state change |
| `OnTokensConsumed` | `LicenseData license` | After tokens consumed |
| `OnSeatStatusChanged` | `string seatStatus, int active, int max` | Seat allocation changes |
| `OnKicked` | `KickedNotice notice` | Seat released by another device |

---

## Inspector Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `projectId` | string | - | Your project ID from dashboard |
| `publicKey` | string | - | Ed25519 public key for signature verification |
| `secretKey` | string | - | AES-256 secret key for encryption |
| `heartbeatIntervalSeconds` | float | 300 | Local default (⚠️ server value takes priority!) |
| `autoValidateOnStart` | bool | true | Auto-validate stored license on startup |
| `softwareVersion` | string | - | Your app version (checked against policy versioning) |

> [!IMPORTANT]
> The `heartbeatIntervalSeconds` in Inspector is only a fallback. The server returns `heartbeatIntervalSeconds` in every response, and the SDK automatically uses that value instead.

---

## Next Steps

- [Status Codes](status-codes.md) - All status values explained
- [Error Handling](error-handling.md) - Handle errors properly
