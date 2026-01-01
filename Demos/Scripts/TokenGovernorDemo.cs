using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;
using System.Collections.Generic;

namespace LTLM.SDK.Demos
{
    /// <summary>
    /// Demonstrates usage-based token consumption and governance.
    /// </summary>
    public class TokenGovernorDemo : MonoBehaviour
    {
        public Text balanceText;
        public Button performActionButton;
        public Text feedbackText;

        [Header("Settings")]
        public string actionName = "render_export";
        public int costPerAction = 5;

        private void Start()
        {
            UpdateUI();
        }

        public void OnPerformActionTriggered()
        {
            if (LTLMManager.Instance == null) return;

            // Check entitlement first (IsEntitled handles tokens + license validity)
            if (LTLMManager.Instance.IsEntitled(null, costPerAction))
            {
                feedbackText.text = "Processing action...";
                performActionButton.interactable = false;

                // Fire the consumption request to the backend
                LTLMManager.Instance.ConsumeTokens(costPerAction, actionName, 
                    success => {
                        feedbackText.text = $"Success! {costPerAction} tokens consumed.";
                        performActionButton.interactable = true;
                        UpdateUI();
                    },
                    err => {
                        feedbackText.text = "Error: " + err;
                        performActionButton.interactable = true;
                    }
                );
            }
            else
            {
                feedbackText.text = "Insufficient tokens or license issues.";
            }
        }

        private void UpdateUI()
        {
            if (LTLMManager.Instance == null) return;
            balanceText.text = "Tokens: " + LTLMManager.Instance.GetTokenBalance();
        }
    }
}
