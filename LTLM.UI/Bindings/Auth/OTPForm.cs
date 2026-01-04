using System;
using System.Collections;
using System.Collections.Generic;
using LTLM.SDK.Core.Models;
using LTLM.SDK.Unity;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;

namespace LTLM.UI
{
    public class OTPForm : FormBinding
    {
        public UnityEvent<List<LicenseData>> OnSuccessListCallback;
        public UnityEvent<string> OnSuccessEmailCallback;
        public override void FormRequest(Dictionary<string, string> values)
        {
            if (LTLM.SDK.Unity.LTLMManager.Instance.IsAuthenticated)
            {
                Debug.LogWarning("ERROR HERE?");
            }
            else
            {
                LTLMManager.Instance.VerifyOTP(values["email"], values["otp"], list  =>
                {
                    Debug.Log(JsonConvert.SerializeObject(list));
                    OnSuccessEmailCallback.Invoke(values["email"]);
                    SendCallback(true, null);
                    OnSuccessListCallback.Invoke(list);
                }, (error) =>
                {
                    SendCallback(false, new Dictionary<string, string>(new[] { new KeyValuePair<string, string>("otp", error) }));
                });
            }
        }
    }
}