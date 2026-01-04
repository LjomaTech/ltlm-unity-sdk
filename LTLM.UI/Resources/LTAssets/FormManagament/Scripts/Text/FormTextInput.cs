using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace LjomaAssets.FormManagement
{
    public class FormTextInput : FormInput
    {
        private TMP_InputField _field;

        public TMP_InputField.InputType InputType;
        public TMP_InputField.ContentType ContentType;
        public TMP_InputField.CharacterValidation CharacterValidation;
        public bool IsCharacterLimit;
        public int CharacterLimit;
        public int MinCharacterLimit;

        public string RegexPattern;
        public string RegexMessage;

        private void Awake()
        {
            SetupFormInput();
        }

        public override void SetValue(string value)
        {
            _field.text = value;
        }

        public override void SetIsReadOnly(bool isReadOnly)
        {
            base.SetIsReadOnly(isReadOnly);
            _field.readOnly = isReadOnly;
        }

        public override bool Validate()
        {
            if (isRequired)
            {
                if (string.IsNullOrEmpty(_field.text))
                {
                    ShowError("This field is required.");
                    return false;
                }
            }

            if (MinCharacterLimit > _field.text.Length)
            {
                ShowWarning("This field must have at least " + MinCharacterLimit + " characters.");
                return false;
            }
            
            if (CharacterLimit < _field.text.Length && IsCharacterLimit)
            {
                ShowWarning("This field must have at max " + MinCharacterLimit + " characters.");
                return false;
            }

            if (RegexPattern != "")
            {
                if (Regex.IsMatch(_field.text, RegexPattern) == false)
                {
                    ShowWarning(RegexMessage);
                    return false;
                }
            }
            return true;
        }

        protected override void SetupFormInput()
        {
            _field = GetComponent<TMP_InputField>();
            _field.inputType = InputType;
            _field.contentType = ContentType;
            _field.characterValidation = CharacterValidation;
            if (_field.characterValidation == TMP_InputField.CharacterValidation.EmailAddress)
            {
                RegexPattern = "^[\\w-\\.]+@([\\w-]+\\.)+[\\w-]{2,4}$";
                RegexMessage = "Please enter a valid email address.";
            }

            _field.interactable = !IsReadOnly;
            _field.characterLimit = IsCharacterLimit ? CharacterLimit : 0;
        }

        public override string Value()
        {
            return _field.text;
        }
    }
}