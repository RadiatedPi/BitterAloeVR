using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RuntimeDebugConsole : MonoBehaviour
{
    public GameObject debugUIPrefab;
    public Transform debugConsoleUIWindow;
    public int maxLogLength = 100;

    private List<string> consoleLog = new List<string>();
    private int consoleLogLength = 0;

    public void Start()
    {
        for (int i = 0; i < maxLogLength; i++)
        {
            var testimonyUI = Instantiate(debugUIPrefab, debugConsoleUIWindow);
            testimonyUI.GetComponent<TextMeshProUGUI>().SetText($" ");
            testimonyUI.GetComponent<TextMeshProUGUI>().ForceMeshUpdate();
        }
    }

    public void LateUpdate()
    {
        if (consoleLog.Count > consoleLogLength)
        {
            var testimonyUI = debugConsoleUIWindow.transform.GetChild(maxLogLength - 1);
            testimonyUI.GetComponent<TextMeshProUGUI>().SetText($"[{consoleLogLength}] {consoleLog[consoleLogLength]}");
            testimonyUI.GetComponent<TextMeshProUGUI>().ForceMeshUpdate();
            testimonyUI.transform.SetSiblingIndex(1);

            consoleLogLength++;
        }
    }

    public void Log(string message)
    {
        Debug.Log(message);

        consoleLog.Add(message);
    }
}
