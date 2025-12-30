# Troubleshooting

Solutions to common issues.

> **Note:** On startup, the SDK always calls the activation endpoint to resync the nonce chain. This prevents issues from power outages or crashes.

## Activation Issues

### "Invalid license key"

**Cause:** The license key was not found or has a typo.

**Solution:**
1. Double-check the key for typos
2. Ensure no extra spaces before or after the key
3. Verify the key exists in your dashboard

---

### "License already activated on another device"

**Cause:** The license has reached its activation limit.

**Solution:**
1. Deactivate the license on the old device
2. Or contact support to release a device slot
3. Or upgrade to a license with more activations

---

### "Version not allowed"

**Cause:** Your app version is not compatible with this license.

**Solution:**
1. Update your app to the latest version
2. Or check the version restrictions in your dashboard

---

## Network Issues

### "Connection failed" or "Timeout"

**Cause:** Network connectivity issue.

**Solution:**
1. Check internet connection
2. Check if your firewall is blocking the connection
3. Try again in a few seconds

---

### "Unable to connect to server"

**Cause:** Server may be temporarily unavailable.

**Solution:**
1. Wait a few minutes and try again
2. Check [status.ltlm.io](https://status.ltlm.io) for outages
3. Contact support if the issue persists

---

## Token Issues

### "Insufficient tokens"

**Cause:** Not enough tokens to perform the action.

**Solution:**
1. Check your token balance
2. Purchase more tokens via the top-up feature
3. Wait for token reset if you have a subscription

---

### Tokens not updating after purchase

**Cause:** License data not refreshed.

**Solution:**
```csharp
// Force refresh the license data
LTLMManager.Instance.ValidateLicense(
    LTLMManager.Instance.ActiveLicense.licenseKey,
    (license, status) => {
        Debug.Log("New balance: " + license.tokensRemaining);
    },
    error => Debug.LogError(error)
);
```

---

## Startup Issues

### App stuck on loading screen

**Cause:** Waiting for validation with no feedback.

**Solution:** Subscribe to validation events:

```csharp
void Start()
{
    LTLMManager.OnValidationStarted += () => ShowLoading("Checking license...");
    LTLMManager.OnValidationCompleted += (success, status) => {
        HideLoading();
        if (!success)
        {
            ShowActivationScreen();
        }
    };
}
```

---

### "No stored license found"

**Cause:** No previously activated license exists.

**Solution:** This is normal for new users. Show the activation screen.

```csharp
LTLMManager.OnValidationCompleted += (success, status) => {
    if (status == LicenseStatus.Unauthenticated)
    {
        ShowActivationScreen();
    }
};
```

---

## Offline Issues

### "Offline grace period exceeded"

**Cause:** The app has been offline too long.

**Solution:**
1. Connect to the internet
2. The license will automatically revalidate

---

### Features not working offline

**Cause:** License data is cached, but some features require online validation.

**Solution:** Design your app to check `IsAuthenticated` rather than making server calls for basic functionality.

---

## Editor Setup

### "Settings not found"

**Cause:** LTLMSettings asset is missing.

**Solution:**
1. Open **LTLM → Project Settings**
2. The editor will create the settings automatically

---

### Test Connection fails

**Cause:** Keys are not configured correctly.

**Solution:**
1. Verify Project ID, Public Key, and Secret Key in settings
2. Re-fetch keys from the dashboard using the Login feature

---

## Console Error Messages

### "[LTLM] Network Validation Failed"

This is a warning, not an error. The SDK will try to use offline grace.

### "[LTLM] SECURITY ALERT"

License file may have been corrupted or tampered with. Clear cache and reactivate:

```csharp
LTLMManager.Instance.ClearLicenseCache();
```

### "[LTLM] CLOCK TAMPERING DETECTED"

System clock was rolled back. Correct the system time and restart the app.

---

## Getting More Help

If issues persist:

1. Check the console for `[LTLM]` prefixed messages
2. Note the exact error message
3. Contact support@ltlm.io with:
   - Unity version
   - SDK version
   - Error message
   - Steps to reproduce
