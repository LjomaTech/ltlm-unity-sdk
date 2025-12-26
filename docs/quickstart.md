# Quick Start Guide

Get the LTLM Unity SDK running in your project in just 5 minutes.

---

## Prerequisites

- Unity 2020.3 LTS or later
- .NET Standard 2.1 compatible project
- An LTLM account with a configured project

---

## Step 1: Import the SDK

1. Download the latest `LTLM.unitypackage` from the [releases page](#)
2. In Unity, go to **Assets → Import Package → Custom Package**
3. Select the downloaded package and click **Import All**

The SDK will be installed to `Assets/LTLM/`.

---

## Step 2: Configure Your Project

### Option A: Using Editor Tools (Recommended)

1. Open **LTLM → Project Settings** from the Unity menu
2. Log in with your dashboard credentials
3. Select your project from the dropdown
4. Click **Inject Selected Project Keys**

![Project Settings Window](images/project-settings.png)

### Option B: Manual Configuration

1. Create a `LTLMSettings` asset: **Create → LTLM → Settings**
2. Place it in `Assets/LTLM/Resources/LTLMSettings.asset`
3. Fill in your Project ID, Public Key, and Secret Key

---

## Step 3: Initialize the SDK

Add the `LTLMBootstrap` script to a GameObject in your first scene:

```csharp
// The bootstrap automatically:
// 1. Creates the LTLMManager singleton
// 2. Loads settings from Resources
// 3. Enables DontDestroyOnLoad
// 4. Starts automatic validation (if enabled)
```

Or initialize manually:

```csharp
using LTLM.SDK.Unity;

public class GameInitializer : MonoBehaviour
{
    void Start()
    {
        // SDK is auto-initialized, just activate your license
        LTLMManager.Instance.ActivateLicense("YOUR-LICENSE-KEY",
            (license, status) => {
                Debug.Log($"License activated! Status: {status}");
                StartGame();
            },
            error => {
                Debug.LogError($"Activation failed: {error}");
                ShowActivationUI();
            }
        );
    }
}
```

---

## Step 4: Verify Integration

Run your game and check the Console for:

```
[LTLM] SDK Bootstrapped and ready.
[LTLM] License validated successfully.
[LTLM] Starting heartbeat cycle (300s interval)
```

---

## What's Next?

- [License Lifecycle](license-lifecycle.md) - Understand activation and validation
- [Token System](tokens.md) - Implement usage-based features
- [Security Best Practices](security/best-practices.md) - Protect your implementation

---

## Example: Complete Integration

```csharp
using UnityEngine;
using LTLM.SDK.Unity;

public class LicenseController : MonoBehaviour
{
    [SerializeField] private GameObject activationUI;
    [SerializeField] private GameObject mainMenu;

    void Start()
    {
        // Check if we have a stored license
        if (LTLMManager.Instance.IsAuthenticated)
        {
            // Already activated - go to main menu
            ShowMainMenu();
        }
        else
        {
            // Need activation
            ShowActivationUI();
        }
    }

    public void OnActivateClicked(string licenseKey)
    {
        LTLMManager.Instance.ActivateLicense(licenseKey,
            (license, status) => {
                if (status == LicenseStatus.Active)
                {
                    ShowMainMenu();
                }
                else if (status == LicenseStatus.Expired)
                {
                    ShowRenewalPrompt();
                }
            },
            error => ShowError(error)
        );
    }

    private void ShowMainMenu()
    {
        activationUI.SetActive(false);
        mainMenu.SetActive(true);
    }

    private void ShowActivationUI()
    {
        mainMenu.SetActive(false);
        activationUI.SetActive(true);
    }
}
```
