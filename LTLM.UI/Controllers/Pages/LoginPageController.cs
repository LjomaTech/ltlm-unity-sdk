using System;
using System.Collections;
using System.Collections.Generic;
using LTLM.SDK.Core.Models;
using LTLM.SDK.Unity;
using Newtonsoft.Json;
using UnityEngine;

public class LoginPageController : MonoBehaviour
{
    public GameObject[] LoginFlowPages;
    public List<PolicyData> BuyablePolicies;
    private void Start()
    {
        LTLMManager.Instance.GetBuyablePolicies((policies) =>
        {
            BuyablePolicies = policies;
        });
    }

    public void SetActiveStep(int step)
    {
        for (int i = 0; i < LoginFlowPages.Length; i++)
        {
            LoginFlowPages[i].SetActive(false);
        }
        LoginFlowPages[step].SetActive(true);
    }
}
