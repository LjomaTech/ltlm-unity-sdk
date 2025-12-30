using System.Collections.Generic;
using LTLM.SDK.Core.Models;
using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;
using TMPro;

namespace LTLM.SDK.Demos
{
    /// <summary>
    /// Complete startup and activation demo that properly handles:
    /// - Auto-validation on startup (with loading state)
    /// - Manual license activation
    /// - All license status cases
    /// - Settings override from license data
    /// 
    /// Attach this to a Canvas in your first scene.
    /// </summary>
    public class SimpleActivationUI : MonoBehaviour
    {
        [Header("UI Panels")]
        [Tooltip("Panel shown while checking license on startup")]
        public GameObject loadingPanel;
        
        [Tooltip("Panel for entering license key")]
        public GameObject activationPanel;
        
        [Tooltip("Panel shown when licensed")]
        public GameObject licensedPanel;

        [Header("Activation Panel")]
        public TMP_InputField licenseKeyInput;
        public Button activateButton;
        public TMP_Text activationStatusText;

        [Header("Licensed Panel")]
        public TMP_Text licenseInfoText;
        public TMP_Text tokenBalanceText;
        public Button signOutButton;

        [Header("Loading Panel")]
        public TMP_Text loadingText;

        private void OnEnable()
        {
            // Subscribe to validation events for startup handling
            LTLMManager.OnValidationStarted += OnValidationStarted;
            LTLMManager.OnValidationCompleted += OnValidationCompleted;
        }

        private void OnDisable()
        {
            LTLMManager.OnValidationStarted -= OnValidationStarted;
            LTLMManager.OnValidationCompleted -= OnValidationCompleted;
        }

        private void Start()
        {
            // Setup button listeners
            activateButton.onClick.AddListener(OnActivateClicked);
            if (signOutButton != null)
            {
                signOutButton.onClick.AddListener(OnSignOutClicked);
            }

            // If autoValidateOnStart is disabled, check manually
            if (!LTLMManager.Instance.autoValidateOnStart)
            {
                // Check if already authenticated
                if (LTLMManager.Instance.IsAuthenticated)
                {
                    ShowLicensedPanel();
                }
                else
                {
                    ShowActivationPanel();
                }
            }
            // If autoValidateOnStart is enabled, the events will handle it
        }

        // Called when validation starts (including auto-validation on startup)
        private void OnValidationStarted()
        {
            ShowLoadingPanel("Checking license...");
        }

        // Called when validation completes (success or failure)
        private void OnValidationCompleted(bool success, LicenseStatus status)
        {
            HideLoadingPanel();

            if (success && status == LicenseStatus.Active)
            {
                ShowLicensedPanel();
            }
            else if (success && status == LicenseStatus.ValidNoSeat)
            {
                // License valid but all seats are occupied
                ShowActivationPanel();
                activationStatusText.text = "License valid but all seats are in use. Close the app on another device.";
                activationStatusText.color = Color.yellow;
            }
            else if (success && status == LicenseStatus.GracePeriod)
            {
                ShowLicensedPanel();
                ShowGraceWarning();
            }
            else if (status == LicenseStatus.Expired)
            {
                ShowActivationPanel();
                activationStatusText.text = "Your license has expired. Please renew.";
                activationStatusText.color = Color.yellow;
            }
            else
            {
                // Unauthenticated, ConnectionRequired, or other states
                ShowActivationPanel();
            }
        }

        private void OnActivateClicked()
        {
            string key = licenseKeyInput.text.Trim();
            
            if (string.IsNullOrEmpty(key))
            {
                activationStatusText.text = "Please enter a license key";
                activationStatusText.color = Color.yellow;
                return;
            }

            SetActivationLoading(true);
            activationStatusText.text = "Activating...";
            activationStatusText.color = Color.white;

            LTLMManager.Instance.ActivateLicense(key,
                OnActivationSuccess,
                OnActivationError
            );
        }

        private void OnActivationSuccess(LicenseData license, LicenseStatus status)
        {
            SetActivationLoading(false);

            switch (status)
            {
                case LicenseStatus.Active:
                    activationStatusText.text = "Activated successfully!";
                    activationStatusText.color = Color.green;
                    ShowLicensedPanel();
                    break;

                case LicenseStatus.GracePeriod:
                    activationStatusText.text = "License is in grace period";
                    activationStatusText.color = Color.yellow;
                    ShowLicensedPanel();
                    ShowGraceWarning();
                    break;

                case LicenseStatus.Expired:
                    activationStatusText.text = "This license has expired";
                    activationStatusText.color = Color.red;
                    break;

                default:
                    activationStatusText.text = "Status: " + status;
                    break;
            }
        }

        private void OnActivationError(string error)
        {
            SetActivationLoading(false);
            activationStatusText.text = GetUserFriendlyError(error);
            activationStatusText.color = Color.red;
        }

        private void OnSignOutClicked()
        {
            // DeactivateSeat returns false if offline (abuse prevention)
            if (!LTLMManager.Instance.DeactivateSeat())
            {
                activationStatusText.text = "Cannot sign out while offline. Connect to the internet.";
                activationStatusText.color = Color.yellow;
                return;
            }
            
            LTLMManager.Instance.ClearLicenseCache();
            licenseKeyInput.text = "";
            ShowActivationPanel();
            activationStatusText.text = "Signed out";
            activationStatusText.color = Color.white;
        }

        // ============================================================
        // UI HELPERS
        // ============================================================

        private void ShowLoadingPanel(string message)
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
                if (loadingText != null) loadingText.text = message;
            }
            if (activationPanel != null) activationPanel.SetActive(false);
            if (licensedPanel != null) licensedPanel.SetActive(false);
        }

        private void HideLoadingPanel()
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
        }

        private void ShowActivationPanel()
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (activationPanel != null) activationPanel.SetActive(true);
            if (licensedPanel != null) licensedPanel.SetActive(false);
        }

        private void ShowLicensedPanel()
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (activationPanel != null) activationPanel.SetActive(false);
            if (licensedPanel != null) licensedPanel.SetActive(true);
            
            UpdateLicenseInfo();
        }

        private void UpdateLicenseInfo()
        {
            var license = LTLMManager.Instance.ActiveLicense;
            if (license == null) return;

            if (licenseInfoText != null)
            {
                licenseInfoText.text = 
                    "License: " + MaskKey(license.licenseKey) + "\n" +
                    "Status: " + license.status + "\n" +
                    "Expires: " + (license.validUntil ?? "Never");
            }

            if (tokenBalanceText != null)
            {
                int remaining = license.tokensRemaining ?? 0;
                int limit = license.tokensLimit ?? 0;
                tokenBalanceText.text = remaining + " / " + limit + " credits";
            }

            // Example: Access settings override from license config
            if (license.config != null)
            {
                Debug.Log("[Demo] License config loaded: " + license.config.Count + " settings");
                // You can access custom settings like:
                // license.config["customSetting"]
            }
            // Example: Access metadata from config
            if (license.config.ContainsKey("metadata"))
            {
                var metadata = license.config["metadata"] as Dictionary<string, object>;
                if (metadata != null)
                {
                    Debug.Log("[Demo] License metadata: " + metadata.Count + " entries");
                }
            }
        }

        private void ShowGraceWarning()
        {
            Debug.Log("[Demo] License is in grace period - show warning to user");
            // Implement your grace period warning UI here
        }

        private void SetActivationLoading(bool loading)
        {
            activateButton.interactable = !loading;
            licenseKeyInput.interactable = !loading;
        }

        private string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 8) return "****";
            return key.Substring(0, 4) + "****" + key.Substring(key.Length - 4);
        }

        private string GetUserFriendlyError(string error)
        {
            if (error.Contains("network") || error.Contains("timeout"))
                return "Connection failed. Check your internet.";
            if (error.Contains("Invalid") || error.Contains("not found"))
                return "License key not recognized.";
            if (error.Contains("limit") || error.Contains("activated"))
                return "License is already in use on another device.";
            if (error.Contains("expired"))
                return "This license has expired.";
            return error;
        }
    }
}
