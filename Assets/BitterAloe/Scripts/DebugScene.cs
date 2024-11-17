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
        yield return new WaitForSeconds(10);
        SceneManager.LoadScene(1);
    }
}
