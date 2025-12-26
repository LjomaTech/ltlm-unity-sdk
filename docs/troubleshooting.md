# Troubleshooting Guide

Solutions to common issues with the LTLM Unity SDK.

---

## Installation Issues

### "Settings not found in Resources"

**Cause**: `LTLMSettings.asset` is missing or not in the correct location.

**Solution**:
1. Open **LTLM → Project Settings**
2. The editor will create the asset automatically
3. Or manually create: **Create → LTLM → Settings**
4. Ensure it's in `Assets/LTLM/Resources/LTLMSettings.asset`

---

### Missing BouncyCastle or Newtonsoft.Json

**Cause**: Dependencies not imported correctly.

**Solution**:
1. Check `Assets/LTLM/Plugins/` for DLLs
2. If missing, re-import the package
3. For Newtonsoft.Json: Install via Package Manager
   - **Add package by name**: `com.unity.nuget.newtonsoft-json`

---

### Assembly Definition Errors

**Cause**: Missing assembly references after import.

**Solution**:
1. Delete `Library/` folder to force recompile
2. Ensure all `.asmdef` files are present:
   - `LTLM.Core.asmdef`
   - `LTLM.Unity.asmdef`
   - `LTLM.Editor.asmdef`
   - `LTLM.Demos.asmdef`

---

## Authentication Issues

### "Invalid signature"

**Cause**: Public key mismatch.

**Solution**:
1. Re-fetch keys from dashboard
2. Ensure public key includes PEM headers:
   ```
   -----BEGIN PUBLIC KEY-----
   ...
   -----END PUBLIC KEY-----
   ```
3. Use **LTLM → Project Settings → Login** to inject keys

---

### "Decryption failed"

**Cause**: Secret key is incorrect or malformed.

**Solution**:
1. Verify secret key is exactly 64 characters (hex)
2. Check for accidental spaces or line breaks
3. Re-copy from dashboard

---

### "HWID mismatch"

**Cause**: Hardware configuration changed.

**Solution**:
1. Normal after hardware upgrade
2. User needs to re-activate (counts against limit)
3. Admin can release old machine from dashboard

---

### "Activation limit reached"

**Cause**: License used on maximum number of machines.

**Solution**:
1. Check dashboard for registered machines
2. Release unused machines
3. Upgrade to policy with higher limit

---

## Runtime Issues

### "Connection required" in offline mode

**Cause**: Offline grace period exceeded.

**Solution**:
1. Connect to internet and restart app
2. License will auto-validate on connection
3. Consider increasing grace period in policy

---

### "Clock tampering detected"

**Cause**: System clock was rolled back.

**Solution**:
1. Correct system time
2. Restart application
3. If persists, clear license cache and re-activate

---

### Heartbeat not working

**Symptoms**: Active seats not updating, stale sessions.

**Solution**:
1. Check `heartbeatIntervalSeconds` is set
2. Verify network connectivity
3. Check console for heartbeat errors
4. Ensure coroutine is running (not stopped on scene load)

---

### Tokens not syncing

**Cause**: Background sync failing.

**Solution**:
1. Check internet connection
2. Call `SyncPendingConsumptions()` manually
3. Check for pending consumption count:
   ```csharp
   int pending = LTLMManager.Instance.GetPendingConsumptionCount();
   Debug.Log($"Pending: {pending}");
   ```

---

## Platform-Specific Issues

### Windows: Registry access denied

**Cause**: Permissions issue with registry.

**Solution**:
1. Run as administrator (for testing)
2. Check antivirus isn't blocking
3. SDK falls back gracefully if registry unavailable

---

### Android: License not persisting

**Cause**: `persistentDataPath` cleared.

**Solution**:
1. Don't use "Clear Data" in app settings
2. Check `AndroidManifest.xml` for backup rules
3. Verify app has storage permissions

---

### iOS: Build fails with crypto errors

**Cause**: Stripping crypto code.

**Solution**:
1. Add to `link.xml`:
   ```xml
   <assembly fullname="BouncyCastle.Cryptography">
     <type fullname="*" preserve="all"/>
   </assembly>
   ```

---

### WebGL: Limited functionality

**Cause**: WebGL has no secure storage.

**Solution**:
1. Expected behavior - WebGL uses PlayerPrefs
2. Enable server-side validation for critical checks
3. Consider WebGL as "untrusted client"

---

## Debug Logging

Enable verbose logging by searching console for `[LTLM]`:

```
[LTLM] SDK Bootstrapped and ready.
[LTLM] License validated successfully.
[LTLM] Starting heartbeat cycle (300s interval)
[LTLM] Token sync successful. Server balance: 847
[LTLM] SECURITY ALERT: Clock tampering detected!
```

---

## Getting Help

If issues persist:

1. **Check logs** for `[LTLM]` prefixed messages
2. **Verify configuration** in Project Settings window
3. **Test connection** using the Test Connection button
4. **Contact support** at support@ltlm.io with:
   - Unity version
   - SDK version
   - Console logs
   - Steps to reproduce
