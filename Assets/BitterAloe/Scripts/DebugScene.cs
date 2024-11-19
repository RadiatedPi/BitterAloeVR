using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DebugScene : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(StartSceneAfterDelay());
    }

    IEnumerator StartSceneAfterDelay()
    {
        //yield return new WaitForSeconds(5);
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(1);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }
}
