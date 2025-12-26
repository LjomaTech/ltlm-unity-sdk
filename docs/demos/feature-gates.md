# Feature Entitlements Demo

Lock and unlock features based on license capabilities.

---

## Overview

This demo shows how to:
- Check for capabilities (pro, enterprise, etc.)
- Show/hide feature panels based on tier
- Combined capability + token checks
- Load feature settings from config override

---

## Files

- **Script**: `Assets/LTLM/Demos/Scripts/FeatureEntitlementDemo.cs`

---

## UI Setup

Create these panels:

- **Basic Panel**: Always visible for licensed users
- **Pro Panel**: Visible only for users with "pro" capability
- **Enterprise Panel**: Visible only for users with "enterprise" capability
- **Upgrade Button**: Opens upgrade flow

---

## How It Works

### 1. Subscribe to Events

```csharp
void OnEnable()
{
    LTLMManager.OnValidationCompleted += OnLicenseLoaded;
    LTLMManager.OnLicenseStatusChanged += OnStatusChanged;
}

void OnLicenseLoaded(bool success, LicenseStatus status)
{
    if (success)
    {
        LoadSettingsOverride();
        ConfigureFeatures();
    }
}
```

### 2. Configure Features by Capability

```csharp
void ConfigureFeatures()
{
    // Basic - available to all licensed users
    basicPanel.SetActive(LTLMManager.Instance.IsAuthenticated);

    // Pro - requires "pro" capability
    bool hasPro = LTLMManager.Instance.HasCapability("pro");
    proPanel.SetActive(hasPro);

    // Enterprise - requires "enterprise" capability
    bool hasEnterprise = LTLMManager.Instance.HasCapability("enterprise");
    enterprisePanel.SetActive(hasEnterprise);

    // Show upgrade button if not at top tier
    upgradeButton.gameObject.SetActive(!hasEnterprise);
}
```

### 3. Determine Current Tier

```csharp
string GetCurrentTier()
{
    if (LTLMManager.Instance.HasCapability("enterprise"))
        return "Enterprise";
    if (LTLMManager.Instance.HasCapability("pro"))
        return "Pro";
    if (LTLMManager.Instance.IsAuthenticated)
        return "Basic";
    return "None";
}
```

### 4. Combined Capability + Token Check

```csharp
void UseFeature(string feature, int cost)
{
    // Check capability first
    if (feature == "pro" && !LTLMManager.Instance.HasCapability("pro"))
    {
        statusText.text = "Upgrade to Pro to use this feature";
        return;
    }

    // Check tokens
    if (LTLMManager.Instance.GetTokenBalance() < cost)
    {
        statusText.text = $"Need {cost} credits for this";
        return;
    }

    // Execute feature
    LTLMManager.Instance.ConsumeTokens(cost, $"{feature}_export",
        license => {
            statusText.text = $"{feature} complete!";
        },
        error => {
            statusText.text = "Error: " + error;
        }
    );
}
```

### 5. Load Custom Settings

```csharp
void LoadSettingsOverride()
{
    var license = LTLMManager.Instance.ActiveLicense;
    if (license?.config == null) return;

    // Load custom feature costs
    if (license.config.ContainsKey("featureCosts"))
    {
        var costs = license.config["featureCosts"] as Dictionary<string, object>;
        if (costs != null)
        {
            basicExportCost = Convert.ToInt32(costs["basic"]);
            proExportCost = Convert.ToInt32(costs["pro"]);
            enterpriseExportCost = Convert.ToInt32(costs["enterprise"]);
        }
    }
}
```

---

## Dashboard Configuration

### Setting Capabilities

In your policy, add capabilities:

```json
{
  "capabilities": ["basic", "pro", "export"]
}
```

### Custom Feature Costs

```json
{
  "config": {
    "featureCosts": {
      "basic": 1,
      "pro": 5,
      "enterprise": 10
    }
  }
}
```

---

## Testing

1. Create three policies: Basic, Pro, Enterprise
2. Add appropriate capabilities to each
3. Activate with different license types
4. Observe which panels are visible
5. Test tier detection
