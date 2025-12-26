# Platform Guides

Platform-specific notes and configurations for the LTLM Unity SDK.

---

## Overview

| Platform | Storage | Tamper Detection | Notes |
|----------|---------|------------------|-------|
| Windows | Registry + Encrypted Files | ✅ Full | Best support |
| macOS | Application Support Files | ✅ HMAC | Full support |
| Linux | XDG Config Directory | ✅ HMAC | Full support |
| Android | Internal Storage | ✅ HMAC | Full support |
| iOS | App Documents | ✅ HMAC | Full support |
| WebGL | PlayerPrefs | ❌ None | Limited |

---

## Windows

### Storage Locations

| Data | Location |
|------|----------|
| License Cache | `%APPDATA%/../LocalLow/{Company}/{Product}/ltlm_vault/` |
| Tamper Markers | `HKCU\Software\LTLM\{projectId}` |
| Secure Time | `ltlm_vault/secure_time.ltlm` |

### Registry Keys

The SDK stores integrity markers in the Windows Registry:

```
HKEY_CURRENT_USER\Software\LTLM\{projectId}\
  ├── license_cache_hash
  ├── license_cache_exists
  └── license_key_hash
```

### Build Settings

No special settings required. Works with both Mono and IL2CPP.

---

## macOS

### Storage Locations

| Data | Location |
|------|----------|
| License Cache | `~/Library/Application Support/{bundleId}/ltlm_vault/` |
| Tamper Markers | `~/Library/Application Support/LTLM/{projectId}.markers` |

### Code Signing

If distributing outside App Store, ensure your app is notarized to avoid Gatekeeper issues.

### Sandboxing

Works within macOS sandboxing. Storage uses Application Support directory.

---

## Linux

### Storage Locations

| Data | Location |
|------|----------|
| License Cache | `~/.config/unity3d/{Company}/{Product}/ltlm_vault/` |
| Tamper Markers | `~/.config/LTLM/{projectId}.markers` |

Uses `XDG_CONFIG_HOME` if set, otherwise `~/.config/`.

### Permissions

Ensure user has write access to config directories.

---

## Android

### Storage Locations

| Data | Location |
|------|----------|
| License Cache | `Internal Storage/Android/data/{packageId}/files/ltlm_vault/` |
| Tamper Markers | `Internal Storage/Android/data/{packageId}/files/.ltlm_markers/` |

### Manifest Permissions

No special permissions required. Uses internal storage.

### ProGuard / R8

If using code shrinking, add to proguard rules:

```
-keep class LTLM.** { *; }
-keep class org.bouncycastle.** { *; }
```

### Lifecycle

The SDK handles `OnApplicationPause` to:
- Pause heartbeat when backgrounded
- Resume heartbeat when foregrounded
- Trigger re-validation on resume

---

## iOS

### Storage Locations

| Data | Location |
|------|----------|
| License Cache | `Application.persistentDataPath/ltlm_vault/` |
| Tamper Markers | `Application.persistentDataPath/.ltlm_markers/` |

### Build Settings

Add to `link.xml` to prevent stripping:

```xml
<linker>
  <assembly fullname="BouncyCastle.Cryptography">
    <type fullname="*" preserve="all"/>
  </assembly>
  <assembly fullname="Newtonsoft.Json">
    <type fullname="*" preserve="all"/>
  </assembly>
</linker>
```

### App Transport Security

LTLM servers use HTTPS. No ATS exceptions needed.

### Background Modes

No background modes required. Heartbeat pauses on background.

---

## WebGL

### Limitations

| Feature | Status |
|---------|--------|
| License Validation | ✅ Works |
| Token Consumption | ✅ Works |
| Secure Storage | ⚠️ PlayerPrefs only |
| Tamper Detection | ❌ Not available |
| Offline Mode | ❌ Limited |

### Storage

Uses `PlayerPrefs` for all storage. Data is not encrypted at rest.

### Security Considerations

- Treat WebGL as an **untrusted client**
- Implement server-side validation for critical operations
- Don't rely on client-side token enforcement

### CORS

LTLM API supports CORS. No proxy needed.

---

## Cross-Platform Tips

### Testing Multiple Platforms

1. Use Unity's platform switching
2. Test on actual devices when possible
3. Verify storage locations on each platform

### Handling Platform Differences

```csharp
#if UNITY_WEBGL
    // Show warning about limited security
    Debug.LogWarning("[LTLM] WebGL mode: Limited tamper detection");
#endif

#if UNITY_ANDROID || UNITY_IOS
    // Mobile-specific handling
    Application.lowMemory += OnLowMemory;
#endif
```

### Build Size Optimization

| DLL | Size | Required |
|-----|------|----------|
| BouncyCastle.Cryptography.dll | ~2.5 MB | Yes (crypto) |
| Newtonsoft.Json.dll | ~600 KB | Yes (JSON) |
| LTLM SDK Scripts | ~150 KB | Yes |

To reduce size:
- Use IL2CPP stripping with `link.xml`
- Enable compression in build settings
