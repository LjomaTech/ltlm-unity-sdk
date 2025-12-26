using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LTLM.SDK.Core.Communication;
using LTLM.SDK.Core.Models;

namespace LTLM.SDK.Unity.Auth
{
    /// <summary>
    /// Handles the in-game Customer Login (OTP) flow.
    /// Allows users to retrieve their license portfolio without a key.
    /// </summary>
    public class CustomerLogin
    {
        private LTLMClient _client;

        public CustomerLogin(LTLMClient client)
        {
            _client = client;
        }

        public void RequestOTP(string email, Action onSuccess, Action<string> onError)
        {
            var data = new { email = email };
            LTLMManager.Instance.StartCoroutine(_client.PostEncrypted<object, object>(
                "/v1/sdk/pro/auth/customer-login-request",
                data,
                res => onSuccess?.Invoke(),
                err => onError?.Invoke(err)
            ));
        }

        public void VerifyOTP(string email, string otp, Action<List<LicenseData>> onPortfolioReceived, Action<string> onError)
        {
            var data = new { email = email, otp = otp };
            LTLMManager.Instance.StartCoroutine(_client.PostEncrypted<object, PortfolioResponse>(
                "/v1/sdk/pro/auth/customer-portfolio",
                data,
                portfolio => {
                    onPortfolioReceived?.Invoke(portfolio.licenses);
                },
                err => onError?.Invoke(err)
            ));
        }

        public void SelectLicense(LicenseData license)
        {
            LTLMManager.Instance.ActivateLicense(license.licenseKey);
        }
    }
}
