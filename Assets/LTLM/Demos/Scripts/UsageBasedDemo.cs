using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;
using System.Collections.Generic;
using LTLM.SDK.Core.Models;
using TMPro;

namespace LTLM.SDK.Demos
{
    /// <summary>
    /// Demonstrates usage-based licensing with tokens.
    /// 
    /// Features:
    /// - Token balance display
    /// - Token consumption for actions
    /// - Top-up integration
    /// - Proper event subscriptions
    /// - Settings override for custom token costs
    /// </summary>
    public class UsageBasedDemo : MonoBehaviour
    {
        [Header("UI Elements")]
        public TMP_Text statusText;
        public TMP_Text tokenBalanceText;
        public Button exportLowButton;
        public Button exportHighButton;
        public Button export4KButton;
        public Button topUpButton;

        [Header("Default Token Costs (can be overridden by license config)")]
        public int lowResCost = 1;
        public int highResCost = 5;
        public int cost4K = 10;

        private void OnEnable()
        {
            // Subscribe to token consumption events
            LTLMManager.OnTokensConsumed += OnTokensUpdated;
            LTLMManager.OnValidationCompleted += OnLicenseLoaded;
        }

        private void OnDisable()
        {
            LTLMManager.OnTokensConsumed -= OnTokensUpdated;
            LTLMManager.OnValidationCompleted -= OnLicenseLoaded;
        }

        private void Start()
        {
            // Setup button listeners
            if (exportLowButton != null)
                exportLowButton.onClick.AddListener(() => ConsumeAndExport("low", lowResCost));
            if (exportHighButton != null)
                exportHighButton.onClick.AddListener(() => ConsumeAndExport("high", highResCost));
            if (export4KButton != null)
                export4KButton.onClick.AddListener(() => ConsumeAndExport("4K", cost4K));
            if (topUpButton != null)
                topUpButton.onClick.AddListener(OnTopUpClicked);

            UpdateUI();
        }

        // Called when license is loaded (including startup)
        private void OnLicenseLoaded(bool success, LicenseStatus status)
        {
            if (success)
            {
                // Load custom token costs from license config if available
                LoadSettingsOverride();
            }
            UpdateUI();
        }

        // Called when tokens are consumed
        private void OnTokensUpdated(LicenseData license)
        {
            UpdateTokenDisplay(license);
        }

        /// <summary>
        /// Load settings override from license config.
        /// The config comes from the policy or license-specific overrides set in the dashboard.
        /// </summary>
        private void LoadSettingsOverride()
        {
            var license = LTLMManager.Instance.ActiveLicense;
            if (license?.config == null) return;

            // Example: Override token costs from license config
            if (license.config.ContainsKey("exportCosts"))
            {
                var costs = license.config["exportCosts"] as Dictionary<string, object>;
                if (costs != null)
                {
                    if (costs.ContainsKey("low"))
                        lowResCost = System.Convert.ToInt32(costs["low"]);
                    if (costs.ContainsKey("high"))
                        highResCost = System.Convert.ToInt32(costs["high"]);
                    if (costs.ContainsKey("4k"))
                        cost4K = System.Convert.ToInt32(costs["4k"]);

                    Debug.Log($"[Demo] Loaded custom costs: Low={lowResCost}, High={highResCost}, 4K={cost4K}");
                }
            }

            // Example: Check for custom metadata
            if (license.metadata != null && license.metadata.ContainsKey("welcomeMessage"))
            {
                string welcome = license.metadata["welcomeMessage"].ToString();
                Debug.Log("[Demo] Welcome message: " + welcome);
            }
        }

        private void UpdateUI()
        {
            if (!LTLMManager.Instance.IsAuthenticated)
            {
                statusText.text = "Please activate a license first";
                SetButtonsEnabled(false);
                if (topUpButton != null) topUpButton.gameObject.SetActive(false);
                return;
            }

            var license = LTLMManager.Instance.ActiveLicense;
            statusText.text = "Ready to export";
            
            UpdateTokenDisplay(license);
            UpdateButtonLabels();
            UpdateButtonStates(license);

            if (topUpButton != null) topUpButton.gameObject.SetActive(true);
        }

        private void UpdateTokenDisplay(LicenseData license)
        {
            if (tokenBalanceText == null || license == null) return;

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

        private void UpdateButtonLabels()
        {
            if (exportLowButton != null)
                exportLowButton.GetComponentInChildren<TMP_Text>().text = $"Low Res ({lowResCost})";
            if (exportHighButton != null)
                exportHighButton.GetComponentInChildren<TMP_Text>().text = $"High Res ({highResCost})";
            if (export4KButton != null)
                export4KButton.GetComponentInChildren<TMP_Text>().text = $"4K ({cost4K})";
        }

        private void UpdateButtonStates(LicenseData license)
        {
            int balance = license?.tokensRemaining ?? 0;

            UpdateTokenDisplay(license);
            if (exportLowButton != null)
                exportLowButton.interactable = balance >= lowResCost;
            if (exportHighButton != null)
                exportHighButton.interactable = balance >= highResCost;
            if (export4KButton != null)
                export4KButton.interactable = balance >= cost4K;
        }

        private void SetButtonsEnabled(bool enabled)
        {
            if (exportLowButton != null) exportLowButton.interactable = enabled;
            if (exportHighButton != null) exportHighButton.interactable = enabled;
            if (export4KButton != null) export4KButton.interactable = enabled;
        }

        private void ConsumeAndExport(string quality, int cost)
        {
            int balance = LTLMManager.Instance.GetTokenBalance();

            if (balance < cost)
            {
                statusText.text = $"Need {cost - balance} more credits";
                return;
            }

            statusText.text = $"Exporting {quality}...";
            SetButtonsEnabled(false);

            LTLMManager.Instance.ConsumeTokens(cost, $"export_{quality}",
                license => {
                    statusText.text = $"Export complete! {quality} quality";
                    PerformExport(quality);
                    UpdateButtonStates(license);
                },
                error => {
                    statusText.text = "Export failed: " + error;
                    UpdateUI();
                }
            );
        }

        private void PerformExport(string quality)
        {
            Debug.Log($"[Demo] Performing {quality} export...");

            // Log analytics event
            LTLMManager.Instance.LogEvent("export_completed", new Dictionary<string, object>
            {
                { "quality", quality },
                { "timestamp", System.DateTime.UtcNow.ToString("o") }
            });
        }

        private void OnTopUpClicked()
        {
            statusText.text = "Opening credits store...";

            // You can create a top-up session with a specific pack
            LTLMManager.Instance.CreateTopUpSession(
                "topup-1766789091466",
                "https://yourapp.com/topup-success",
                url => {
                    statusText.text = "Opening checkout...";
                    Application.OpenURL(url);
                },
                error => {
                    statusText.text = "Error: " + error;
                }
            );
        }
    }
}
