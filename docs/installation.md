# Installation Guide

Detailed instructions for installing the LTLM Unity SDK.

---

## Requirements

| Requirement | Minimum | Recommended |
|-------------|---------|-------------|
| Unity Version | 2020.3 LTS | 2022.3 LTS |
| .NET Framework | .NET Standard 2.1 | .NET Standard 2.1 |
| API Compatibility | .NET 4.x | .NET 4.x |

---

## Installation Methods

### Method 1: Unity Package (Recommended)

1. Download `LTLM.unitypackage` from the [releases page](#)
2. Open your Unity project
3. Go to **Assets → Import Package → Custom Package**
4. Select the downloaded package
5. Click **Import All**

### Method 2: Manual Installation

1. Clone or download the SDK repository
2. Copy the `Assets/LTLM` folder to your project's `Assets` folder
3. Ensure dependencies are present (see below)

### Method 3: Unity Package Manager (UPM)

Add to your `manifest.json`:

```json
{
  "dependencies": {
    "com.ltlm.sdk": "https://github.com/ltlm/unity-sdk.git#v1.2.0"
  }
}
```

---

## Dependencies

The SDK requires these packages (included in the .unitypackage):

| Package | Version | Purpose |
|---------|---------|---------|
| Newtonsoft.Json | 13.0.3+ | JSON serialization |
| BouncyCastle.Cryptography | 2.2.1+ | Ed25519 & AES-256 encryption |

### Installing Dependencies Manually

If dependencies are missing:

1. **Newtonsoft.Json**: Install via Package Manager
   - Window → Package Manager → Add package by name
   - Enter: `com.unity.nuget.newtonsoft-json`

2. **BouncyCastle**: Included as DLL
   - Already in `Assets/LTLM/Plugins/BouncyCastle.Cryptography.dll`

---

## Project Structure

After installation, you'll have:

```
Assets/
└── LTLM/
    ├── Demos/                    # Example scenes and scripts
    │   ├── Scenes/
    │   └── Scripts/
    ├── Editor/                   # Editor tools (not in builds)
    │   └── ProjectSettingsEditor.cs
    ├── Plugins/                  # Third-party DLLs
    │   ├── BouncyCastle.Cryptography.dll
    │   └── Newtonsoft.Json.dll
    ├── Resources/                # Runtime resources
    │   └── LTLMSettings.asset
    └── Scripts/
        ├── Core/                 # Core SDK (crypto, models, storage)
        │   ├── Communication/
        │   ├── Models/
        │   ├── Security/
        │   └── Storage/
        └── Unity/                # Unity integration
            ├── Hardware/
            └── LTLMManager.cs
```

---

## Assembly Definitions

The SDK uses Unity Assembly Definitions for optimal compilation:

| Assembly | Contents | Platform |
|----------|----------|----------|
| `LTLM.Core` | Crypto, Models, Storage | All |
| `LTLM.Unity` | LTLMManager, DeviceID | All |
| `LTLM.Editor` | Editor tools | Editor only |
| `LTLM.Demos` | Demo scripts | All |

This ensures:
- Faster incremental compilation
- Editor code excluded from builds
- Proper dependency ordering for DLL builds

---

## Verification

After installation, verify the SDK is working:

1. Open **LTLM → Project Settings**
2. You should see the configuration window
3. Check Console for any errors

If you see errors:
- Ensure all DLLs are present in `Plugins/`
- Check that Newtonsoft.Json is installed
- Verify .NET settings in Player Settings

---

## Updating the SDK

### From .unitypackage

1. Backup your `Resources/LTLMSettings.asset`
2. Delete the `Assets/LTLM` folder
3. Import the new package
4. Restore your settings file

### From UPM

Update the version in `manifest.json`:

```json
"com.ltlm.sdk": "https://github.com/ltlm/unity-sdk.git#v1.3.0"
```

---

## Next Steps

- [Configuration](configuration.md) - Set up your project keys
- [Quick Start](quickstart.md) - Get running in 5 minutes
