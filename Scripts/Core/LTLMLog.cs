using UnityEngine;

namespace LTLM.SDK.Core
{
    /// <summary>
    /// Central logging helper. Verbose output (request/response bodies) is off by default
    /// so license data never leaks into player logs unless explicitly enabled.
    /// </summary>
    public static class LTLMLog
    {
        private const string Prefix = "[LTLM] ";

        /// <summary>Enable to print request/response payloads. Do not ship builds with this on.</summary>
        public static bool Verbose { get; set; } = false;

        public static void Info(string message) => Debug.Log(Prefix + message);
        public static void Warn(string message) => Debug.LogWarning(Prefix + message);
        public static void Error(string message) => Debug.LogError(Prefix + message);

        public static void Trace(string message)
        {
            if (Verbose) Debug.Log(Prefix + "[verbose] " + message);
        }
    }
}
