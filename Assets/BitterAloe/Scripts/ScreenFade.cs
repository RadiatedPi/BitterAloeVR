using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.GraphicsBuffer;

public class ScreenFade : MonoBehaviour
{
    private Material mat;

    private void Start()
    {
        mat = GetComponent<MeshRenderer>().material;
    }
    public async UniTask FadeInScreen(float duration)
    {
        while (mat == null)
        {
            await UniTask.Yield();
        }
        mat.SetFloat("_alpha", 1);
        while (mat.GetFloat("_alpha") > 0)
        {
            mat.SetFloat("_alpha", Mathf.MoveTowards(mat.GetFloat("_alpha"), 0, (1 / duration) * Time.deltaTime));
            await UniTask.Yield();
        }
    }
    public async UniTask FadeOutScreen(float duration)
    {
        while (mat == null)
        {
            await UniTask.Yield();
        }
        mat.SetFloat("_alpha", 0);
        while (mat.GetFloat("_alpha") < 1f)
        {
            mat.SetFloat("_alpha", Mathf.MoveTowards(mat.GetFloat("_alpha"), 1, (1 / duration) * Time.deltaTime));
            await UniTask.Yield();
        }
    }
}
