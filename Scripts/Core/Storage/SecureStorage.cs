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
    /// Encrypted file vault in <c>persistentDataPath/ltlm_vault</c>.
    /// Data is encrypted with a machine-specific key so vault files cannot be copied between devices.
    ///
    /// <para><b>Integrity model:</b> every file has two markers in <see cref="PlatformStorage"/>
    /// (<c>{file}_exists</c> and <c>{file}_hash</c>). If a file that should exist is missing, or its
    /// content hash differs, <see cref="Load"/> returns <see cref="LTLMConstants.TamperedMarker"/>.
    /// <see cref="Delete"/> removes the markers as well, so a legitimate sign-out never looks like tampering.</para>
    ///
    /// <para>Decrypted values are cached in memory; disk is only touched on the first load, on save and on delete.</para>
    /// </summary>
    public static class SecureStorage
    {
        private static string StoragePath => Path.Combine(Application.persistentDataPath, "ltlm_vault");

        private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();
        private static bool _dirEnsured;

        private static void EnsureDirectory()
        {
            if (_dirEnsured) return;
            try
            {
                if (!Directory.Exists(StoragePath)) Directory.CreateDirectory(StoragePath);
                _dirEnsured = true;
            }
            catch (Exception ex)
            {
                LTLMLog.Error($"Cannot create vault directory: {ex.Message}");
            }
        }

        private static string CacheKey(string fileName, string projectId) => projectId + "\n" + fileName;

        /// <summary>
        /// Saves encrypted data to a file with anti-tamper markers.
        /// </summary>
        /// <param name="fileName">The name of the file to save (without path).</param>
        /// <param name="plainText">The plaintext data to encrypt and save.</param>
        /// <param name="machineKey">The machine-specific key (usually HWID).</param>
        /// <param name="projectId">Project ID used to scope the integrity markers. Defaults to "global".</param>
        public static void Save(string fileName, string plainText, string machineKey, string projectId = "global")
        {
            try
            {
                EnsureDirectory();
                byte[] data = Encoding.UTF8.GetBytes(plainText ?? string.Empty);
                byte[] key = DeriveKey(machineKey);
                byte[] encrypted = Encrypt(data, key);

                File.WriteAllBytes(Path.Combine(StoragePath, fileName), encrypted);

                string hash = HashHelper.ComputeMD5(encrypted);
                PlatformStorage.SetMarker(projectId, fileName + "_hash", hash);
                PlatformStorage.SetMarker(projectId, fileName + "_exists", "true");

                _cache[CacheKey(fileName, projectId)] = plainText;
            }
            catch (Exception ex)
            {
                LTLMLog.Error($"Storage Save Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads and decrypts data from a file.
        /// </summary>
        /// <returns>The decrypted plaintext, <see cref="LTLMConstants.TamperedMarker"/> if integrity failed, or null if not found.</returns>
        public static string Load(string fileName, string machineKey, string projectId = "global")
        {
            string cacheKey = CacheKey(fileName, projectId);
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

            string result = LoadFromDisk(fileName, machineKey, projectId);
            if (result != null && result != LTLMConstants.TamperedMarker)
                _cache[cacheKey] = result;
            return result;
        }

        /// <summary>
        /// Like <see cref="Load"/> but returns null instead of the tampered marker, and reports tampering separately.
        /// </summary>
        public static string TryLoad(string fileName, string machineKey, string projectId, out bool tampered)
        {
            string value = Load(fileName, machineKey, projectId);
            tampered = value == LTLMConstants.TamperedMarker;
            return tampered ? null : value;
        }

        private static string LoadFromDisk(string fileName, string machineKey, string projectId)
        {
            try
            {
                string path = Path.Combine(StoragePath, fileName);

                if (HasBeenTampered(fileName, projectId))
                {
                    LTLMLog.Error($"SECURITY ALERT: Local file '{fileName}' has been tampered with or deleted!");
                    return LTLMConstants.TamperedMarker;
                }

                if (!File.Exists(path)) return null;

                byte[] encrypted = File.ReadAllBytes(path);
                byte[] key = DeriveKey(machineKey);
                byte[] decrypted = Decrypt(encrypted, key);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                LTLMLog.Warn($"Storage Load Error for '{fileName}' (corrupt or key mismatch): {ex.Message}");
                return null;
            }
        }

        private static bool HasBeenTampered(string fileName, string projectId)
        {
            string path = Path.Combine(StoragePath, fileName);

            string markerExists = PlatformStorage.GetMarker(projectId, fileName + "_exists");
            string markerHash = PlatformStorage.GetMarker(projectId, fileName + "_hash");

            if (markerExists == LTLMConstants.TamperedMarker || markerHash == LTLMConstants.TamperedMarker) return true;

            bool fileExists = File.Exists(path);

            // Deletion check: markers say the file should exist but it is gone.
            if (markerExists == "true" && !fileExists) return true;

            // Content check: hash of the encrypted bytes must match the marker.
            if (fileExists && !string.IsNullOrEmpty(markerHash))
            {
                byte[] encrypted = File.ReadAllBytes(path);
                string currentHash = HashHelper.ComputeMD5(encrypted);
                if (currentHash != markerHash) return true;
            }

            return false;
        }

        /// <summary>Deletes a vault file and its integrity markers.</summary>
        public static void Delete(string fileName, string projectId = "global")
        {
            try
            {
                string path = Path.Combine(StoragePath, fileName);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                LTLMLog.Warn($"Storage Delete Error for '{fileName}': {ex.Message}");
            }

            PlatformStorage.RemoveMarker(projectId, fileName + "_exists");
            PlatformStorage.RemoveMarker(projectId, fileName + "_hash");
            _cache.Remove(CacheKey(fileName, projectId));
        }

        public static bool Exists(string fileName)
        {
            return File.Exists(Path.Combine(StoragePath, fileName));
        }

        /// <summary>Drops the in-memory cache (tests / diagnostics).</summary>
        public static void InvalidateCache() => _cache.Clear();

        #region Encryption Logic

        private static byte[] DeriveKey(string machineKey)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(machineKey ?? string.Empty));
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
            if (data == null || data.Length < 16) throw new CryptographicException("Vault file too short.");
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
