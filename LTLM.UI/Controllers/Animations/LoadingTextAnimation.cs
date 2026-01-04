using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LoadingTextAnimation : MonoBehaviour
{
    public TMP_Text LoadingText;

    private void Awake()
    {
        LoadingText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        StartCoroutine(LoadingAnimation());
    }

    public IEnumerator LoadingAnimation()
    {
        int i = 0;
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            LoadingText.text = "Loading";
            i++;
            var dots = new string('.', i % 4);
            LoadingText.text += dots;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }
}
