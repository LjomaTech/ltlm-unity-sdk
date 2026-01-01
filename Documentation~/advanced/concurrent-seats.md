# Concurrent Seats

Multi-user and floating license management.

---

## Overview

Concurrent seat licensing allows a single license to be used on multiple devices simultaneously, up to a configured limit.

**Use Cases:**
- Team licenses (5 seats for a studio)
- Floating licenses (10 users, 5 can work at once)
- Enterprise deployments

---

## How It Works

```
┌──────────────────────────────────────────────────────────────┐
│                      License: TEAM-001                        │
│                    Max Concurrent Seats: 5                    │
├──────────────────────────────────────────────────────────────┤
│                                                                │
│   ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐        │
│   │ User A  │  │ User B  │  │ User C  │  │ User D  │        │
│   │  Seat 1 │  │  Seat 2 │  │  Seat 3 │  │ WAITING │        │
│   │ Active  │  │ Active  │  │ Active  │  │  ↓↓↓↓   │        │
│   └─────────┘  └─────────┘  └─────────┘  └─────────┘        │
│                                                                │
│   When User A quits → Seat released → User D can join        │
│                                                                │
└──────────────────────────────────────────────────────────────┘
```

### Seat Lifecycle

1. **Activation**: Device attempts to claim a seat
2. **Heartbeat**: Periodic heartbeat maintains seat
3. **Timeout**: Stale seats auto-released (5-10 min)
4. **Release**: Explicit release on app quit

---

## Configuration

### Policy Setting

In dashboard, set concurrent seats:

```json
{
  "limits": {
    "seats": {
      "enabled": true,
      "maxSeats": 5,
      "mode": "concurrent"
    }
  }
}
```

### SDK Configuration

```csharp
// Heartbeat keeps seat alive
LTLMManager.Instance.heartbeatIntervalSeconds = 300; // 5 minutes
```

---

## Implementation

### Activation

```csharp
LTLMManager.Instance.ActivateLicense("TEAM-LICENSE-KEY",
    (license, status) => {
        if (status == LicenseStatus.Active)
        {
            Debug.Log($"Seat claimed! {license.activeSeats}/{license.maxConcurrentSeats}");
        }
    },
    error => {
        if (error.Contains("seat limit"))
        {
            ShowSeatLimitDialog();
        }
    }
);
```

### Checking Seat Status

```csharp
var license = LTLMManager.Instance.ActiveLicense;

int activeSeats = license.activeSeats ?? 0;
int maxSeats = license.maxConcurrentSeats ?? 1;

seatIndicator.text = $"🪑 {activeSeats}/{maxSeats} seats in use";
```

### Releasing Seat

```csharp
// On sign out
public void OnSignOutClicked()
{
    LTLMManager.Instance.DeactivateSeat();
    LTLMManager.Instance.ClearLicenseCache();
    ShowLoginScreen();
}

// Automatic on app quit (via OnApplicationQuit)
```

---

## Handling Seat Limits

### ValidNoSeat Status

When all seats are occupied, the SDK returns `ValidNoSeat` status instead of `Active`:

```csharp
LTLMManager.Instance.ActivateLicense("TEAM-LICENSE-KEY",
    (license, status) => {
        if (status == LicenseStatus.ValidNoSeat)
        {
            // License is valid but no seat available
            Debug.Log($"All {license.maxConcurrentSeats} seats occupied");
            ShowSeatManagementUI();
        }
    },
    error => Debug.LogError(error)
);
```

### Remote Seat Release (Kick Other Devices)

Users can release another device's seat to claim it for themselves:

```csharp
// 1. Get list of active seats
LTLMManager.Instance.GetActiveSeats(
    response => {
        foreach (var seat in response.seats)
        {
            if (!seat.isCurrentDevice)
            {
                // Show this device with a "Disconnect" button
                AddSeatRow(seat.nickname ?? seat.hwid, seat.lastSeen, () => {
                    ReleaseSeatAndClaim(seat.hwid);
                });
            }
        }
    },
    error => Debug.LogError(error)
);

// 2. Release a seat and claim it
void ReleaseSeatAndClaim(string targetHwid)
{
    LTLMManager.Instance.ReleaseSeat(targetHwid, claimSeat: true,
        response => {
            if (response.seatClaimed)
            {
                Debug.Log("Seat claimed! Starting heartbeat...");
                StartHeartbeat();
            }
        },
        error => {
            if (error.Contains("cooldown"))
            {
                ShowMessage("Please wait before releasing another seat.");
            }
        }
    );
}
```

> [!NOTE]
> **Cooldown:** If `seatReleaseCooldownSeconds` is configured, users must wait between releases.

---

## Handling KICKED Status

When another device releases your seat, you receive the `KICKED` status:

### Using OnKicked Event (Recommended)

```csharp
void OnEnable()
{
    LTLMManager.OnKicked += HandleKicked;
}

void HandleKicked(KickedNotice notice)
{
    // Stop heartbeat immediately
    StopHeartbeat();
    
    // Show who kicked you
    string message = notice.kickedByNickname != null
        ? $"Your session was ended by '{notice.kickedByNickname}'"
        : "Your session was ended by another device";
    
    ShowDialog(
        title: "Session Ended",
        message: message,
        actions: new[] {
            ("Try Again", () => ReactivateLicense()),
            ("OK", () => ShowLoginScreen())
        }
    );
}

void OnDisable()
{
    LTLMManager.OnKicked -= HandleKicked;
}
```

### Checking Status in Heartbeat

The `KICKED` status is also returned in heartbeat responses:

```csharp
IEnumerator HeartbeatLoop()
{
    while (isActive)
    {
        var response = await SendHeartbeat();
        
        if (response.status == "KICKED" || response.kickedNotice != null)
        {
            HandleKicked(response.kickedNotice);
            yield break; // Stop loop
        }
        
        yield return new WaitForSeconds(response.heartbeatIntervalSeconds);
    }
}
```

### Waiting for Available Seat

```csharp
IEnumerator WaitForSeat()
{
    int attempts = 0;
    
    while (attempts < 10)
    {
        attempts++;
        statusText.text = $"Waiting for available seat... ({attempts})";
        
        bool success = false;
        
        LTLMManager.Instance.ActivateLicense(key,
            (license, status) => success = true,
            error => { }
        );
        
        yield return new WaitForSeconds(30);
        
        if (success) break;
    }
}
```

---

## Seat Timeout

Inactive seats are automatically released:

| Scenario | Timeout |
|----------|---------|
| App crash | Heartbeat timeout (5-10 min) |
| Network loss | Heartbeat timeout |
| Device sleep | Heartbeat stops, timeout |
| App quit | Immediate (if release succeeds) |

### Server-Side Cleanup

```
Every heartbeat:
  1. Update lastCheckIn for this HWID
  2. Remove seats where lastCheckIn > timeout
  3. Return current seat count
```

---

## UI Patterns

### Seat Indicator

```csharp
void UpdateSeatIndicator()
{
    var license = LTLMManager.Instance.ActiveLicense;
    
    if (license?.maxConcurrentSeats > 1)
    {
        seatIndicator.gameObject.SetActive(true);
        seatIndicator.text = $"{license.activeSeats}/{license.maxConcurrentSeats}";
        
        float ratio = (float)license.activeSeats / license.maxConcurrentSeats;
        seatIndicator.color = ratio > 0.8f ? Color.yellow : Color.green;
    }
    else
    {
        seatIndicator.gameObject.SetActive(false);
    }
}
```

### Active Users List

```csharp
void ShowActiveUsers()
{
    var license = LTLMManager.Instance.ActiveLicense;
    
    foreach (var machine in license.machines)
    {
        AddUserRow(
            name: machine.nickname ?? machine.hwid.Substring(0, 8),
            lastSeen: machine.lastCheckIn,
            isMe: machine.hwid == DeviceID.GetHWID()
        );
    }
}
```

---

## Admin Features

### Kick User (Dashboard)

Admins can force-release a seat from the dashboard.

### View Active Sessions

```csharp
// machines array shows all registered devices
foreach (var m in license.machines)
{
    Debug.Log($"HWID: {m.hwid}");
    Debug.Log($"Nickname: {m.nickname}");
    Debug.Log($"Last Check-in: {m.lastCheckIn}");
}
```

---

## Best Practices

1. **Set appropriate timeout** - Balance between ghosted seats and frequent re-auth
2. **Show seat status** - Users should know capacity
3. **Provide release UI** - Let users explicitly sign out
4. **Handle errors gracefully** - Don't crash on seat limit
5. **Use nicknames** - Help users identify their devices

---

## See Also

- [License Lifecycle](../license-lifecycle.md)
- [LTLMManager API](../api/ltlm-manager.md)
