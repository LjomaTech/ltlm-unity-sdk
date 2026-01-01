using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using LTLM.SDK.Unity;
using LTLM.SDK.Core.Models;

namespace LTLM.SDK.Demos
{
    /// <summary>
    /// Demonstrates the full in-game commerce flow:
    /// 1. Fetch available store items (Policies/Top-ups).
    /// 2. Generate and open hosted checkout link.
    /// 3. Poll for license status update after payment.
    /// </summary>
    public class InGameStoreDemo : MonoBehaviour
    {
        [Header("UI References")]
        public Text storeInfoText;
        public Button fetchPoliciesButton;
        public Button buyFirstPolicyButton;
        public Button buyTopUpButton;
        public Button refreshStatusButton;
        public Text licenseStatusDisplay;

        private List<PolicyData> _availablePolicies;

        private void Start()
        {
            fetchPoliciesButton.onClick.AddListener(FetchStoreItems);
            buyFirstPolicyButton.onClick.AddListener(BuyFirstStoreItem);
            buyTopUpButton.onClick.AddListener(BuyTopUp);
            refreshStatusButton.onClick.AddListener(ManualRefresh);
            
            UpdateLicenseDisplay();
        }

        private void UpdateLicenseDisplay()
        {
            var license = LTLMManager.Instance.ActiveLicense;
            if (license == null)
            {
                licenseStatusDisplay.text = "No Active License";
            }
            else
            {
                licenseStatusDisplay.text = $"Status: {license.status}\nTokens: {license.tokensRemaining}\nExpiry: {license.validUntil}";
            }
        }

        private void FetchStoreItems()
        {
            storeInfoText.text = "Fetching store items...";
            LTLMManager.Instance.GetBuyablePolicies(
                policies => {
                    _availablePolicies = policies;
                    string list = "Available Items:\n";
                    foreach (var p in policies)
                    {
                        string pricing = $"{p.price} {p.currency}";
                        if (p.type == "subscription")
                        {
                            pricing += $" every {p.recurringIntervalCount} {p.recurringInterval}(s)";
                        }

                        list += $"• {p.name} ({p.shortDescription})\n";
                        list += $"  Type: {p.type} | {pricing}\n";
                        if (p.config?.limits != null)
                        {
                            list += $"  Limits: {p.config.limits.maxMachines} devices, {p.config.limits.maxActivations} activations\n";
                        }
                        list += "\n";
                    }
                    storeInfoText.text = list;
                    buyFirstPolicyButton.interactable = policies.Count > 0;
                },
                err => storeInfoText.text = "Error fetching store: " + err
            );
        }

        private void BuyFirstStoreItem()
        {
            if (_availablePolicies == null || _availablePolicies.Count == 0) return;

            string policyId = _availablePolicies[0].PolicyID.ToString();
            string demoEmail = "customer@example.com"; // In a real game, you'd get this from an InputField

            storeInfoText.text = "Creating Checkout Session...";
            
            LTLMManager.Instance.CreateCheckoutSession(
                policyId,
                demoEmail,
                "https://ltlm.io/demo-success", // Redirect URL
                checkoutUrl => {
                    storeInfoText.text = "Opening secure browser...";
                    Application.OpenURL(checkoutUrl);
                    
                    // After opening the URL, we start polling for status changes
                    StartCoroutine(PollForStatusUpdate());
                },
                err => storeInfoText.text = "Checkout Failed: " + err
            );
        }

        private void BuyTopUp()
        {
            if (!LTLMManager.Instance.IsAuthenticated)
            {
                storeInfoText.text = "Please activate a license first to buy top-ups.";
                return;
            }

            // In a real scenario, you'd fetch Top-up options from ActiveLicense.features or a dedicated list
            string packId = "topup_5"; // Example pack ID defined in your policy

            storeInfoText.text = "Creating Top-up Session...";
            LTLMManager.Instance.CreateTopUpSession(
                packId,
                "https://ltlm.io/demo-success",
                checkoutUrl => {
                    storeInfoText.text = "Opening Top-up browser...";
                    Application.OpenURL(checkoutUrl);
                    StartCoroutine(PollForStatusUpdate());
                },
                err => storeInfoText.text = "Top-up Error: " + err
            );
        }

        private void ManualRefresh()
        {
            if (LTLMManager.Instance.ActiveLicense == null) return;
            
            storeInfoText.text = "Refreshing license status...";
            LTLMManager.Instance.ValidateLicense(LTLMManager.Instance.ActiveLicense.licenseKey);
            Invoke("UpdateLicenseDisplay", 1f); // Brief delay for network
        }

        /// <summary>
        /// Coroutine to poll the server for a status update.
        /// This mimics checking if the payment was completed after the user returns to the game.
        /// </summary>
        private System.Collections.IEnumerator PollForStatusUpdate()
        {
            storeInfoText.text = "Waiting for payment confirmation...";
            
            // Poll every 5 seconds for up to 2 minutes
            for (int i = 0; i < 24; i++)
            {
                yield return new WaitForSeconds(5f);
                
                if (LTLMManager.Instance.ActiveLicense != null)
                {
                    LTLMManager.Instance.ValidateLicense(LTLMManager.Instance.ActiveLicense.licenseKey);
                    UpdateLicenseDisplay();
                }
            }
            
            storeInfoText.text = "Polling stopped. Use Refresh button if payment was completed.";
        }
    }
}
