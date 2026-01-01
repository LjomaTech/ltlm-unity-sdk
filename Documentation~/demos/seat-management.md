# Seat Management Demo

Handling concurrent usage limits with seat management.

---

## Overview

When your license has concurrent seat limits (e.g., 2 simultaneous users), the SDK returns `VALID_NO_SEAT` when all seats are occupied. This demo shows how to:

- Detect the `ValidNoSeat` status
- Display active seats to the user
- Allow releasing another device's seat
- Handle being kicked by another device

---

## Status Flow

```
┌─────────────────┐
│   Activation    │
└────────┬────────┘
         │
    ┌────▼────┐
    │ Active? │
    └────┬────┘
         │
    ┌────┴────────────────┐
    │                     │
 ┌──▼──┐           ┌──────▼──────┐
 │ YES │           │ VALID_NO_SEAT│
 └──┬──┘           └──────┬──────┘
    │                     │
┌───▼────┐        ┌───────▼───────┐
│Use App │        │Show Seat List │
└────────┘        │ + Release UI  │
                  └───────────────┘
```

---

## Files

- **Script**: `Assets/LTLM/Demos/Scripts/SeatManagementDemo.cs`

---

## Events Used

```csharp
// Subscribe in OnEnable
LTLMManager.OnValidationCompleted += OnValidationCompleted;
LTLMManager.OnSeatStatusChanged += OnSeatStatusChanged;
LTLMManager.OnKicked += OnKicked;
```

### OnValidationCompleted

```csharp
void OnValidationCompleted(bool success, LicenseStatus status)
{
    switch (status)
    {
        case LicenseStatus.Active:
            ShowMainApp();
            break;
            
        case LicenseStatus.ValidNoSeat:
            ShowWaitingUI();
            LoadActiveSeats();
            break;
    }
}
```

### OnSeatStatusChanged

```csharp
void OnSeatStatusChanged(string seatStatus, int activeSeats, int maxSeats)
{
    // Update UI
    seatCountText.text = $"Seats: {activeSeats}/{maxSeats}";
    
    // Check if we lost our seat during a heartbeat
    if (seatStatus == "NO_SEAT")
    {
        ShowWaitingUI();
    }
}
```

### OnKicked

```csharp
void OnKicked(KickedNotice notice)
{
    string nickname = notice.kickedByNickname ?? "Another device";
    ShowToast($"Session ended by '{nickname}'");
    ShowWaitingUI();
}
```

---

## Loading Active Seats

```csharp
LTLMManager.Instance.GetActiveSeats(
    response => {
        foreach (var seat in response.seats)
        {
            if (seat.isCurrentDevice) continue;
            
            // Display: seat.nickname, seat.lastSeen
            // Add release button if response.canReleaseSeat
        }
    },
    error => Debug.LogError(error)
);
```

---

## Releasing a Seat

```csharp
LTLMManager.Instance.ReleaseSeat(
    targetHwid: "abc123...",
    claimSeat: true,
    onSuccess: response => {
        if (response.seatClaimed)
        {
            // We now have a seat!
            ShowMainApp();
        }
    },
    onError: error => {
        if (error.Contains("wait"))
        {
            // Cooldown active, show timer
        }
    }
);
```

---

## UI Structure

### Active Panel
- Shows when user has a seat
- Displays seat count (e.g., "1/2 seats")
- Sign out button

### Waiting Panel
- Shows when `VALID_NO_SEAT`
- List of other devices with release buttons
- Cooldown timer if rate limited

### Kicked Notice Panel
- Modal popup when kicked
- Shows who kicked this device
- OK button to dismiss

---

## Policy Configuration

Enable seat management in your policy:

```json
{
  "limits": {
    "seats": {
      "enabled": true,
      "maxSeats": 2
    }
  },
  "customerActions": {
    "allowRemoteSeatRelease": true,
    "seatReleaseCooldownSeconds": 300
  }
}
```

---

## Testing

1. Create a policy with `maxSeats: 2`
2. Activate on Device A - gets a seat
3. Activate on Device B - gets a seat  
4. Activate on Device C - gets `VALID_NO_SEAT`
5. On Device C, release Device A's seat
6. Device A's next heartbeat shows `kickedNotice`
7. Device C now has an active seat
