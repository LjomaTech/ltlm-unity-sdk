using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LTLM.SDK.Core.Security;
using UnityEngine;

namespace LTLM.SDK.Core.Storage
{
    /// <summary>
    /// Cross-platform secure marker storage for anti-tamper detection.
    /// Markers mirror the state of the encrypted vault so that deleting or editing vault files is detectable.
    ///
    /// <para><b>Backends:</b></para>
    /// <list type="bullet">
    ///   <item><b>Windows</b>: HKCU registry</item>
    ///   <item><b>macOS / Linux / Android / iOS</b>: HMAC-signed JSON file outside the vault folder</item>
    ///   <item><b>WebGL</b>: PlayerPrefs (no tamper detection possible)</item>
    /// </list>
    ///
    /// All reads are served from an in-memory cache after the first access; writes are write-through.
    /// </summary>
    public static class PlatformStorage
    {
        private static string _markerPath;
        private static bool _initialized;

        // projectId -> marker data (file backend cache)
        private static readonly Dictionary<string, MarkerData> _fileCache = new Dictionary<string, MarkerData>();
        // projectId -> tampered flag (file backend)
        private static readonly HashSet<string> _tamperedProjects = new HashSet<string>();
        // "projectId\nkey" -> value (registry / PlayerPrefs cache)
        private static readonly Dictionary<string, string> _kvCache = new Dictionary<string, string>();

        private enum Backend { Registry, PlayerPrefs, File }

        private static Backend _backend;

        private static void Initialize()
        {
            if (_initialized) return;

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    _backend = LTLMRegistryHelper.IsSupported ? Backend.Registry : Backend.File;
                    _markerPath = Path.Combine(Application.persistentDataPath, ".ltlm_markers");
                    break;

                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    _backend = Backend.File;
                    _markerPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                        "Library", "Application Support", "LTLM");
                    break;

                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    _backend = Backend.File;
                    string xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                    if (string.IsNullOrEmpty(xdgConfig))
                        xdgConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config");
                    _markerPath = Path.Combine(xdgConfig, "LTLM");
                    break;

                case RuntimePlatform.WebGLPlayer:
                    _backend = Backend.PlayerPrefs;
                    _markerPath = null;
                    LTLMLog.Warn("WebGL platform detected. Using PlayerPrefs for markers (no tamper detection available).");
                    break;

                default:
                    // Android, iOS, consoles, unknown: sandboxed persistentDataPath
                    _backend = Backend.File;
                    _markerPath = Path.Combine(Application.persistentDataPath, ".ltlm_markers");
                    break;
            }

            if (_backend == Backend.File && !string.IsNullOrEmpty(_markerPath) && !Directory.Exists(_markerPath))
            {
                try { Directory.CreateDirectory(_markerPath); }
                catch (Exception ex) { LTLMLog.Warn($"Failed to create marker directory: {ex.Message}"); }
            }

            _initialized = true;
        }

        /// <summary>Human readable backend name, for diagnostics.</summary>
        public static string BackendName
        {
            get
            {
                Initialize();
                return _backend.ToString();
            }
        }

        public static void SetMarker(string projectId, string key, string value)
        {
            Initialize();
            projectId = projectId ?? "global";

            switch (_backend)
            {
                case Backend.Registry:
                    LTLMRegistryHelper.SetMarker(projectId, key, value);
                    _kvCache[projectId + "\n" + key] = value;
                    return;
                case Backend.PlayerPrefs:
                    PlayerPrefs.SetString($"LTLM_{projectId}_{key}", value ?? string.Empty);
                    PlayerPrefs.Save();
                    _kvCache[projectId + "\n" + key] = value;
                    return;
                default:
                    SetFileMarker(projectId, key, value);
                    return;
            }
        }

        /// <summary>Returns the marker value, null if absent, or "TAMPERED" when the marker store failed integrity checks.</summary>
        public static string GetMarker(string projectId, string key)
        {
            Initialize();
            projectId = projectId ?? "global";

            switch (_backend)
            {
                case Backend.Registry:
                {
                    string cacheKey = projectId + "\n" + key;
                    if (_kvCache.TryGetValue(cacheKey, out var cached)) return cached;
                    string v = LTLMRegistryHelper.GetMarker(projectId, key);
                    _kvCache[cacheKey] = v;
                    return v;
                }
                case Backend.PlayerPrefs:
                {
                    string cacheKey = projectId + "\n" + key;
                    if (_kvCache.TryGetValue(cacheKey, out var cached)) return cached;
                    string ppKey = $"LTLM_{projectId}_{key}";
                    string v = PlayerPrefs.HasKey(ppKey) ? PlayerPrefs.GetString(ppKey) : null;
                    _kvCache[cacheKey] = v;
                    return v;
                }
                default:
                    return GetFileMarker(projectId, key);
            }
        }

        public static void RemoveMarker(string projectId, string key)
        {
            Initialize();
            projectId = projectId ?? "global";

            switch (_backend)
            {
                case Backend.Registry:
                    LTLMRegistryHelper.RemoveMarker(projectId, key);
                    _kvCache.Remove(projectId + "\n" + key);
                    return;
                case Backend.PlayerPrefs:
                    PlayerPrefs.DeleteKey($"LTLM_{projectId}_{key}");
                    PlayerPrefs.Save();
                    _kvCache.Remove(projectId + "\n" + key);
                    return;
                default:
                    RemoveFileMarker(projectId, key);
                    return;
            }
        }

        /// <summary>Clears all markers for the specified project.</summary>
        public static void ClearMarkers(string projectId)
        {
            Initialize();
            projectId = projectId ?? "global";

            switch (_backend)
            {
                case Backend.Registry:
                    LTLMRegistryHelper.Clear(projectId);
                    break;
                case Backend.PlayerPrefs:
                    // PlayerPrefs cannot be enumerated; drop the keys we know about from the cache.
                    foreach (var k in new List<string>(_kvCache.Keys))
                    {
                        if (!k.StartsWith(projectId + "\n")) continue;
                        PlayerPrefs.DeleteKey($"LTLM_{projectId}_{k.Substring(projectId.Length + 1)}");
                    }
                    PlayerPrefs.Save();
                    break;
                default:
                    try
                    {
                        string filePath = Path.Combine(_markerPath, $"{projectId}.markers");
                        if (File.Exists(filePath)) File.Delete(filePath);
                    }
                    catch (Exception ex) { LTLMLog.Warn($"Failed to clear markers: {ex.Message}"); }
                    _fileCache.Remove(projectId);
                    _tamperedProjects.Remove(projectId);
                    break;
            }

            var prefix = projectId + "\n";
            foreach (var k in new List<string>(_kvCache.Keys))
                if (k.StartsWith(prefix)) _kvCache.Remove(k);
        }

        #region File-Based Implementation

        private static MarkerData LoadFileData(string projectId)
        {
            if (_fileCache.TryGetValue(projectId, out var cached)) return cached;

            var data = new MarkerData();
            if (!string.IsNullOrEmpty(_markerPath))
            {
                try
                {
                    string filePath = Path.Combine(_markerPath, $"{projectId}.markers");
                    if (File.Exists(filePath))
                    {
                        string json = File.ReadAllText(filePath);
                        var loaded = JsonUtility.FromJson<MarkerData>(json);
                        if (loaded != null)
                        {
                            string expected = ComputeHMAC(loaded.GetDataString(), GetDeviceKey());
                            if (loaded.signature != expected)
                            {
                                LTLMLog.Error("SECURITY ALERT: Marker file has been tampered with!");
                                _tamperedProjects.Add(projectId);
                            }
                            data = loaded;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LTLMLog.Warn($"Failed to read marker file: {ex.Message}");
                }
            }

            _fileCache[projectId] = data;
            return data;
        }

        private static void SaveFileData(string projectId, MarkerData data)
        {
            if (string.IsNullOrEmpty(_markerPath)) return;
            try
            {
                data.signature = ComputeHMAC(data.GetDataString(), GetDeviceKey());
                string filePath = Path.Combine(_markerPath, $"{projectId}.markers");
                File.WriteAllText(filePath, JsonUtility.ToJson(data, false));
                // A freshly written file is by definition intact.
                _tamperedProjects.Remove(projectId);
            }
            catch (Exception ex)
            {
                LTLMLog.Warn($"Failed to write marker file: {ex.Message}");
            }
        }

        private static void SetFileMarker(string projectId, string key, string value)
        {
            var data = LoadFileData(projectId);
            data.SetValue(key, value ?? string.Empty);
            SaveFileData(projectId, data);
        }

        private static void RemoveFileMarker(string projectId, string key)
        {
            var data = LoadFileData(projectId);
            if (!data.Remove(key)) return;
            SaveFileData(projectId, data);
        }

        private static string GetFileMarker(string projectId, string key)
        {
            var data = LoadFileData(projectId);
            if (_tamperedProjects.Contains(projectId)) return LTLMConstants.TamperedMarker;
            return data.GetValue(key);
        }

        private static string ComputeHMAC(string data, string key)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string _deviceKey;

        private static string GetDeviceKey()
        {
            if (_deviceKey != null) return _deviceKey;
            string combinedKey = $"{SystemInfo.deviceUniqueIdentifier}_{Application.identifier}_{Application.companyName}";
            using (SHA256 sha256 = SHA256.Create())
            {
                _deviceKey = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedKey)));
            }
            return _deviceKey;
        }

        #endregion

        #region Marker Data Structure

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
                    return;
                }
                Array.Resize(ref keys, keys.Length + 1);
                Array.Resize(ref values, values.Length + 1);
                keys[keys.Length - 1] = key;
                values[values.Length - 1] = value;
            }

            public bool Remove(string key)
            {
                int index = Array.IndexOf(keys, key);
                if (index < 0) return false;
                var newKeys = new List<string>(keys);
                var newValues = new List<string>(values);
                newKeys.RemoveAt(index);
                newValues.RemoveAt(index);
                keys = newKeys.ToArray();
                values = newValues.ToArray();
                return true;
            }

            public string GetValue(string key)
            {
                int index = Array.IndexOf(keys, key);
                return index >= 0 ? values[index] : null;
            }

            public string GetDataString()
            {
                var sb = new StringBuilder();
                for (int i = 0; i < keys.Length; i++)
                    sb.Append(keys[i]).Append('=').Append(values[i]).Append(';');
                return sb.ToString();
            }
        }

        #endregion
    }
}
