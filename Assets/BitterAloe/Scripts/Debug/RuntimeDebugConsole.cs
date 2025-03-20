using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RuntimeDebugConsole : MonoBehaviour
{
    public GameObject debugUIPrefab;
    public Transform debugConsoleUIWindow;

    private List<string> consoleLog = new List<string>();
    int consoleLogLength = 0;

    public void Update()
    {
        if (consoleLog.Count > consoleLogLength)
        {
            //foreach (Transform child in debugConsoleUIWindow)
            //    Destroy(child.gameObject);
            var testimonyUI = Instantiate(debugUIPrefab, debugConsoleUIWindow);
            testimonyUI.GetComponent<TextMeshProUGUI>().SetText(consoleLog[consoleLogLength]);
            testimonyUI.GetComponent<TextMeshProUGUI>().ForceMeshUpdate();

            consoleLogLength++;
        }
    }

    public void Log(string message)
    {
        Debug.Log(message);

        consoleLog.Add(message);
    }
}
