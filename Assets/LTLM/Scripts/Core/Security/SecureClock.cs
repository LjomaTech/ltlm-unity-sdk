using System;
using LTLM.SDK.Core.Storage;
using LTLM.SDK.Unity.Hardware;
using UnityEngine;

namespace LTLM.SDK.Core.Security
{
    /// <summary>
    /// Implements a Monotonic Secure Clock to prevent license bypass via system clock rollback.
    /// It keeps track of the 'Last Known Secure Time' in encrypted storage.
    /// </summary>
    public static class SecureClock
    {
        private const string STORE_KEY = "last_known_secure_time";

        /// <summary>
        /// Gets the most reliable current time. 
        /// If the system clock has been rolled back, it returns the last known good time.
        /// </summary>
        public static DateTime GetEffectiveTime(string projectId)
        {
            DateTime systemTime = DateTime.UtcNow;
            DateTime storedTime = GetLastKnownTime(projectId);

            if (systemTime < storedTime)
            {
                Debug.LogWarning("[LTLM] CLOCK TAMPERING DETECTED. System clock is earlier than last known secure time.");
                return storedTime; // Pessimistic drift protection
            }

            // Clock seems fine, update the watermark
            UpdateLastKnownTime(projectId, systemTime);
            return systemTime;
        }

        /// <summary>
        /// Checks if the system clock is currently suspicious compared to persistent logs.
        /// </summary>
        public static bool IsClockTampered(string projectId)
        {
            return DateTime.UtcNow < GetLastKnownTime(projectId);
        }

        private static DateTime GetLastKnownTime(string projectId)
        {
            string key = $"{STORE_KEY}_{projectId}";
            string raw = SecureStorage.Load(key, DeviceID.GetHWID(), projectId);
            
            if (long.TryParse(raw, out long ticks))
            {
                return new DateTime(ticks, DateTimeKind.Utc);
            }
            
            return DateTime.MinValue;
        }

        private static void UpdateLastKnownTime(string projectId, DateTime current)
        {
            string key = $"{STORE_KEY}_{projectId}";
            SecureStorage.Save(key, current.Ticks.ToString(), DeviceID.GetHWID(), projectId);
        }
    }
}
