using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using LTLM.SDK.Core.Security;

namespace LTLM.SDK.Core.Storage
{
    /// <summary>
    /// Handles encrypted file storage in PersistentDataPath.
    /// Data is encrypted using a machine-specific key to prevent license sharing.
    /// </summary>
    public static class SecureStorage
    {
        private static string StoragePath => Path.Combine(Application.persistentDataPath, "ltlm_vault");

        static SecureStorage()
        {
            if (!Directory.Exists(StoragePath))
            {
                Directory.CreateDirectory(StoragePath);
            }
        }

        /// <summary>
        /// Saves encrypted data to a file with anti-tamper markers.
        /// Data is encrypted using a machine-specific key (derived from HWID).
        /// 
        /// <para><b>Security Features:</b></para>
        /// <list type="bullet">
        ///   <item>AES-256-CBC encryption with HWID-derived key</item>
        ///   <item>Cross-platform tamper detection via PlatformStorage</item>
        ///   <item>MD5 hash verification on load</item>
        /// </list>
        /// </summary>
        /// <param name="fileName">The name of the file to save (without path).</param>
        /// <param name="plainText">The plaintext data to encrypt and save.</param>
        /// <param name="machineKey">The machine-specific key (usually HWID).</param>
        /// <param name="projectId">Optional project ID for marker isolation. Defaults to "global".</param>
        public static void Save(string fileName, string plainText, string machineKey, string projectId = "global")
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                byte[] key = DeriveKey(machineKey);
                byte[] encrypted = Encrypt(data, key);
                
                File.WriteAllBytes(Path.Combine(StoragePath, fileName), encrypted);

                // Anti-Tamper: Use cross-platform PlatformStorage for markers
                string hash = HashHelper.ComputeMD5(encrypted);
                PlatformStorage.SetMarker(projectId, fileName + "_hash", hash);
                PlatformStorage.SetMarker(projectId, fileName + "_exists", "true");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LTLM] Storage Save Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads and decrypts data from a file.
        /// Returns "TAMPERED" if file integrity check fails (deletion or modification detected).
        /// </summary>
        /// <param name="fileName">The name of the file to load (without path).</param>
        /// <param name="machineKey">The machine-specific key (usually HWID).</param>
        /// <param name="projectId">Optional project ID for marker isolation. Defaults to "global".</param>
        /// <returns>The decrypted plaintext, "TAMPERED" if tampered, or null if not found.</returns>
        public static string Load(string fileName, string machineKey, string projectId = "global")
        {
            try
            {
                string path = Path.Combine(StoragePath, fileName);
                
                // Anti-Tamper Check using cross-platform PlatformStorage
                if (HasBeenTampered(fileName, projectId))
                {
                    Debug.LogError($"[LTLM] SECURITY ALERT: Local file '{fileName}' has been tampered with or deleted!");
                    return "TAMPERED";
                }

                if (!File.Exists(path)) return null;

                byte[] encrypted = File.ReadAllBytes(path);
                byte[] key = DeriveKey(machineKey);
                byte[] decrypted = Decrypt(encrypted, key);

                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LTLM] Storage Load Error (Corrupt or Key Mismatch): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if a stored file has been tampered with (deleted or modified).
        /// Uses cross-platform PlatformStorage for marker verification.
        /// </summary>
        private static bool HasBeenTampered(string fileName, string projectId)
        {
            string path = Path.Combine(StoragePath, fileName);
            
            // Use cross-platform PlatformStorage for marker retrieval
            string markerExists = PlatformStorage.GetMarker(projectId, fileName + "_exists");
            string markerHash = PlatformStorage.GetMarker(projectId, fileName + "_hash");

            // If markers themselves indicate tampering (from PlatformStorage HMAC check)
            if (markerExists == "TAMPERED" || markerHash == "TAMPERED") return true;

            bool fileExists = File.Exists(path);

            // 1. Deletion Check: If markers say it should exist but file is gone
            if (markerExists == "true" && !fileExists) return true;

            // 2. Content Check: If file exists, verify MD5 against stored marker
            if (fileExists && !string.IsNullOrEmpty(markerHash))
            {
                byte[] encrypted = File.ReadAllBytes(path);
                string currentHash = HashHelper.ComputeMD5(encrypted);
                if (currentHash != markerHash) return true;
            }

            return false;
        }

        public static void Delete(string fileName)
        {
            string path = Path.Combine(StoragePath, fileName);
            if (File.Exists(path)) File.Delete(path);
        }

        public static bool Exists(string fileName)
        {
            return File.Exists(Path.Combine(StoragePath, fileName));
        }

        #region Encryption Logic

        private static byte[] DeriveKey(string machineKey)
        {
            // Simple key derivation from HWID/MachineKey
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(machineKey));
            }
        }

        private static byte[] Encrypt(byte[] data, byte[] key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);
                    byte[] result = new byte[aes.IV.Length + encrypted.Length];
                    Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
                    Array.Copy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
                    return result;
                }
            }
        }

        private static byte[] Decrypt(byte[] data, byte[] key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                byte[] iv = new byte[16];
                Array.Copy(data, 0, iv, 0, 16);
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(data, 16, data.Length - 16);
                }
            }
        }

        #endregion
    }
}
