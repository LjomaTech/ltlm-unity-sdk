using System;
using UnityEngine;
using UnityEngine.UI;

namespace LjomaAssets.FormManagement
{
    public class FormButton : MonoBehaviour
    {
        public enum ButtonType
        {
            Submit,
            Reset,
            Action,
            Condition
        }

        public ButtonType Type;

        [HideInInspector] public Button Button;

        private void Awake()
        {
            Button = GetComponent<Button>(); 
        }
    }
}