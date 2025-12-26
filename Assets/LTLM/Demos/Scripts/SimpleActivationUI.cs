using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;
using TMPro;

namespace LTLM.SDK.Demos
{
    /// <summary>
    /// A simple UI for license activation without login.
    /// Just enter a key and click activate.
    /// </summary>
    public class SimpleActivationUI : MonoBehaviour
    {
        public TMP_InputField licenseKeyInput;
        public Button activateButton;
        public TMP_Text statusText;

        private void Start()
        {
            activateButton.onClick.AddListener(OnActivateClicked);
            
            // Auto-check if we already have a license
            if (LTLMManager.Instance.IsAuthenticated)
            {
                statusText.text = "License currently active.";
            }
        }

        private void OnActivateClicked()
        {
            string key = licenseKeyInput.text;
            if (string.IsNullOrEmpty(key)) return;

            statusText.text = "Activating...";
            activateButton.interactable = false;

            LTLMManager.Instance.ActivateLicense(key, 
                (license, status) => {
                    statusText.text = "Activation Successful!";
                    activateButton.interactable = true;
                    Debug.Log($"[LTLM] Activated License: {license.licenseKey}. Status: {status}");
                },
                err => {
                    statusText.text = "Error: " + err;
                    activateButton.interactable = true;
                }
            );
        }
    }
}
