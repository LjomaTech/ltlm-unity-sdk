using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;
using System.Collections.Generic;
using LTLM.SDK.Core.Models;

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
        public Text statusText;
        public Text tokenBalanceText;
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
        /// Load settings override from license config (projectSettings).
        /// projectSettings are custom org-defined key-value pairs that flow through
        /// Project → Policy → License inheritance.
        /// </summary>
        private void LoadSettingsOverride()
        {
            var license = LTLMManager.Instance.ActiveLicense;
            if (license == null) return;

            // Load custom export costs from projectSettings
            // These can be defined in Project, Policy, or License level overrides
            var lowCost = license.GetProjectSetting<int>("lowResCost", 0);
            var highCost = license.GetProjectSetting<int>("highResCost", 0);
            var fourKCost = license.GetProjectSetting<int>("cost4K", 0);

            if (lowCost > 0) lowResCost = lowCost;
            if (highCost > 0) highResCost = highCost;
            if (fourKCost > 0) cost4K = fourKCost;

            Debug.Log($"[Demo] Export costs: Low={lowResCost}, High={highResCost}, 4K={cost4K}");

            // Example: Check for custom welcome message
            var welcomeMessage = license.GetProjectSetting<string>("welcomeMessage", null);
            if (!string.IsNullOrEmpty(welcomeMessage))
            {
                Debug.Log("[Demo] Welcome message: " + welcomeMessage);
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
                exportLowButton.GetComponentInChildren<Text>().text = $"Low Res ({lowResCost})";
            if (exportHighButton != null)
                exportHighButton.GetComponentInChildren<Text>().text = $"High Res ({highResCost})";
            if (export4KButton != null)
                export4KButton.GetComponentInChildren<Text>().text = $"4K ({cost4K})";
        }

        private void UpdateButtonStates(LicenseData license)
        {
            int balance = license?.tokensRemaining ?? 0;

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
                "credits_100",
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
