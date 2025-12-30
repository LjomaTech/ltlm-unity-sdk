# Demo Scenes

Working examples showing how to use the LTLM SDK.

---

## Included Demos

| Demo | Description |
|------|-------------|
| [Simple Activation](simple-activation.md) | Basic license key activation with startup handling |
| [Usage-Based Licensing](usage-based.md) | Token consumption for export features |
| [Feature Entitlements](feature-gates.md) | Lock/unlock features by tier |
| [Seat Management](seat-management.md) | Handle concurrent usage limits |
| [Customer Portal](customer-portal.md) | Self-service license management |
| [In-Game Store](in-game-store.md) | Purchase licenses and tokens |
| [Offline Activation](offline-activation.md) | Air-gapped activation with .ltlm files |

---

## Setting Up a Demo Scene

1. Create a new scene in Unity
2. Add the `LTLMManager` prefab or create via **GameObject → Create Empty → Add Component → LTLMManager**
3. Add a Canvas for UI elements
4. Add the demo script to the Canvas
5. Connect UI references in the Inspector

---

## Demo Scripts Location

All demo scripts are located in:
```
Assets/LTLM/Demos/Scripts/
```

---

## Key Patterns Used in Demos

### Startup Validation Events

All demos subscribe to validation events to properly handle loading states:

```csharp
void OnEnable()
{
    LTLMManager.OnValidationStarted += OnValidationStarted;
    LTLMManager.OnValidationCompleted += OnValidationCompleted;
}

void OnDisable()
{
    LTLMManager.OnValidationStarted -= OnValidationStarted;
    LTLMManager.OnValidationCompleted -= OnValidationCompleted;
}

void OnValidationStarted()
{
    // Show loading spinner
    loadingPanel.SetActive(true);
}

void OnValidationCompleted(bool success, LicenseStatus status)
{
    loadingPanel.SetActive(false);
    
    if (success && status == LicenseStatus.Active)
    {
        ShowMainApp();
    }
    else
    {
        ShowActivationScreen();
    }
}
```

### Settings Override

Demos load custom settings from the license config:

```csharp
void LoadSettingsOverride()
{
    var license = LTLMManager.Instance.ActiveLicense;
    if (license?.config == null) return;

    // Access custom settings from dashboard
    if (license.config.ContainsKey("exportCosts"))
    {
        var costs = license.config["exportCosts"] as Dictionary<string, object>;
        // Apply custom costs
    }
}
```

### Token Consumption

```csharp
LTLMManager.Instance.ConsumeTokens(cost, "action_name",
    license => {
        Debug.Log("Success! Remaining: " + license.tokensRemaining);
    },
    error => {
        Debug.LogError("Failed: " + error);
    }
);
```

### Capability Checks

```csharp
if (LTLMManager.Instance.HasCapability("pro"))
{
    EnableProFeatures();
}
```

---

## Next Steps

- [Quick Start](../quickstart.md) - Get started in 5 minutes
- [API Reference](../api-reference.md) - Full method documentation
