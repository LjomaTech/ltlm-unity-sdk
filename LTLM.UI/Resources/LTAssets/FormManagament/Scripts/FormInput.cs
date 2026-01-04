using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace LjomaAssets.FormManagement
{
    public abstract class FormInput : MonoBehaviour
    {
        public string InputName;
        public TMP_Text InputLabel;
        public TMP_Text InputPlaceholder;
        public TMP_Text InputWarningText;
        public bool IsReadOnly;

        public virtual string Value()
        {
            return "";
        }
        
        public virtual void SetValue(string value)
        {
            
        }
        
        public virtual void SetIsReadOnly(bool isReadOnly)
        {
            if (IsReadOnly)
            {
                // already made Readonly in setup, we don't change otherwise.
                return;
            }
        }

        public virtual void ShowWarning(string warning)
        {
            InputWarningText.text = warning;
            InputWarningText.color = Color.yellow;
        }
        
        public virtual void HideWarning()
        {
            InputWarningText.text = string.Empty;
        }
        
        public virtual bool Validate()
        {
            return true;
        }
        
        // Show Error
        
        public virtual void ShowError(string error)
        {
            InputWarningText.text = error;
            InputWarningText.color = Color.red;
        }
        
        public virtual void HideError()
        {
            InputWarningText.text = string.Empty;
        }
        
        private void OnValidate()
        {
            SetupFormInput();
        }

        protected abstract void SetupFormInput();

        [Header("Validation Settings")] 
        public bool isRequired;
    }
}
