using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LTLM.SDK.Core.Security;
using UnityEngine;

namespace LTLM.SDK.Core.Storage
{
    /// <summary>
    /// Cross-platform secure marker storage for anti-tamper detection.
    /// Provides platform-specific implementations for storing integrity markers.
    /// 
    /// <para><b>Supported Platforms:</b></para>
    /// <list type="bullet">
    ///   <item><b>Windows</b>: Registry-based storage (most secure)</item>
    ///   <item><b>macOS</b>: Application Support file with HMAC signature</item>
    ///   <item><b>Linux</b>: XDG config directory with HMAC signature</item>
    ///   <item><b>Android</b>: Internal storage file with HMAC signature</item>
    ///   <item><b>iOS</b>: Application Support file with HMAC signature</item>
    ///   <item><b>WebGL</b>: PlayerPrefs fallback (no tamper detection)</item>
    /// </list>
    /// </summary>
    public static class PlatformStorage
    {
        private static string _markerPath;
        private static bool _initialized = false;

        /// <summary>
        /// Initializes the platform storage system. Called automatically on first use.
        /// </summary>
        private static void Initialize()
        {
            if (_initialized) return;

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    // Windows uses registry via LTLMRegistryHelper - no file path needed
                    _markerPath = null;
                    break;

                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    // macOS: ~/Library/Application Support/LTLM/
                    _markerPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                        "Library", "Application Support", "LTLM"
                    );
                    break;

                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    // Linux: ~/.config/LTLM/ (XDG_CONFIG_HOME fallback)
                    string xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                    if (string.IsNullOrEmpty(xdgConfig))
                    {
                        xdgConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config");
                    }
                    _markerPath = Path.Combine(xdgConfig, "LTLM");
                    break;

                case RuntimePlatform.Android:
                    // Android: Application.persistentDataPath is already sandboxed
                    _markerPath = Path.Combine(Application.persistentDataPath, ".ltlm_markers");
                    break;

                case RuntimePlatform.IPhonePlayer:
                    // iOS: Documents directory
                    _markerPath = Path.Combine(Application.persistentDataPath, ".ltlm_markers");
                    break;

                case RuntimePlatform.WebGLPlayer:
                    // WebGL: No file access, will use PlayerPrefs
                    _markerPath = null;
                    Debug.LogWarning("[LTLM] WebGL platform detected. Using PlayerPrefs for markers (no tamper detection available).");
                    break;

                default:
                    // Fallback for unknown platforms
                    _markerPath = Path.Combine(Application.persistentDataPath, ".ltlm_markers");
                    break;
            }

            // Create directory if needed
            if (!string.IsNullOrEmpty(_markerPath) && !Directory.Exists(_markerPath))
            {
                try
                {
                    Directory.CreateDirectory(_markerPath);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LTLM] Failed to create marker directory: {ex.Message}");
                }
            }

            _initialized = true;
        }

        /// <summary>
        /// Stores a security marker for the specified project.
        /// </summary>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="key">The marker key (e.g., "license_cache_hash").</param>
        /// <param name="value">The marker value.</param>
        public static void SetMarker(string projectId, string key, string value)
        {
            Initialize();

            // Windows: Use registry
            if (Application.platform == RuntimePlatform.WindowsPlayer || 
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                LTLMRegistryHelper.SetMarker(projectId, key, value);
                return;
            }

            // WebGL: Use PlayerPrefs
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                string ppKey = $"LTLM_{projectId}_{key}";
                PlayerPrefs.SetString(ppKey, value);
                PlayerPrefs.Save();
                return;
            }

            // All other platforms: File-based with HMAC signature
            SetFileMarker(projectId, key, value);
        }

        /// <summary>
        /// Retrieves a security marker for the specified project.
        /// </summary>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="key">The marker key.</param>
        /// <returns>The marker value, or null if not found or tampered.</returns>
        public static string GetMarker(string projectId, string key)
        {
            Initialize();

            // Windows: Use registry
            if (Application.platform == RuntimePlatform.WindowsPlayer || 
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                return LTLMRegistryHelper.GetMarker(projectId, key);
            }

            // WebGL: Use PlayerPrefs
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                string ppKey = $"LTLM_{projectId}_{key}";
                return PlayerPrefs.GetString(ppKey, null);
            }

            // All other platforms: File-based with HMAC verification
            return GetFileMarker(projectId, key);
        }

        /// <summary>
        /// Clears all markers for the specified project.
        /// </summary>
        /// <param name="projectId">The project identifier.</param>
        public static void ClearMarkers(string projectId)
        {
            Initialize();

            // Windows: Use registry
            if (Application.platform == RuntimePlatform.WindowsPlayer || 
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                LTLMRegistryHelper.Clear(projectId);
                return;
            }

            // WebGL: Clear PlayerPrefs (can't enumerate, so just clear known keys)
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                // Known marker keys
                string[] knownKeys = { "_hash", "_exists" };
                foreach (var suffix in knownKeys)
                {
                    PlayerPrefs.DeleteKey($"LTLM_{projectId}{suffix}");
                }
                PlayerPrefs.Save();
                return;
            }

            // All other platforms: Delete marker file
            try
            {
                string filePath = Path.Combine(_markerPath, $"{projectId}.markers");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LTLM] Failed to clear markers: {ex.Message}");
            }
        }

        #region File-Based Implementation (macOS, Linux, Android, iOS)

        /// <summary>
        /// Stores a marker in a file with HMAC signature for tamper detection.
        /// File format: JSON object with data and HMAC signature.
        /// </summary>
        private static void SetFileMarker(string projectId, string key, string value)
        {
            if (string.IsNullOrEmpty(_markerPath)) return;

            try
            {
                string filePath = Path.Combine(_markerPath, $"{projectId}.markers");
                MarkerData data;

                // Load existing data or create new
                if (File.Exists(filePath))
                {
                    string existingJson = File.ReadAllText(filePath);
                    data = JsonUtility.FromJson<MarkerData>(existingJson) ?? new MarkerData();
                }
                else
                {
                    data = new MarkerData();
                }

                // Update the specific key
                data.SetValue(key, value);

                // Recalculate HMAC for the entire data set
                data.signature = ComputeHMAC(data.GetDataString(), GetDeviceKey());

                // Save
                string json = JsonUtility.ToJson(data, false);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LTLM] Failed to set file marker: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a marker from file, verifying HMAC signature.
        /// Returns "TAMPERED" if signature verification fails.
        /// </summary>
        private static string GetFileMarker(string projectId, string key)
        {
            if (string.IsNullOrEmpty(_markerPath)) return null;

            try
            {
                string filePath = Path.Combine(_markerPath, $"{projectId}.markers");
                if (!File.Exists(filePath)) return null;

                string json = File.ReadAllText(filePath);
                MarkerData data = JsonUtility.FromJson<MarkerData>(json);

                if (data == null) return null;

                // Verify HMAC signature
                string expectedHmac = ComputeHMAC(data.GetDataString(), GetDeviceKey());
                if (data.signature != expectedHmac)
                {
                    Debug.LogError("[LTLM] SECURITY ALERT: Marker file has been tampered with!");
                    return "TAMPERED";
                }

                return data.GetValue(key);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LTLM] Failed to get file marker: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Computes HMAC-SHA256 for tamper detection.
        /// </summary>
        private static string ComputeHMAC(string data, string key)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Gets a device-specific key for HMAC signing.
        /// Combines multiple hardware signals to create a stable device fingerprint.
        /// </summary>
        private static string GetDeviceKey()
        {
            // Use Unity's device unique identifier combined with application ID
            string combinedKey = $"{SystemInfo.deviceUniqueIdentifier}_{Application.identifier}_{Application.companyName}";
            
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedKey));
                return Convert.ToBase64String(hash);
            }
        }

        #endregion

        #region Marker Data Structure

        /// <summary>
        /// Internal data structure for file-based marker storage.
        /// </summary>
        [Serializable]
        private class MarkerData
        {
            public string[] keys = new string[0];
            public string[] values = new string[0];
            public string signature;

            public void SetValue(string key, string value)
            {
                int index = Array.IndexOf(keys, key);
                if (index >= 0)
                {
                    values[index] = value;
                }
                else
                {
                    // Add new key-value pair
                    Array.Resize(ref keys, keys.Length + 1);
                    Array.Resize(ref values, values.Length + 1);
                    keys[keys.Length - 1] = key;
                    values[values.Length - 1] = value;
                }
            }

            public string GetValue(string key)
            {
                int index = Array.IndexOf(keys, key);
                return index >= 0 ? values[index] : null;
            }

            public string GetDataString()
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < keys.Length; i++)
                {
                    sb.Append(keys[i]).Append("=").Append(values[i]).Append(";");
                }
                return sb.ToString();
            }
        }

        #endregion
    }
}
