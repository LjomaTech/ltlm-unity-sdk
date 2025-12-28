using UnityEngine;
using TMPro;
using UnityEngine.UI;
using LTLM.SDK.Unity;
using LTLM.SDK.Core.Models;
using LTLM.SDK.Hardware;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Collections.Generic;

namespace LTLM.SDK.Demos
{
    /// <summary>
    /// A comprehensive unified demo script demonstrating all core features of the LTLM SDK:
    /// - Initialization and Cached Loading
    /// - Online/Offline Activation
    /// - Token Consumption (with Offline Sync)
    /// - Entitlement Checks
    /// - Store Link Generation
    /// - License Status & Hardware Authorization
    /// </summary>
    public class LTLMUnifiedDemo : MonoBehaviour
    {
        [Header("UI - Connection & Activation")]
        public TMP_InputField licenseKeyInput;
        public Button activateButton;
        public TMP_Text statusMessageText;
        public Image statusIndicator; // Optional: Green for active, Red for error

        [Header("UI - License Metadata")]
        public TMP_Text licenseKeyLabel;
        public TMP_Text hardwareIdLabel;
        public TMP_Text resolvedStatusLabel;
        public TMP_Text expirationLabel;
        public TMP_Text activationsLabel; // "Seats" usage

        [Header("UI - Token Management")]
        public TMP_Text tokenBalanceLabel;
        public TMP_InputField consumeAmountInput;
        public Button consumeButton;
        public TMP_Text tokenSyncStatusLabel;

        [Header("UI - Entitlements & Features")]
        public TMP_InputField capabilityInput;
        public Button checkButton;
        public TMP_Text entitlementResultLabel;

        [Header("UI - Marketplace")]
        public Button buyPackButton;
        public Button upgradePlanButton;

        private LTLMManager _manager;

        private void Start()
        {
            _manager = LTLMManager.Instance;
            
            // 1. Initial Setup: Buttons
            activateButton.onClick.AddListener(OnActivateClicked);
            consumeButton.onClick.AddListener(OnConsumeClicked);
            checkButton.onClick.AddListener(OnCheckEntitlementClicked);
            buyPackButton.onClick.AddListener(OnBuyPackClicked);
            upgradePlanButton.onClick.AddListener(OnUpgradeClicked);

            UpdateUI(null, LicenseStatus.Unauthenticated);

            // 2. Try to load from cache automatically
            statusMessageText.text = "Checking for stored license...";
            _manager.TryLoadStoredLicense(OnSuccess, OnError);
        }

        private void OnActivateClicked()
        {
            string key = licenseKeyInput.text.Trim();
            if (string.IsNullOrEmpty(key))
            {
                statusMessageText.text = "<color=red>Please enter a license key</color>";
                return;
            }

            statusMessageText.text = "Activating online...";
            _manager.ActivateLicense(key, OnSuccess, OnError);
        }

        private void OnSuccess(LicenseData licenseData, LicenseStatus status)
        {
            statusMessageText.text = status == LicenseStatus.Active 
                ? "<color=green>License Valid & Active</color>" 
                : $"<color=orange>Status: {status}</color>";
            
            UpdateUI(licenseData, status);
        }

        private void OnError(string message)
        {
            statusMessageText.text = $"<color=red>Error: {message}</color>";
            UpdateUI(null, LicenseStatus.Unauthenticated);
        }

        private void OnConsumeClicked()
        {
            if (!int.TryParse(consumeAmountInput.text, out int amount)) amount = 1;

            _manager.ConsumeTokens(amount, "DemoAction", (updatedLicense) => {
                UpdateUI(updatedLicense, _manager.GetLicenseStatus());
                tokenSyncStatusLabel.text = "Usage recorded successfully.";
            }, (err) => {
                tokenSyncStatusLabel.text = $"<color=red>Sync Error: {err}</color>";
            });
        }

        private void OnCheckEntitlementClicked()
        {
            string feat = capabilityInput.text;
            bool allowed = _manager.IsEntitled(feat);

            if (allowed)
            {
                entitlementResultLabel.text = $"<color=green>ACCESS GRANTED: {feat}</color>";
            }
            else
            {
                var status = _manager.GetLicenseStatus();
                entitlementResultLabel.text = status == LicenseStatus.Active 
                    ? $"<color=red>ACCESS DENIED: Feature '{feat}' not in policy.</color>"
                    : $"<color=red>BLOCK: License state is {status}.</color>";
            }
        }

        private void OnBuyPackClicked()
        {
            // Usually, you'd show a list of packs first, but for the demo we'll try to generate a link
            // if we have a real pack ID. Here we'll just show the method usage.
            _manager.GetBuyablePolicies(policies => {
                if (policies.Count > 0) {
                    statusMessageText.text = $"Discovered {policies.Count} policies in store.";
                }
            });
            
            // Example generating a top-up link for an imaginary pack
            _manager.CreateTopUpSession("starter_pack", "https://yourapp.com/success", url => {
                Application.OpenURL(url);
            }, err => OnError(err));
        }

        private void OnUpgradeClicked()
        {
             _manager.GetBuyablePolicies(policies => {
                if (policies.Count > 0) {
                     _manager.CreateCheckoutSession(policies[0].PolicyID.ToString(), "test@user.com", "myapp://callback", url => {
                        Application.OpenURL(url);
                    }, err => OnError(err));
                }
            });
        }

        private void UpdateUI(LicenseData license, LicenseStatus status)
        {
            if (license == null)
            {
                licenseKeyLabel.text = "Key: NONE";
                hardwareIdLabel.text = $"HWID: {DeviceID.GetHWID()}";
                resolvedStatusLabel.text = "Status: Unauthenticated";
                expirationLabel.text = "Expires: N/A";
                activationsLabel.text = "Seats: 0 / 0";
                tokenBalanceLabel.text = "Balance: 0";
                
                if (statusIndicator != null) statusIndicator.color = Color.gray;
                return;
            }

            licenseKeyLabel.text = $"Key: {license.licenseKey}";
            hardwareIdLabel.text = $"HWID: {DeviceID.GetHWID()}";
            resolvedStatusLabel.text = $"Status: <color=yellow>{status}</color>";
            expirationLabel.text = $"Expires: {(string.IsNullOrEmpty(license.validUntil) ? "Never" : license.validUntil)}";
            
            // "Seat" Handling: 
            // In LTLM, 'Seats' are mapped to 'Max Activations'.
            // Each machine registration counts as a seat.
            int usedSeats = license.activeSeats ?? (license.machines != null ? license.machines.Count : 0);
            int totalSeats = license.maxConcurrentSeats ?? 1;
            activationsLabel.text = $"Seats (Live): {usedSeats} / {totalSeats}";

            tokenBalanceLabel.text = $"Balance: <color=cyan>{license.tokensRemaining ?? 0}</color>";

            if (statusIndicator != null)
            {
                if (status == LicenseStatus.Active) statusIndicator.color = Color.green;
                else if (status == LicenseStatus.Tampered || status == LicenseStatus.Revoked) statusIndicator.color = Color.red;
                else statusIndicator.color = Color.yellow;
            }
        }
    }
}
