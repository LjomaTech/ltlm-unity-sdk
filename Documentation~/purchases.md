# In-App Purchases

Let users purchase licenses, upgrade plans, or buy tokens from within your app.

---

## Overview

The SDK supports:
- **New License Purchase** - User buys a license
- **Plan Upgrade** - User upgrades to higher tier
- **Token Top-Up** - User buys more tokens

All purchases open a secure checkout page in the browser.

---

## Check Trial Eligibility First

Before showing trial options to returning customers, check if they're eligible:

```csharp
public class PricingUI : MonoBehaviour
{
    public GameObject trialButton;
    public GameObject fullPriceButton;

    void Start()
    {
        // Check eligibility before showing pricing
        LTLMManager.Instance.CheckTrialEligibility(customerEmail,
            result => {
                trialButton.SetActive(result.eligible);
                fullPriceButton.SetActive(true);
                
                if (!result.eligible)
                {
                    Debug.Log($"Trial not available: {result.message}");
                }
            },
            error => {
                // On error, show full price only
                trialButton.SetActive(false);
                fullPriceButton.SetActive(true);
            }
        );
    }
}
```

> [!TIP]
> Always check trial eligibility when you have the customer's email (e.g., after OTP login) to prevent showing trial options to customers who have already used their trial.

---

## Get Available Products

Fetch purchasable plans:

```csharp
using UnityEngine;
using LTLM.SDK.Unity;

public class StoreLoader : MonoBehaviour
{
    public Transform productContainer;
    public GameObject productPrefab;

    void Start()
    {
        LoadProducts();
    }

    void LoadProducts()
    {
        LTLMManager.Instance.GetBuyablePolicies(
            OnProductsLoaded,
            OnError
        );
    }

    void OnProductsLoaded(List<PolicyData> policies)
    {
        foreach (var policy in policies)
        {
            CreateProductUI(policy);
        }
    }

    void CreateProductUI(PolicyData policy)
    {
        GameObject item = Instantiate(productPrefab, productContainer);
        
        var nameText = item.transform.Find("Name").GetComponent<Text>();
        var priceText = item.transform.Find("Price").GetComponent<Text>();
        var buyButton = item.transform.Find("BuyButton").GetComponent<Button>();

        nameText.text = policy.displayName;
        priceText.text = policy.price + " " + policy.currency;
        
        buyButton.onClick.AddListener(() => OnBuyClicked(policy));
    }

    void OnBuyClicked(PolicyData policy)
    {
        StartPurchase(policy.id);
    }

    void OnError(string error)
    {
        Debug.LogError("Failed to load products: " + error);
    }
}
```

---

## Purchase a New License

For users who don't have a license yet:

```csharp
using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;

public class PurchaseFlow : MonoBehaviour
{
    public InputField emailInput;
    public Text statusText;
    public string selectedPolicyId;

    public void OnPurchaseClicked()
    {
        string email = emailInput.text.Trim();
        
        if (string.IsNullOrEmpty(email))
        {
            statusText.text = "Please enter your email";
            return;
        }

        statusText.text = "Opening checkout...";

        LTLMManager.Instance.CreateCheckoutSession(
            selectedPolicyId,
            email,
            "https://yourapp.com/purchase-complete",
            OnCheckoutReady,
            OnCheckoutError
        );
    }

    void OnCheckoutReady(string checkoutUrl)
    {
        statusText.text = "Redirecting to checkout...";
        Application.OpenURL(checkoutUrl);
    }

    void OnCheckoutError(string error)
    {
        statusText.text = "Error: " + error;
    }
}
```

---

## Buy More Tokens

For users who need to top up:

```csharp
using UnityEngine;
using LTLM.SDK.Unity;

public class TokenTopUp : MonoBehaviour
{
    public void BuyTokens(string packId)
    {
        // packId examples: "tokens_50", "tokens_100", "tokens_500"
        
        LTLMManager.Instance.CreateTopUpSession(
            packId,
            "https://yourapp.com/topup-complete",
            OnCheckoutReady,
            OnCheckoutError
        );
    }

    void OnCheckoutReady(string checkoutUrl)
    {
        Application.OpenURL(checkoutUrl);
    }

    void OnCheckoutError(string error)
    {
        Debug.LogError("Top-up error: " + error);
    }
}
```

---

## Token Store UI

Complete token store implementation:

```csharp
using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;

public class TokenStore : MonoBehaviour
{
    [System.Serializable]
    public class TokenPack
    {
        public string packId;
        public string name;
        public int tokens;
        public string price;
    }

    [Header("Token Packs")]
    public TokenPack[] packs = new TokenPack[] {
        new TokenPack { packId = "tokens_50", name = "50 Credits", tokens = 50, price = "$4.99" },
        new TokenPack { packId = "tokens_100", name = "100 Credits", tokens = 100, price = "$8.99" },
        new TokenPack { packId = "tokens_500", name = "500 Credits", tokens = 500, price = "$39.99" }
    };

    [Header("UI")]
    public Transform packContainer;
    public GameObject packPrefab;
    public Text currentBalanceText;
    public GameObject loadingOverlay;

    void Start()
    {
        BuildStoreUI();
        UpdateBalance();
    }

    void BuildStoreUI()
    {
        foreach (var pack in packs)
        {
            CreatePackButton(pack);
        }
    }

    void CreatePackButton(TokenPack pack)
    {
        GameObject item = Instantiate(packPrefab, packContainer);

        item.transform.Find("Name").GetComponent<Text>().text = pack.name;
        item.transform.Find("Tokens").GetComponent<Text>().text = "+" + pack.tokens;
        item.transform.Find("Price").GetComponent<Text>().text = pack.price;

        Button button = item.GetComponent<Button>();
        button.onClick.AddListener(() => PurchasePack(pack));
    }

    void UpdateBalance()
    {
        int balance = LTLMManager.Instance.GetTokenBalance();
        currentBalanceText.text = "Current Balance: " + balance;
    }

    void PurchasePack(TokenPack pack)
    {
        loadingOverlay.SetActive(true);

        LTLMManager.Instance.CreateTopUpSession(
            pack.packId,
            "https://yourapp.com/topup-success",
            url => {
                loadingOverlay.SetActive(false);
                Application.OpenURL(url);
            },
            error => {
                loadingOverlay.SetActive(false);
                Debug.LogError("Purchase error: " + error);
            }
        );
    }
}
```

---

## Upgrade Plan

Let existing users upgrade to a better plan:

```csharp
public class UpgradeDialog : MonoBehaviour
{
    public Text currentPlanText;
    public GameObject upgradePanel;

    public void Show()
    {
        // Show current plan
        var license = LTLMManager.Instance.ActiveLicense;
        currentPlanText.text = "Current: " + (license?.policyName ?? "None");
        
        upgradePanel.SetActive(true);
    }

    public void OnUpgradeClicked(string newPolicyId)
    {
        string email = LTLMManager.Instance.ActiveLicense?.customerEmail;
        
        if (string.IsNullOrEmpty(email))
        {
            Debug.LogError("No email found on license");
            return;
        }

        LTLMManager.Instance.CreateCheckoutSession(
            newPolicyId,
            email,
            "https://yourapp.com/upgrade-success",
            url => Application.OpenURL(url),
            error => Debug.LogError("Upgrade error: " + error)
        );
    }
}
```

---

## Handle Purchase Completion

After the browser checkout completes, the user returns to your app.

### Deep Link Handling (Mobile)

```csharp
public class DeepLinkHandler : MonoBehaviour
{
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            // User returned from checkout - refresh license
            RefreshLicense();
        }
    }

    void RefreshLicense()
    {
        if (!LTLMManager.Instance.IsAuthenticated)
        {
            // New purchase - try to load license
            CheckForNewLicense();
        }
        else
        {
            // Existing user - refresh for updates (tokens, upgrades)
            var key = LTLMManager.Instance.ActiveLicense.licenseKey;
            LTLMManager.Instance.ValidateLicense(key,
                (license, status) => {
                    Debug.Log("License refreshed: " + license.tokensRemaining + " tokens");
                },
                error => Debug.LogError(error)
            );
        }
    }

    void CheckForNewLicense()
    {
        // Show activation UI for new users to enter their key
        FindObjectOfType<ActivationUI>()?.Show();
    }
}
```

---

## Complete Store Example

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using LTLM.SDK.Unity;

public class InAppStore : MonoBehaviour
{
    [Header("Panels")]
    public GameObject storePanel;
    public GameObject loadingPanel;
    public GameObject errorPanel;

    [Header("Products")]
    public Transform productsContainer;
    public GameObject productPrefab;

    [Header("Current License Info")]
    public Text currentPlanText;
    public Text tokensText;

    [Header("Error UI")]
    public Text errorText;
    public Button retryButton;

    void Start()
    {
        retryButton.onClick.AddListener(LoadStore);
        LoadStore();
    }

    void LoadStore()
    {
        errorPanel.SetActive(false);
        loadingPanel.SetActive(true);
        storePanel.SetActive(false);

        UpdateLicenseInfo();

        LTLMManager.Instance.GetBuyablePolicies(
            OnPoliciesLoaded,
            OnLoadError
        );
    }

    void UpdateLicenseInfo()
    {
        var license = LTLMManager.Instance.ActiveLicense;
        
        if (license != null)
        {
            currentPlanText.text = "Current Plan: " + (license.policyName ?? "Active");
            tokensText.text = "Credits: " + (license.tokensRemaining ?? 0);
        }
        else
        {
            currentPlanText.text = "No active license";
            tokensText.text = "";
        }
    }

    void OnPoliciesLoaded(List<PolicyData> policies)
    {
        loadingPanel.SetActive(false);
        storePanel.SetActive(true);

        // Clear existing
        foreach (Transform child in productsContainer)
        {
            Destroy(child.gameObject);
        }

        // Create product cards
        foreach (var policy in policies)
        {
            CreateProductCard(policy);
        }
    }

    void CreateProductCard(PolicyData policy)
    {
        GameObject card = Instantiate(productPrefab, productsContainer);

        card.transform.Find("Name").GetComponent<Text>().text = policy.displayName;
        card.transform.Find("Description").GetComponent<Text>().text = policy.description;
        card.transform.Find("Price").GetComponent<Text>().text = FormatPrice(policy);

        // Features list
        Text featuresText = card.transform.Find("Features").GetComponent<Text>();
        featuresText.text = FormatFeatures(policy);

        // Buy button
        Button buyBtn = card.transform.Find("BuyButton").GetComponent<Button>();
        buyBtn.onClick.AddListener(() => OnBuyProduct(policy));
    }

    string FormatPrice(PolicyData policy)
    {
        if (policy.billingCycle == "monthly")
            return policy.price + " " + policy.currency + "/month";
        else if (policy.billingCycle == "yearly")
            return policy.price + " " + policy.currency + "/year";
        else
            return policy.price + " " + policy.currency;
    }

    string FormatFeatures(PolicyData policy)
    {
        string features = "";
        
        if (policy.tokensLimit > 0)
            features += "• " + policy.tokensLimit + " credits included\n";
        
        if (policy.capabilities != null)
        {
            foreach (var cap in policy.capabilities)
            {
                features += "• " + FormatCapability(cap) + "\n";
            }
        }
        
        return features;
    }

    string FormatCapability(string cap)
    {
        // Convert capability IDs to readable names
        switch (cap)
        {
            case "pro": return "Pro Features";
            case "export": return "Export Enabled";
            case "team": return "Team Collaboration";
            default: return cap;
        }
    }

    void OnBuyProduct(PolicyData policy)
    {
        loadingPanel.SetActive(true);

        string email = "";
        var license = LTLMManager.Instance.ActiveLicense;
        if (license != null)
        {
            email = license.customerEmail ?? "";
        }

        // If no email, prompt user
        if (string.IsNullOrEmpty(email))
        {
            // Show email input dialog
            ShowEmailPrompt(policy);
            return;
        }

        CreateCheckout(policy.id, email);
    }

    void ShowEmailPrompt(PolicyData policy)
    {
        loadingPanel.SetActive(false);
        // Show your email input UI, then call:
        // CreateCheckout(policy.id, userEmail);
    }

    void CreateCheckout(string policyId, string email)
    {
        LTLMManager.Instance.CreateCheckoutSession(
            policyId,
            email,
            "https://yourapp.com/checkout-complete",
            url => {
                loadingPanel.SetActive(false);
                Application.OpenURL(url);
            },
            error => {
                loadingPanel.SetActive(false);
                ShowError("Checkout failed: " + error);
            }
        );
    }

    void OnLoadError(string error)
    {
        loadingPanel.SetActive(false);
        ShowError("Failed to load store: " + error);
    }

    void ShowError(string message)
    {
        errorText.text = message;
        errorPanel.SetActive(true);
    }
}
```

---

## Next Steps

- [Token Consumption](token-consumption.md) - Use purchased tokens
- [Feature Entitlements](entitlements.md) - Access upgraded features
- [Troubleshooting](troubleshooting.md) - Purchase issues
