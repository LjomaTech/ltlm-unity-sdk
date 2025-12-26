using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;

namespace LTLM.SDK.Demos
{
    public class UsageBasedDemo : MonoBehaviour
    {
        public Text statusText;
        public Text tokenBalanceText;
        public Button exportButton;
        public Button topUpButton;

        private void Start()
        {
            exportButton.onClick.AddListener(OnExportClicked);
            if (topUpButton != null) topUpButton.onClick.AddListener(OnTopUpClicked);
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (LTLMManager.Instance.IsAuthenticated)
            {
                statusText.text = "License Active: " + LTLMManager.Instance.ActiveLicense.licenseKey;
                if (topUpButton != null) topUpButton.gameObject.SetActive(true);
                
                LTLMManager.Instance.DoesHaveTokens(1, (hasTokens) => {
                    tokenBalanceText.text = hasTokens ? $"Tokens: {LTLMManager.Instance.ActiveLicense.tokensRemaining}" : "Refill required!";
                    exportButton.interactable = hasTokens;
                });
            }
            else
            {
                statusText.text = "Not Authenticated";
                exportButton.interactable = false;
                if (topUpButton != null) topUpButton.gameObject.SetActive(false);
            }
        }

        private void OnTopUpClicked()
        {
            statusText.text = "Generating Topup Link...";
            LTLMManager.Instance.CreateTopUpSession("pack_1000_tokens", "https://ltlm.com/payment-success", 
                url => {
                    statusText.text = "Opening Checkout...";
                    Application.OpenURL(url);
                },
                err => statusText.text = "Topup Error: " + err
            );
        }

        private void OnExportClicked()
        {
            statusText.text = "Checking tokens...";
            
            LTLMManager.Instance.DoesHaveTokens(10, (canExport) => {
                if (canExport)
                {
                    statusText.text = "Processing Export...";
                    LTLMManager.Instance.ConsumeTokens(10, "VideoExport", (license) => {
                        statusText.text = "Export Successful!";
                        tokenBalanceText.text = $"Tokens: {license.tokensRemaining}";
                        
                        // Log a custom event
                        LTLMManager.Instance.LogEvent("ResourceExported", new System.Collections.Generic.Dictionary<string, object> {
                            { "type", "video" },
                            { "resolution", "4K" }
                        });
                    });
                }
                else
                {
                    statusText.text = "Insufficient tokens!";
                }
            });
        }
    }
}
