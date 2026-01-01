# Token Consumption

Use tokens for usage-based features like exports, renders, or API calls.

---

## What Are Tokens?

Tokens are credits that users spend when using specific features. Examples:
- Export a 3D model = 1 token
- Render a video = 5 tokens
- Use AI feature = 2 tokens

---

## Check Token Balance

Get the current token balance:

```csharp
using LTLM.SDK.Unity;

public class TokenDisplay : MonoBehaviour
{
    public Text tokenLabel;

    void Start()
    {
        UpdateTokenDisplay();
    }

    void UpdateTokenDisplay()
    {
        int balance = LTLMManager.Instance.GetTokenBalance();
        tokenLabel.text = "Credits: " + balance;
    }
}
```

---

## Consume Tokens

When a user performs a metered action:

```csharp
using LTLM.SDK.Unity;

public class ExportFeature : MonoBehaviour
{
    public int exportCost = 5;
    public Button exportButton;
    public Text statusText;

    void Start()
    {
        exportButton.onClick.AddListener(OnExportClicked);
        UpdateButtonState();
    }

    void OnExportClicked()
    {
        // Check if user has enough tokens
        int balance = LTLMManager.Instance.GetTokenBalance();
        
        if (balance < exportCost)
        {
            statusText.text = "Not enough credits. You need " + exportCost;
            return;
        }

        statusText.text = "Exporting...";

        // Consume tokens
        LTLMManager.Instance.ConsumeTokens(
            exportCost,
            "export_model",
            OnTokensConsumed,
            OnTokenError
        );
    }

    void OnTokensConsumed(LicenseData license)
    {
        statusText.text = "Export complete! Credits remaining: " + license.tokensRemaining;
        
        // Now perform the actual export
        PerformExport();
        
        UpdateButtonState();
    }

    void OnTokenError(string error)
    {
        statusText.text = "Error: " + error;
    }

    void UpdateButtonState()
    {
        int balance = LTLMManager.Instance.GetTokenBalance();
        exportButton.interactable = balance >= exportCost;
    }

    void PerformExport()
    {
        // Your export logic here
        Debug.Log("Exporting model...");
    }
}
```

---

## Check Before Consuming

Always check balance before showing the feature:

```csharp
public class FeatureGate : MonoBehaviour
{
    public int requiredTokens = 10;
    public Button featureButton;
    public Text costLabel;

    void Start()
    {
        costLabel.text = requiredTokens + " credits";
        UpdateButtonState();
    }

    void UpdateButtonState()
    {
        int balance = LTLMManager.Instance.GetTokenBalance();
        
        if (balance >= requiredTokens)
        {
            featureButton.interactable = true;
            featureButton.GetComponentInChildren<Text>().text = "Use Feature";
        }
        else
        {
            featureButton.interactable = false;
            int needed = requiredTokens - balance;
            featureButton.GetComponentInChildren<Text>().text = "Need " + needed + " more";
        }
    }
}
```

---

## Show Top-Up Prompt

When user runs out of tokens:

```csharp
public class TokenManager : MonoBehaviour
{
    public GameObject topUpDialog;
    public Text dialogText;

    public void CheckAndConsume(int amount, string action, System.Action onSuccess)
    {
        int balance = LTLMManager.Instance.GetTokenBalance();
        
        if (balance < amount)
        {
            ShowTopUpDialog(amount - balance);
            return;
        }

        LTLMManager.Instance.ConsumeTokens(
            amount,
            action,
            license => onSuccess?.Invoke(),
            error => Debug.LogError(error)
        );
    }

    void ShowTopUpDialog(int shortage)
    {
        dialogText.text = "You need " + shortage + " more credits to continue.";
        topUpDialog.SetActive(true);
    }

    public void OnBuyCreditsClicked()
    {
        // Open top-up page - see Purchases documentation
        LTLMManager.Instance.CreateTopUpSession(
            "credits_100",
            "https://yourapp.com/success",
            url => Application.OpenURL(url),
            error => Debug.LogError(error)
        );
    }
}
```

---

## Display Token Balance in UI

Keep the balance visible to users:

```csharp
using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;

public class TokenBalanceUI : MonoBehaviour
{
    public Text balanceText;
    public Image balanceIcon;

    void Start()
    {
        UpdateDisplay();
    }

    void OnEnable()
    {
        // Subscribe to token changes
        LTLMManager.OnTokensConsumed += OnTokensChanged;
    }

    void OnDisable()
    {
        LTLMManager.OnTokensConsumed -= OnTokensChanged;
    }

    void OnTokensChanged(LicenseData license)
    {
        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        LicenseData license = LTLMManager.Instance.ActiveLicense;
        
        if (license == null)
        {
            balanceText.text = "---";
            return;
        }

        int remaining = license.tokensRemaining ?? 0;
        int limit = license.tokensLimit ?? 0;

        balanceText.text = remaining.ToString();

        // Change color based on balance
        if (remaining <= 10)
        {
            balanceText.color = Color.red;
        }
        else if (remaining <= 50)
        {
            balanceText.color = Color.yellow;
        }
        else
        {
            balanceText.color = Color.white;
        }
    }
}
```

---

## Track What Actions Cost

Define token costs in one place:

```csharp
public static class TokenCosts
{
    public const int EXPORT_LOW_RES = 1;
    public const int EXPORT_HIGH_RES = 5;
    public const int EXPORT_4K = 10;
    
    public const int AI_GENERATE = 2;
    public const int AI_ENHANCE = 3;
    
    public const int BATCH_PROCESS = 20;
}

// Usage:
LTLMManager.Instance.ConsumeTokens(
    TokenCosts.EXPORT_HIGH_RES,
    "export_high_res",
    OnSuccess,
    OnError
);
```

---

## Complete Example: Export Menu

```csharp
using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;

public class ExportMenu : MonoBehaviour
{
    [Header("Buttons")]
    public Button exportLowButton;
    public Button exportHighButton;
    public Button export4KButton;

    [Header("Costs")]
    public int lowResCost = 1;
    public int highResCost = 5;
    public int cost4K = 10;

    [Header("Feedback")]
    public Text balanceLabel;
    public Text statusLabel;
    public GameObject processingOverlay;

    void Start()
    {
        exportLowButton.onClick.AddListener(() => Export("low", lowResCost));
        exportHighButton.onClick.AddListener(() => Export("high", highResCost));
        export4KButton.onClick.AddListener(() => Export("4k", cost4K));
        
        UpdateUI();
    }

    void Export(string quality, int cost)
    {
        int balance = LTLMManager.Instance.GetTokenBalance();
        
        if (balance < cost)
        {
            statusLabel.text = "Need " + (cost - balance) + " more credits";
            statusLabel.color = Color.red;
            return;
        }

        processingOverlay.SetActive(true);
        statusLabel.text = "Processing...";
        statusLabel.color = Color.white;

        LTLMManager.Instance.ConsumeTokens(
            cost,
            "export_" + quality,
            license => OnExportSuccess(quality, license),
            OnExportError
        );
    }

    void OnExportSuccess(string quality, LicenseData license)
    {
        processingOverlay.SetActive(false);
        statusLabel.text = "Exported in " + quality + " quality!";
        statusLabel.color = Color.green;
        
        UpdateUI();
        
        // Perform actual export
        DoExport(quality);
    }

    void OnExportError(string error)
    {
        processingOverlay.SetActive(false);
        statusLabel.text = "Export failed: " + error;
        statusLabel.color = Color.red;
    }

    void UpdateUI()
    {
        int balance = LTLMManager.Instance.GetTokenBalance();
        balanceLabel.text = "Credits: " + balance;

        exportLowButton.interactable = balance >= lowResCost;
        exportHighButton.interactable = balance >= highResCost;
        export4KButton.interactable = balance >= cost4K;

        // Update button labels with costs
        exportLowButton.GetComponentInChildren<Text>().text = "Low (" + lowResCost + ")";
        exportHighButton.GetComponentInChildren<Text>().text = "High (" + highResCost + ")";
        export4KButton.GetComponentInChildren<Text>().text = "4K (" + cost4K + ")";
    }

    void DoExport(string quality)
    {
        Debug.Log("Performing " + quality + " export...");
        // Your export implementation
    }
}
```

---

## Next Steps

- [In-App Purchases](purchases.md) - Let users buy more tokens
- [Feature Entitlements](entitlements.md) - Lock features without tokens
- [API Reference](api-reference.md) - All token methods
