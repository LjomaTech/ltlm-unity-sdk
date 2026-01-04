using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using LTLM.SDK.Core.Models;
using LTLM.SDK.Unity;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LicenseCard : MonoBehaviour
{
    public TMP_Text Key;
    public TMP_Text PolicyName;
    public TMP_Text TokensUsage;
    public TMP_Text ActiveSeats;
    public TMP_Text Capabilites;
    public TMP_Text ExpireDate;
    public Button Activate;

    public void SetupLicense(LicenseData licenseData)
    {
        // licenseKey is LIC-0BEC53C679BF012F, I want it to be LIC-0BExxxxxxxxF012F
        var licenseKey = licenseData.licenseKey;
        licenseKey = licenseKey.Replace(licenseKey.Substring(4, 12), "xxxxxxxxxx");
        Key.text = licenseKey;
        PolicyName.text = licenseData.policy?.name ?? "Unknown Policy";

        // Use licenseData.config (resolved config) instead of policy.config
        var config = licenseData.config;
        
        if (config?.limits?.tokens?.enabled == true)
            TokensUsage.text = "Tokens : " + licenseData.tokensRemaining.ToString() + "/" +
                               licenseData.tokensLimit.ToString();
        
        if (config?.limits?.seats?.enabled == true)
            ActiveSeats.text = "Active Seats: " +
                               ((licenseData.activeSeats != null) ? licenseData.activeSeats.ToString() : "0") +
                               "/" +
                               (config.limits.seats.maxSeats > 0 ? config.limits.seats.maxSeats.ToString() : "1");

        var CapabilitesList = LTLMManager.Instance.GetEntitledCapabilites(licenseData);
        var CapabilitesString = "Capabilites : ";
        for (int i = 0; i < CapabilitesList.Count; i++)
        {
            Debug.Log("CapabilitesList[i] : " + CapabilitesList[i]);
            CapabilitesString += CapabilitesList[i];
            if (i != 0)
            {
                CapabilitesString += ", ";
            }
        }

        Capabilites.text = CapabilitesString.ToString();

        if (licenseData.policy?.type == "perpetual")
            ExpireDate.text = "perpetual";
        else
        {
            if (licenseData.policy?.type == "usage-based")
            {
                // Use config.limits.time instead of policy.config.limits.time
                if (config?.limits?.time?.mode == "duration")
                {
                    ExpireDate.text =
                        "Expire At : " + DateTime.Parse(licenseData.validUntil).ToString("dd-MM-yyyy");
                }
                else
                {
                    ExpireDate.text = "perpetual";
                }
            }
            else
            {
                ExpireDate.text =
                    "Expire At : " + DateTime.Parse(licenseData.validUntil).ToString("dd-MM-yyyy");
            }
        }

        Activate.onClick.AddListener(() =>
        {
            LTLMManager.Instance.ActivateLicense(licenseData.licenseKey, (license, status) => { });
        });
    }
}