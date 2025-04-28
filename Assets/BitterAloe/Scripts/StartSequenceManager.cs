using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.GraphicsBuffer;

public class StartSequenceManager : MonoBehaviour
{
    public GameObject button, poemTitle, poemLeft, poemCenter, poemRight, poemAuthor, poemCopywright;
    public ScreenFade screenFade;
    private int pageNum = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        await screenFade.FadeInScreen(5);
        await StartSequence();
    }

    private async UniTask StartSequence()
    {
        switch (pageNum)
        {
            case 0:
                await UniTask.WaitForSeconds(3);
                await FadeIn(poemTitle, 1f);
                await UniTask.WaitForSeconds(10);
                await FadeOut(poemTitle, 2f);
                await UniTask.WaitForSeconds(2);
                await FadeIn(poemLeft, 1f);
                await FadeIn(poemCenter, 1f);
                await FadeIn(poemRight, 1f);
                await UniTask.WaitForSeconds(0.5f);
                await FadeIn(button, 0.5f);
                break;
            case 1:
                await FadeOut(button, 0.5f);
                await FadeOut(poemLeft, 1f);
                await FadeOut(poemCenter, 1f);
                await FadeOut(poemRight, 1f);
                await UniTask.WaitForSeconds(2f);
                await FadeIn(poemAuthor, 1f);
                await UniTask.WaitForSeconds(10);
                await FadeOut(poemAuthor, 1f);
                await UniTask.WaitForSeconds(0.5f);
                await FadeIn(button, 0.5f);
                break;
            case 2:
                await FadeOut(button, 0.5f);
                await FadeIn(poemCopywright, 1f);
                await UniTask.WaitForSeconds(5f);
                await FadeOut(poemCopywright, 1f);
                await UniTask.WaitForSeconds(0.5f);
                await FadeIn(button, 0.5f);
                break;
            case 3:
                await FadeOut(button, 0.5f);
                await StartGame();
                break;
        }
    }

    private async UniTask FadeIn(GameObject ui, float duration)
    {
        ui.GetComponent<CanvasGroup>().alpha = 0;
        ui.SetActive(true);
        while (ui.GetComponent<CanvasGroup>().alpha < 1.0f)
        {
            if (duration < 0)
            {
                duration = 0.001f;
            }
            ui.GetComponent<CanvasGroup>().alpha = Mathf.MoveTowards(ui.GetComponent<CanvasGroup>().alpha, 1, (1 / duration) * Time.deltaTime);
            await UniTask.Yield();
        }
    }
    private async UniTask FadeOut(GameObject ui, float duration)
    {
        ui.GetComponent<CanvasGroup>().alpha = 1;
        while (ui.GetComponent<CanvasGroup>().alpha > 0)
        {
            if (duration < 0)
            {
                duration = 0.001f;
            }
            ui.GetComponent<CanvasGroup>().alpha = Mathf.MoveTowards(ui.GetComponent<CanvasGroup>().alpha, 0, (1 / duration) * Time.deltaTime);
            await UniTask.Yield();
        }
        ui.SetActive(false);
    }

    public async void Continue()
    {
        pageNum++;
        await StartSequence();
    }

    private async UniTask StartGame()
    {
        await screenFade.FadeOutScreen(3);
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(1);
        while (!asyncLoad.isDone)
        {
            await UniTask.Yield();
        }
    }
}

