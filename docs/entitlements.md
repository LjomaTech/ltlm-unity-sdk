# Feature Entitlements

Lock and unlock features based on the user's license.

---

## What Are Entitlements?

Entitlements are features or capabilities included with a license. Examples:
- "Pro" users get the advanced editor
- "Enterprise" users get team collaboration
- "Starter" users only get basic features

---

## Check If User Has a Feature

```csharp
using LTLM.SDK.Unity;

public class FeatureChecker : MonoBehaviour
{
    public void CheckFeatures()
    {
        // Check if user has a specific capability
        bool hasPro = LTLMManager.Instance.HasCapability("pro_features");
        bool hasAdvancedEditor = LTLMManager.Instance.HasCapability("advanced_editor");
        bool hasTeamAccess = LTLMManager.Instance.HasCapability("team_collaboration");

        Debug.Log("Pro Features: " + hasPro);
        Debug.Log("Advanced Editor: " + hasAdvancedEditor);
        Debug.Log("Team Access: " + hasTeamAccess);
    }
}
```

---

## Enable/Disable Features Based on License

```csharp
using UnityEngine;
using LTLM.SDK.Unity;

public class FeatureManager : MonoBehaviour
{
    [Header("Feature GameObjects")]
    public GameObject basicTools;
    public GameObject advancedTools;
    public GameObject proExportPanel;
    public GameObject teamPanel;

    [Header("Upgrade Button")]
    public GameObject upgradeButton;

    void Start()
    {
        ConfigureFeatures();
    }

    void ConfigureFeatures()
    {
        // Basic tools - always enabled if licensed
        basicTools.SetActive(LTLMManager.Instance.IsAuthenticated);

        // Advanced tools - requires "advanced_tools" capability
        advancedTools.SetActive(
            LTLMManager.Instance.HasCapability("advanced_tools")
        );

        // Pro export - requires "pro_export" capability
        proExportPanel.SetActive(
            LTLMManager.Instance.HasCapability("pro_export")
        );

        // Team features - requires "team" capability
        teamPanel.SetActive(
            LTLMManager.Instance.HasCapability("team")
        );

        // Show upgrade button if any premium features are missing
        bool hasAllFeatures = 
            LTLMManager.Instance.HasCapability("advanced_tools") &&
            LTLMManager.Instance.HasCapability("pro_export");
        
        upgradeButton.SetActive(!hasAllFeatures);
    }
}
```

---

## Combined Check: Feature + Tokens

Check both capability AND token balance:

```csharp
public class ProFeature : MonoBehaviour
{
    public int tokenCost = 10;
    public Button useFeatureButton;
    public Text statusLabel;

    void Start()
    {
        useFeatureButton.onClick.AddListener(OnUseFeature);
        UpdateButtonState();
    }

    void UpdateButtonState()
    {
        // Must have capability AND tokens
        bool isEntitled = LTLMManager.Instance.IsEntitled("pro_render", tokenCost);
        useFeatureButton.interactable = isEntitled;

        if (!LTLMManager.Instance.HasCapability("pro_render"))
        {
            statusLabel.text = "Upgrade to Pro to unlock";
        }
        else if (LTLMManager.Instance.GetTokenBalance() < tokenCost)
        {
            statusLabel.text = "Need " + tokenCost + " credits";
        }
        else
        {
            statusLabel.text = "Ready (" + tokenCost + " credits)";
        }
    }

    void OnUseFeature()
    {
        LTLMManager.Instance.ConsumeTokens(
            tokenCost,
            "pro_render",
            license => {
                PerformProRender();
                UpdateButtonState();
            },
            error => {
                statusLabel.text = "Error: " + error;
            }
        );
    }

    void PerformProRender()
    {
        Debug.Log("Performing pro render...");
    }
}
```

---

## Show Locked Features with Upgrade Prompt

```csharp
using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;

public class LockedFeature : MonoBehaviour
{
    [Header("Required Capability")]
    public string requiredCapability = "pro_features";

    [Header("UI")]
    public GameObject featureContent;
    public GameObject lockedOverlay;
    public Button upgradeButton;
    public Text lockedMessage;

    void Start()
    {
        upgradeButton.onClick.AddListener(OnUpgradeClicked);
        CheckAccess();
    }

    void CheckAccess()
    {
        bool hasAccess = LTLMManager.Instance.HasCapability(requiredCapability);

        featureContent.SetActive(hasAccess);
        lockedOverlay.SetActive(!hasAccess);

        if (!hasAccess)
        {
            lockedMessage.text = "Upgrade to unlock this feature";
        }
    }

    void OnUpgradeClicked()
    {
        // Show upgrade options or open store
        FindObjectOfType<UpgradeDialog>()?.Show();
    }
}
```

---

## Feature Tiers Example

Handle different subscription tiers:

```csharp
public class TierManager : MonoBehaviour
{
    public enum Tier { Free, Starter, Pro, Enterprise }

    public Tier GetCurrentTier()
    {
        if (!LTLMManager.Instance.IsAuthenticated)
            return Tier.Free;

        if (LTLMManager.Instance.HasCapability("enterprise"))
            return Tier.Enterprise;
        
        if (LTLMManager.Instance.HasCapability("pro"))
            return Tier.Pro;
        
        if (LTLMManager.Instance.HasCapability("starter"))
            return Tier.Starter;

        return Tier.Free;
    }

    public void ConfigureForTier()
    {
        Tier tier = GetCurrentTier();

        switch (tier)
        {
            case Tier.Free:
                EnableFreeFeatures();
                break;
            case Tier.Starter:
                EnableStarterFeatures();
                break;
            case Tier.Pro:
                EnableProFeatures();
                break;
            case Tier.Enterprise:
                EnableEnterpriseFeatures();
                break;
        }

        Debug.Log("Current tier: " + tier);
    }

    void EnableFreeFeatures()
    {
        // Basic features only
    }

    void EnableStarterFeatures()
    {
        EnableFreeFeatures();
        // Plus starter features
    }

    void EnableProFeatures()
    {
        EnableStarterFeatures();
        // Plus pro features
    }

    void EnableEnterpriseFeatures()
    {
        EnableProFeatures();
        // Plus enterprise features
    }
}
```

---

## Read Custom Metadata

Access custom data stored in the license:

```csharp
public class MetadataReader : MonoBehaviour
{
    void ReadLicenseMetadata()
    {
        // Get custom values set on the license
        object maxProjects = LTLMManager.Instance.GetMetadata("max_projects");
        object companyName = LTLMManager.Instance.GetMetadata("company_name");
        object expiryWarning = LTLMManager.Instance.GetMetadata("show_expiry_warning");

        if (maxProjects != null)
        {
            int limit = System.Convert.ToInt32(maxProjects);
            Debug.Log("Max projects: " + limit);
        }

        if (companyName != null)
        {
            string name = companyName.ToString();
            Debug.Log("Licensed to: " + name);
        }

        if (expiryWarning != null)
        {
            bool showWarning = System.Convert.ToBoolean(expiryWarning);
            if (showWarning)
            {
                ShowExpiryWarning();
            }
        }
    }

    void ShowExpiryWarning()
    {
        // Show warning UI
    }
}
```

---

## Complete Example: App Feature Configuration

```csharp
using UnityEngine;
using LTLM.SDK.Unity;

public class AppConfiguration : MonoBehaviour
{
    [Header("Feature Panels")]
    public GameObject basicPanel;
    public GameObject exportPanel;
    public GameObject advancedPanel;
    public GameObject teamPanel;
    public GameObject adminPanel;

    [Header("Menu Items")]
    public GameObject exportMenuItem;
    public GameObject shareMenuItem;
    public GameObject adminMenuItem;

    [Header("Upgrade")]
    public GameObject upgradePrompt;

    void Start()
    {
        Configure();
    }

    void Configure()
    {
        // Check authentication first
        if (!LTLMManager.Instance.IsAuthenticated)
        {
            DisableAll();
            return;
        }

        // Basic - always on for licensed users
        basicPanel.SetActive(true);

        // Export Panel - requires "export" capability
        bool canExport = LTLMManager.Instance.HasCapability("export");
        exportPanel.SetActive(canExport);
        exportMenuItem.SetActive(canExport);

        // Advanced Panel - requires "advanced" capability
        bool hasAdvanced = LTLMManager.Instance.HasCapability("advanced");
        advancedPanel.SetActive(hasAdvanced);

        // Team Panel - requires "team" capability
        bool hasTeam = LTLMManager.Instance.HasCapability("team");
        teamPanel.SetActive(hasTeam);
        shareMenuItem.SetActive(hasTeam);

        // Admin Panel - requires "admin" capability
        bool isAdmin = LTLMManager.Instance.HasCapability("admin");
        adminPanel.SetActive(isAdmin);
        adminMenuItem.SetActive(isAdmin);

        // Show upgrade if missing premium features
        bool hasPremium = hasAdvanced && hasTeam;
        upgradePrompt.SetActive(!hasPremium);
    }

    void DisableAll()
    {
        basicPanel.SetActive(false);
        exportPanel.SetActive(false);
        advancedPanel.SetActive(false);
        teamPanel.SetActive(false);
        adminPanel.SetActive(false);
    }
}
```

---

## Next Steps

- [In-App Purchases](purchases.md) - Upgrade to unlock features
- [Token Consumption](token-consumption.md) - Usage-based features
- [API Reference](api-reference.md) - All entitlement methods
