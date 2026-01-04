using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;
using System.Collections.Generic;
using LTLM.SDK.Core.Models;

namespace LTLM.SDK.Demos
{
    public class CustomerPortalUI : MonoBehaviour
    {
        public InputField emailInput;
        public InputField otpInput;
        public GameObject loginPanel;
        public GameObject otpPanel;
        public GameObject portfolioPanel;
        public Transform licenseListContainer;
        public GameObject licenseItemPrefab;


        private void Start()
        {
            loginPanel.SetActive(true);
            otpPanel.SetActive(false);
            portfolioPanel.SetActive(false);
        }

        public void OnLoginClicked()
        {
            LTLMManager.Instance.RequestOTP(emailInput.text, 
                () => {
                    loginPanel.SetActive(false);
                    otpPanel.SetActive(true);
                },
                err => Debug.LogError("OTP Request Failed: " + err)
            );
        }

        public void OnVerifyClicked()
        {
            LTLMManager.Instance.VerifyOTP(emailInput.text, otpInput.text,
                licenses => {
                    otpPanel.SetActive(false);
                    portfolioPanel.SetActive(true);
                    PopulatePortfolio(licenses);
                },
                err => Debug.LogError("OTP Verification Failed: " + err)
            );
        }

        private void PopulatePortfolio(List<LicenseData> licenses)
        {
            foreach (var license in licenses)
            {
                // Instantiate licenseItemPrefab and set its data
                Debug.Log($"[LTLM] Found License: {license.licenseKey} ({license.status})");
            }
        }
    }
}
