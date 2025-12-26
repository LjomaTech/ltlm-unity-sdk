# Status Codes

All license status values and what they mean.

---

## LicenseStatus Enum

```csharp
public enum LicenseStatus
{
    Unauthenticated,
    Active,
    Expired,
    GracePeriod,
    ConnectionRequired,
    Suspended,
    Revoked,
    Tampered
}
```

---

## Status Descriptions

### Unauthenticated

No license is loaded. User needs to enter a license key.

```csharp
if (status == LicenseStatus.Unauthenticated)
{
    ShowActivationScreen();
}
```

---

### Active

License is valid and active. Full access should be granted.

```csharp
if (status == LicenseStatus.Active)
{
    EnableAllFeatures();
    ShowMainApp();
}
```

---

### Expired

License has passed its expiration date. User needs to renew.

```csharp
if (status == LicenseStatus.Expired)
{
    ShowRenewalPrompt();
    DisableExportFeature();
}
```

**When this happens:**
- The `validUntil` date has passed
- License was not renewed

**Recommended action:**
- Show renewal dialog
- Optionally allow limited functionality

---

### GracePeriod

License is expired but within the grace period. User can still use the app but should be warned.

```csharp
if (status == LicenseStatus.GracePeriod)
{
    int daysLeft = CalculateGraceDaysLeft();
    ShowWarning("Your license expired. " + daysLeft + " days left in grace period.");
    EnableAllFeatures(); // Still allow access
}
```

**When this happens:**
- License expired but grace period is configured
- Currently within grace window

**Recommended action:**
- Show warning banner
- Allow full access
- Prompt for renewal

---

### ConnectionRequired

Device is offline and has exceeded the offline grace period.

```csharp
if (status == LicenseStatus.ConnectionRequired)
{
    ShowError("Please connect to the internet to continue.");
    DisableAllFeatures();
}
```

**When this happens:**
- Network validation failed
- Offline time exceeded configured limit

**Recommended action:**
- Show connection required message
- Block features until online

---

### Suspended

License was suspended by administrator.

```csharp
if (status == LicenseStatus.Suspended)
{
    ShowMessage("Your license has been suspended. Please contact support.");
    DisableAllFeatures();
}
```

**When this happens:**
- Admin manually suspended the license
- Payment issue detected
- Terms of service violation

**Recommended action:**
- Show "contact support" message
- Block all access

---

### Revoked

License was permanently revoked.

```csharp
if (status == LicenseStatus.Revoked)
{
    ShowError("This license has been revoked.");
    ClearLicenseAndShowActivation();
}
```

**When this happens:**
- Refund was processed
- Fraud detected
- License permanently cancelled

**Recommended action:**
- Block all access
- Clear stored license
- Show activation screen

---

### Tampered

Local license data was modified or corrupted.

```csharp
if (status == LicenseStatus.Tampered)
{
    Debug.LogWarning("License tampering detected!");
    ClearLicenseCache();
    ShowActivationScreen();
}
```

**When this happens:**
- License file was manually edited
- File was corrupted
- Security markers don't match

**Recommended action:**
- Clear cached data
- Require fresh activation
- Consider logging the incident

---

## Complete Status Handler

```csharp
using UnityEngine;
using LTLM.SDK.Unity;

public class StatusHandler : MonoBehaviour
{
    public GameObject mainApp;
    public GameObject activationScreen;
    public GameObject expiredDialog;
    public GameObject graceWarning;
    public GameObject offlineError;
    public GameObject suspendedMessage;
    public Text graceWarningText;

    void Start()
    {
        LTLMManager.OnValidationCompleted += HandleStatus;
    }

    void HandleStatus(bool success, LicenseStatus status)
    {
        // Hide all first
        HideAll();

        switch (status)
        {
            case LicenseStatus.Active:
                mainApp.SetActive(true);
                break;

            case LicenseStatus.Expired:
                expiredDialog.SetActive(true);
                break;

            case LicenseStatus.GracePeriod:
                mainApp.SetActive(true);
                graceWarning.SetActive(true);
                UpdateGraceWarning();
                break;

            case LicenseStatus.ConnectionRequired:
                offlineError.SetActive(true);
                break;

            case LicenseStatus.Suspended:
            case LicenseStatus.Revoked:
                suspendedMessage.SetActive(true);
                break;

            case LicenseStatus.Tampered:
                LTLMManager.Instance.ClearLicenseCache();
                activationScreen.SetActive(true);
                break;

            case LicenseStatus.Unauthenticated:
            default:
                activationScreen.SetActive(true);
                break;
        }
    }

    void HideAll()
    {
        mainApp.SetActive(false);
        activationScreen.SetActive(false);
        expiredDialog.SetActive(false);
        graceWarning.SetActive(false);
        offlineError.SetActive(false);
        suspendedMessage.SetActive(false);
    }

    void UpdateGraceWarning()
    {
        // Calculate days remaining
        var license = LTLMManager.Instance.ActiveLicense;
        if (license != null && license.gracePeriodEnds != null)
        {
            var remaining = license.gracePeriodEnds.Value - System.DateTime.UtcNow;
            graceWarningText.text = "License expired. " + remaining.Days + " days left to renew.";
        }
    }

    void OnDestroy()
    {
        LTLMManager.OnValidationCompleted -= HandleStatus;
    }
}
```

---

## Mapping String Status to Enum

The SDK automatically converts the server's string status to the enum:

| Server String | Enum Value |
|---------------|------------|
| `"active"` | `LicenseStatus.Active` |
| `"expired"` | `LicenseStatus.Expired` |
| `"suspended"` | `LicenseStatus.Suspended` |
| `"revoked"` | `LicenseStatus.Revoked` |

---

## Next Steps

- [Error Handling](error-handling.md) - Handle API errors
- [API Reference](api-reference.md) - All methods and properties
