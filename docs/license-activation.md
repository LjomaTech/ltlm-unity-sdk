# License Activation

Complete guide to activating and managing licenses in your application.

---

## Overview

License activation binds a license key to the user's device. Once activated:
- The license is stored locally (encrypted)
- The user won't need to enter their key again
- The SDK automatically validates on app start

---

## Activating a License

### Basic Activation

```csharp
using LTLM.SDK.Unity;

public class ActivationExample : MonoBehaviour
{
    public void ActivateLicense(string licenseKey)
    {
        LTLMManager.Instance.ActivateLicense(
            licenseKey,
            OnSuccess,
            OnError
        );
    }

    void OnSuccess(LicenseData license, LicenseStatus status)
    {
        Debug.Log("License Key: " + license.licenseKey);
        Debug.Log("Status: " + status);
        Debug.Log("Valid Until: " + license.validUntil);
        Debug.Log("Tokens Available: " + license.tokensRemaining);
    }

    void OnError(string errorMessage)
    {
        Debug.LogError("Activation failed: " + errorMessage);
    }
}
```

---

## License Status Values

After activation, check the status to know what to do next:

| Status | Meaning | What to Do |
|--------|---------|------------|
| `Active` | License is valid | Enable all features |
| `Expired` | Past expiration date | Show renewal prompt |
| `GracePeriod` | Expired but grace period active | Show warning, enable features |
| `Suspended` | Admin suspended this license | Show "contact support" |
| `Revoked` | License permanently cancelled | Block access |

### Example: Handle All Status Types

```csharp
void OnActivationSuccess(LicenseData license, LicenseStatus status)
{
    switch (status)
    {
        case LicenseStatus.Active:
            // Full access
            ShowMainMenu();
            break;
            
        case LicenseStatus.Expired:
            // License expired
            ShowExpiredDialog(license.validUntil);
            break;
            
        case LicenseStatus.GracePeriod:
            // Expired but still usable temporarily
            int daysLeft = CalculateDaysLeft(license.gracePeriodEnds);
            ShowGraceWarning(daysLeft);
            ShowMainMenu();
            break;
            
        case LicenseStatus.Suspended:
            // Admin suspended this license
            ShowSuspendedMessage();
            break;
            
        case LicenseStatus.Revoked:
            // Permanently revoked
            ShowRevokedMessage();
            break;
    }
}
```

---

## Checking Stored License on App Start

When your app starts, check if the user already has a valid license:

```csharp
using UnityEngine;
using LTLM.SDK.Unity;

public class StartupCheck : MonoBehaviour
{
    public GameObject loginScreen;
    public GameObject mainApp;

    void Start()
    {
        // IsAuthenticated returns true if a valid license exists
        if (LTLMManager.Instance.IsAuthenticated)
        {
            GoToMainApp();
        }
        else
        {
            ShowLoginScreen();
        }
    }

    void GoToMainApp()
    {
        loginScreen.SetActive(false);
        mainApp.SetActive(true);
    }

    void ShowLoginScreen()
    {
        mainApp.SetActive(false);
        loginScreen.SetActive(true);
    }
}
```

---

## Validating License with Server

Force a fresh validation from the server:

```csharp
public void RefreshLicenseStatus()
{
    string currentKey = LTLMManager.Instance.ActiveLicense.licenseKey;
    
    LTLMManager.Instance.ValidateLicense(
        currentKey,
        (license, status) => {
            Debug.Log("Fresh status: " + status);
        },
        error => {
            Debug.LogError("Validation failed: " + error);
        }
    );
}
```

---

## Accessing License Information

Get details about the current license:

```csharp
void DisplayLicenseInfo()
{
    LicenseData license = LTLMManager.Instance.ActiveLicense;
    
    if (license == null)
    {
        Debug.Log("No active license");
        return;
    }

    Debug.Log("License Key: " + license.licenseKey);
    Debug.Log("Status: " + license.status);
    Debug.Log("Valid Until: " + license.validUntil);
    Debug.Log("Tokens Limit: " + license.tokensLimit);
    Debug.Log("Tokens Used: " + license.tokensConsumed);
    Debug.Log("Tokens Remaining: " + license.tokensRemaining);
}
```

---

## Signing Out (Clearing License)

Let users sign out and use a different license:

```csharp
public void SignOut()
{
    // Release the seat on the server
    LTLMManager.Instance.DeactivateSeat();
    
    // Clear local license data
    LTLMManager.Instance.ClearLicenseCache();
    
    // Return to login screen
    ShowLoginScreen();
}
```

---

## Complete UI Example

Full working example with UI feedback:

```csharp
using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;

public class ActivationUI : MonoBehaviour
{
    [Header("UI Elements")]
    public InputField licenseKeyInput;
    public Button activateButton;
    public Button signOutButton;
    public Text statusLabel;
    public Text licenseInfoLabel;
    public GameObject loadingSpinner;

    [Header("Screens")]
    public GameObject activationScreen;
    public GameObject mainScreen;

    void Start()
    {
        activateButton.onClick.AddListener(OnActivateClicked);
        signOutButton.onClick.AddListener(OnSignOutClicked);
        
        CheckExistingLicense();
    }

    void CheckExistingLicense()
    {
        if (LTLMManager.Instance.IsAuthenticated)
        {
            ShowMainScreen();
            UpdateLicenseInfo();
        }
        else
        {
            ShowActivationScreen();
        }
    }

    void OnActivateClicked()
    {
        string key = licenseKeyInput.text.Trim();
        
        if (string.IsNullOrEmpty(key))
        {
            ShowStatus("Please enter your license key", Color.yellow);
            return;
        }

        ShowLoading(true);
        ShowStatus("Activating...", Color.white);

        LTLMManager.Instance.ActivateLicense(key,
            (license, status) => {
                ShowLoading(false);
                HandleActivationResult(license, status);
            },
            error => {
                ShowLoading(false);
                ShowStatus("Error: " + error, Color.red);
            }
        );
    }

    void HandleActivationResult(LicenseData license, LicenseStatus status)
    {
        switch (status)
        {
            case LicenseStatus.Active:
                ShowStatus("Activated successfully!", Color.green);
                ShowMainScreen();
                UpdateLicenseInfo();
                break;

            case LicenseStatus.Expired:
                ShowStatus("This license has expired", Color.red);
                break;

            case LicenseStatus.GracePeriod:
                ShowStatus("License expired but in grace period", Color.yellow);
                ShowMainScreen();
                UpdateLicenseInfo();
                break;

            default:
                ShowStatus("License status: " + status, Color.yellow);
                break;
        }
    }

    void OnSignOutClicked()
    {
        LTLMManager.Instance.DeactivateSeat();
        LTLMManager.Instance.ClearLicenseCache();
        licenseKeyInput.text = "";
        ShowActivationScreen();
        ShowStatus("Signed out", Color.white);
    }

    void UpdateLicenseInfo()
    {
        var license = LTLMManager.Instance.ActiveLicense;
        licenseInfoLabel.text = 
            "Key: " + MaskKey(license.licenseKey) + "\n" +
            "Expires: " + license.validUntil + "\n" +
            "Tokens: " + license.tokensRemaining + " / " + license.tokensLimit;
    }

    string MaskKey(string key)
    {
        if (key.Length > 8)
            return key.Substring(0, 4) + "****" + key.Substring(key.Length - 4);
        return "****";
    }

    void ShowStatus(string message, Color color)
    {
        statusLabel.text = message;
        statusLabel.color = color;
    }

    void ShowLoading(bool show)
    {
        loadingSpinner.SetActive(show);
        activateButton.interactable = !show;
    }

    void ShowActivationScreen()
    {
        activationScreen.SetActive(true);
        mainScreen.SetActive(false);
    }

    void ShowMainScreen()
    {
        activationScreen.SetActive(false);
        mainScreen.SetActive(true);
    }
}
```

---

## Common Errors

| Error | Cause | Solution |
|-------|-------|----------|
| "Invalid license key" | Key doesn't exist or typo | Check key for errors |
| "License already activated on another device" | Reached activation limit | Contact support or deactivate old device |
| "Network error" | No internet connection | Check connection and retry |
| "Version not allowed" | App version blocked | Update your app |

---

## Next Steps

- [Token Consumption](token-consumption.md) - Use tokens for features
- [Feature Entitlements](entitlements.md) - Lock/unlock features
- [Error Handling](error-handling.md) - Handle all error cases
