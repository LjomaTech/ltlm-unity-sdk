# Quick Start

Get the LTLM SDK running in your Unity project in 5 minutes.

---

## Step 1: Import the SDK

1. Download `LTLM.unitypackage` from your dashboard
2. In Unity: **Assets → Import Package → Custom Package**
3. Select the package and click **Import All**

---

## Step 2: Add LTLMManager to Your Scene

1. Create an empty GameObject: **GameObject → Create Empty**
2. Name it `LTLM_Manager`
3. Add the `LTLMManager` component: **Add Component → LTLM → LTLMManager**

---

## Step 3: Configure Your Project Keys

### Using Editor Tools (Recommended)

1. Open **LTLM → Project Settings** from the menu bar
2. Enter your dashboard email and password
3. Click **Login & Fetch Projects**
4. Select your project from the dropdown
5. Click **Inject Selected Project Keys**

### Manual Configuration

Set these values in the LTLMManager Inspector:

| Field | Where to Find |
|-------|---------------|
| Project ID | Dashboard → Project → Settings |
| Public Key | Dashboard → Project → API Keys |
| Secret Key | Dashboard → Project → API Keys |

---

## Step 4: Activate a License

Add this script to handle license activation:

```csharp
using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;

public class LicenseActivation : MonoBehaviour
{
    public InputField licenseKeyInput;
    public Button activateButton;
    public Text statusText;

    void Start()
    {
        activateButton.onClick.AddListener(OnActivateClicked);
        
        // Check if already licensed
        if (LTLMManager.Instance.IsAuthenticated)
        {
            statusText.text = "License active!";
        }
    }

    void OnActivateClicked()
    {
        string key = licenseKeyInput.text.Trim();
        
        if (string.IsNullOrEmpty(key))
        {
            statusText.text = "Please enter a license key";
            return;
        }

        statusText.text = "Activating...";
        
        LTLMManager.Instance.ActivateLicense(key,
            OnActivationSuccess,
            OnActivationError
        );
    }

    void OnActivationSuccess(LicenseData license, LicenseStatus status)
    {
        switch (status)
        {
            case LicenseStatus.Active:
                statusText.text = "License activated successfully!";
                // Proceed to your main menu
                break;
            case LicenseStatus.ValidNoSeat:
                statusText.text = "All seats occupied. Try again later.";
                // Optionally show seat management UI
                break;
            case LicenseStatus.Expired:
                statusText.text = "This license has expired. Please renew.";
                break;
            case LicenseStatus.GracePeriod:
                statusText.text = "License expires soon. Please renew.";
                // Still allow access
                break;
            default:
                statusText.text = "License status: " + status;
                break;
        }
    }

    void OnActivationError(string error)
    {
        statusText.text = "Error: " + error;
    }
}
```

---

## Step 5: Check License on App Start

For returning users, check their stored license automatically:

```csharp
using UnityEngine;
using LTLM.SDK.Unity;

public class AppStart : MonoBehaviour
{
    public GameObject activationScreen;
    public GameObject mainMenu;

    void Start()
    {
        // Check if user has a valid stored license
        if (LTLMManager.Instance.IsAuthenticated)
        {
            // Already licensed - go to main menu
            activationScreen.SetActive(false);
            mainMenu.SetActive(true);
        }
        else
        {
            // No license - show activation
            activationScreen.SetActive(true);
            mainMenu.SetActive(false);
        }
    }
}
```

---

## Verify It Works

1. Run your game
2. Enter a valid license key
3. You should see "License activated successfully!"
4. Check console for `[LTLM] License validated successfully`

---

## What's Next?

- [License Activation](license-activation.md) - Complete activation guide
- [Token Consumption](token-consumption.md) - Usage-based features
- [Feature Entitlements](entitlements.md) - Lock/unlock features
- [Demo Scenes](demos/index.md) - Working examples

---

## Need Help?

- Check [Troubleshooting](troubleshooting.md) for common issues
- Email [support@ltlm.io](mailto:support@ltlm.io)
