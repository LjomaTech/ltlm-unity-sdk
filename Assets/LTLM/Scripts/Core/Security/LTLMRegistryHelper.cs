using System;
using Microsoft.Win32;
using UnityEngine;

namespace LTLM.SDK.Core.Security
{
    /// <summary>
    /// Professional Windows Registry helper for anti-tamper markers.
    /// Stores mirrors of license state to detect local file manipulation.
    /// </summary>
    public static class LTLMRegistryHelper
    {
        private const string BASE_PATH = @"Software\LTLM";

        public static void SetMarker(string projectId, string key, string value)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor) return;

            try
            {
                using (var root = Registry.CurrentUser.CreateSubKey($@"{BASE_PATH}\{projectId}"))
                {
                    if (root != null)
                    {
                        root.SetValue(key, value);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LTLM] Registry Marker Error: {ex.Message}");
            }
        }

        public static string GetMarker(string projectId, string key)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor) return null;

            try
            {
                using (var root = Registry.CurrentUser.OpenSubKey($@"{BASE_PATH}\{projectId}"))
                {
                    if (root != null)
                    {
                        return root.GetValue(key) as string;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LTLM] Registry Access Error: {ex.Message}");
            }
            return null;
        }

        public static void Clear(string projectId)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor) return;

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"{BASE_PATH}\{projectId}", false);
            }
            catch { }
        }
    }
}
