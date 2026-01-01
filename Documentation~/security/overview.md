# Security Architecture

How the LTLM SDK protects your software and licenses.

---

## Overview

The LTLM SDK implements multiple layers of security to protect against:

- **Man-in-the-middle attacks** - Encrypted communications
- **Replay attacks** - Nonce-based request validation
- **License sharing** - Hardware-bound activation
- **Offline tampering** - File integrity verification
- **Clock manipulation** - Monotonic time tracking

---

## Triple-Wrap Protocol

All SDK-to-backend communication uses three layers of protection:

```
┌────────────────────────────────────────────┐
│           Layer 1: HTTPS (TLS 1.2+)        │
│   - Certificate validation                  │
│   - Transport encryption                    │
├────────────────────────────────────────────┤
│         Layer 2: AES-256 Encryption         │
│   - Per-project secret key                  │
│   - Random IV per request                   │
│   - Format: IV:CipherText (CBC)             │
├────────────────────────────────────────────┤
│         Layer 3: Ed25519 Signatures         │
│   - Server signs all responses              │
│   - SDK verifies before trusting            │
│   - Prevents response tampering             │
└────────────────────────────────────────────┘
```

### Encryption (AES-256-CBC)

```csharp
// Request encryption
string plaintext = JsonConvert.SerializeObject(payload);
string encrypted = cryptoProvider.Encrypt(plaintext);
// Output: "iv_hex:ciphertext_hex"

// Response decryption
string decrypted = cryptoProvider.Decrypt(serverResponse);
var data = JsonConvert.DeserializeObject<T>(decrypted);
```

### Signature Verification

```csharp
// Server response format
{
  "data": { ... },
  "signature": "base64_ed25519_signature",
  "timestamp": 1703548800000
}

// SDK verification
bool valid = cryptoProvider.VerifySignature(
    data: jsonStringifiedData,
    signature: response.signature
);

if (!valid) throw new SecurityException("Invalid signature");
```

---

## Nonce Management

Prevents replay attacks by ensuring each request is unique:

```
Request 1:
  Client sends: { nonce: null }      (first request)
  Server returns: { server_nonce: "abc123" }

Request 2:
  Client sends: { nonce: "abc123" }  (must match)
  Server returns: { server_nonce: "def456" }

Request 3:
  Client sends: { nonce: "def456" }  (must match new)
  ...
```

If a request is replayed with an old nonce, the server rejects it.

---

## Hardware ID (HWID)

Licenses are bound to specific machines:

### Generation

```csharp
// Combines multiple hardware signals
string hwid = SHA256(
    CPU_ID +
    GPU_Name +
    RAM_Size +
    OS_Version +
    Motherboard_ID
);
```

### Stability

The HWID is designed to be:
- **Stable** - Doesn't change with minor updates
- **Unique** - Different across machines
- **Non-transferable** - Can't be copied to another device

### Collision Detection

The backend tracks HWIDs across licenses to detect sharing:
- Same HWID on multiple license keys → Fraud alert
- Rapid HWID changes → Suspicious activity

---

## Tamper Detection

### Cross-Platform Storage

License data integrity is verified using platform-specific storage:

| Platform | Storage | Tamper Detection |
|----------|---------|------------------|
| Windows | Registry | Registry values |
| macOS | App Support | HMAC signature |
| Linux | XDG Config | HMAC signature |
| Android | Internal | HMAC signature |
| iOS | Documents | HMAC signature |
| WebGL | PlayerPrefs | ⚠️ None |

### How It Works

1. **On Save**: File hash stored in separate location
2. **On Load**: Current hash compared to stored hash
3. **Mismatch**: `TAMPERED` status returned
4. **Deletion**: Detected via existence marker

```csharp
// Automatic on load
string data = SecureStorage.Load("license_cache");
if (data == "TAMPERED")
{
    // Security violation detected
    TriggerEnforcement("terminate", "License tampering detected");
}
```

---

## Secure Clock

Prevents license bypass via system clock manipulation:

### Monotonic Watermark

```csharp
// SecureClock.cs
public static DateTime GetEffectiveTime()
{
    DateTime systemTime = DateTime.UtcNow;
    DateTime storedTime = GetLastKnownTime();
    
    if (systemTime < storedTime)
    {
        Debug.LogWarning("[LTLM] CLOCK TAMPERING DETECTED");
        return storedTime; // Use watermark
    }
    
    UpdateLastKnownTime(systemTime);
    return systemTime;
}
```

### Protection Against

- Rolling back system clock to extend trials
- Setting future date then reverting
- Time zone manipulation

---

## Secure Storage

License data is encrypted at rest:

### Encryption

```csharp
// Key derivation from HWID
byte[] key = SHA256(machineHWID);

// AES-256-CBC encryption
byte[] encrypted = AES256.Encrypt(plaintext, key, randomIV);

// Stored as: IV + CipherText
File.WriteAllBytes(path, encrypted);
```

### Storage Location

```
{persistentDataPath}/
└── ltlm_vault/
    ├── license_cache_{projectId}.ltlm
    ├── license_key_{projectId}.ltlm
    ├── pending_consumptions.ltlm
    └── secure_time.ltlm
```

---

## Trust Boundaries

### What the SDK Trusts

| Source | Trust Level | Verification |
|--------|-------------|--------------|
| Backend responses | After signature verification | Ed25519 |
| Local cache | After tamper check | MD5 + Registry/HMAC |
| System clock | After watermark check | Monotonic comparison |

### What the Backend Trusts

| Source | Trust Level | Verification |
|--------|-------------|--------------|
| SDK requests | After decryption + nonce | AES-256 + Nonce |
| HWID claims | Cross-validated | Collision detection |
| Token consumption | After sync | Balance reconciliation |

---

## Attack Scenarios & Mitigations

### Scenario 1: Traffic Interception

**Attack**: Capture and modify network traffic
**Mitigation**: 
- HTTPS encryption (Layer 1)
- AES-256 payload encryption (Layer 2)
- Ed25519 signature verification (Layer 3)

### Scenario 2: Replay Attack

**Attack**: Capture valid request, replay later
**Mitigation**: Per-request nonces with server validation

### Scenario 3: License Sharing

**Attack**: Copy license to another machine
**Mitigation**: 
- HWID binding
- Encrypted storage with machine-specific key
- Machine registration limit

### Scenario 4: Clock Rollback

**Attack**: Set system clock back to extend license
**Mitigation**: Monotonic watermark in SecureClock

### Scenario 5: File Tampering

**Attack**: Edit encrypted license file
**Mitigation**:
- AES-256 encryption (unreadable)
- MD5 hash in separate location
- HMAC signature verification

---

## Best Practices

1. **Never log sensitive data** - Keys, tokens, encrypted payloads
2. **Obfuscate in production** - Use IL2CPP or dotnet obfuscator
3. **Validate on server** - Don't trust client-only checks
4. **Handle tampering gracefully** - Log and alert, don't crash
5. **Keep SDK updated** - Security patches in new versions

---

## See Also

- [Tamper Detection](tamper-detection.md)
- [Best Practices](best-practices.md)
