using UnityEngine;

namespace LTLM.SDK.Unity
{
    /// <summary>
    /// Creates the <see cref="LTLMManager"/> singleton at startup if none exists in the scene.
    /// Attach this to any GameObject in your first scene (or add the LTLMManager prefab directly).
    /// Keys left empty here are read from <c>Resources/LTLMSettings</c>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LTLMBootstrap : MonoBehaviour
    {
        [Header("SDK Configuration (optional - overrides LTLMSettings)")]
        public string projectId;
        public string publicKey;
        [Tooltip("64-char hex secret. Prefer LTLMSettings so the value is not visible in the scene file.")]
        public string secretKey;

        [Header("Behaviour")]
        public bool autoValidateOnStart = true;
        public bool allowOfflineTokenConsumption = true;

        private void Awake()
        {
            if (LTLMManager.Instance != null) return;

            // Create inactive so the inspector values are applied before LTLMManager.Awake runs.
            GameObject go = new GameObject("LTLM_Provider");
            go.SetActive(false);
            var manager = go.AddComponent<LTLMManager>();

            if (!string.IsNullOrEmpty(projectId)) manager.projectId = projectId;
            if (!string.IsNullOrEmpty(publicKey)) manager.publicKey = publicKey;
            if (!string.IsNullOrEmpty(secretKey)) manager.secretKey = secretKey;
            manager.autoValidateOnStart = autoValidateOnStart;
            manager.allowOfflineTokenConsumption = allowOfflineTokenConsumption;

            go.SetActive(true);
            DontDestroyOnLoad(go);
            Debug.Log("[LTLM] SDK bootstrapped.");
        }
    }
}
