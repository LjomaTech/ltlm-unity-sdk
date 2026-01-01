# LTLM System - AI-Ready Quick Reference

> **Purpose:** Rapid onboarding document for AI assistants and developers to understand and use the LTLM SDK system correctly.
> **Version:** 2.2 | **Last Updated:** December 2024

---

## System Overview

LTLM (License & Token Lifecycle Manager) is a **SaaS license management platform** for software vendors. It provides:

- **License validation** (perpetual, subscription, trial, time-limited)
- **Concurrent seat management** (multiple devices, same license)
- **Token/credit consumption** (usage-based billing)
- **Offline grace periods** (work without internet temporarily)
- **Geofencing & versioning** enforcement
- **Air-gapped licenses** (fully offline operation)

---

## Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Unity SDK     │────>│  Backend API    │────>│   Database      │
│  (LTLMManager)  │<────│  (Node.js)      │<────│  (MySQL/Redis)  │
└─────────────────┘     └─────────────────┘     └─────────────────┘
        │                       │
        │                       ├── Standard REST API (/v1/sdk/*)
        │                       └── PRO SDK Encrypted (/v1/sdk/pro/*)
        │
        └── Air-Gapped: .ltlm file (no server needed)
```

---

## SDK Types

| SDK | Endpoint Base | When to Use |
|-----|---------------|-------------|
| **Standard REST** | `/v1/sdk/*` | Web apps, server-to-server |
| **PRO SDK (Encrypted)** | `/v1/sdk/pro/*` | Desktop/mobile apps (AES-256 + nonce) |
| **Air-Gapped** | No network | High-security/isolated environments |

> **CRITICAL:** If policy has `sdkEnforcement.mode = "sdk_only"`, Standard REST returns 403. MUST use PRO SDK.

---

## Unity SDK Quick Start

### 1. Setup
```csharp
// Access singleton anywhere
LTLMManager.Instance.ActivateLicense("XXXX-XXXX-XXXX", 
    (license, status) => { /* success */ },
    (error) => { /* failure */ }
);
```

### 2. Check Authentication
```csharp
if (LTLMManager.Instance.IsAuthenticated) {
    // User has valid license
}
```

### 3. Get License Data
```csharp
LicenseData license = LTLMManager.Instance.ActiveLicense;
// license.status, license.tokensRemaining, license.validUntil, etc.
```

---

## License Status Values

| Status | Meaning | SDK Action |
|--------|---------|------------|
| `active` | Valid, seat occupied | ✅ Full access |
| `VALID_NO_SEAT` | Valid, no seat available | Show seat management UI |
| `KICKED` | Seat released by another device | Stop heartbeat, show reactivation |
| `grace_period` | Expired but in grace | ⚠️ Allow access, show warning |
| `expired` | License expired | ❌ Block, show renewal |
| `suspended` | Manually suspended | ❌ Block, contact support |
| `revoked` | Permanently revoked | ❌ Block |
| `connection_required` | Offline grace exceeded | ❌ Block until online |

---

## Enforcement Settings (Server → SDK)

Every response includes these server-driven settings. **DO NOT HARDCODE, USE THESE:**

| Field | Description |
|-------|-------------|
| `heartbeatIntervalSeconds` | How often to send heartbeat |
| `seatsEnabled` | Is concurrent seat management active? |
| `maxConcurrentSeats` | Max simultaneous sessions |
| `tokensEnabled` | Is token consumption active? |
| `tokensRemaining` | Available tokens |
| `offlineEnabled` | Is offline grace allowed? |
| `offlineGraceHours` | Hours allowed offline (0 = disabled) |

---

## Events (Unity SDK)

| Event | Parameters | When Fired |
|-------|------------|------------|
| `OnValidationStarted` | - | Validation request begins |
| `OnValidationCompleted` | `bool success, LicenseStatus status` | Validation finishes |
| `OnLicenseStatusChanged` | `LicenseStatus status` | Any license state change |
| `OnTokensConsumed` | `LicenseData license` | After token consumption |
| `OnSeatStatusChanged` | `string seatStatus, int active, int max` | Seat allocation changes |
| `OnKicked` | `KickedNotice notice` | Seat released by other device |

### Event Usage
```csharp
void OnEnable() {
    LTLMManager.OnLicenseStatusChanged += HandleStatus;
    LTLMManager.OnKicked += HandleKicked;
}

void HandleStatus(LicenseStatus status) {
    // Update UI based on status
}

void HandleKicked(KickedNotice notice) {
    // Stop heartbeat, show "kicked by {notice.kickedByNickname}"
}
```

---

## Key Methods

### License Operations
```csharp
// Activate new license
LTLMManager.Instance.ActivateLicense(key, onSuccess, onError);

// Validate existing license
LTLMManager.Instance.ValidateLicense(key, onSuccess, onError);

// Load stored license on startup
LTLMManager.Instance.TryLoadStoredLicense(onSuccess, onError);

// Sign out (release seat)
LTLMManager.Instance.DeactivateSeat();

// Clear stored license
LTLMManager.Instance.ClearLicenseCache();
```

### Seat Management
```csharp
// Check if waiting for seat
if (LTLMManager.Instance.IsWaitingForSeat) { }

// Get active seats
LTLMManager.Instance.GetActiveSeats(onSuccess, onError);

// Release another device's seat
LTLMManager.Instance.ReleaseSeat(targetHwid, claimSeat: true, onSuccess, onError);
```

### Token Operations
```csharp
// Get balance
int balance = LTLMManager.Instance.GetTokenBalance();

// Consume tokens
LTLMManager.Instance.ConsumeTokens(amount, meta, onSuccess, onError);

// Check capability + token requirement
if (LTLMManager.Instance.IsEntitled("pro_feature", requiredTokens: 5)) { }
```

### User Settings (Cloud Sync)
```csharp
// Get settings from server
LTLMManager.Instance.GetUserSettings(
    settings => Debug.Log($"Theme: {settings["theme"]}"),
    error => Debug.LogError(error)
);

// Save settings to server (synced across devices)
var settings = new Dictionary<string, object> {
    { "theme", "dark" },
    { "language", "en" }
};
LTLMManager.Instance.SaveUserSettings(settings, 
    () => Debug.Log("Saved!"),
    error => Debug.LogError(error)
);

// Get from local cache (no network)
var cached = LTLMManager.Instance.GetLocalUserSettings();
```

---

## API Endpoints Quick Reference

### Standard REST API (`/v1/sdk/`)

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/license/{key}` | Get status |
| POST | `/license/{key}/activate` | Activate/register device |
| POST | `/license/{key}/heartbeat` | Keep session alive |
| POST | `/license/{key}/signout` | Release seat |
| GET | `/license/{key}/seats` | List active seats |
| POST | `/license/{key}/release-seat` | Kick other device |
| POST | `/license/{key}/consume` | Consume tokens |
| POST | `/license/{key}/consume-batch` | Sync offline usage |
| GET | `/license/{key}/settings` | Get user settings |
| PUT | `/license/{key}/settings` | Save user settings |

### PRO SDK (Encrypted) (`/v1/sdk/pro/`)

Same endpoints, but:
- All payloads encrypted with AES-256
- Nonce-based replay protection
- Required when `sdk_only` mode enabled
- Settings: POST `/pro/license/settings/get` and `/pro/license/settings/save`

---

## Error Codes (403)

| Code | Meaning | Solution |
|------|---------|----------|
| `PRO_SDK_REQUIRED` | Policy requires PRO SDK | Use `/v1/sdk/pro/*` |
| `OFFLINE_ACTIVATION_REQUIRED` | Air-gapped license | Download .ltlm file |
| `VERSION_NOT_SUPPORTED` | Version blocked | Update app |
| `REGION_BLOCKED` | Geofencing active | N/A |
| `SEAT_LIMIT_REACHED` | No seats | Wait or release another |
| `SEAT_RELEASE_COOLDOWN` | Too fast | Wait X seconds |
| `DEVICE_LIMIT_REACHED` | Too many HWIDs | Remove device |
| `INSUFFICIENT_TOKENS` | Not enough tokens | Buy top-up |

---

## Typical Integration Flow

```
1. App Start
   └─> TryLoadStoredLicense()
       ├─> Success: Check status, start heartbeat
       └─> Failure: Show activation screen

2. User Enters License Key
   └─> ActivateLicense(key)
       ├─> Active: Save key, start heartbeat, show app
       ├─> VALID_NO_SEAT: Show seat management UI
       └─> Error: Show message

3. During App Use
   └─> Heartbeat every {heartbeatIntervalSeconds}
       ├─> Check status changes
       ├─> Check for kickedNotice
       └─> Update UI if needed

4. Token Usage
   └─> ConsumeTokens(amount)
       ├─> Success: Update local balance
       └─> Failure: Handle insufficient tokens

5. App Exit
   └─> DeactivateSeat() or heartbeat(isClosing: true)
```

---

## Common Patterns

### Pattern 1: Status Handler
```csharp
switch (LTLMManager.Instance.GetLicenseStatus()) {
    case LicenseStatus.Active: EnableAllFeatures(); break;
    case LicenseStatus.ValidNoSeat: ShowSeatManagement(); break;
    case LicenseStatus.Kicked: ShowReactivateButton(); break;
    case LicenseStatus.GracePeriod: ShowRenewalWarning(); break;
    case LicenseStatus.Expired: ShowRenewalRequired(); break;
    default: ShowActivationScreen(); break;
}
```

### Pattern 2: Heartbeat Loop
```csharp
IEnumerator HeartbeatLoop() {
    while (isAuthenticated) {
        var response = await SendHeartbeat();
        if (response.status == "KICKED") {
            HandleKicked();
            yield break;
        }
        yield return new WaitForSeconds(response.heartbeatIntervalSeconds);
    }
}
```

### Pattern 3: Seat Release Flow
```csharp
async void OnReleaseSeatClicked(string targetHwid) {
    var result = await ReleaseSeat(targetHwid, claimSeat: true);
    if (result.seatClaimed) {
        // We now have the seat
        StartHeartbeat();
    }
}
```

---

## File Locations

### Backend
- API Routes: `backend/src/api/v1/sdk.js`
- Service Logic: `backend/src/services/sdk.js`
- Settings Resolution: `backend/src/services/settingsResolver.js`

### Unity SDK
- Manager: `unitysdk/Assets/LTLM/Scripts/Unity/LTLMManager.cs`
- Models: `unitysdk/Assets/LTLM/Scripts/Core/Models/LTLMModels.cs`
- Client: `unitysdk/Assets/LTLM/Scripts/Core/LTLMClient.cs`

### Documentation
- Backend SDK Docs: `docs/sdk/`
- Unity SDK Docs: `unitysdk/docs/`
- OpenAPI Spec: `docs/externalDocs/client-api.openapi.yaml`

---

## Quick Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| 403 PRO_SDK_REQUIRED | Policy has `sdk_only` mode | Use PRO SDK endpoints |
| 403 OFFLINE_ACTIVATION_REQUIRED | Air-gapped license | Download .ltlm from portal |
| Status always VALID_NO_SEAT | All seats occupied | Release another seat or wait |
| Tokens not updating | `tokensEnabled: false` | Enable tokens in policy |
| Heartbeat rejected | Invalid HWID or nonce | Re-activate license |
| KICKED status | Another device released you | Call ReactivateSeat() |
