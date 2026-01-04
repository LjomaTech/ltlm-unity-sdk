using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;

namespace LjomaAssets.FormManagement{
    public class FormManager : MonoBehaviour
    {
        public FormInput[] FormInputs;
        public FormButton[] FormButtons;

        public UnityEvent<string> OnSubmitJson;
        public UnityEvent<Dictionary<string, string>> OnSubmitDictionary;

        private void Awake()
        {
            FormInputs = GetComponentsInChildren<FormInput>(true);
            FormButtons = GetComponentsInChildren<FormButton>(true);
        }

        private void Start()
        {
            Setup();
        }

        private void Setup()
        {
            for (int i = 0; i < FormButtons.Length; i++)
            {
                if (FormButtons[i].Type == FormButton.ButtonType.Submit)
                {
                    FormButtons[i].Button.onClick.AddListener(OnSubmit);
                } else if (FormButtons[i].Type == FormButton.ButtonType.Reset)
                {
                    FormButtons[i].Button.onClick.AddListener(OnReset);
                }
            }
        }

        public void OnSubmit()
        {
            bool isValid = true;
            for (int i = 0; i < FormInputs.Length; i++)
            {
                if (isValid == true)
                {
                    isValid = FormInputs[i].Validate();
                }
                else
                {
                    FormInputs[i].Validate();
                }
            }
            
            if (isValid == false)
            {
                return;
            }
            
            // Disablee all inputs & buttons
            for (int i = 0; i < FormInputs.Length; i++)
            {
                FormInputs[i].SetIsReadOnly(true);
            }
            for (int i = 0; i < FormButtons.Length; i++)
            {
                FormButtons[i].Button.interactable = false;
            }

            var InputValues = GetInputValues();
            OnSubmitDictionary?.Invoke(InputValues);
            OnSubmitJson?.Invoke(JsonConvert.SerializeObject(InputValues));
        }

        private Dictionary<string,string> GetInputValues()
        {
            Dictionary<string, string> inputValues = new Dictionary<string, string>();
            for (int i = 0; i < FormInputs.Length; i++)
            {
                inputValues.Add(FormInputs[i].InputName, FormInputs[i].Value());
            }
            return inputValues;
        }

        // Expects dictionary of string key and string value for InputName -> Warning, if failed.
        // if suceess it's not his job anymore.
        public void OnLoadingFinished(bool suceess, Dictionary<string, string> inputNameToWarning = null)
        {
            if (suceess)
            {
                OnReset();
            }
            else
            {
                // Disablee all inputs & buttons
                for (int i = 0; i < FormInputs.Length; i++)
                {
                    FormInputs[i].SetIsReadOnly(false);
                }
                for (int i = 0; i < FormButtons.Length; i++)
                {
                    FormButtons[i].Button.interactable = true;
                }

                if (inputNameToWarning != null)
                    for (int i = 0; i < inputNameToWarning.Count; i++)
                    {
                        var Input = FormInputs.First(x => x.InputName == inputNameToWarning.Keys.ElementAt(i));
                        Input.ShowWarning(inputNameToWarning[inputNameToWarning.Keys.ElementAt(i)]);
                    }
            }
        }
        
        public void OnReset()
        {
            // Reseet Values and make what's not just readyonly from start writeable again
            for (int i = 0; i < FormInputs.Length; i++)
            {
                FormInputs[i].SetIsReadOnly(false);
                FormInputs[i].HideWarning();
                FormInputs[i].HideError();
                FormInputs[i].SetValue(string.Empty);
            }
            for (int i = 0; i < FormButtons.Length; i++)
            {
                FormButtons[i].Button.interactable = true;
            }
        }
    }
}