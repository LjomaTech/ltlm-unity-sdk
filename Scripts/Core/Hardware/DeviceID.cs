using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace LTLM.SDK.Core.Hardware
{
    /// <summary>
    /// Generates a stable hardware identifier (HWID) for the current machine.
    ///
    /// <para><b>v2 formula (default since 1.3.0):</b> deviceUniqueIdentifier + processorType + processorCount.
    /// The original v1 formula also mixed in the OS version string, processor frequency and RAM size, which
    /// change after ordinary OS updates or hardware upgrades and caused licensed devices to lose their
    /// activation. <see cref="UseLegacyFormula"/> is switched on automatically by LTLMManager when it detects
    /// a vault that was written with the v1 HWID, so existing activations keep working.</para>
    /// </summary>
    public static class DeviceID
    {
        private static string _cachedHwid;
        private static string _cachedLegacyHwid;
        private static bool _useLegacy;

        /// <summary>
        /// When true, <see cref="GetHWID"/> returns the v1 (pre-1.3.0) fingerprint.
        /// Set by LTLMManager during vault migration; you normally never touch this.
        /// </summary>
        public static bool UseLegacyFormula
        {
            get => _useLegacy;
            set
            {
                if (_useLegacy == value) return;
                _useLegacy = value;
                _cachedHwid = null;
            }
        }

        public static string GetHWID()
        {
            if (!string.IsNullOrEmpty(_cachedHwid)) return _cachedHwid;
            _cachedHwid = _useLegacy ? GetLegacyHWID() : ComputeV2();
            return _cachedHwid;
        }

        /// <summary>The v1 fingerprint (SDK &lt; 1.3.0). Exposed for migration only.</summary>
        public static string GetLegacyHWID()
        {
            if (!string.IsNullOrEmpty(_cachedLegacyHwid)) return _cachedLegacyHwid;

            var sb = new StringBuilder();
            sb.Append(SystemInfo.processorType);
            sb.Append(SystemInfo.processorFrequency);
            sb.Append(SystemInfo.processorCount);
            sb.Append(SystemInfo.systemMemorySize);
            sb.Append(SystemInfo.operatingSystem);
            sb.Append(SystemInfo.deviceUniqueIdentifier);
            _cachedLegacyHwid = Sha256Hex(sb.ToString());
            return _cachedLegacyHwid;
        }

        private static string ComputeV2()
        {
            var sb = new StringBuilder();
            sb.Append("ltlm-hwid-v2|");
            sb.Append(SystemInfo.deviceUniqueIdentifier).Append('|');
            sb.Append(SystemInfo.processorType).Append('|');
            sb.Append(SystemInfo.processorCount);
            return Sha256Hex(sb.ToString());
        }

        private static string Sha256Hex(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string GetDeviceName() => SystemInfo.deviceName;

        public static string GetOS() => SystemInfo.operatingSystem;

        /// <summary>Short platform tag sent in activation metadata (e.g. "windows", "android").</summary>
        public static string GetPlatformTag()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor: return "windows";
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor: return "macos";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor: return "linux";
                case RuntimePlatform.Android: return "android";
                case RuntimePlatform.IPhonePlayer: return "ios";
                case RuntimePlatform.WebGLPlayer: return "webgl";
                default: return Application.platform.ToString().ToLowerInvariant();
            }
        }
    }
}
