# License Lifecycle

Understanding license activation, validation, and enforcement.

---

## License States

```
                    ┌──────────────────┐
                    │                  │
              ┌─────│  Unauthenticated │◄────────────┐
              │     │                  │             │
              │     └────────┬─────────┘             │
              │              │                       │
              │         Activate                  Revoke/
              │              │                   Clear Cache
              │              ▼                       │
              │     ┌──────────────────┐             │
              │     │                  │             │
              │     │     Active       │─────────────┤
              │     │                  │             │
              │     └────────┬─────────┘             │
              │              │                       │
              │         Expired                      │
              │              │                       │
              │              ▼                       │
              │     ┌──────────────────┐             │
              │     │                  │             │
              │     │   Grace Period   │─────────────┤
              │     │                  │             │
              │     └────────┬─────────┘             │
              │              │                       │
              │         Timeout                      │
              │              │                       │
              │              ▼                       │
              │     ┌──────────────────┐             │
              └────►│                  │             │
                    │ Connection Req.  │─────────────┘
                    │                  │
                    └──────────────────┘
```

| Status | Description | User Experience |
|--------|-------------|-----------------|
| `Unauthenticated` | No license loaded | Show activation UI |
| `Active` | Valid license | Full access |
| `Expired` | Past `validUntil` date | Show renewal prompt |
| `GracePeriod` | Expired but within grace | Show warning + full access |
| `ConnectionRequired` | Offline grace exceeded | Block until online |
| `Suspended` | Admin suspended | Show contact support |
| `Revoked` | Permanently revoked | Block access |
---

## Activation Flow

### Online Activation

```csharp
LTLMManager.Instance.ActivateLicense("LICENSE-KEY",
    (license, status) => {
        switch (status)
        {
            case LicenseStatus.Active:
                // Success - enable features
                break;
            case LicenseStatus.GracePeriod:
                // Warn user about expiration
                break;
            case LicenseStatus.Expired:
                // Show renewal dialog
                break;
        }
    },
    error => {
        // Handle activation error
        Debug.LogError(error);
    }
);
```

### What Happens on Activation

1. **Request sent** with license key, HWID, and metadata
2. **Server validates** license status, version, and geofencing
3. **Machine registered** if within activation limit
4. **Response encrypted** and signed
5. **SDK verifies** signature and caches license
6. **Heartbeat starts** for concurrent seat management

### Offline Activation

For air-gapped environments:

```csharp
// Read .ltlm file (encrypted activation blob)
string blob = File.ReadAllText("/path/to/license.ltlm");

LTLMManager.Instance.ActivateOffline(blob,
    (license, status) => {
        // Offline license activated
    },
    error => {
        // Invalid or expired blob
    }
);
```

---

## Validation Flow

### Auto-Validation on Start

If `autoValidateOnStart` is enabled:

```csharp
// Called automatically in Start()
LTLMManager.Instance.TryLoadStoredLicense(
    (license, status) => OnLicenseValidated(status),
    error => OnLicenseError(error)
);
```

### Manual Validation

Force a refresh from server:

```csharp
LTLMManager.Instance.ValidateLicense("LICENSE-KEY",
    (license, status) => {
        // Fresh data from server
    },
    error => {
        // Falls back to offline grace if network fails
    }
);
```

---

## Heartbeat System

Heartbeats maintain active sessions and enforce concurrent seat limits.

### How It Works

1. **Interval**: Default 300 seconds (5 minutes)
2. **Payload**: License key, HWID, timestamp
3. **Server updates**: `lastCheckIn` time, active seat count
4. **Response includes**: Updated license data, seat count

### Configuration

```csharp
// In Inspector or code
LTLMManager.Instance.heartbeatIntervalSeconds = 300f;
```

### Lifecycle Integration

- **OnApplicationPause**: Heartbeat pauses when app backgrounds
- **OnApplicationQuit**: Final heartbeat with `isClosing=true`
- **Scene Reload**: Continues via `DontDestroyOnLoad`

---

## Offline Grace Period

When the device goes offline, the SDK enters grace mode:

### How It Works

1. Last successful sync time is stored
2. On each operation, current time is compared
3. If offline duration < grace period, access continues
4. If exceeded, status becomes `ConnectionRequired`

### Configuration

Grace period is set in your policy configuration on the dashboard:

```json
{
  "limits": {
    "offlineGracePeriodHours": 24
  }
}
```

### Checking Grace Status

```csharp
if (LTLMManager.Instance.GetLicenseStatus() == LicenseStatus.GracePeriod)
{
    int hoursRemaining = LTLMManager.Instance.GetGraceHoursRemaining();
    ShowWarning($"Offline mode: {hoursRemaining}h remaining");
}
```

---

## Enforcement

### Events

Subscribe to enforcement events:

```csharp
void Start()
{
    LTLMManager.OnLicenseStatusChanged += HandleStatusChange;
    LTLMManager.OnEnforcementTriggered += HandleEnforcement;
}

void HandleStatusChange(LicenseStatus newStatus)
{
    switch (newStatus)
    {
        case LicenseStatus.Expired:
            DisablePremiumFeatures();
            ShowRenewalPrompt();
            break;
        case LicenseStatus.Revoked:
            ForceLogout();
            break;
    }
}

void HandleEnforcement(string action, string reason)
{
    // action: "warn", "disable", "terminate"
    Debug.Log($"Enforcement: {action} - {reason}");
}
```

### Enforcement Actions

| Action | Trigger | Behavior |
|--------|---------|----------|
| `warn` | Grace period entered | Log + callback |
| `disable` | License expired | Disable features |
| `terminate` | Critical violation | Application quit |

---

## Sign Out / Clear License

```csharp
// Release seat (notifies server)
LTLMManager.Instance.DeactivateSeat();

// Clear local cache
LTLMManager.Instance.ClearLicenseCache();
```

---

## Best Practices

1. **Always handle all status types** in your callbacks
2. **Show clear messaging** for expired/suspended states
3. **Implement renewal flow** for expired licenses
4. **Test offline scenarios** during development
5. **Don't block UI** during validation (use loading indicators)

---

## Next Steps

- [Token System](tokens.md) - Usage-based metering
- [LTLMManager API](api/ltlm-manager.md) - Full API reference
