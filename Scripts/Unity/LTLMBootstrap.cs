using UnityEngine;
using LTLM.SDK.Unity;

namespace LTLM.SDK.Demos
{
    /// <summary>
    /// Clean initialization script for production environments.
    /// Attach this to a persistent GameObject in your first scene.
    /// </summary>
    public class LTLMBootstrap : MonoBehaviour
    {
        [Header("SDK Configuration")]
        public string projectId;
        public string publicKey;

        private void Awake()
        {
            // If LTLMManager is already in the scene (e.g. from a prefab), skip manual init
            if (LTLMManager.Instance != null) return;

            GameObject go = new GameObject("LTLM_Provider");
            var manager = go.AddComponent<LTLMManager>();
            
            // Set keys if provided in inspector
            if (!string.IsNullOrEmpty(projectId)) manager.projectId = projectId;
            if (!string.IsNullOrEmpty(publicKey)) manager.publicKey = publicKey;
            
            DontDestroyOnLoad(go);
            Debug.Log("[LTLM] SDK Bootstrapped and ready.");
        }
    }
}
