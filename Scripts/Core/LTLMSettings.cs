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

        [Header("Network")]
        [Tooltip("Leave empty to use the default LTLM cloud backend. Set for self-hosted or staging deployments.")]
        public string backendUrlOverride;

        [Tooltip("HTTP timeout (seconds) for every SDK request.")]
        public int requestTimeoutSeconds = LTLMConstants.DefaultRequestTimeoutSeconds;

        [Header("Diagnostics")]
        [Tooltip("Log request/response bodies. NEVER enable in production builds - it prints license data to the log.")]
        public bool verboseLogging = false;

        [Tooltip("Reject signed responses whose server timestamp is older than this many seconds (0 = disabled). " +
                 "Protects against replayed responses but requires a reasonably accurate system clock.")]
        public int maxResponseAgeSeconds = 0;

        [Header("Project Metadata (Read-Only)")]
        public System.Collections.Generic.List<string> capabilities = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> analyticsEvents = new System.Collections.Generic.List<string>();

        /// <summary>Resolved backend URL (override or default), without a trailing slash.</summary>
        public string ResolvedBackendUrl =>
            string.IsNullOrWhiteSpace(backendUrlOverride)
                ? LTLMConstants.BackendUrl
                : backendUrlOverride.Trim().TrimEnd('/');

        public static LTLMSettings Load()
        {
            var settings = Resources.Load<LTLMSettings>("LTLMSettings");
            if (settings == null)
            {
                Debug.LogWarning("[LTLM] Settings not found in Resources/LTLMSettings. Please configure in LTLM -> Project Settings.");
            }
            return settings;
        }
    }
}
