using Parquet;
using Parquet.Schema;
using System.Collections;
using System.IO;
using System.IO.Enumeration;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class ParquetParser : MonoBehaviour
{
    public TextMeshPro textGUI;
    public string fileName = "trctestimonies.parquet";

    private string filePath;


    // Start is called before the first frame update
    void Start()
    {
        GetParquet(fileName);
    }

    public async void GetParquet(string fileName)
    {
        Debug.Log("Getting parquet");
        string streamingFilePath = Path.Combine(Application.streamingAssetsPath, fileName);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        Debug.Log("windows/editor");
        filePath = streamingFilePath;
#elif UNITY_ANDROID
        Debug.Log("android");

        await CopyParquetToPersistentPath(streamingFilePath);
#endif
        long count;

        using (var reader = await ParquetReader.CreateAsync(filePath))
        {
            count = reader.OpenRowGroupReader(0).RowCount;
            Debug.Log(count);
        }
        textGUI.SetText($"Columns: {count}");

    }

    public async Task CopyParquetToPersistentPath(string streamingFilePath)
    {
        string persistentFilePath = streamingFilePath.Replace(Application.streamingAssetsPath, Application.persistentDataPath);

        var persistentFileDirectory = Path.GetDirectoryName(persistentFilePath);
        if (!Directory.Exists(persistentFileDirectory))
        {
            Directory.CreateDirectory(persistentFileDirectory);
        }
        UnityWebRequest loader = UnityWebRequest.Get(streamingFilePath);
        //await Task.Yield();
        await loader.SendWebRequest();

        if (loader.result == UnityWebRequest.Result.Success)
        {
            File.WriteAllBytes(persistentFilePath, loader.downloadHandler.data);
        }
        else
        {
            Debug.LogError("Cannot load file at " + streamingFilePath);
        }

        filePath = persistentFilePath;
    }

}
