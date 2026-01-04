using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;
using System.Collections.Generic;

namespace LTLM.SDK.Demos
{
    /// <summary>
    /// A single unified UI for User Authentication and License Activation.
    /// This demonstrates how to use the SDK's CustomerLogin system in a real app.
    /// </summary>
    public class UnifiedAuthDemoUI : MonoBehaviour
    {
        [Header("Direct Activation Panel")]
        public InputField licenseKeyInput;
        public Button activateKeyButton;

        [Header("User Login Panel")]
        public InputField emailInput;
        public InputField otpInput;
        public Button sendOtpButton;
        public Button verifyOtpButton;
        
        [Header("Portfolio Panel")]
        public GameObject portfolioPanel;
        public Transform portfolioContainer;
        public GameObject licenseItemPrefab;

        [Header("Feedback")]
        public Text globalStatus;
        private void Start()
        {
            if (LTLMManager.Instance == null)
            {
                globalStatus.text = "LTLM Manager not found!";
                return;
            }

            activateKeyButton.onClick.AddListener(OnActivateWithKey);
            sendOtpButton.onClick.AddListener(OnSendOtpClicked);
            verifyOtpButton.onClick.AddListener(OnVerifyOtpClicked);
            
            portfolioPanel.SetActive(false);
        }

        private void OnActivateWithKey()
        {
            globalStatus.text = "Activating key...";
            LTLMManager.Instance.ActivateLicense(licenseKeyInput.text, 
                (license, status) => {
                    globalStatus.text = "Success! License Active.";
                },
                err => {
                    globalStatus.text = "Activation Failed: " + err;
                }
            );
        }

        private void OnSendOtpClicked()
        {
            globalStatus.text = "Sending OTP...";
            LTLMManager.Instance.RequestOTP(emailInput.text, 
                () => globalStatus.text = "OTP Sent to: " + emailInput.text,
                err => globalStatus.text = "OTP Error: " + err
            );
        }

        private void OnVerifyOtpClicked()
        {
            globalStatus.text = "Verifying OTP...";
            LTLMManager.Instance.VerifyOTP(emailInput.text, otpInput.text, 
                portfolio => {
                    globalStatus.text = "Logged In. Select a license.";
                    ShowPortfolio(portfolio);
                },
                err => globalStatus.text = "Login Failed: " + err
            );
        }

        private void ShowPortfolio(List<LTLM.SDK.Core.Models.LicenseData> licenses)
        {
            portfolioPanel.SetActive(true);
            
            // Clear previous items
            foreach (Transform child in portfolioContainer) Destroy(child.gameObject);

            foreach (var l in licenses)
            {
                var item = Instantiate(licenseItemPrefab, portfolioContainer);
                string lType = (l.policy != null) ? l.policy.type : "Unknown";
                item.GetComponentInChildren<Text>().text = $"{lType} - {l.licenseKey.Substring(0, 8)}...";
                item.GetComponent<Button>().onClick.AddListener(() => {
                    LTLMManager.Instance.ActivateLicense(l.licenseKey, 
                        (license, status) => {
                            globalStatus.text = "Portfolio License Activated!";
                            portfolioPanel.SetActive(false);
                        },
                        err => globalStatus.text = "Portfolio Error: " + err
                    );
                });
            }
        }
    }
}
