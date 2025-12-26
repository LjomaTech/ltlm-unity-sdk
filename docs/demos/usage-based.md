# Usage-Based Demo

Token consumption for metered features.

---

## Overview

This demo shows how to:
- Display token balance
- Consume tokens for actions
- Load custom token costs from settings override
- Handle insufficient tokens
- Integrate top-up purchases

---

## Files

- **Script**: `Assets/LTLM/Demos/Scripts/UsageBasedDemo.cs`

---

## UI Setup

Create these UI elements:

- **Status Text**: Displays current state
- **Token Balance Text**: Shows credits remaining
- **Export Buttons**: Low, High, 4K quality options
- **Top-Up Button**: Opens purchase flow

---

## How It Works

### 1. Subscribe to Events

```csharp
void OnEnable()
{
    LTLMManager.OnTokensConsumed += OnTokensUpdated;
    LTLMManager.OnValidationCompleted += OnLicenseLoaded;
}
```

### 2. Load Settings Override

Custom costs can be set per-policy or per-license in the dashboard:

```csharp
void LoadSettingsOverride()
{
    var license = LTLMManager.Instance.ActiveLicense;
    if (license?.config == null) return;

    // Load custom export costs from license config
    if (license.config.ContainsKey("exportCosts"))
    {
        var costs = license.config["exportCosts"] as Dictionary<string, object>;
        if (costs != null)
        {
            if (costs.ContainsKey("low"))
                lowResCost = Convert.ToInt32(costs["low"]);
            if (costs.ContainsKey("high"))
                highResCost = Convert.ToInt32(costs["high"]);
            if (costs.ContainsKey("4k"))
                cost4K = Convert.ToInt32(costs["4k"]);
        }
    }
}
```

### 3. Display Token Balance

```csharp
void UpdateTokenDisplay(LicenseData license)
{
    int remaining = license.tokensRemaining ?? 0;
    int limit = license.tokensLimit ?? 0;

    tokenBalanceText.text = $"Credits: {remaining} / {limit}";

    // Color code based on balance
    if (remaining <= 5)
        tokenBalanceText.color = Color.red;
    else if (remaining <= 20)
        tokenBalanceText.color = Color.yellow;
    else
        tokenBalanceText.color = Color.white;
}
```

### 4. Consume Tokens

```csharp
void ConsumeAndExport(string quality, int cost)
{
    int balance = LTLMManager.Instance.GetTokenBalance();

    if (balance < cost)
    {
        statusText.text = $"Need {cost - balance} more credits";
        return;
    }

    statusText.text = $"Exporting {quality}...";

    LTLMManager.Instance.ConsumeTokens(cost, $"export_{quality}",
        license => {
            statusText.text = $"Export complete!";
            PerformExport(quality);
        },
        error => {
            statusText.text = "Export failed: " + error;
        }
    );
}
```

### 5. Update UI on Consumption

```csharp
void OnTokensUpdated(LicenseData license)
{
    UpdateTokenDisplay(license);
    UpdateButtonStates(license);
}

void UpdateButtonStates(LicenseData license)
{
    int balance = license?.tokensRemaining ?? 0;

    exportLowButton.interactable = balance >= lowResCost;
    exportHighButton.interactable = balance >= highResCost;
    export4KButton.interactable = balance >= cost4K;
}
```

### 6. Top-Up Integration

```csharp
void OnTopUpClicked()
{
    LTLMManager.Instance.CreateTopUpSession(
        "credits_100",
        "https://yourapp.com/topup-success",
        url => Application.OpenURL(url),
        error => statusText.text = "Error: " + error
    );
}
```

---

## Dashboard Configuration

In your policy config, you can set custom token costs:

```json
{
  "config": {
    "exportCosts": {
      "low": 1,
      "high": 5,
      "4k": 10
    }
  }
}
```

These values will be loaded automatically when the license validates.

---

## Testing

1. Activate with a license that has tokens
2. Click export buttons to consume tokens
3. Observe balance updates
4. Try to export when low on tokens
5. Test the top-up flow
