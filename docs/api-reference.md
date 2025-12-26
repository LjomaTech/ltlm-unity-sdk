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
LTLMManager.Instance.DeactivateSeat();
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

## Inspector Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `projectId` | string | - | Your project ID from dashboard |
| `publicKey` | string | - | Ed25519 public key |
| `secretKey` | string | - | AES-256 secret key |
| `heartbeatIntervalSeconds` | float | 300 | Seconds between heartbeats |
| `autoValidateOnStart` | bool | true | Auto-validate stored license |
| `softwareVersion` | string | - | Your app version |

---

## Next Steps

- [Status Codes](status-codes.md) - All status values explained
- [Error Handling](error-handling.md) - Handle errors properly
