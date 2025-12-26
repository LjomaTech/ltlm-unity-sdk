# LTLM Unity SDK

**Version 1.2.0** | License & Token Management for Unity

Manage software licenses, token-based usage, and feature entitlements in your Unity applications.

---

## 📖 Documentation

**[View Full Documentation →](docs/index.md)**

| Topic | Description |
|-------|-------------|
| [Quick Start](docs/quickstart.md) | Get running in 5 minutes |
| [License Activation](docs/license-activation.md) | Activate and validate licenses |
| [Token Consumption](docs/token-consumption.md) | Usage-based metering |
| [Feature Entitlements](docs/entitlements.md) | Lock/unlock features |
| [API Reference](docs/api-reference.md) | Complete method reference |
| [Demo Scenes](docs/demos/index.md) | Working examples |
| [Troubleshooting](docs/troubleshooting.md) | Common issues |

---

## 🚀 Quick Start

### 1. Import the SDK

Import `LTLM.unitypackage` into your Unity project.

### 2. Configure Keys

Open **LTLM → Project Settings** and inject your project keys.

### 3. Activate a License

```csharp
using LTLM.SDK.Unity;

LTLMManager.Instance.ActivateLicense("YOUR-LICENSE-KEY",
    (license, status) => {
        Debug.Log("Activated! Status: " + status);
    },
    error => {
        Debug.LogError("Error: " + error);
    }
);
```

### 4. Handle Startup Validation

Subscribe to events to know when auto-validation completes:

```csharp
void OnEnable()
{
    LTLMManager.OnValidationStarted += ShowLoading;
    LTLMManager.OnValidationCompleted += OnValidated;
}

void ShowLoading()
{
    loadingPanel.SetActive(true);
}

void OnValidated(bool success, LicenseStatus status)
{
    loadingPanel.SetActive(false);
    
    if (success && status == LicenseStatus.Active)
    {
        ShowMainApp();
    }
    else
    {
        ShowActivationScreen();
    }
}
```

---

## ✨ Features

- **License Activation** - Online and offline activation
- **Token System** - Usage-based metering with sync
- **Feature Entitlements** - Capability-based feature gating
- **In-App Purchases** - Checkout and upgrades
- **Settings Override** - Custom config per policy/license
- **Cross-Platform** - Windows, macOS, Linux, Android, iOS, WebGL

---

## 📁 Project Structure

```
Assets/LTLM/
├── Demos/               # Example scenes and scripts
│   ├── Scenes/
│   └── Scripts/
├── Editor/              # Editor tools (not in builds)
├── Plugins/             # BouncyCastle, Newtonsoft.Json
├── Resources/           # LTLMSettings asset
└── Scripts/
    ├── Core/            # Crypto, models, storage
    └── Unity/           # LTLMManager, platform scripts
```

---

## 🔑 Core Concepts

### License Status

| Status | Meaning |
|--------|---------|
| `Active` | License is valid |
| `Expired` | Past expiration date |
| `GracePeriod` | Expired but in grace |
| `Suspended` | Admin suspended |
| `Revoked` | Permanently revoked |

### Settings Override

Custom settings from your dashboard are available in the license:

```csharp
var license = LTLMManager.Instance.ActiveLicense;

// Access policy/license config
if (license.config != null)
{
    var customValue = license.config["myCustomSetting"];
}

// Access metadata
if (license.metadata != null)
{
    var companyName = license.metadata["company"];
}
```

---

## 🔗 Links

- **Dashboard**: [dashboard.ltlm.io](https://dashboard.ltlm.io)
- **Documentation**: [docs/index.md](docs/index.md)
- **Support**: support@ltlm.io

---

## 📋 Requirements

- Unity 2020.3 LTS or later
- .NET Standard 2.1

---

© 2025 Ljomatech. All rights reserved.
