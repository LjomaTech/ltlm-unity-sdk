# LTLM Unity SDK

**Version 1.2.0-PRO** | License & Token Management for Unity

The LTLM (License & Token Management) SDK for Unity provides a robust, secure, and easy-to-integrate solution for managing software licenses, token-based usage, and feature entitlements.

---

## 📦 Project Structure

To ensure maximum security and integrity, the LTLM SDK is divided into two parts:

*   **Core SDK (DLL Protected)**: The core logic, including encryption (Triple-Wrap), signature verification, and communication layers, is distributed as a pre-compiled DLL. This prevents tampering and ensures that security protocols remain intact.
*   **Demos & Scripts (Source Provided)**: We provide full C# source code for all UI examples, demo scenes, and integration scripts. These are designed to be modified and adapted to your project's specific needs.

### Assembly Definitions

The SDK uses Unity Assembly Definitions for optimal compilation:

| Assembly | Description | Platform |
|----------|-------------|----------|
| `LTLM.Core` | Core crypto, models, storage, communication | All |
| `LTLM.Unity` | LTLMManager, DeviceID, platform scripts | All |
| `LTLM.Editor` | Project settings, build tools | Editor only |
| `LTLM.Demos` | Demo scenes and UI scripts | All |

---

## 🚀 Getting Started

### 1. Installation

Import the `LTLM.unitypackage` into your project. The core files will be located in `Assets/LTLM`.

### 2. Configuration (Using Editor Tools)

1. Open the **LTLM Settings** window from the menu: `LTLM -> Project Settings`
2. Log in with your developer account credentials
3. Select your project from the dropdown
4. Click **Inject Selected Project Keys** to automatically configure your `Project ID`, `Public Key`, and `Secret Key`

![Editor Settings Window](https://via.placeholder.com/600x400?text=LTLM+Settings+Window)

### 3. Initialization

Attach the `LTLMBootstrap` script to a persistent GameObject in your first scene:

```csharp
// The bootstrap script automatically:
// 1. Creates the LTLMManager singleton
// 2. Loads settings from Resources/LTLMSettings.asset
// 3. Marks the manager as DontDestroyOnLoad
// 4. Starts automatic license validation (if enabled)
```

Or create the manager manually:

```csharp
// Manual initialization
GameObject go = new GameObject("LTLM_Provider");
var manager = go.AddComponent<LTLMManager>();
manager.projectId = "your-project-id";
manager.publicKey = "-----BEGIN PUBLIC KEY-----\n...";
manager.secretKey = "your-64-char-hex-secret";
DontDestroyOnLoad(go);
```

---

## 🛠️ Core API Reference

### License Activation

Activate a license key provided by the user:

```csharp
LTLMManager.Instance.ActivateLicense("YOUR-LICENSE-KEY", 
    (license, status) => {
        Debug.Log($"Welcome! License status: {status}");
        Debug.Log($"Expires: {license.validUntil}");
        Debug.Log($"Tokens remaining: {license.tokensRemaining}");
    },
    error => {
        Debug.LogError($"Activation failed: {error}");
    }
);
```

### License Validation

Validate an already-activated license (called automatically on app start):

```csharp
LTLMManager.Instance.ValidateLicense("YOUR-LICENSE-KEY",
    (license, status) => {
        if (status == LicenseStatus.Active)
            Debug.Log("License is active!");
        else if (status == LicenseStatus.GracePeriod)
            Debug.LogWarning("License expired but in grace period.");
    },
    error => {
        Debug.LogError($"Validation failed: {error}");
    }
);
```

### Entitlement Checks

Check if the current user is authorized to use a specific feature:

```csharp
// Simple feature check
if (LTLMManager.Instance.HasCapability("advanced_editor"))
{
    EnableAdvancedEditorFeature();
}

// Combined feature + token check
if (LTLMManager.Instance.IsEntitled("pro_export", requiredTokens: 10))
{
    EnableProExportFeature();
}

// Check token balance
int tokens = LTLMManager.Instance.GetTokenBalance();
Debug.Log($"You have {tokens} tokens available");
```

### Token Consumption

Consume tokens for usage-based features:

```csharp
// Simple consumption
LTLMManager.Instance.ConsumeTokens(1, "render_export", 
    license => {
        Debug.Log($"Tokens remaining: {license.tokensRemaining}");
    },
    error => {
        Debug.LogError($"Failed to consume tokens: {error}");
    }
);

// Check before consuming
LTLMManager.Instance.DoesHaveTokens(5, hasTokens => {
    if (hasTokens)
    {
        LTLMManager.Instance.ConsumeTokens(5, "batch_export", 
            license => StartBatchExport(),
            error => ShowError(error)
        );
    }
    else
    {
        ShowTopUpDialog();
    }
});
```

### Offline Activation

Activate a license using an offline `.ltlm` file:

```csharp
// Read the .ltlm file content (encrypted blob)
string encryptedBlob = File.ReadAllText("/path/to/license.ltlm");

LTLMManager.Instance.ActivateOffline(encryptedBlob,
    (license, status) => Debug.Log("Offline activation successful!"),
    error => Debug.LogError($"Offline activation failed: {error}")
);
```

### Status & Information

Get license status and information:

```csharp
// Get current status
LicenseStatus status = LTLMManager.Instance.GetLicenseStatus();

switch (status)
{
    case LicenseStatus.Active:
        // Full access
        break;
    case LicenseStatus.GracePeriod:
        int hours = LTLMManager.Instance.GetDaysRemaining() * 24;
        ShowWarning($"License expired! {hours} hours of grace remaining.");
        break;
    case LicenseStatus.Expired:
        ShowRenewalDialog();
        break;
    case LicenseStatus.Tampered:
        ShowSecurityAlert();
        break;
}

// Get metadata
object customData = LTLMManager.Instance.GetMetadata("customField");
```

### Sign Out / Clear License

Clear the current license and sign out:

```csharp
// Release concurrent seat first (notifies server)
LTLMManager.Instance.DeactivateSeat();

// Clear cached license data
LTLMManager.Instance.ClearLicenseCache();
```

---

## 💳 In-App Purchases

### Get Available Products

Fetch purchasable policies (plans/tiers):

```csharp
LTLMManager.Instance.GetBuyablePolicies(
    policies => {
        foreach (var policy in policies)
        {
            Debug.Log($"{policy.name} - ${policy.price} {policy.currency}");
        }
    },
    error => Debug.LogError(error)
);
```

### Create Checkout Session

Generate a hosted checkout URL:

```csharp
// New license purchase
LTLMManager.Instance.CreateCheckoutSession(
    policyId: "policy-id-here",
    customerEmail: "user@example.com",
    redirectUrl: "https://yourapp.com/success",
    url => Application.OpenURL(url),
    error => Debug.LogError(error)
);

// Token top-up (requires active license)
LTLMManager.Instance.CreateTopUpSession(
    packId: "tokens_100",
    redirectUrl: "https://yourapp.com/success",
    url => Application.OpenURL(url),
    error => Debug.LogError(error)
);
```

---

## 🔒 Security Features

| Feature | Description |
|---------|-------------|
| **Triple-Wrap Protocol** | AES-256-CBC encryption + Ed25519 signature on all communications |
| **Nonce Management** | Per-request nonces prevent replay attacks |
| **Secure Storage** | License data encrypted with HWID-derived key |
| **Tamper Detection** | Cross-platform integrity verification (Registry/HMAC files) |
| **Clock Tampering** | Monotonic clock detects rollback attempts |
| **Offline Grace** | Configurable offline operation with automatic lockdown |

---

## 📱 Platform Support

| Platform | Secure Storage | Tamper Detection | Notes |
|----------|----------------|------------------|-------|
| **Windows** | ✅ Registry + Encrypted Files | ✅ Registry HMAC | Full support |
| **macOS** | ✅ Application Support | ✅ HMAC Signature | Full support |
| **Linux** | ✅ XDG Config Dir | ✅ HMAC Signature | Full support |
| **Android** | ✅ Internal Storage | ✅ HMAC Signature | Full support |
| **iOS** | ✅ App Documents | ✅ HMAC Signature | Full support |
| **WebGL** | ⚠️ PlayerPrefs | ❌ None | Limited security |

---

## 📂 Demos Included

| Demo | Description |
|------|-------------|
| `SimpleActivationUI` | Basic "Enter Key" activation flow |
| `UnifiedAuthDemo` | Combines user login (OTP) with direct license activation |
| `UsageBasedDemo` | Token consumption and balance checks |
| `FeatureEntitlementDemo` | Feature gating based on capabilities |
| `OfflineActivationDemo` | Local verification of encrypted `.ltlm` files |
| `InGameStoreDemo` | In-app purchase flow integration |
| `CustomerPortalUI` | Customer self-service portal |

---

## 🔧 Editor Tools

### Project Settings Window (`LTLM -> Project Settings`)

The Project Settings Editor provides:

1. **Manual Configuration**: Directly enter Project ID, Public Key, and Secret Key
2. **Developer Login**: Log in with your dashboard credentials to fetch project keys automatically
3. **Project Selection**: Choose from your available projects
4. **Key Injection**: One-click injection of all security keys
5. **Capability Preview**: View enabled capabilities and analytics events
6. **Test Connection**: Verify backend connectivity

### Build Integration

The SDK automatically handles build-time requirements:

```csharp
// BuildProcessHandler.cs hooks into the Unity build process
// - Validates LTLMSettings before build
// - Strips debug logs in Release builds
// - Ensures secret key is present
```

---

## 🏗️ Building as DLL

To build the SDK into a DLL:

1. Ensure all assembly definitions are properly configured
2. Build `LTLM.Core.dll` from the `Scripts/Core` folder
3. Build `LTLM.Unity.dll` from the `Scripts/Unity` folder
4. Reference both DLLs plus `BouncyCastle.Cryptography.dll` and `Newtonsoft.Json.dll`

The assembly definitions ensure proper dependency ordering:
- `LTLM.Core` has no Unity runtime dependencies (core crypto/models)
- `LTLM.Unity` depends on `LTLM.Core` (MonoBehaviour integrations)
- `LTLM.Editor` depends on both (Editor-only tools)

---

## ⚙️ Configuration Options

### LTLMSettings (ScriptableObject)

| Property | Type | Description |
|----------|------|-------------|
| `projectId` | string | Your LTLM Project ID |
| `projectName` | string | Display name for the project |
| `publicKey` | string | Ed25519 public key (PEM format) |
| `secretKey` | string | AES-256 secret key (64-char hex) |
| `capabilities` | List<string> | Enabled feature capabilities |
| `analyticsEvents` | List<string> | Configured analytics events |

### LTLMManager Inspector

| Property | Default | Description |
|----------|---------|-------------|
| `heartbeatIntervalSeconds` | 300 | Time between heartbeat requests (seconds) |
| `autoValidateOnStart` | true | Auto-load and validate stored license on Start() |
| `softwareVersion` | Application.version | Your app version for version gating |

---

## 🐛 Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| "Settings not found in Resources" | Create `LTLMSettings.asset` via `LTLM -> Project Settings` |
| "Invalid signature" | Check that publicKey matches project on dashboard |
| "Decryption failed" | Verify secretKey is correct 64-char hex string |
| "HWID mismatch" | Device hardware changed; re-activate license |
| "Clock tampering detected" | System clock was rolled back; correct time and restart |

### Debug Logging

Logs are prefixed with `[LTLM]` for easy filtering:

```
[LTLM] SDK Bootstrapped and ready.
[LTLM] License validated successfully.
[LTLM] Heartbeat sent. Active seats: 1/3
[LTLM] Token sync successful. Server balance: 847
```

---

## 📄 License

© 2025 Ljomatech. All rights reserved.
