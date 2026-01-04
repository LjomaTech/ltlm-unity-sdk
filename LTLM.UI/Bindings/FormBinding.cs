using System.Collections.Generic;
using LjomaAssets.FormManagement;
using UnityEngine;
using UnityEngine.Events;

namespace LTLM.UI
{
    public abstract class FormBinding : MonoBehaviour
    {
        public FormManager FormManager;
        public UnityEvent OnSuccess;
        public abstract void FormRequest(Dictionary<string, string> values);

        public virtual void SendCallback(bool success, Dictionary<string, string> errors)
        {
            FormManager.OnLoadingFinished(success, errors);
            if (success)
            {
                OnSuccess?.Invoke();
            }
        }
    }
}