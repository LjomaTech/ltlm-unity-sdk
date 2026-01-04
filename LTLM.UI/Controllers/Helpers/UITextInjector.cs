using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class UITextInjector : MonoBehaviour
{
    [Header("Use $value to inject the value you send into the text")]
    public string TextValueRef;
    public void WriteText(string value)
    {
        var finalText = new StringBuilder(TextValueRef).Replace("$value", value);
        GetComponent<TMP_Text>().text = finalText.ToString();
    }
}
