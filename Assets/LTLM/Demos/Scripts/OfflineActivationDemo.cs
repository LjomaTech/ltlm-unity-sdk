using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Core.Security;
using LTLM.SDK.Core.Models;
using LTLM.SDK.Core.Storage;
using LTLM.SDK.Unity.Hardware;
using System.IO;
using System;
using LTLM.SDK.Unity;

namespace LTLM.SDK.Demos
{
    /// <summary>
    /// Demonstrates Offline (Air-Gapped) Activation.
    /// User provides an encrypted and signed .ltlm file, and the SDK verifies it locally.
    /// </summary>
    public class OfflineActivationDemo : MonoBehaviour
    {
        public Text statusText;
        public Button selectFileButton;

        private void Start()
        {
            if (selectFileButton != null)
                selectFileButton.onClick.AddListener(OnSelectFileClicked);
        }

        private void OnSelectFileClicked()
        {
            // In a real Unity app, you might use a File Browser plugin (e.g., SimpleFileBrowser)
            // For this demo, we assume the file is at a known path or passed via a mock.
            string mockPath = Path.Combine(Application.persistentDataPath, "license.ltlm");
            
            if (File.Exists(mockPath))
            {
                ProcessOfflineFile(mockPath);
            }
            else
            {
                statusText.text = "Mock file 'license.ltlm' not found in PersistentDataPath.";
                Debug.LogWarning("[LTLM] Please place an encrypted .ltlm file in: " + Application.persistentDataPath);
            }
        }

        public void ProcessOfflineFile(string filePath)
        {
            statusText.text = "Reading offline license...";
            
            try
            {
                // The .ltlm file contains the "Triple-Wrapped" blob (Encrypted Signed JSON)
                string encryptedBlob = File.ReadAllText(filePath);

                LTLMManager.Instance.ActivateOffline(encryptedBlob, 
                    (license, status) => {
                        statusText.text = "Offline Activation Success!\nBound to: " + license.licenseKey;
                        Debug.Log($"[LTLM] Offline License Verified. Status: {status}. Expiry: {license.validUntil}");
                    },
                    err => {
                        statusText.text = "Offline Activation Failed:\n" + err;
                    }
                );
            }
            catch (Exception ex)
            {
                statusText.text = "IO Error: " + ex.Message;
            }
        }
    }
}
