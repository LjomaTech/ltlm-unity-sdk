using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LjomaAssets.FormManagement.Inputs{
    public class RadioboxFormInput : FormInput
    {
        protected override void SetupFormInput()
        {
            throw new System.NotImplementedException();
        }
        
        public static string[] FormatValue(string value)
        {
            return new[] {""};
        }
    }
}