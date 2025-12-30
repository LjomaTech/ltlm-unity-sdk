# Status Codes

All license status values and what they mean.

---

## LicenseStatus Enum

```csharp
public enum LicenseStatus
{
    Unauthenticated,
    Active,
    ValidNoSeat,       // All seats are occupied
    Kicked,            // Seat released by another device
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

### ValidNoSeat

License is valid but all concurrent seats are occupied by other devices.

```csharp
if (status == LicenseStatus.ValidNoSeat)
{
    ShowSeatManagementUI();
    // Allow user to release another device's seat
}
```

**When this happens:**
- License is valid and not expired
- Device is registered (HWID limit OK)
- BUT all concurrent seat slots are occupied

**Response includes:**
```csharp
license.seatStatus        // "NO_SEAT"
license.activeSeats       // e.g., 2
license.maxConcurrentSeats // e.g., 2
license.canReleaseSeat    // true if allowed to kick others
```

**Recommended action:**
- Show "Waiting for seat" UI
- Load active seats with `GetActiveSeats()`
- Let user release another device with `ReleaseSeat()`

---

### Kicked

Your seat was released by another device. You must reactivate.

```csharp
if (status == LicenseStatus.Kicked)
{
    ShowMessage("Your session was ended by another device.");
    ShowReactivateButton();
}
```

**When this happens:**
- Another user called `ReleaseSeat()` targeting this device
- The `OnKicked` event is also fired with details

**Response includes:**
```csharp
license.kickedNotice.kickedBy         // HWID of kicker
license.kickedNotice.kickedByNickname // Nickname of kicker
license.kickedNotice.timestamp
```

**Recommended action:**
- Stop heartbeat (SDK does this automatically)
- Show message with who kicked
- Offer "Try Again" button to reactivate

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

Device is offline and has exceeded the offline grace period OR offline is disabled.

```csharp
if (status == LicenseStatus.ConnectionRequired)
{
    ShowError("Please connect to the internet to continue.");
    DisableAllFeatures();
}
```

**When this happens:**
- Network validation failed AND:
  - `offlineEnabled` is `false`, OR
  - Offline time exceeded `offlineGraceHours`

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
    public GameObject seatManagementUI;
    public Text graceWarningText;

    void Start()
    {
        LTLMManager.OnLicenseStatusChanged += HandleStatus;
    }

    void HandleStatus(LicenseStatus status)
    {
        // Hide all first
        HideAll();

        switch (status)
        {
            case LicenseStatus.Active:
                mainApp.SetActive(true);
                break;

            case LicenseStatus.ValidNoSeat:
            case LicenseStatus.Kicked:
                seatManagementUI.SetActive(true);
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
        seatManagementUI.SetActive(false);
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
        LTLMManager.OnLicenseStatusChanged -= HandleStatus;
    }
}
```

---

## Mapping String Status to Enum

The SDK automatically converts the server's string status to the enum:

| Server String | Enum Value |
|---------------|------------|
| `"active"` | `LicenseStatus.Active` |
| `"VALID_NO_SEAT"` | `LicenseStatus.ValidNoSeat` |
| `"kicked"` or `"KICKED"` | `LicenseStatus.Kicked` |
| `"expired"` | `LicenseStatus.Expired` |
| `"grace_period"` | `LicenseStatus.GracePeriod` |
| `"connection_required"` | `LicenseStatus.ConnectionRequired` |
| `"suspended"` | `LicenseStatus.Suspended` |
| `"revoked"` | `LicenseStatus.Revoked` |

---

## Next Steps

- [Error Handling](error-handling.md) - Handle API errors
- [API Reference](api-reference.md) - All methods and properties
