using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// Handles all communication with the LTLM backend.
    /// Implements the PRO "Triple-Wrap" protocol (AES-256 envelope + Ed25519 signed payload + rolling nonce).
    ///
    /// <para><b>Nonce chain:</b> the server hands out a <c>server_nonce</c> with every response that it
    /// also stores; the next request must echo it. When the SDK has no nonce (first run, after sign-out)
    /// it sends <c>null</c>, which the server accepts on activation/status calls and rejects on the rest
    /// with a 401 that <see cref="LTLMRequestError.IsNonceMismatch"/> reports - the manager then
    /// re-activates to resynchronise.</para>
    /// </summary>
    public class LTLMClient
    {
        private readonly string _baseUrl;
        private readonly string _projectId;
        private readonly string _secretKey;
        private readonly CryptoProvider _crypto;
        private string _lastNonce;

        private static readonly JsonSerializerSettings NoDateParsing =
            new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };

        private static readonly JsonSerializerSettings SerializeSettings =
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include };

        /// <summary>HTTP timeout per request, seconds.</summary>
        public int TimeoutSeconds { get; set; } = LTLMConstants.DefaultRequestTimeoutSeconds;

        /// <summary>When &gt; 0, signed responses older than this are rejected (replay protection).</summary>
        public int MaxResponseAgeSeconds { get; set; } = 0;

        /// <summary>Base URL in use (no trailing slash).</summary>
        public string BaseUrl => _baseUrl;

        /// <summary>The nonce that will be sent with the next encrypted request (null = none).</summary>
        public string CurrentNonce => _lastNonce;

        public LTLMClient(string baseUrl, string projectId, string secretKey, string publicKey)
        {
            _baseUrl = (baseUrl ?? LTLMConstants.BackendUrl).TrimEnd('/');
            _projectId = string.IsNullOrEmpty(projectId) ? "global" : projectId;
            _secretKey = secretKey;
            _crypto = new CryptoProvider(secretKey, publicKey);

            string stored = SecureStorage.Load(NonceFile, DeviceID.GetHWID(), _projectId);
            _lastNonce = string.IsNullOrEmpty(stored) || stored == LTLMConstants.TamperedMarker ? null : stored;
        }

        private string NonceFile => "nonce_" + _projectId;

        public CryptoProvider GetCrypto() => _crypto;

        /// <summary>Forgets the nonce chain (sign-out / cache clear). The next activation starts a new chain.</summary>
        public void ClearNonce()
        {
            _lastNonce = null;
            SecureStorage.Delete(NonceFile, _projectId);
        }

        private void StoreNonce(string nonce)
        {
            _lastNonce = nonce;
            SecureStorage.Save(NonceFile, nonce, DeviceID.GetHWID(), _projectId);
        }

        private void ApplyCommonHeaders(UnityWebRequest request)
        {
            request.timeout = Mathf.Max(5, TimeoutSeconds);
            request.SetRequestHeader("x-project-secret", _secretKey);
            request.SetRequestHeader("x-sdk-version", LTLMConstants.Version);
            request.SetRequestHeader("x-sdk-platform", "unity-" + DeviceID.GetPlatformTag());
        }

        // ------------------------------------------------------------------
        // Plain (standard) endpoints
        // ------------------------------------------------------------------

        /// <summary>Sends an authenticated GET request to a standard (non-encrypted) endpoint.</summary>
        public IEnumerator GetRequest<TResponse>(string endpoint, Action<TResponse> onSuccess, Action<LTLMRequestError> onError)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(_baseUrl + endpoint))
            {
                ApplyCommonHeaders(request);
                yield return request.SendWebRequest();

                var error = ClassifyTransportError(request);
                if (error != null)
                {
                    LTLMLog.Trace($"GET {endpoint} failed: {error.Message}");
                    onError?.Invoke(error);
                    yield break;
                }

                string body = request.downloadHandler.text;
                LTLMLog.Trace($"GET {endpoint} -> {body}");
                TResponse parsed;
                try
                {
                    parsed = JsonConvert.DeserializeObject<TResponse>(body);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(LTLMRequestError.Integrity("Failed to parse response: " + ex.Message, body));
                    yield break;
                }
                onSuccess?.Invoke(parsed);
            }
        }

        /// <summary>Sends an authenticated JSON POST to a standard (non-encrypted) endpoint.</summary>
        public IEnumerator PostPlain<TRequest, TResponse>(string endpoint, TRequest data, Action<TResponse> onSuccess, Action<LTLMRequestError> onError)
        {
            string json = JsonConvert.SerializeObject(data, SerializeSettings);
            using (UnityWebRequest request = new UnityWebRequest(_baseUrl + endpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                ApplyCommonHeaders(request);
                yield return request.SendWebRequest();

                var error = ClassifyTransportError(request);
                if (error != null)
                {
                    onError?.Invoke(error);
                    yield break;
                }

                string body = request.downloadHandler.text;
                LTLMLog.Trace($"POST {endpoint} -> {body}");
                TResponse parsed;
                try
                {
                    parsed = JsonConvert.DeserializeObject<TResponse>(body);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(LTLMRequestError.Integrity("Failed to parse response: " + ex.Message, body));
                    yield break;
                }
                onSuccess?.Invoke(parsed);
            }
        }

        // ------------------------------------------------------------------
        // Encrypted (PRO) endpoints
        // ------------------------------------------------------------------

        /// <summary>
        /// Sends an encrypted POST request using the Triple-Wrap protocol.
        /// </summary>
        /// <param name="skipNonce">Send the current nonce but do not accept a new one (fire-and-forget calls such as the closing heartbeat).</param>
        /// <param name="rotatesNonce">False for endpoints that do not store a nonce server-side (seat listing, checkout links, OTP).
        /// The returned <c>server_nonce</c> is then ignored so the chain does not desync.</param>
        public IEnumerator PostEncrypted<TRequest, TResponse>(string endpoint, TRequest data,
            Action<TResponse> onSuccess, Action<LTLMRequestError> onError, bool skipNonce = false, bool rotatesNonce = true)
        {
            // Requests that participate in the nonce chain must not overlap: two in-flight requests would
            // both echo the same nonce and the second one would be rejected as a replay.
            bool needsLock = rotatesNonce || skipNonce;
            if (needsLock)
            {
                float waited = 0f;
                while (_nonceBusy && waited < TimeoutSeconds + 5f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (_nonceBusy)
                {
                    onError?.Invoke(LTLMRequestError.Local("Timed out waiting for the previous nonce request."));
                    yield break;
                }
                _nonceBusy = true;
            }

            try
            {
                yield return PostEncryptedInternal(endpoint, data, onSuccess, onError, skipNonce, rotatesNonce);
            }
            finally
            {
                if (needsLock) _nonceBusy = false;
            }
        }

        private bool _nonceBusy;

        /// <summary>Fire-and-forget variant used during application quit (bypasses the nonce lock).</summary>
        public IEnumerator PostEncryptedUnlocked<TRequest, TResponse>(string endpoint, TRequest data,
            Action<TResponse> onSuccess, Action<LTLMRequestError> onError)
        {
            return PostEncryptedInternal(endpoint, data, onSuccess, onError, skipNonce: true, rotatesNonce: false);
        }

        private IEnumerator PostEncryptedInternal<TRequest, TResponse>(string endpoint, TRequest data,
            Action<TResponse> onSuccess, Action<LTLMRequestError> onError, bool skipNonce, bool rotatesNonce)
        {
            // 1. Inner envelope { data, nonce, hwid }
            var innerBody = new Dictionary<string, object>
            {
                { "data", data },
                { "nonce", _lastNonce },
                { "hwid", DeviceID.GetHWID() }
            };

            string encryptedBlob;
            try
            {
                string jsonInner = JsonConvert.SerializeObject(innerBody, SerializeSettings);
                LTLMLog.Trace($"POST {endpoint}");
                encryptedBlob = _crypto.Encrypt(jsonInner);
            }
            catch (Exception ex)
            {
                onError?.Invoke(LTLMRequestError.Local("Failed to encrypt request: " + ex.Message));
                yield break;
            }

            // 2. Send
            using (UnityWebRequest request = new UnityWebRequest(_baseUrl + endpoint, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes("{\"blob\":\"" + encryptedBlob + "\"}");
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                ApplyCommonHeaders(request);

                yield return request.SendWebRequest();

                var transportError = ClassifyTransportError(request);
                if (transportError != null)
                {
                    LTLMLog.Trace($"POST {endpoint} failed ({transportError.HttpStatus}): {transportError.Message}");
                    onError?.Invoke(transportError);
                    yield break;
                }

                // 3. Decrypt + verify
                string responseBody = request.downloadHandler.text;
                if (!TryUnwrap<TResponse>(responseBody, out SignedResponse<TResponse> signed, out LTLMRequestError unwrapError))
                {
                    onError?.Invoke(unwrapError);
                    yield break;
                }

                // 4. Rotate nonce
                if (!skipNonce && rotatesNonce && !string.IsNullOrEmpty(signed.server_nonce))
                {
                    StoreNonce(signed.server_nonce);
                }

                onSuccess?.Invoke(signed.data);
            }
        }

        /// <summary>
        /// Decrypts an encrypted blob, verifies the Ed25519 signature and deserialises the payload.
        /// Used for live responses and for offline (.ltlm) license files.
        /// </summary>
        public bool TryUnwrap<T>(string encryptedBlob, out SignedResponse<T> result, out LTLMRequestError error)
        {
            result = null;
            error = null;

            string decrypted;
            try
            {
                decrypted = _crypto.Decrypt(encryptedBlob);
            }
            catch (Exception ex)
            {
                error = LTLMRequestError.Integrity("Failed to decrypt response (wrong secret key or corrupt data): " + ex.Message, encryptedBlob);
                error.Code = LTLMErrorCodes.DECRYPT_FAILED;
                return false;
            }

            JObject envelope;
            try
            {
                using (var reader = new JsonTextReader(new System.IO.StringReader(decrypted)))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    envelope = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                }
            }
            catch (Exception ex)
            {
                error = LTLMRequestError.Integrity("Decrypted payload is not valid JSON: " + ex.Message, decrypted);
                return false;
            }

            string signature = envelope?["signature"]?.ToString();
            if (string.IsNullOrEmpty(signature))
            {
                error = LTLMRequestError.Integrity("Invalid response: missing signature.", decrypted);
                error.Code = LTLMErrorCodes.RESPONSE_TAMPERED;
                return false;
            }

            envelope.Remove("signature");
            // The backend emits stable JSON. Retain its exact number/string lexemes: reserializing
            // through .NET changes JS doubles (1e-7, 1e+21, -0) and Unicode escapes.
            string verificationJson = SignedWireJson(decrypted);

            if (!_crypto.VerifySignature(verificationJson, signature))
            {
                error = LTLMRequestError.Integrity("Response verification failed. Tampering detected.", decrypted);
                error.Code = LTLMErrorCodes.RESPONSE_TAMPERED;
                return false;
            }

            if (envelope["data"] == null || envelope["data"].Type == JTokenType.Null ||
                envelope["timestamp"]?.Type != JTokenType.Integer)
            {
                error = LTLMRequestError.Integrity("Signed response is missing data or timestamp.", decrypted);
                return false;
            }

            try
            {
                result = JsonConvert.DeserializeObject<SignedResponse<T>>(decrypted);
            }
            catch (Exception ex)
            {
                error = LTLMRequestError.Integrity("Failed to deserialise response payload: " + ex.Message, decrypted);
                return false;
            }

            if (MaxResponseAgeSeconds > 0 && result.timestamp > 0)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (Math.Abs(now - result.timestamp) > MaxResponseAgeSeconds)
                {
                    error = LTLMRequestError.Integrity($"Response timestamp is {Math.Abs(now - result.timestamp)}s off. Possible replay or clock skew.", decrypted);
                    error.Code = LTLMErrorCodes.RESPONSE_TAMPERED;
                    return false;
                }
            }

            return true;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static string SignedWireJson(string json)
        {
            json = json.Trim();
            var properties = new List<string>();
            int depth = 0, start = 1;
            bool quoted = false, escaped = false;
            for (int i = 1; i < json.Length; i++)
            {
                char c = json[i];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') { quoted = true; continue; }
                if (depth == 0 && (c == ',' || i == json.Length - 1))
                {
                    string part = json.Substring(start, i - start).Trim();
                    var field = JsonConvert.DeserializeObject<JObject>("{" + part + "}", NoDateParsing);
                    if (field.Properties().First().Name != "signature") properties.Add(part);
                    start = i + 1;
                }
                else if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
            }
            return "{" + string.Join(",", properties) + "}";
        }


        private static LTLMRequestError ClassifyTransportError(UnityWebRequest request)
        {
            switch (request.result)
            {
                case UnityWebRequest.Result.Success:
                    return null;

                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                {
                    var err = LTLMRequestError.Network(string.IsNullOrEmpty(request.error) ? "Network error" : request.error);
                    err.Code = request.error != null && request.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                        ? LTLMErrorCodes.TIMEOUT
                        : LTLMErrorCodes.NETWORK_ERROR;
                    return err;
                }

                default: // ProtocolError (HTTP 4xx/5xx)
                {
                    string body = request.downloadHandler?.text;
                    var err = new LTLMRequestError
                    {
                        HttpStatus = request.responseCode,
                        RawBody = body,
                        Message = string.IsNullOrEmpty(request.error) ? $"HTTP {request.responseCode}" : request.error
                    };

                    if (!string.IsNullOrEmpty(body))
                    {
                        try
                        {
                            var details = JsonConvert.DeserializeObject<LTLMError>(body);
                            if (details != null)
                            {
                                if (!string.IsNullOrEmpty(details.message)) err.Message = details.message;
                                if (!string.IsNullOrEmpty(details.code)) err.Code = details.code;
                            }
                        }
                        catch
                        {
                            err.Message = body.Length > 300 ? body.Substring(0, 300) : body;
                        }
                    }

                    if (request.responseCode >= 500 && string.IsNullOrEmpty(err.Code))
                        err.Code = LTLMErrorCodes.SERVER_ERROR;
                    if (err.IsNonceMismatch && string.IsNullOrEmpty(err.Code))
                        err.Code = LTLMErrorCodes.NONCE_MISMATCH;

                    return err;
                }
            }
        }

        /// <summary>Recursively sorts JObject properties (ordinal) to match Node's fast-json-stable-stringify.</summary>
        private static JToken SortJToken(JToken token)
        {
            if (token is JObject obj)
            {
                var sortedObj = new JObject();
                foreach (var property in obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
                    sortedObj.Add(property.Name, SortJToken(property.Value));
                return sortedObj;
            }

            if (token is JArray array)
            {
                var sortedArray = new JArray();
                foreach (var item in array) sortedArray.Add(SortJToken(item));
                return sortedArray;
            }

            return token;
        }

        /// <summary>Produces a compact, key-sorted JSON string identical to the backend's signing format.</summary>
        public static string ToStableJson(string rawJson)
        {
            var token = JsonConvert.DeserializeObject<JToken>(rawJson, NoDateParsing);
            return ToStableJson(token);
        }

        public static string ToStableJson(JToken token)
        {
            var sorted = SortJToken(token);
            string json = sorted.ToString(Formatting.None);
            return json.Replace("\\/", "/");
        }
    }
}
