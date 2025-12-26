# Simple Activation Demo

Basic license activation with proper startup handling.

---

## Overview

This demo shows how to:
- Handle startup validation with loading indicator
- Activate a license key
- Handle all license status types
- Access settings override from license data

---

## Files

- **Script**: `Assets/LTLM/Demos/Scripts/SimpleActivationUI.cs`
- **Scene**: `Assets/LTLM/Demos/Scenes/SimpleActivation.unity`

---

## UI Setup

Create these UI elements:

### Loading Panel
- Panel with "Checking license..." text
- Shown during startup validation

### Activation Panel
- InputField for license key
- Activate Button
- Status Text

### Licensed Panel
- License info display
- Token balance display
- Sign Out Button

---

## How It Works

### 1. Subscribe to Validation Events

```csharp
void OnEnable()
{
    LTLMManager.OnValidationStarted += OnValidationStarted;
    LTLMManager.OnValidationCompleted += OnValidationCompleted;
}
```

### 2. Handle Startup Validation

```csharp
void OnValidationStarted()
{
    // Show loading panel while checking stored license
    loadingPanel.SetActive(true);
    activationPanel.SetActive(false);
    licensedPanel.SetActive(false);
}

void OnValidationCompleted(bool success, LicenseStatus status)
{
    loadingPanel.SetActive(false);
    
    if (success && status == LicenseStatus.Active)
    {
        // User has valid license
        licensedPanel.SetActive(true);
        UpdateLicenseInfo();
    }
    else
    {
        // Need activation
        activationPanel.SetActive(true);
    }
}
```

### 3. Activate License

```csharp
void OnActivateClicked()
{
    string key = licenseKeyInput.text.Trim();
    
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
            ShowLicensedPanel();
            break;
        case LicenseStatus.GracePeriod:
            ShowLicensedPanel();
            ShowGraceWarning();
            break;
        case LicenseStatus.Expired:
            ShowExpiredMessage();
            break;
    }
}
```

### 4. Access Settings Override

```csharp
void UpdateLicenseInfo()
{
    var license = LTLMManager.Instance.ActiveLicense;
    
    // Access config (settings override from policy or license)
    if (license.config != null)
    {
        // license.config contains custom settings from dashboard
        Debug.Log("Config keys: " + license.config.Count);
    }
    
    // Access metadata (custom license data)
    if (license.metadata != null)
    {
        // license.metadata contains per-license custom data
    }
}
```

### 5. Sign Out

```csharp
void OnSignOutClicked()
{
    LTLMManager.Instance.DeactivateSeat();
    LTLMManager.Instance.ClearLicenseCache();
    ShowActivationPanel();
}
```

---

## Complete Code

See the full implementation in:
`Assets/LTLM/Demos/Scripts/SimpleActivationUI.cs`

---

## Testing

1. Run the scene
2. Enter a valid license key
3. Click Activate
4. Observe the licensed panel appears
5. Click Sign Out
6. Restart can verify stored license auto-loads
