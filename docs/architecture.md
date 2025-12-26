# Architecture Overview

Understanding how the LTLM Unity SDK works internally.

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Your Unity Game                          │
├─────────────────────────────────────────────────────────────────┤
│                         LTLMManager                              │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐ │
│   │ Activation  │  │ Validation  │  │ Token Consumption       │ │
│   │ Flow        │  │ Flow        │  │ Flow                    │ │
│   └──────┬──────┘  └──────┬──────┘  └───────────┬─────────────┘ │
│          │                │                      │               │
├──────────┼────────────────┼──────────────────────┼───────────────┤
│          └────────────────┼──────────────────────┘               │
│                           ▼                                      │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │                      LTLMClient                          │   │
│   │   ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │   │
│   │   │ Encryption  │  │ Signing     │  │ Nonce Mgmt      │ │   │
│   │   │ (AES-256)   │  │ (Ed25519)   │  │ (Replay Prev)   │ │   │
│   │   └──────┬──────┘  └──────┬──────┘  └────────┬────────┘ │   │
│   └──────────┼────────────────┼──────────────────┼───────────┘   │
│              └────────────────┼──────────────────┘               │
│                               ▼                                  │
│   ┌────────────────────────────────────────────────────────────┐│
│   │                    CryptoProvider                           ││
│   │          (AES-256-CBC/GCM + Ed25519 Verification)           ││
│   └────────────────────────────────────────────────────────────┘│
├─────────────────────────────────────────────────────────────────┤
│                        HTTPS (TLS 1.2+)                          │
├─────────────────────────────────────────────────────────────────┤
│                       LTLM Backend API                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Core Components

### LTLMManager

The main entry point and Unity singleton.

**Responsibilities:**
- License activation and validation
- Heartbeat management
- Token consumption
- Enforcement callbacks
- Lifecycle handling (pause, quit)

```csharp
// Access anywhere via singleton
LTLMManager.Instance.ActivateLicense(...);
```

### LTLMClient

Handles encrypted communication with the backend.

**Responsibilities:**
- Triple-Wrap encryption/decryption
- Request signing and signature verification
- Nonce management for replay prevention
- HTTP request execution

### CryptoProvider

Low-level cryptographic operations.

**Algorithms:**
- **AES-256-CBC**: Request/response encryption
- **AES-256-GCM**: Alternative authenticated encryption
- **Ed25519**: Signature verification

### SecureStorage

Encrypted persistent storage.

**Features:**
- HWID-derived encryption keys
- Cross-platform tamper detection
- Encrypted file storage in `persistentDataPath`

### PlatformStorage

Cross-platform marker storage for tamper detection.

**Implementations:**
| Platform | Storage |
|----------|---------|
| Windows | Registry |
| macOS/Linux | File + HMAC |
| Android/iOS | File + HMAC |
| WebGL | PlayerPrefs |

### SecureClock

Monotonic time verification.

**Features:**
- Prevents clock rollback attacks
- Stores last known secure time
- Detects tampering

---

## Communication Protocol

### Triple-Wrap Protocol

All SDK-to-backend communication uses a three-layer protocol:

```
┌─────────────────────────────────────────────────┐
│ Layer 1: HTTPS (TLS 1.2+)                       │
├─────────────────────────────────────────────────┤
│ Layer 2: AES-256 Encryption                     │
│   - IV:CipherText (CBC) or IV:AuthTag:Cipher    │
├─────────────────────────────────────────────────┤
│ Layer 3: Ed25519 Signature                      │
│   - Server signs all responses                  │
│   - SDK verifies before trusting                │
└─────────────────────────────────────────────────┘
```

### Request Flow

1. **Prepare Payload**
   ```json
   {
     "data": { "key": "LICENSE-KEY", "hwid": "..." },
     "nonce": "previous-server-nonce",
     "hwid": "device-hardware-id"
   }
   ```

2. **Encrypt**
   - Generate random IV
   - AES-256-CBC encrypt with secret key
   - Output: `IV:CipherText`

3. **Send**
   - POST to endpoint
   - Include `x-project-secret` header

4. **Receive & Decrypt**
   - Decrypt response body
   - Parse signed envelope

5. **Verify Signature**
   - Extract signature from response
   - Verify against public key
   - Compare using stable JSON serialization

6. **Update Nonce**
   - Store new server nonce for next request

---

## Data Flow

### Activation Flow

```
User enters license key
        │
        ▼
┌───────────────────┐
│ LTLMManager       │
│ .ActivateLicense()│
└─────────┬─────────┘
          │
          ▼
┌───────────────────┐
│ Build Request     │
│ - key, hwid, meta │
└─────────┬─────────┘
          │
          ▼
┌───────────────────┐
│ LTLMClient        │
│ .PostEncrypted()  │
└─────────┬─────────┘
          │
          ▼
┌───────────────────┐
│ CryptoProvider    │
│ .Encrypt()        │
└─────────┬─────────┘
          │
          ▼
   HTTPS POST
   /v1/sdk/pro/license/validate
          │
          ▼
┌───────────────────┐
│ Backend processes │
│ - Verify license  │
│ - Check HWID      │
│ - Record machine  │
└─────────┬─────────┘
          │
          ▼
┌───────────────────┐
│ CryptoProvider    │
│ .Decrypt()        │
│ .VerifySignature()│
└─────────┬─────────┘
          │
          ▼
┌───────────────────┐
│ LTLMManager       │
│ - Cache license   │
│ - Start heartbeat │
│ - Invoke callback │
└───────────────────┘
```

---

## State Management

### License Cache

License data is cached locally for offline support:

```
persistentDataPath/
└── ltlm_vault/
    ├── license_cache_{projectId}.ltlm  # Encrypted license data
    ├── license_key_{projectId}.ltlm    # Encrypted license key
    ├── pending_consumptions.ltlm       # Queued token usage
    └── secure_time.ltlm                # Monotonic clock state
```

### Token Queue

Offline token consumption is queued and synced:

```csharp
// Local optimistic update
localBalance -= amount;
pendingQueue.Add(usage);

// Background sync when online
POST /v1/sdk/pro/license/consume-batch
```

---

## Thread Safety

The SDK operates primarily on Unity's main thread:

- All coroutines run on main thread
- Callbacks dispatched to main thread
- No explicit locking needed

Background operations:
- Network I/O is async via `UnityWebRequest`
- File I/O is synchronous but fast

---

## Next Steps

- [License Lifecycle](license-lifecycle.md) - Detailed flow documentation
- [Security Architecture](security/overview.md) - In-depth security details
