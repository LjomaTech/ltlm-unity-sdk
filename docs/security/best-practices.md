# Security Best Practices

Recommendations for securing your LTLM implementation.

---

## Code Protection

### Use IL2CPP

IL2CPP provides better protection than Mono:

```
Player Settings → Other Settings → Scripting Backend → IL2CPP
```

Benefits:
- Converts C# to C++
- Harder to decompile
- Better performance

### Code Obfuscation

Consider obfuscating your builds:
- [Beebyte Obfuscator](https://assetstore.unity.com/packages/tools/utilities/obfuscator-48919)
- [Dotfuscator](https://www.preemptive.com/products/dotfuscator/)
- [ConfuserEx](https://mkaring.github.io/ConfuserEx/)

---

## Key Management

### Never Commit Secrets

Add to `.gitignore`:

```gitignore
# LTLM Secrets
**/LTLMSettings.asset
**/LTLMSettings.asset.meta
```

### Build-Time Injection

Inject keys at build time:

```csharp
public class BuildProcessor : IPreprocessBuildWithReport
{
    public void OnPreprocessBuild(BuildReport report)
    {
        var settings = Resources.Load<LTLMSettings>("LTLMSettings");
        settings.secretKey = Environment.GetEnvironmentVariable("LTLM_SECRET_KEY");
        EditorUtility.SetDirty(settings);
    }
}
```

### CI/CD Integration

```yaml
# GitHub Actions example
build:
  env:
    LTLM_SECRET_KEY: ${{ secrets.LTLM_SECRET_KEY }}
  steps:
    - run: unity-build -executeMethod InjectSecrets
```

---

## License Enforcement

### Don't Trust Client-Only Checks

```csharp
// BAD - Client only
if (HasPremiumFeature())
{
    UnlockContent();
}

// GOOD - Server-validated
LTLMManager.Instance.ValidateLicense(key,
    (license, status) => {
        if (status == LicenseStatus.Active && license.HasCapability("premium"))
        {
            UnlockContent();
        }
    }
);
```

### Validate Periodically

Don't just check once at startup:

```csharp
IEnumerator PeriodicValidation()
{
    while (true)
    {
        yield return new WaitForSeconds(3600); // Every hour
        LTLMManager.Instance.ValidateLicense(currentKey);
    }
}
```

### Handle All Status Types

```csharp
switch (status)
{
    case LicenseStatus.Active:
        EnableAllFeatures();
        break;
    case LicenseStatus.GracePeriod:
        EnableAllFeatures();
        ShowGraceWarning();
        break;
    case LicenseStatus.Expired:
        DisablePremiumFeatures();
        ShowRenewalPrompt();
        break;
    case LicenseStatus.Tampered:
        DisableAllFeatures();
        LogSecurityIncident();
        break;
    default:
        DisableAllFeatures();
        break;
}
```

---

## Tamper Response

### Don't Crash Immediately

Obvious crashes make it easier to find protection points:

```csharp
// BAD - Immediately obvious
if (status == LicenseStatus.Tampered)
{
    Application.Quit();
}

// BETTER - Subtle degradation
if (status == LicenseStatus.Tampered)
{
    // Log silently
    Analytics.LogEvent("tamper_detected");
    
    // Disable features gradually
    StartCoroutine(GradualDegradation());
}
```

### Log Security Events

```csharp
void OnSecurityViolation(string type, string details)
{
    // Send to your analytics
    Analytics.LogEvent("security_violation", new Dictionary<string, object> {
        { "type", type },
        { "hwid", DeviceID.GetHWID() },
        { "timestamp", DateTime.UtcNow }
    });
}
```

---

## Network Security

### Certificate Pinning (Advanced)

For high-security applications:

```csharp
// Custom certificate handler
public class LTLMCertHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        X509Certificate2 cert = new X509Certificate2(certificateData);
        string expectedThumbprint = "YOUR_CERT_THUMBPRINT";
        return cert.Thumbprint == expectedThumbprint;
    }
}
```

### Request Signing

The SDK already signs requests with nonces. For additional security, you can verify request timing.

---

## Storage Security

### Clear Sensitive Data

```csharp
void OnApplicationQuit()
{
    // Clear sensitive data from memory
    secretKey = null;
    GC.Collect();
}
```

### Secure Memory (Advanced)

For highly sensitive data:

```csharp
// Use SecureString for keys
SecureString secretKey = new SecureString();
foreach (char c in rawKey) secretKey.AppendChar(c);
secretKey.MakeReadOnly();
```

---

## Testing Security

### Test Tampering

1. Modify license file → Should detect
2. Delete license file → Should detect
3. Roll back clock → Should detect
4. Replay old request → Should fail

### Test Edge Cases

- Expired license behavior
- Network failure handling
- Concurrent seat limits
- Token exhaustion

---

## Monitoring

### Dashboard Alerts

Configure alerts for:
- Multiple HWID registrations
- Failed validation attempts
- Suspicious activity patterns

### Client Logging

```csharp
// Log security events (don't log secrets!)
Debug.Log($"[LTLM] Validation result: {status}");
Debug.Log($"[LTLM] Machine registered: {hwid.Substring(0, 8)}...");
```

---

## Checklist

- [ ] Using IL2CPP for production builds
- [ ] Secret key not in version control
- [ ] Keys injected at build time
- [ ] All status types handled
- [ ] Periodic validation enabled
- [ ] Tamper response implemented
- [ ] Security events logged
- [ ] Edge cases tested
