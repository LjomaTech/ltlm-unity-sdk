using System;
using System.Security.Cryptography;
using System.Text;

namespace LTLM.SDK.Core.Security
{
    public static class HashHelper
    {
        public static string ComputeMD5(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string ComputeMD5(byte[] input)
        {
            if (input == null || input.Length == 0) return null;
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(input);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
