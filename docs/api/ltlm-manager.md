# LTLMManager API Reference

Complete API reference for the main SDK singleton.

---

## Class: LTLMManager

```csharp
namespace LTLM.SDK.Unity
public class LTLMManager : MonoBehaviour
```

The main entry point for LTLM SDK functionality. Implements the singleton pattern and persists across scene loads.

---

## Properties

### Instance

```csharp
public static LTLMManager Instance { get; }
```

The singleton instance. Available after `Awake()` runs.

```csharp
// Usage
LTLMManager.Instance.ActivateLicense(...);
```

---

### IsAuthenticated

```csharp
public bool IsAuthenticated { get; }
```

Returns `true` if a valid active license is loaded.

```csharp
if (LTLMManager.Instance.IsAuthenticated)
{
    EnablePremiumFeatures();
}
```

---

### IsValidating

```csharp
public bool IsValidating { get; }
```

Returns `true` if a validation request is in progress.

```csharp
if (LTLMManager.Instance.IsValidating)
{
    ShowLoadingSpinner();
}
```

---

### ActiveLicense

```csharp
public LicenseData ActiveLicense { get; }
```

The currently loaded license data, or `null` if not authenticated.

```csharp
var license = LTLMManager.Instance.ActiveLicense;
Debug.Log($"Key: {license.licenseKey}");
Debug.Log($"Status: {license.status}");
Debug.Log($"Expires: {license.validUntil}");
```

---

## Configuration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `projectId` | string | - | LTLM Project ID |
| `publicKey` | string | - | Ed25519 public key (PEM) |
| `secretKey` | string | - | AES-256 secret key (hex) |
| `heartbeatIntervalSeconds` | float | 300 | Heartbeat interval |
| `autoValidateOnStart` | bool | true | Auto-validate on Start() |
| `softwareVersion` | string | Application.version | App version for gating |

---

## Methods

### ActivateLicense

```csharp
public void ActivateLicense(
    string licenseKey,
    Action<LicenseData, LicenseStatus> onSuccess = null,
    Action<string> onError = null
)
```

Activates a license key with the backend.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `licenseKey` | string | The license key to activate |
| `onSuccess` | Action | Called on successful activation |
| `onError` | Action | Called on failure with error message |

**Example:**
```csharp
LTLMManager.Instance.ActivateLicense("XXXX-XXXX-XXXX",
    (license, status) => {
        Debug.Log($"Activated with status: {status}");
    },
    error => {
        Debug.LogError(error);
    }
);
```

---

### ValidateLicense

```csharp
public void ValidateLicense(
    string licenseKey,
    Action<LicenseData, LicenseStatus> onSuccess = null,
    Action<string> onError = null
)
```

Validates an existing license with the backend. Falls back to offline grace check on network failure.

**Example:**
```csharp
LTLMManager.Instance.ValidateLicense(storedKey,
    (license, status) => RefreshUI(status),
    error => HandleOffline()
);
```

---

### ActivateOffline

```csharp
public void ActivateOffline(
    string encryptedBlob,
    Action<LicenseData, LicenseStatus> onSuccess = null,
    Action<string> onError = null
)
```

Activates using an offline `.ltlm` file (encrypted activation blob).

**Example:**
```csharp
string blob = File.ReadAllText("license.ltlm");
LTLMManager.Instance.ActivateOffline(blob,
    (license, status) => Debug.Log("Offline activated!"),
    error => Debug.LogError(error)
);
```

---

### TryLoadStoredLicense

```csharp
public void TryLoadStoredLicense(
    Action<LicenseData, LicenseStatus> onSuccess = null,
    Action<string> onError = null
)
```

Attempts to load and validate a previously stored license.

---

### ConsumeTokens

```csharp
public void ConsumeTokens(
    int amount,
    string action,
    Action<LicenseData> onConsumed = null,
    Action<string> onError = null
)
```

Consumes tokens for a usage-based action.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `amount` | int | Number of tokens to consume |
| `action` | string | Action identifier for tracking |
| `onConsumed` | Action | Called after local update |
| `onError` | Action | Called on failure |

**Example:**
```csharp
LTLMManager.Instance.ConsumeTokens(5, "export_hd",
    license => Debug.Log($"Remaining: {license.tokensRemaining}"),
    error => Debug.LogError(error)
);
```

---

### DoesHaveTokens

```csharp
public void DoesHaveTokens(
    int amount,
    Action<bool> callback,
    bool forceServerCheck = false
)
```

Checks if the license has sufficient tokens.

**Example:**
```csharp
LTLMManager.Instance.DoesHaveTokens(10, hasTokens => {
    exportButton.interactable = hasTokens;
});
```

---

### GetTokenBalance

```csharp
public int GetTokenBalance()
```

Returns the current local token balance.

```csharp
int balance = LTLMManager.Instance.GetTokenBalance();
tokenLabel.text = $"Credits: {balance}";
```

---

### GetLicenseStatus

```csharp
public LicenseStatus GetLicenseStatus()
```

Returns the current license status as an enum.

```csharp
switch (LTLMManager.Instance.GetLicenseStatus())
{
    case LicenseStatus.Active:
        // Full access
        break;
    case LicenseStatus.GracePeriod:
        // Warn user
        break;
    case LicenseStatus.Expired:
        // Block features
        break;
}
```

---

### HasCapability

```csharp
public bool HasCapability(string capability)
```

Checks if the license includes a specific capability/feature.

```csharp
if (LTLMManager.Instance.HasCapability("advanced_editor"))
{
    EnableAdvancedEditor();
}
```

---

### IsEntitled

```csharp
public bool IsEntitled(string feature, int requiredTokens = 0)
```

Combined check for capability AND sufficient tokens.

```csharp
if (LTLMManager.Instance.IsEntitled("pro_export", requiredTokens: 5))
{
    EnableProExport();
}
```

---

### GetMetadata

```csharp
public object GetMetadata(string key)
```

Retrieves custom metadata from the license.

```csharp
string tier = LTLMManager.Instance.GetMetadata("tier") as string;
int seats = (int)LTLMManager.Instance.GetMetadata("max_seats");
```

---

### DeactivateSeat

```csharp
public void DeactivateSeat()
```

Releases the concurrent seat for this machine. Use for sign-out functionality.

```csharp
public void OnSignOutClicked()
{
    LTLMManager.Instance.DeactivateSeat();
    LTLMManager.Instance.ClearLicenseCache();
    ShowLoginScreen();
}
```

---

### ClearLicenseCache

```csharp
public void ClearLicenseCache()
```

Clears all locally cached license data.

---

### SyncPendingConsumptions

```csharp
public void SyncPendingConsumptions()
```

Forces a sync of pending offline token consumptions.

---

## Events

### OnLicenseStatusChanged

```csharp
public static event Action<LicenseStatus> OnLicenseStatusChanged;
```

Fired when license status changes.

```csharp
void Start()
{
    LTLMManager.OnLicenseStatusChanged += status => {
        Debug.Log($"Status changed to: {status}");
    };
}
```

---

### OnTokensConsumed

```csharp
public static event Action<LicenseData> OnTokensConsumed;
```

Fired after tokens are consumed.

```csharp
LTLMManager.OnTokensConsumed += license => {
    UpdateTokenUI(license.tokensRemaining);
};
```

---

### OnEnforcementTriggered

```csharp
public static event Action<string, string> OnEnforcementTriggered;
```

Fired when enforcement action is triggered. Parameters: (action, reason).

```csharp
LTLMManager.OnEnforcementTriggered += (action, reason) => {
    if (action == "terminate")
    {
        ShowCriticalError(reason);
    }
};
```

---

## Lifecycle Methods

| Method | When Called |
|--------|-------------|
| `Awake()` | Initializes singleton, loads settings, creates client |
| `Start()` | Calls `TryLoadStoredLicense()` if `autoValidateOnStart` |
| `OnApplicationPause(bool)` | Pauses/resumes heartbeat on mobile |
| `OnApplicationQuit()` | Attempts seat release before exit |
| `OnDestroy()` | Cleans up coroutines and singleton reference |

---

## See Also

- [License Operations](license-operations.md)
- [Token Operations](token-operations.md)
- [Events & Callbacks](events.md)
