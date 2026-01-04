using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LTLM.SDK.Core.Models;
using LTLM.SDK.Unity;
using LTLM.UI;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PolicyCard : MonoBehaviour
{
    public TMP_Text PolicyName;
    public TMP_Text PolicyDescription;
    public TMP_Text Tokens;
    public TMP_Text Seats;
    public TMP_Text Type;
    public TMP_Text Time;
    public TMP_Text Capabilites;
    public TMP_Text Price;
    public Button Buy;

    public void SetupPolicy(PolicyData policyData, string email)
    {
        PolicyName.text = policyData.name;
        PolicyDescription.text = policyData.shortDescription;
        if (policyData.config.limits.tokens.enabled)
            Tokens.text = "Tokens : " + policyData.config.limits.tokens.maxTokens.ToString();
        else
        {
            Tokens.text = "";
        }

        if (policyData.config.limits.seats.enabled)
            Seats.text = "Seats : " + policyData.config.limits.seats.maxSeats.ToString();
        else
        {
            Seats.text = "";
        }

        var features = policyData.config.features;
        List<string> entitledCapabilities = new List<string>();

        for (int i = 0; i < features.Count; i++)
        {
            if (features.ElementAt(i).Value == null) continue;
            string sVal = features.ElementAt(i).Value.ToLower();
            if (sVal == "true" || sVal == "1")
            {
                entitledCapabilities.Add(features.ElementAt(i).Key);
            }
        }

        var CapabilitesString = "Capabilites : ";
        for (int i = 0; i < entitledCapabilities.Count; i++)
        {
            CapabilitesString += entitledCapabilities[i];
            if (i != 0)
            {
                CapabilitesString += ", ";
            }
        }        
        
        Capabilites.text = CapabilitesString;
        Type.text = policyData.type.ToString();
        if(policyData.type == "subscription")
            Price.text = policyData.recurringPrice.ToString() + "$ / " + policyData.recurringIntervalCount.ToString() + " " + policyData.recurringInterval.ToString();
        else if (policyData.type == "trial")
        {
            Price.text = "Free for " + policyData.recurringIntervalCount.ToString() + " " + policyData.recurringInterval.ToString();
        } else if (policyData.type == "usage-based")
        {
            if (policyData.config.limits.time.mode == "duration")
            {
                Time.text = policyData.config.limits.time.durationDays.ToString() + " Days";
            }
            Price.text = policyData.price.ToString() + "$";
        }
        else
        {
            Time.text = policyData.config.limits.time.durationDays.ToString() + " Days";
            Price.text = policyData.price.ToString() + "$";
        }

        Buy.onClick.AddListener(() =>
        {
            LTLMManager.Instance.CreateCheckoutSession(policyData.PolicyID.ToString(), email, LTLMUIManager.Instance.BuyCompleteRedirectURL, OnUrlReceived);
        });
    }

    private void OnUrlReceived(string CheckoutURL)
    {
        Application.OpenURL(CheckoutURL);
    }
}