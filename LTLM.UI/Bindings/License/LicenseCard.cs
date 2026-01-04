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
        PolicyName.text = licenseData.policy.name;

        if (licenseData.policy.config.limits.tokens.enabled)
            TokensUsage.text = "Tokens : " + licenseData.tokensRemaining.ToString() + "/" +
                               licenseData.tokensLimit.ToString();
        if (licenseData.policy.config.limits.seats.enabled)
            ActiveSeats.text = "Active Seats: " +
                               ((licenseData.activeSeats != null) ? licenseData.activeSeats.ToString() : "0") +
                               "/" +
                               licenseData.policy.config.limits.seats.maxSeats.ToString();

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

        if (licenseData.policy.type == "perpetual")
            ExpireDate.text = "perpetual";
        else
        {
            if (licenseData.policy.type == "usage-based")
            {
                if (licenseData.policy.config.limits.time.mode == "duration")
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