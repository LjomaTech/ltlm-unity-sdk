using System;
using System.Reflection;
using UnityEngine;

namespace LTLM.SDK.Core.Security
{
    /// <summary>
    /// Windows Registry helper for anti-tamper markers (HKCU\Software\LTLM\{projectId}).
    ///
    /// The registry API is resolved through reflection so the SDK compiles under every Unity
    /// API compatibility level (.NET Standard profiles do not expose Microsoft.Win32.Registry at
    /// compile time, but the Mono runtime still ships it). When the API is unavailable
    /// <see cref="IsSupported"/> is false and <c>PlatformStorage</c> falls back to HMAC-signed files.
    /// </summary>
    public static class LTLMRegistryHelper
    {
        private const string BASE_PATH = @"Software\LTLM";

        private static bool _probed;
        private static object _currentUser;          // Microsoft.Win32.RegistryKey (HKCU)
        private static MethodInfo _createSubKey;     // RegistryKey CreateSubKey(string)
        private static MethodInfo _openSubKey;       // RegistryKey OpenSubKey(string, bool)
        private static MethodInfo _setValue;         // void SetValue(string, object)
        private static MethodInfo _getValue;         // object GetValue(string)
        private static MethodInfo _deleteValue;      // void DeleteValue(string, bool)
        private static MethodInfo _deleteSubKeyTree; // void DeleteSubKeyTree(string, bool)

        public static bool IsSupported
        {
            get
            {
                if (Application.platform != RuntimePlatform.WindowsPlayer &&
                    Application.platform != RuntimePlatform.WindowsEditor)
                    return false;
                Probe();
                return _currentUser != null;
            }
        }

        private static void Probe()
        {
            if (_probed) return;
            _probed = true;
            try
            {
                Type registry = Type.GetType("Microsoft.Win32.Registry, mscorlib")
                                ?? Type.GetType("Microsoft.Win32.Registry, Microsoft.Win32.Registry")
                                ?? Type.GetType("Microsoft.Win32.Registry");
                if (registry == null) return;

                var hkcuProp = registry.GetField("CurrentUser", BindingFlags.Public | BindingFlags.Static);
                _currentUser = hkcuProp?.GetValue(null);
                if (_currentUser == null) return;

                Type keyType = _currentUser.GetType();
                _createSubKey = keyType.GetMethod("CreateSubKey", new[] { typeof(string) });
                _openSubKey = keyType.GetMethod("OpenSubKey", new[] { typeof(string), typeof(bool) });
                _setValue = keyType.GetMethod("SetValue", new[] { typeof(string), typeof(object) });
                _getValue = keyType.GetMethod("GetValue", new[] { typeof(string) });
                _deleteValue = keyType.GetMethod("DeleteValue", new[] { typeof(string), typeof(bool) });
                _deleteSubKeyTree = keyType.GetMethod("DeleteSubKeyTree", new[] { typeof(string), typeof(bool) });

                if (_createSubKey == null || _openSubKey == null || _setValue == null || _getValue == null)
                    _currentUser = null;
            }
            catch (Exception ex)
            {
                LTLMLog.Warn("Registry API unavailable, using file markers: " + ex.Message);
                _currentUser = null;
            }
        }

        private static void Dispose(object key)
        {
            (key as IDisposable)?.Dispose();
        }

        public static void SetMarker(string projectId, string key, string value)
        {
            if (!IsSupported) return;
            object sub = null;
            try
            {
                sub = _createSubKey.Invoke(_currentUser, new object[] { $@"{BASE_PATH}\{projectId}" });
                if (sub != null) _setValue.Invoke(sub, new object[] { key, value ?? string.Empty });
            }
            catch (Exception ex)
            {
                LTLMLog.Warn($"Registry Marker Error: {ex.Message}");
            }
            finally
            {
                Dispose(sub);
            }
        }

        public static string GetMarker(string projectId, string key)
        {
            if (!IsSupported) return null;
            object sub = null;
            try
            {
                sub = _openSubKey.Invoke(_currentUser, new object[] { $@"{BASE_PATH}\{projectId}", false });
                if (sub == null) return null;
                return _getValue.Invoke(sub, new object[] { key }) as string;
            }
            catch (Exception ex)
            {
                LTLMLog.Warn($"Registry Access Error: {ex.Message}");
                return null;
            }
            finally
            {
                Dispose(sub);
            }
        }

        public static void RemoveMarker(string projectId, string key)
        {
            if (!IsSupported || _deleteValue == null) return;
            object sub = null;
            try
            {
                sub = _openSubKey.Invoke(_currentUser, new object[] { $@"{BASE_PATH}\{projectId}", true });
                if (sub != null) _deleteValue.Invoke(sub, new object[] { key, false });
            }
            catch (Exception ex)
            {
                LTLMLog.Warn($"Registry Remove Error: {ex.Message}");
            }
            finally
            {
                Dispose(sub);
            }
        }

        public static void Clear(string projectId)
        {
            if (!IsSupported || _deleteSubKeyTree == null) return;
            try
            {
                _deleteSubKeyTree.Invoke(_currentUser, new object[] { $@"{BASE_PATH}\{projectId}", false });
            }
            catch { }
        }
    }
}
