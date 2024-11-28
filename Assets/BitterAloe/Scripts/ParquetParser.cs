using Microsoft.Data.Analysis;
using Parquet;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Networking;
using static Unity.Collections.AllocatorManager;

public class ParquetParser : MonoBehaviour
{
    public TextMeshPro textGUI;
    public string fileName = "trctestimonies.parquet";
    public Vector2 coordinateRange = new Vector2(0f, 10f);

    private string filePath;
    public DataFrame df;
    public DataFrame dfFiltered;
    public NativeArray<Vector3> coordinates;

    // Start is called before the first frame update
    void Start()
    {
        //GetParquetDataset(fileName);
    }

    public async Task<NativeArray<Vector3>> GetParquetDataset(string fileName)
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

        dfFiltered = await GetCoordinateRange(rangeMin, rangeMax);
        textGUI.SetText($"Columns: {dfFiltered.Rows.Count}");
        coordinates = await GetCoordinateArray(dfFiltered, Allocator.Persistent);
        return coordinates;
    }

    public async Task<DataFrame> GetCoordinateRange(Vector2 min, Vector2 max)
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

    public async Task<NativeArray<Vector3>> GetCoordinateArray(DataFrame df, Allocator allocator)
    {
        NativeArray<Vector3> dfArray = new NativeArray<Vector3>((int)df.Rows.Count, allocator);
        int xIndex = 10;
        int zIndex = 11;

        await Task.Run(() =>
        {
            for (int i = 0; i < df.Rows.Count; i++)
            {
                float x = System.Convert.ToSingle(df[i, xIndex]);
                float z = System.Convert.ToSingle(df[i, zIndex]);
                dfArray[i] = new Vector3(x * 1000-5000, 0f, z * 1000 - 5000);
            }
        });
        return dfArray;
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
