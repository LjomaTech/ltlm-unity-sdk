# Frequently Asked Questions

Common questions about the LTLM Unity SDK.

---

## General

### What Unity versions are supported?

Unity 2020.3 LTS and later. We recommend 2022.3 LTS for best compatibility.

### What .NET version is required?

.NET Standard 2.1 with .NET 4.x API compatibility.

### Does the SDK work with IL2CPP?

Yes. Ensure you add a `link.xml` to preserve Newtonsoft.Json and BouncyCastle assemblies.

### Can I use the SDK in a published game?

Yes, the SDK is designed for production use.

---

## Licensing

### How many machines can activate a license?

Depends on your policy configuration. Default is typically 1-3 machines per license.

### What happens when a license expires?

The SDK returns `LicenseStatus.Expired`. You can implement grace periods and renewal prompts.

### Can users share licenses?

The HWID binding prevents simple copying. Each activation is tracked and limited.

### How do offline licenses work?

1. Generate `.ltlm` file from dashboard
2. User imports file into app
3. SDK validates signature and expiry locally
4. No internet required after import

---

## Security

### Is the communication encrypted?

Yes, triple-layer encryption:
1. HTTPS (TLS 1.2+)
2. AES-256-CBC payload encryption
3. Ed25519 signature verification

### Can someone crack the SDK?

While nothing is 100% secure, the SDK implements industry-standard protections. For maximum security:
- Use IL2CPP
- Enable code obfuscation
- Implement server-side validation for critical features

### What if someone modifies the license file?

Tamper detection will trigger and return `status = "TAMPERED"`.

---

## Tokens

### What are tokens used for?

Usage-based metering. Examples:
- API calls
- Exports/renders
- AI generations
- Premium feature usage

### Do tokens refill automatically?

Depends on policy configuration. Options include:
- One-time allocation (perpetual)
- Monthly reset (subscription)
- Top-up purchases

### What happens offline?

Tokens are consumed locally and synced when online. The server reconciles with `allowNegative: true` to handle offline usage.

---

## Integration

### Can I have multiple LTLMManager instances?

No, it's a singleton. One instance persists across scenes.

### How do I handle scene loads?

The manager uses `DontDestroyOnLoad` - it persists automatically.

### Can I use a different license key at runtime?

Yes, call `ClearLicenseCache()` then `ActivateLicense()` with the new key.

### How do I implement a login flow?

```csharp
// Show activation UI
var key = inputField.text;
LTLMManager.Instance.ActivateLicense(key,
    (license, status) => ShowMainMenu(),
    error => ShowError(error)
);
```

---

## Mobile

### Does it work on Android?

Yes, fully supported with optimized storage.

### Does it work on iOS?

Yes, fully supported with App Documents storage.

### How does app backgrounding work?

Heartbeat automatically pauses when app backgrounds and resumes on foreground.

---

## WebGL

### Are there limitations on WebGL?

Yes:
- No secure file storage (uses PlayerPrefs)
- No tamper detection
- Consider server validation for critical checks

### Should I use LTLM on WebGL?

It works, but treat WebGL as an untrusted client. Validate important operations server-side.

---

## Development

### How do I test without a real license?

1. Create a test policy on dashboard
2. Generate a test license
3. Use in development builds

### Can I use staging/development servers?

Yes, modify `LTLMConstants.BackendUrl` or use preprocessor directives.

### How do I debug license issues?

1. Check console for `[LTLM]` logs
2. Use Test Connection in Editor tools
3. Verify keys in Project Settings

---

## Billing

### How do I enable in-app purchases?

```csharp
// Get purchasable plans
LTLMManager.Instance.GetBuyablePolicies(
    policies => ShowPurchaseUI(policies),
    error => Debug.LogError(error)
);

// Create checkout
LTLMManager.Instance.CreateCheckoutSession(
    policyId, email, redirectUrl,
    url => Application.OpenURL(url),
    error => Debug.LogError(error)
);
```

### How do token top-ups work?

Configure top-up packs in your policy, then:
```csharp
LTLMManager.Instance.CreateTopUpSession(
    packId: "tokens_100",
    redirectUrl: "https://...",
    url => Application.OpenURL(url),
    error => Debug.LogError(error)
);
```

---

## Support

### Where can I get help?

- Documentation: This site
- Dashboard: dashboard.ltlm.io
- Email: support@ltlm.io
- GitHub Issues: For SDK bugs

### How do I report a bug?

1. Check existing issues on GitHub
2. Include Unity version, SDK version, logs
3. Provide reproduction steps
