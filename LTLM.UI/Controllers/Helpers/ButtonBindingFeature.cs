using System;
using System.Collections;
using System.Collections.Generic;
using LTLM.UI;
using UnityEngine;

[Flags]
public enum FeatureBinding
{
    None = 0,
    Login = 1 << 1,
    Logout= 1 << 2,
    Shop = 1 << 3,
    Airgapped= 1 << 4,
}


public class ButtonBindingFeature : MonoBehaviour
{
    public FeatureBinding feature;
    private FeatureBinding _previous;
    
    private void Start()
    {
        if ((LTLMUIManager.Instance.features & feature) != 0)
        {
            gameObject.SetActive(true);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    
    private void OnValidate()
    {
        if (feature == _previous)
            return;

        var changed = feature ^ _previous;

        if (changed != 0)
        {
            // Keep only the newly selected flag
            feature = changed;
        }

        _previous = feature;
    }
}
