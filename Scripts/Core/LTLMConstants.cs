namespace LTLM.SDK.Core
{
    public static class LTLMConstants
    {
        /// <summary>
        /// The default LTLM backend URL. Can be overridden per project through
        /// <see cref="LTLMSettings.backendUrlOverride"/> (self-hosted / staging deployments).
        /// </summary>
        public const string BackendUrl = "https://ltlm.ljomatech.com/api";

        /// <summary>
        /// Current SDK Version (sent to the server as the "version" field).
        /// </summary>
        public const string Version = "1.3.0-PRO";

        /// <summary>Default HTTP timeout for SDK requests.</summary>
        public const int DefaultRequestTimeoutSeconds = 30;

        /// <summary>Retry interval used by the heartbeat loop while the device is offline.</summary>
        public const float OfflineRetryIntervalSeconds = 60f;

        /// <summary>Hard upper bound for user settings payload (mirrors the server limit).</summary>
        public const int MaxUserSettingsBytes = 65536;

        /// <summary>Marker value returned by storage when integrity verification fails.</summary>
        public const string TamperedMarker = "TAMPERED";
    }
}
