using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;

namespace LTLM.SDK.Demos
{
    /// <summary>
    /// Demonstrates high-level feature gating using IsEntitled.
    /// This pattern is DLL-safe as it only uses public manager APIs.
    /// </summary>
    public class FeatureEntitlementDemo : MonoBehaviour
    {
        [Header("UI References")]
        public Text statusText;
        public GameObject premiumFeaturePanel;
        public Button accessButton;

        [Header("Gating Settings")]
        public string requiredCapability = "premium_access";
        public int requiredTokens = 10;

        private void Update()
        {
            // Update UI based on real-time entitlement
            UpdateEntitlementUI();
        }

        private void UpdateEntitlementUI()
        {
            if (LTLMManager.Instance == null) return;

            // Simple, single-line entitlement check
            bool canAccess = LTLMManager.Instance.IsEntitled(requiredCapability, requiredTokens);
            
            premiumFeaturePanel.SetActive(canAccess);
            accessButton.interactable = canAccess;

            // Provide detailed status feedback
            var currentStatus = LTLMManager.Instance.GetLicenseStatus();
            int days = LTLMManager.Instance.GetDaysRemaining();
            int tokens = LTLMManager.Instance.GetTokenBalance();

            string statusMsg = $"License Status: {currentStatus}\n";
            statusMsg += $"Days Remaining: {(days == -1 ? "Lifetime" : days.ToString())}\n";
            statusMsg += $"Tokens: {tokens}";

            if (statusText != null) statusText.text = statusMsg;
        }

        public void OnAccessButtonClicked()
        {
            Debug.Log("[LTLM Demo] Proceeding with Premium Action!");
        }
    }
}
