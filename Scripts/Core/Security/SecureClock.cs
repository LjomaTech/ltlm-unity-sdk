using System;
using LTLM.SDK.Core.Storage;
using LTLM.SDK.Core.Hardware;

namespace LTLM.SDK.Core.Security
{
    /// <summary>
    /// Monotonic secure clock that prevents license bypass via system clock rollback.
    /// Keeps a "last known good time" watermark in the encrypted vault and refuses to move backwards.
    /// The watermark is persisted at most once per minute to avoid hammering the disk.
    /// </summary>
    public static class SecureClock
    {
        private const string STORE_KEY = "last_known_secure_time";
        private static readonly TimeSpan PersistInterval = TimeSpan.FromMinutes(1);

        private static DateTime _lastPersisted = DateTime.MinValue;
        private static DateTime _memoryWatermark = DateTime.MinValue;
        private static string _loadedForProject;

        /// <summary>
        /// Gets the most reliable current time (UTC).
        /// If the system clock has been rolled back, returns the last known good time instead.
        /// </summary>
        public static DateTime GetEffectiveTime(string projectId)
        {
            DateTime systemTime = DateTime.UtcNow;
            DateTime storedTime = GetLastKnownTime(projectId);

            if (systemTime < storedTime)
            {
                LTLMLog.Warn("CLOCK TAMPERING DETECTED. System clock is earlier than last known secure time.");
                return storedTime;
            }

            UpdateLastKnownTime(projectId, systemTime);
            return systemTime;
        }

        /// <summary>True when the system clock is behind the persisted watermark.</summary>
        public static bool IsClockTampered(string projectId)
        {
            return DateTime.UtcNow < GetLastKnownTime(projectId);
        }

        private static DateTime GetLastKnownTime(string projectId)
        {
            if (_loadedForProject == projectId && _memoryWatermark != DateTime.MinValue)
                return _memoryWatermark;

            string key = $"{STORE_KEY}_{projectId}";
            string raw = SecureStorage.Load(key, DeviceID.GetHWID(), projectId);

            _loadedForProject = projectId;
            if (long.TryParse(raw, out long ticks))
                _memoryWatermark = new DateTime(ticks, DateTimeKind.Utc);
            else
                _memoryWatermark = DateTime.MinValue;

            return _memoryWatermark;
        }

        private static void UpdateLastKnownTime(string projectId, DateTime current)
        {
            if (current <= _memoryWatermark) return;
            _memoryWatermark = current;
            _loadedForProject = projectId;

            if (current - _lastPersisted < PersistInterval) return;
            _lastPersisted = current;

            string key = $"{STORE_KEY}_{projectId}";
            SecureStorage.Save(key, current.Ticks.ToString(), DeviceID.GetHWID(), projectId);
        }

        /// <summary>Forces the watermark to be written now (called on quit / sign-out).</summary>
        public static void Flush(string projectId)
        {
            if (_memoryWatermark == DateTime.MinValue) return;
            string key = $"{STORE_KEY}_{projectId}";
            SecureStorage.Save(key, _memoryWatermark.Ticks.ToString(), DeviceID.GetHWID(), projectId);
            _lastPersisted = _memoryWatermark;
        }
    }
}
