# Offline Support

How to handle offline scenarios with the LTLM SDK.

---

## Overview

The LTLM SDK is designed for offline-first operation:

- **Grace Period**: Continue operating when temporarily offline
- **Offline Activation**: Air-gapped deployments with `.ltlm` files
- **Token Queueing**: Consume tokens offline, sync when online
- **Secure Clock**: Prevent clock manipulation attacks

---

## Grace Period

### How It Works

```
┌──────────────┐     Network     ┌──────────────┐
│    Active    │ ───Failure───► │ Grace Period │
│              │                 │   (24-72h)   │
└──────────────┘                 └───────┬──────┘
                                         │
                              Timeout    │
                                         ▼
                                 ┌──────────────┐
                                 │  Connection  │
                                 │   Required   │
                                 └──────────────┘
```

### Configuration

Set grace period in your policy (dashboard):

```json
{
  "sdkEnforcement": {
    "offlineGracePeriod": {
      "enabled": true,
      "duration": 72
    }
  }
}
```

> [!NOTE]
> The server includes `offlineEnabled` and `offlineGraceHours` in every license response. The SDK uses these values to determine if offline mode is allowed and for how long.

### Checking Grace Status

```csharp
var status = LTLMManager.Instance.GetLicenseStatus();

if (status == LicenseStatus.GracePeriod)
{
    int hoursRemaining = LTLMManager.Instance.GetGraceHoursRemaining();
    
    ShowWarning($"Operating offline. {hoursRemaining}h until connection required.");
}
else if (status == LicenseStatus.ConnectionRequired)
{
    ShowError("Internet connection required to continue.");
    DisableFeatures();
}
```

---

## Offline Activation

For air-gapped environments without internet access.

### Generating Offline License

1. User provides their HWID (from app)
2. Admin generates `.ltlm` file in dashboard
3. User imports file into app

### Getting User's HWID

```csharp
// Display to user for dashboard entry
string hwid = DeviceID.GetHWID();
hwidText.text = hwid;
copyButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = hwid);
```

### Importing Offline License

```csharp
public void OnImportClicked()
{
    // Open file dialog
    string path = EditorUtility.OpenFilePanel("Select License File", "", "ltlm");
    
    if (!string.IsNullOrEmpty(path))
    {
        string blob = File.ReadAllText(path);
        
        LTLMManager.Instance.ActivateOffline(blob,
            (license, status) => {
                Debug.Log("Offline license activated!");
                ShowMainMenu();
            },
            error => {
                Debug.LogError($"Import failed: {error}");
                ShowError("Invalid or expired license file.");
            }
        );
    }
}
```

### Offline License Contents

The `.ltlm` file contains:

```
{
  "data": {
    "licenseKey": "...",
    "validUntil": "2025-12-31T23:59:59Z",
    "hwid": "bound-hwid",
    "features": [...],
    "tokensLimit": 1000
  },
  "signature": "ed25519_signature",
  "generatedAt": "2024-01-15T10:00:00Z"
}

// Entire structure is AES-256 encrypted
```

### Limitations

- **No token sync** - Consumption tracked locally only
- **No renewal** - New file needed for extension
- **No revocation** - Can't remotely revoke
- **Fixed expiry** - Date set at generation time

---

## Token Queueing

### Offline Consumption

Tokens are consumed optimistically and queued:

```csharp
// Works offline
LTLMManager.Instance.ConsumeTokens(5, "export",
    license => {
        // Local balance updated immediately
        Debug.Log($"Local balance: {license.tokensRemaining}");
    }
);

// Queue syncs automatically when online
```

### Manual Sync

Force a sync attempt:

```csharp
public void OnSyncClicked()
{
    int pending = LTLMManager.Instance.GetPendingConsumptionCount();
    
    if (pending > 0)
    {
        syncStatus.text = $"Syncing {pending} actions...";
        
        LTLMManager.Instance.SyncPendingConsumptions();
    }
}
```

### Handling Sync Conflicts

The server uses `allowNegative: true` for offline sync:

1. User consumes 10 tokens offline
2. Another device consumed 5 tokens online
3. Server balance was 8
4. After sync: 8 - 10 = -2 (negative allowed)
5. User prompted to top-up

---

## Secure Clock

Prevents clock rollback attacks during offline operation.

### How It Works

```csharp
// SecureClock maintains a watermark
DateTime lastKnown = SecureClock.GetLastKnownTime();
DateTime systemTime = DateTime.UtcNow;

if (systemTime < lastKnown)
{
    // Clock rolled back!
    return lastKnown; // Use watermark
}
else
{
    // Update watermark
    SecureClock.UpdateLastKnownTime(systemTime);
    return systemTime;
}
```

### Clock Tampering Response

```csharp
if (SecureClock.IsClockTampered())
{
    ShowWarning("System clock appears to have been modified.");
    
    // Optionally require online validation
    ForceOnlineValidation();
}
```

---

## Implementation Patterns

### Pattern 1: Graceful Degradation

```csharp
IEnumerator NetworkMonitor()
{
    while (true)
    {
        if (!Application.internetReachability.HasFlag(NetworkReachability.ReachableViaCarrierDataNetwork))
        {
            // Show offline indicator
            offlineIndicator.SetActive(true);
            
            // Check grace status
            if (LTLMManager.Instance.GetLicenseStatus() == LicenseStatus.GracePeriod)
            {
                graceWarning.SetActive(true);
            }
        }
        else
        {
            offlineIndicator.SetActive(false);
            graceWarning.SetActive(false);
            
            // Sync pending actions
            LTLMManager.Instance.SyncPendingConsumptions();
        }
        
        yield return new WaitForSeconds(10);
    }
}
```

### Pattern 2: Offline-First UI

```csharp
public void OnFeatureClicked()
{
    // Check local entitlement first
    if (!LTLMManager.Instance.IsEntitled("feature_x"))
    {
        ShowUpgradePrompt();
        return;
    }
    
    // Consume locally
    LTLMManager.Instance.ConsumeTokens(1, "feature_x",
        _ => ExecuteFeature(),
        _ => ExecuteFeature() // Execute even on sync error
    );
}
```

---

## Best Practices

1. **Show offline status** - Users should know they're offline
2. **Display grace countdown** - Warn before lockout
3. **Queue user actions** - Don't block on network
4. **Sync on resume** - When app foregrounds
5. **Handle negative balance** - Show top-up prompt

---

## See Also

- [License Lifecycle](../license-lifecycle.md)
- [Token System](../tokens.md)
