using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace LTLM.SDK.Hardware
{
    /// <summary>
    /// Generates a robust, stable HWID for the machine.
    /// Combines multiple hardware signals to prevent spoofing.
    /// </summary>
    public static class DeviceID
    {
        private static string _cachedHwid;

        public static string GetHWID()
        {
            if (!string.IsNullOrEmpty(_cachedHwid)) return _cachedHwid;

            StringBuilder sb = new StringBuilder();
            
            // 1. Processor information
            sb.Append(SystemInfo.processorType);
            sb.Append(SystemInfo.processorFrequency);
            sb.Append(SystemInfo.processorCount);

            // 2. Memory information
            sb.Append(SystemInfo.systemMemorySize);

            // 3. Graphics information (useful but can change with drivers, so use sparingly)
            sb.Append(SystemInfo.graphicsDeviceName);
            sb.Append(SystemInfo.graphicsMemorySize);

            // 4. Operating System
            sb.Append(SystemInfo.operatingSystem);

            // 5. Device Unique Identifier (Unity default, usually based on Mac/IDFV)
            sb.Append(SystemInfo.deviceUniqueIdentifier);

            // Hash the combined signals to create a fixed-length HWID
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                _cachedHwid = BitConverter.ToString(hash).Replace("-", "").ToLower();
            }

            return _cachedHwid;
        }

        public static string GetDeviceName()
        {
            return SystemInfo.deviceName;
        }

        public static string GetOS()
        {
            return SystemInfo.operatingSystem;
        }
    }
}
