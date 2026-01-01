using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;
using System.Collections.Generic;

namespace LTLM.SDK.Demos
{
    /// <summary>
    /// Demonstrates feature gating based on license capabilities.
    /// 
    /// Features:
    /// - Check if user has specific capabilities
    /// - Combined capability + token checks
    /// - Dynamic feature panel visibility
    /// - Settings override for feature configuration
    /// </summary>
    public class FeatureEntitlementDemo : MonoBehaviour
    {
        [Header("Feature Panels")]
        [Tooltip("Panel shown for basic features (always available when licensed)")]
        public GameObject basicPanel;
        
        [Tooltip("Panel shown for pro features (requires 'pro' capability)")]
        public GameObject proPanel;
        
        [Tooltip("Panel shown for enterprise features (requires 'enterprise' capability)")]
        public GameObject enterprisePanel;

        [Header("UI Elements")]
        public Text statusText;
        public Text tierText;
        public Button upgradeButton;

        [Header("Feature Buttons")]
        public Button basicExportButton;
        public Button proExportButton;
        public Button enterpriseExportButton;

        [Header("Token Costs (can be overridden by license config)")]
        public int basicExportCost = 1;
        public int proExportCost = 5;
        public int enterpriseExportCost = 10;

        private void OnEnable()
        {
            LTLMManager.OnValidationCompleted += OnLicenseLoaded;
            LTLMManager.OnLicenseStatusChanged += OnStatusChanged;
        }

        private void OnDisable()
        {
            LTLMManager.OnValidationCompleted -= OnLicenseLoaded;
            LTLMManager.OnLicenseStatusChanged -= OnStatusChanged;
        }

        private void Start()
        {
            // Setup button listeners
            if (basicExportButton != null)
                basicExportButton.onClick.AddListener(() => UseFeature("basic", basicExportCost));
            if (proExportButton != null)
                proExportButton.onClick.AddListener(() => UseFeature("pro", proExportCost));
            if (enterpriseExportButton != null)
                enterpriseExportButton.onClick.AddListener(() => UseFeature("enterprise", enterpriseExportCost));
            if (upgradeButton != null)
                upgradeButton.onClick.AddListener(OnUpgradeClicked);

            // Initial UI update
            if (LTLMManager.Instance.IsAuthenticated)
            {
                ConfigureFeatures();
            }
            else
            {
                DisableAll();
            }
        }

        private void OnLicenseLoaded(bool success, LicenseStatus status)
        {
            if (success)
            {
                LoadSettingsOverride();
                ConfigureFeatures();
            }
            else
            {
                DisableAll();
            }
        }

        private void OnStatusChanged(LicenseStatus status)
        {
            ConfigureFeatures();
        }

        /// <summary>
        /// Load feature settings from license config (settings override).
        /// </summary>
        private void LoadSettingsOverride()
        {
            var license = LTLMManager.Instance.ActiveLicense;
            if (license?.config == null) return;

            // Example: Load custom export costs from license config
            if (license.config.ContainsKey("featureCosts"))
            {
                var costs = license.config["featureCosts"] as Dictionary<string, object>;
                if (costs != null)
                {
                    if (costs.ContainsKey("basic"))
                        basicExportCost = System.Convert.ToInt32(costs["basic"]);
                    if (costs.ContainsKey("pro"))
                        proExportCost = System.Convert.ToInt32(costs["pro"]);
                    if (costs.ContainsKey("enterprise"))
                        enterpriseExportCost = System.Convert.ToInt32(costs["enterprise"]);

                    Debug.Log($"[Demo] Loaded feature costs from config");
                }
            }
        }

        /// <summary>
        /// Configure which features are visible and enabled based on license.
        /// </summary>
        private void ConfigureFeatures()
        {
            if (!LTLMManager.Instance.IsAuthenticated)
            {
                DisableAll();
                return;
            }

            var license = LTLMManager.Instance.ActiveLicense;
            int tokenBalance = license?.tokensRemaining ?? 0;

            // Determine tier
            string tier = GetCurrentTier();
            if (tierText != null)
            {
                tierText.text = "Tier: " + tier;
            }

            // Basic panel - available to all licensed users
            if (basicPanel != null)
            {
                basicPanel.SetActive(true);
            }
            if (basicExportButton != null)
            {
                basicExportButton.interactable = tokenBalance >= basicExportCost;
                basicExportButton.GetComponentInChildren<Text>().text = $"Export ({basicExportCost})";
            }

            // Pro panel - requires "pro" capability
            bool hasPro = LTLMManager.Instance.HasCapability("pro");
            if (proPanel != null)
            {
                proPanel.SetActive(hasPro);
            }
            if (proExportButton != null && hasPro)
            {
                proExportButton.interactable = tokenBalance >= proExportCost;
                proExportButton.GetComponentInChildren<Text>().text = $"Pro Export ({proExportCost})";
            }

            // Enterprise panel - requires "enterprise" capability
            bool hasEnterprise = LTLMManager.Instance.HasCapability("enterprise");
            if (enterprisePanel != null)
            {
                enterprisePanel.SetActive(hasEnterprise);
            }
            if (enterpriseExportButton != null && hasEnterprise)
            {
                enterpriseExportButton.interactable = tokenBalance >= enterpriseExportCost;
                enterpriseExportButton.GetComponentInChildren<Text>().text = $"Enterprise Export ({enterpriseExportCost})";
            }

            // Show upgrade button if not at highest tier
            if (upgradeButton != null)
            {
                upgradeButton.gameObject.SetActive(!hasEnterprise);
            }

            // Update status
            UpdateStatus();
        }

        private string GetCurrentTier()
        {
            if (LTLMManager.Instance.HasCapability("enterprise"))
                return "Enterprise";
            if (LTLMManager.Instance.HasCapability("pro"))
                return "Pro";
            if (LTLMManager.Instance.IsAuthenticated)
                return "Basic";
            return "None";
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;

            var license = LTLMManager.Instance.ActiveLicense;
            var status = LTLMManager.Instance.GetLicenseStatus();
            int days = LTLMManager.Instance.GetDaysRemaining();
            int tokens = license?.tokensRemaining ?? 0;

            statusText.text = 
                $"Status: {status}\n" +
                $"Valid: {(days == -1 ? "Lifetime" : days + " days")}\n" +
                $"Credits: {tokens}";
        }

        private void DisableAll()
        {
            if (basicPanel != null) basicPanel.SetActive(false);
            if (proPanel != null) proPanel.SetActive(false);
            if (enterprisePanel != null) enterprisePanel.SetActive(false);
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(false);

            if (statusText != null)
            {
                statusText.text = "Please activate a license";
            }
            if (tierText != null)
            {
                tierText.text = "Tier: None";
            }
        }

        private void UseFeature(string feature, int cost)
        {
            // Check capability first
            if (feature == "pro" && !LTLMManager.Instance.HasCapability("pro"))
            {
                statusText.text = "Upgrade to Pro to use this feature";
                return;
            }
            if (feature == "enterprise" && !LTLMManager.Instance.HasCapability("enterprise"))
            {
                statusText.text = "Upgrade to Enterprise to use this feature";
                return;
            }

            // Check tokens
            if (LTLMManager.Instance.GetTokenBalance() < cost)
            {
                statusText.text = $"Need {cost} credits for {feature} export";
                return;
            }

            // Consume and execute
            LTLMManager.Instance.ConsumeTokens(cost, $"{feature}_export",
                license => {
                    statusText.text = $"{feature} export complete!";
                    Debug.Log($"[Demo] {feature} export executed");
                    ConfigureFeatures(); // Refresh button states
                },
                error => {
                    statusText.text = "Error: " + error;
                }
            );
        }

        private void OnUpgradeClicked()
        {
            statusText.text = "Opening upgrade options...";

            // Get purchasable policies and show upgrade dialog
            LTLMManager.Instance.GetBuyablePolicies(
                policies => {
                    Debug.Log($"[Demo] {policies.Count} upgrade options available");
                    // In a real app, show a dialog with these options
                },
                error => {
                    statusText.text = "Error loading upgrades: " + error;
                }
            );
        }
    }
}
