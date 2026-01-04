using System.Collections.Generic;
using LTLM.SDK.Unity;
using UnityEngine;

namespace LTLM.UI
{
    public class ActivateLicenseForm : FormBinding
    {
        public override void FormRequest(Dictionary<string, string> values)
        {
            if (LTLM.SDK.Unity.LTLMManager.Instance.IsAuthenticated)
            {
                Debug.LogWarning("ERROR HERE?");
            }
            else
            {
                LTLMManager.Instance.ActivateLicense(values["licenseKey"], (license, status) =>
                {
                    if (status != LicenseStatus.Active)
                    {
                        SendCallback(false, new Dictionary<string, string>(new[] { new KeyValuePair<string, string>("licenseKey", "Activation Failed, License is currently " +  status.ToString()) }));
                    }
                    SendCallback(true, null);
                }, (error) =>
                {
                    SendCallback(false, new Dictionary<string, string>(new[] { new KeyValuePair<string, string>("licenseKey", error) }));
                });
            }
        }
    }
}