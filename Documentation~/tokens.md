# Token System

Usage-based metering and token consumption.

---

## Overview

The LTLM token system enables usage-based licensing where users consume tokens for specific actions. Tokens can be used for:

- API call limits
- Export/render credits
- Feature usage metering
- Pay-per-use functionality

---

## Token Concepts

### Token Balance

Each license has a token allocation:

```csharp
var license = LTLMManager.Instance.ActiveLicense;

int limit = license.tokensLimit ?? 0;     // Total allocation
int consumed = license.tokensConsumed ?? 0; // Used tokens
int remaining = license.tokensRemaining ?? 0; // Available tokens
```

### Consumption Flow

```
┌─────────────────┐
│ User triggers   │
│ metered action  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Check balance   │
│ (local/server)  │
└────────┬────────┘
         │
    ┌────┴────┐
    │Has tokens?│
    └────┬────┘
    No   │   Yes
    │    │    │
    ▼    │    ▼
┌──────┐ │ ┌─────────────────┐
│Reject│ │ │ Execute action  │
└──────┘ │ └────────┬────────┘
         │          │
         │          ▼
         │ ┌─────────────────┐
         │ │ Consume tokens  │
         │ │ (optimistic)    │
         │ └────────┬────────┘
         │          │
         │          ▼
         │ ┌─────────────────┐
         │ │ Queue for sync  │
         └─│ (background)    │
           └─────────────────┘
```

---

## Basic Usage

### Check Token Balance

```csharp
// Get current balance
int balance = LTLMManager.Instance.GetTokenBalance();
Debug.Log($"Tokens available: {balance}");

// Async check with server refresh
LTLMManager.Instance.DoesHaveTokens(5, hasTokens => {
    if (hasTokens)
    {
        EnableExportButton();
    }
    else
    {
        ShowTopUpPrompt();
    }
});
```

### Consume Tokens

```csharp
LTLMManager.Instance.ConsumeTokens(
    amount: 1,
    action: "export_model",
    onConsumed: license => {
        Debug.Log($"Success! Remaining: {license.tokensRemaining}");
        ExecuteExport();
    },
    onError: error => {
        Debug.LogError($"Consumption failed: {error}");
        ShowError(error);
    }
);
```

### Check Before Consuming

```csharp
// Pattern: Check → Consume → Execute
public void OnExportClicked()
{
    const int EXPORT_COST = 5;
    
    if (LTLMManager.Instance.GetTokenBalance() >= EXPORT_COST)
    {
        LTLMManager.Instance.ConsumeTokens(EXPORT_COST, "export",
            license => StartExport(),
            error => ShowError(error)
        );
    }
    else
    {
        ShowInsufficientTokensDialog(EXPORT_COST);
    }
}
```

---

## Optimistic Consumption

The SDK uses optimistic updates for smooth UX:

1. **Local balance updated immediately**
2. **UI reflects new balance instantly**
3. **Request queued for server sync**
4. **Background sync handles the actual deduction**

```csharp
// This returns immediately with updated local balance
LTLMManager.Instance.ConsumeTokens(1, "action",
    license => {
        // license.tokensRemaining reflects optimistic update
        UpdateUI(license.tokensRemaining);
    },
    error => { }
);
// Server sync happens in background
```

---

## Offline Consumption

Tokens can be consumed while offline:

1. Consumptions are queued locally
2. Queue is encrypted and persisted
3. When online, batch sync sends all pending
4. Server reconciles with `allowNegative: true`

### Force Sync

```csharp
// Manually trigger sync of pending consumptions
LTLMManager.Instance.SyncPendingConsumptions();
```

### Check Pending Count

```csharp
int pending = LTLMManager.Instance.GetPendingConsumptionCount();
if (pending > 0)
{
    ShowSyncIndicator($"{pending} actions pending sync");
}
```

---

## Advanced Usage

### With Metadata

Attach custom data to consumption records:

```csharp
LTLMManager.Instance.ConsumeTokens(
    amount: 10,
    action: "render_high_quality",
    meta: new Dictionary<string, object> {
        { "resolution", "4K" },
        { "duration_seconds", 120 },
        { "scene_name", "MainLevel" }
    },
    onConsumed: license => { },
    onError: error => { }
);
```

### Batch Consumption

For multiple actions at once:

```csharp
// Queue multiple consumptions
LTLMManager.Instance.ConsumeTokens(1, "action_a");
LTLMManager.Instance.ConsumeTokens(2, "action_b");
LTLMManager.Instance.ConsumeTokens(1, "action_c");

// All will be batched in next sync
```

### Entitlement Check with Tokens

Combined feature + token check:

```csharp
// Check capability AND sufficient tokens
if (LTLMManager.Instance.IsEntitled("pro_export", requiredTokens: 5))
{
    EnableProExportFeature();
}
else
{
    // Either missing capability or insufficient tokens
    ShowUpgradeDialog();
}
```

---

## Token Top-Up

Allow users to purchase more tokens:

```csharp
// Get available top-up packs from policy
var packs = LTLMManager.Instance.GetTopUpOptions();

foreach (var pack in packs)
{
    Debug.Log($"{pack.displayName}: {pack.tokens} tokens for ${pack.price}");
}

// Create checkout session for a pack
LTLMManager.Instance.CreateTopUpSession(
    packId: "tokens_100",
    redirectUrl: "https://yourapp.com/success",
    checkoutUrl => Application.OpenURL(checkoutUrl),
    error => Debug.LogError(error)
);
```

---

## Usage History

View consumption history:

```csharp
LTLMManager.Instance.GetTokenHistory(
    history => {
        foreach (var record in history)
        {
            Debug.Log($"{record.timestamp}: {record.action} - {record.amount} tokens");
        }
    },
    error => Debug.LogError(error)
);
```

---

## Best Practices

### 1. Always Check Before Expensive Operations

```csharp
if (LTLMManager.Instance.GetTokenBalance() >= REQUIRED_TOKENS)
{
    // Proceed
}
```

### 2. Use Meaningful Action Names

```csharp
// Good - specific and trackable
ConsumeTokens(1, "export_fbx_model");
ConsumeTokens(5, "render_4k_video");

// Bad - vague
ConsumeTokens(1, "action");
ConsumeTokens(5, "use");
```

### 3. Handle Insufficient Balance

```csharp
if (balance < required)
{
    int shortage = required - balance;
    ShowDialog($"Need {shortage} more tokens. Top up?");
}
```

### 4. Show Real-Time Balance

```csharp
void UpdateTokenDisplay()
{
    int balance = LTLMManager.Instance.GetTokenBalance();
    tokenText.text = $"🪙 {balance}";
}

void Start()
{
    LTLMManager.OnTokensConsumed += _ => UpdateTokenDisplay();
}
```

---

## Next Steps

- [In-App Purchases](api/purchases.md) - Token top-up integration
- [License Lifecycle](license-lifecycle.md) - Understanding license states
