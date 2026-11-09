using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace LTLM.SDK.Core.Security
{
    /// <summary>
    /// Handles AES-256 encryption/decryption and Ed25519 signature verification.
    /// This is the heart of the LTLM Triple-Wrap protocol.
    ///
    /// <para>Wire format for encrypted blobs is hex: <c>iv:cipher</c> (AES-256-CBC, PKCS7) which is what the
    /// backend emits. <c>iv:tag:cipher</c> (AES-256-GCM) is accepted on decrypt for forward compatibility.
    /// GCM is implemented with BouncyCastle because <c>System.Security.Cryptography.AesGcm</c> is not
    /// available on Unity's Mono/IL2CPP profiles.</para>
    /// </summary>
    public class CryptoProvider
    {
        private readonly byte[] _secretKey;
        private readonly string _publicKeyPem;
        private Ed25519PublicKeyParameters _publicKey; // parsed lazily and cached

        public CryptoProvider(string secretKeyHex, string publicKeyPem)
        {
            if (string.IsNullOrWhiteSpace(secretKeyHex))
                throw new ArgumentException("Secret key hex is required.", nameof(secretKeyHex));

            secretKeyHex = secretKeyHex.Trim().Replace("-", "").Replace(" ", "");
            byte[] keyBytes = HexToBytes(secretKeyHex);

            if (keyBytes.Length != 32)
                throw new InvalidOperationException(
                    $"Secret key must be 32 bytes (64 hex chars). Got {keyBytes.Length} bytes.");

            _secretKey = keyBytes;
            _publicKeyPem = publicKeyPem;
        }

        /// <summary>True when a public key was supplied, i.e. signature verification is possible.</summary>
        public bool CanVerify => !string.IsNullOrWhiteSpace(_publicKeyPem);

        #region AES

        /// <summary>Encrypts a string using AES-256-CBC. Output: <c>ivHex:cipherHex</c>.</summary>
        public string Encrypt(string plainText)
        {
            if (plainText == null) return null;

            using (Aes aes = Aes.Create())
            {
                aes.Key = _secretKey;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();

                using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    return BytesToHex(aes.IV) + ":" + BytesToHex(cipherBytes);
                }
            }
        }

        /// <summary>Decrypts <c>iv:cipher</c> (CBC) or <c>iv:tag:cipher</c> (GCM) hex blobs.</summary>
        public string Decrypt(string encryptedData)
        {
            if (string.IsNullOrEmpty(encryptedData)) return null;

            string[] parts = encryptedData.Trim().Split(':');

            if (parts.Length == 3)
            {
                byte[] iv = HexToBytes(parts[0]);
                byte[] tag = HexToBytes(parts[1]);
                byte[] cipher = HexToBytes(parts[2]);

                // BouncyCastle expects cipher || tag
                byte[] input = new byte[cipher.Length + tag.Length];
                Buffer.BlockCopy(cipher, 0, input, 0, cipher.Length);
                Buffer.BlockCopy(tag, 0, input, cipher.Length, tag.Length);

                var gcm = new GcmBlockCipher(new AesEngine());
                gcm.Init(false, new AeadParameters(new KeyParameter(_secretKey), tag.Length * 8, iv));
                byte[] output = new byte[gcm.GetOutputSize(input.Length)];
                int len = gcm.ProcessBytes(input, 0, input.Length, output, 0);
                len += gcm.DoFinal(output, len);
                return Encoding.UTF8.GetString(output, 0, len);
            }

            if (parts.Length == 2)
            {
                byte[] iv = HexToBytes(parts[0]);
                byte[] cipherBytes = HexToBytes(parts[1]);
                if (iv.Length != 16) throw new ArgumentException($"Invalid IV length: expected 16 bytes, got {iv.Length}.");

                using (Aes aes = Aes.Create())
                {
                    aes.Key = _secretKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                    {
                        byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                        return Encoding.UTF8.GetString(plainBytes);
                    }
                }
            }

            throw new ArgumentException($"Invalid encrypted data format. Expected 2 or 3 parts, got {parts.Length}.");
        }

        #endregion

        #region Ed25519 Verification

        /// <summary>Verifies a base64 Ed25519 signature over the UTF-8 bytes of <paramref name="data"/>.</summary>
        public bool VerifySignature(string data, string signatureBase64)
        {
            if (!CanVerify)
            {
                LTLMLog.Error("VerifySignature called without a public key. Configure the project public key.");
                return false;
            }

            try
            {
                byte[] message = Encoding.UTF8.GetBytes(data);
                byte[] signature = Convert.FromBase64String(signatureBase64);
                if (signature.Length != 64) return false;

                var verifier = new Ed25519Signer();
                verifier.Init(false, GetPublicKey());
                verifier.BlockUpdate(message, 0, message.Length);
                return verifier.VerifySignature(signature);
            }
            catch (Exception ex)
            {
                LTLMLog.Error("VerifySignature error: " + ex.Message);
                return false;
            }
        }

        private Ed25519PublicKeyParameters GetPublicKey()
        {
            if (_publicKey != null) return _publicKey;
            _publicKey = ReadEd25519PublicKeyFromPem(_publicKeyPem);
            return _publicKey;
        }

        private static Ed25519PublicKeyParameters ReadEd25519PublicKeyFromPem(string pem)
        {
            pem = pem?.Trim();
            if (string.IsNullOrEmpty(pem)) throw new InvalidOperationException("Public key PEM is empty.");

            // Allow a raw base64 / hex 32-byte key without PEM armour.
            if (!pem.Contains("-----BEGIN"))
            {
                byte[] raw = null;
                if (pem.Length == 64)
                {
                    try { raw = HexToBytes(pem); } catch (FormatException) { }
                }
                if (raw == null) { try { raw = Convert.FromBase64String(pem); } catch (FormatException) { } }
                if (raw != null && raw.Length == 32) return new Ed25519PublicKeyParameters(raw, 0);
                if (raw != null && raw.Length > 32)
                {
                    var parsed = PublicKeyFactory.CreateKey(raw) as Ed25519PublicKeyParameters;
                    if (parsed != null) return parsed;
                }
                throw new InvalidOperationException("Unsupported public key format.");
            }

            using (var sr = new StringReader(pem))
            {
                var pemReader = new PemReader(sr);
                object obj = pemReader.ReadObject();

                if (obj is Ed25519PublicKeyParameters edPub) return edPub;

                if (obj is Org.BouncyCastle.Asn1.X509.SubjectPublicKeyInfo spki)
                {
                    AsymmetricKeyParameter keyParam = PublicKeyFactory.CreateKey(spki);
                    if (keyParam is Ed25519PublicKeyParameters edPub3) return edPub3;
                }

                throw new InvalidOperationException("Unsupported public key PEM format for Ed25519.");
            }
        }

        #endregion

        #region Hex helpers

        private static string BytesToHex(byte[] bytes)
        {
            char[] c = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int b = bytes[i] >> 4;
                c[i * 2] = (char)(b < 10 ? '0' + b : 'a' + (b - 10));
                b = bytes[i] & 0xF;
                c[i * 2 + 1] = (char)(b < 10 ? '0' + b : 'a' + (b - 10));
            }
            return new string(c);
        }

        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return new byte[0];
            if (hex.Length % 2 != 0) throw new FormatException("Invalid hex length.");
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        #endregion
    }
}
