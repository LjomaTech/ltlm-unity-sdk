using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;

namespace LTLM.SDK.Demos
{
    /// <summary>
    /// Demonstrates how a user can purchase a license or subscription
    /// directly from within the game/app.
    /// </summary>
    public class PurchaseUI : MonoBehaviour
    {
        public string targetPolicyId = "policy_pro_monthly";
        public InputField emailInput;
        public Button buyButton;
        public Text statusText;

        private void Start()
        {
            buyButton.onClick.AddListener(OnBuyClicked);
        }

        private void OnBuyClicked()
        {
            string email = emailInput.text;
            if (string.IsNullOrEmpty(email))
            {
                statusText.text = "Please enter an email.";
                return;
            }

            statusText.text = "Generating Checkout URL...";
            buyButton.interactable = false;

            // Generate a checkout link for the user
            LTLMManager.Instance.CreateCheckoutSession(
                targetPolicyId, 
                email, 
                "https://yourgame.com/purchase-complete", 
                url => {
                    statusText.text = "Opening secure payment window...";
                    buyButton.interactable = true;
                    
                    // Open the host's default web browser to handle the payment securely
                    Application.OpenURL(url);
                },
                err => {
                    statusText.text = "Checkout Error: " + err;
                    buyButton.interactable = true;
                }
            );
        }
    }
}
