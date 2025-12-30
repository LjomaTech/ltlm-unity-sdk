using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using LTLM.SDK.Core.Models;
using LTLM.SDK.Core.Security;
using LTLM.SDK.Core.Storage;
using LTLM.SDK.Core.Hardware;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LTLM.SDK.Core.Communication
{
    /// <summary>
    /// Handles all encrypted communication with the LTLM PRO Backend.
    /// Implements the Triple-Wrap protocol and nonce management.
    /// </summary>
    public class LTLMClient
    {
        private string _baseUrl;
        private string _projectId;
        private CryptoProvider _crypto;
        private string _lastNonce;
        private string secretKey;

        public LTLMClient(string baseUrl, string projectId, string secretKey, string publicKey)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _projectId = projectId;
            _crypto = new CryptoProvider(secretKey, publicKey);
            this.secretKey = secretKey;
            // Load last used nonce from secure storage to prevent replay sync issues
            _lastNonce = SecureStorage.Load("nonce_" + projectId, DeviceID.GetHWID());
        }

        public CryptoProvider GetCrypto() => _crypto;

        /// <summary>
        /// Sends an authenticated GET request.
        /// </summary>
        public IEnumerator GetRequest<TResponse>(string endpoint, Action<TResponse> onSuccess, Action<string> onError)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(_baseUrl + endpoint))
            {
                request.SetRequestHeader("x-project-secret", secretKey);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorText = request.downloadHandler?.text ?? request.error;
                    onError?.Invoke(errorText);
                }
                else
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<TResponse>(request.downloadHandler.text);
                        onSuccess?.Invoke(response);
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke("Failed to parse response: " + ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Recursively sorts JObject properties alphabetically to match Node.js fast-json-stable-stringify.
        /// </summary>
        private static JToken SortJToken(JToken token)
        {
            if (token is JObject obj)
            {
                var sortedObj = new JObject();
                foreach (var property in obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    sortedObj.Add(property.Name, SortJToken(property.Value));
                }
                return sortedObj;
            }
            if (token is JArray array)
            {
                var sortedArray = new JArray();
                foreach (var item in array)
                {
                    sortedArray.Add(SortJToken(item));
                }
                return sortedArray;
            }
            return token;
        }

        /// <summary>
        /// Produces a compact, sorted JSON string identical to the backend's signing format.
        /// </summary>
        public static string ToStableJson(string rawJson)
        {
            // IMPORTANT: DateParseHandling.None prevents auto-converting strings to Date objects, 
            // which would change their string representation and break the signature.
            var settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
            var token = JsonConvert.DeserializeObject<JToken>(rawJson, settings);
            return ToStableJson(token);
        }

        public static string ToStableJson(JToken token)
        {
            var sorted = SortJToken(token);
            // Formatting.None for compactness. 
            // We also need to ensure slashes aren't escaped to match Node.js JSON.stringify component.
            string json = sorted.ToString(Formatting.None);
            return json.Replace("\\/", "/");
        }

        /// <summary>
        /// Sends an encrypted POST request using the Triple-Wrap protocol.
        /// </summary>
        /// <param name="skipNonce">If true, uses current nonce without updating. 
        /// Use for fire-and-forget calls like isClosing heartbeats to prevent nonce desync.</param>
        public IEnumerator PostEncrypted<TRequest, TResponse>(string endpoint, TRequest data,
            Action<TResponse> onSuccess, Action<string> onError, bool skipNonce = false)
        {
            // 1. Prepare Inner Body (Pro Request Envelope)
            // For skipNonce calls (like isClosing), use current nonce without generating new one
            string nonceToUse = skipNonce ? (_lastNonce ?? "skip") : (_lastNonce ?? Guid.NewGuid().ToString());
            
            var innerBody = new Dictionary<string, object>
            {
                { "data", data },
                { "nonce", nonceToUse },
                { "hwid", DeviceID.GetHWID() }
            };

            string jsonInner = JsonConvert.SerializeObject(innerBody);
            
            // 2. Encrypt (The Triple-Wrap layer)
            string encryptedBlob = _crypto.Encrypt(jsonInner);

            // 3. Send
            using (UnityWebRequest request = new UnityWebRequest(_baseUrl + endpoint, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes("{\"blob\":\"" + encryptedBlob + "\"}");
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-project-secret", secretKey);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorText = request.downloadHandler.text;
                    string finalMessage = "Network Error";

                    if (!string.IsNullOrEmpty(errorText))
                    {
                        try
                        {
                            var errorDetails = JsonUtility.FromJson<LTLMError>(errorText);
                            if (errorDetails != null && !string.IsNullOrEmpty(errorDetails.message))
                            {
                                finalMessage = errorDetails.message;
                            }
                            else
                            {
                                finalMessage = errorText;
                            }
                        }
                        catch
                        {
                            finalMessage = errorText;
                        }
                    }

                    onError?.Invoke(finalMessage);
                }
                else
                {
                    try
                    {
                        // 4. Decrypt and Verify Response
                        string responseBody = request.downloadHandler.text;
                        string decryptedBody = _crypto.Decrypt(responseBody);

                        // Parse with Newtonsoft to preserve order-flexible verification and support Dictionaries
                        var settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
                        var envelope = JsonConvert.DeserializeObject<JObject>(decryptedBody, settings);
                        
                        string signature = envelope["signature"]?.ToString();
                        if (string.IsNullOrEmpty(signature))
                        {
                            onError?.Invoke("Invalid response: missing signature.");
                            yield break;
                        }

                        // Remove signature to get the signed data envelope
                        envelope.Remove("signature");
                        
                        // Standardize serialization to match backend's fast-json-stable-stringify
                        string verificationJson = ToStableJson(envelope);

                        // Verify Signature
                        if (!_crypto.VerifySignature(verificationJson, signature))
                        {
                            onError?.Invoke("Response verification failed. Tampering detected.");
                            yield break;
                        }

                        // Use Newtonsoft for the final Typed output (supports Dictionaries and complex nesting)
                        var signedResponse = JsonConvert.DeserializeObject<SignedResponse<TResponse>>(decryptedBody);

                        // Update Nonce (server sends server_nonce)
                        // Skip nonce update for fire-and-forget calls to prevent desync
                        if (!skipNonce && !string.IsNullOrEmpty(signedResponse.server_nonce))
                        {
                            _lastNonce = signedResponse.server_nonce;
                            SecureStorage.Save("nonce_" + _projectId, _lastNonce, DeviceID.GetHWID());
                        }

                        onSuccess?.Invoke(signedResponse.data);
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke("Failed to process response: " + ex.Message);
                    }
                }
            }
        }
    }
}