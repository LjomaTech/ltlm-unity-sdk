using UnityEngine;

namespace LTLM.SDK.Core
{
    /// <summary>
    /// ScriptableObject to store project-specific settings.
    /// This is used by the LTLMManager and injected by the Editor tools.
    /// </summary>
    [CreateAssetMenu(fileName = "LTLMSettings", menuName = "LTLM/Settings")]
    public class LTLMSettings : ScriptableObject
    {
        public string projectId;
        public string projectName;
        public string publicKey;
        [Tooltip("Keep this secret! Usually injected at build time for PRO apps.")]
        public string secretKey;

        [Header("Project Metadata (Read-Only)")]
        public System.Collections.Generic.List<string> capabilities = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> analyticsEvents = new System.Collections.Generic.List<string>();

        public static LTLMSettings Load()
        {
            var settings = Resources.Load<LTLMSettings>("LTLMSettings");
            if (settings == null)
            {
                // Fallback or create in Resources if in Editor
                Debug.LogWarning("[LTLM] Settings not found in Resources/LTLMSettings. Please configure in LTLM -> Project Settings.");
            }
            return settings;
        }
    }
}
