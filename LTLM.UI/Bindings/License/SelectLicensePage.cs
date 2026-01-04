using System;
using System.Collections.Generic;
using LTLM.SDK.Core.Models;
using Unity.VisualScripting;
using UnityEngine;

namespace LTLM.UI
{
    public class SelectLicensePage : MonoBehaviour
    {
        public LoginPageController loginPageController;
        public GameObject licenseCardPrefab;
        public GameObject policyCardPrefab;
        public Transform licenseCardsParent;

        private string SavedEmail;

        public void SetupEmail(string email)
        {
            SavedEmail = email;
        }
        
        public void SetupLicenseCardsPage(List<LicenseData> licenseDatas)
        {
            // clean out licenseCardsParent
            foreach (Transform child in licenseCardsParent)
            {
                Destroy(child.gameObject);
            }
            
            foreach (LicenseData licenseData in licenseDatas)
            {
                GameObject licenseCard = Instantiate(licenseCardPrefab, licenseCardsParent);
                licenseCard.GetComponent<LicenseCard>().SetupLicense(licenseData);
            }

            if (LTLMUIManager.Instance.features.HasFlag(FeatureBinding.Shop))
            {
                if (loginPageController.BuyablePolicies.Count > 0)
                {
                    foreach (PolicyData policyData in loginPageController.BuyablePolicies)
                    {
                        GameObject policyCard = Instantiate(policyCardPrefab, licenseCardsParent);
                        policyCard.GetComponent<PolicyCard>().SetupPolicy(policyData, SavedEmail);
                    }
                }
            }
        }
    }
}