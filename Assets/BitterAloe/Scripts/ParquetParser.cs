using Microsoft.Data.Analysis;
using Parquet;
using Parquet.Data.Analysis;
using Parquet.Schema;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Net;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class ParquetParser : MonoBehaviour
{
    public TextMeshPro textGUI;
    public string fileName = "trctestimonies.parquet";
    public Vector2 coordinateRange = new Vector2(0f, 10f);

    private string filePath;
    public DataFrame df;


    // Start is called before the first frame update
    void Start()
    {
        GetParquetDataset(fileName);
    }

    public async void GetParquetDataset(string fileName)
    {
        Debug.Log("Getting parquet");
        string streamingFilePath = Path.Combine(Application.streamingAssetsPath, fileName);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        Debug.Log("Detected platform: Windows/Editor");
        filePath = streamingFilePath;
#elif UNITY_ANDROID
        Debug.Log("Detected platform: Android");
        await CopyParquetToPersistentPath(streamingFilePath);
#endif
        await ReadParquet();

        // minimum XY values
        Vector2 rangeMin = new Vector2(4.75f, 4.75f);
        // maximum XY values
        Vector2 rangeMax = new Vector2(5.25f, 5.25f);

        DataFrame dfFiltered = await GetCoordinateRange(rangeMin, rangeMax);
        textGUI.SetText($"Columns: {dfFiltered.Rows.Count}");
    }

    public async Task<DataFrame>GetCoordinateRange(Vector2 min, Vector2 max)
    {
        Debug.Log("Getting DataFrame filtered by coordinate");
        if (df.Rows.Count <= 0)
        {
            Debug.LogError("DataFrame is empty, returning null");
            return null;
        }

        DataFrame dfFiltered = df;
        await Task.Run(() =>
        {
            dfFiltered = dfFiltered[dfFiltered["umap_x"].ElementwiseGreaterThan(min.x)];
            dfFiltered = dfFiltered[dfFiltered["umap_x"].ElementwiseLessThan(max.x)];
            dfFiltered = dfFiltered[dfFiltered["umap_y"].ElementwiseGreaterThan(min.y)];
            dfFiltered = dfFiltered[dfFiltered["umap_y"].ElementwiseLessThan(max.y)];
        });
        Debug.Log("Filtering complete, returning DataFrame");
        return dfFiltered;
    }

    private async Task ReadParquet()
    {
        Debug.Log($"Reading parquet from {filePath}");

        await Task.Run(async () =>
        {
            using (var stream = File.OpenRead(filePath))
            {
                df = await stream.ReadParquetAsDataFrameAsync();
            }
        });
    }

    private async Task CopyParquetToPersistentPath(string streamingFilePath)
    {
        Debug.Log("Getting persistent asset path location for parquet");
        string persistentFilePath = streamingFilePath.Replace(Application.streamingAssetsPath, Application.persistentDataPath);

        var persistentFileDirectory = Path.GetDirectoryName(persistentFilePath);
        if (!Directory.Exists(persistentFileDirectory))
        {
            Debug.Log("Parquet persistent path directory does not exist, creating new directory");
            Directory.CreateDirectory(persistentFileDirectory);
        }

        UnityWebRequest loader = UnityWebRequest.Get(streamingFilePath);
        Debug.Log("Sending parquet web request...");
        await loader.SendWebRequest();
        if (loader.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Parquet web request succeeded, copying parquet to persistent asset path");
            File.WriteAllBytes(persistentFilePath, loader.downloadHandler.data);
        }
        else
        {
            Debug.LogError("Cannot load parquet at " + streamingFilePath);
        }

        filePath = persistentFilePath;
    }

}
