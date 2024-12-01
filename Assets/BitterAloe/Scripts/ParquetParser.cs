using Cysharp.Threading.Tasks;
using Microsoft.Data.Analysis;
using Parquet;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Networking;
using static Unity.Collections.AllocatorManager;

public class ParquetParser : MonoBehaviour
{
    public TextMeshPro textGUI;
    public string fileName = "trctestimonies.parquet";
    //public Vector2 coordinateRange = new Vector2(0f, 10f);

    private string filePath;
    public DataFrame df = null;
    public DataFrame dfFiltered;
    //public NativeArray<Vector3> coordinates;

    // Start is called before the first frame update
    void Start()
    {
        GetParquetAsDataFrame(fileName);
    }

    public async void GetParquetAsDataFrame(string fileName)
    {
        Debug.Log("Getting parquet");
        string streamingFilePath = Path.Combine(Application.streamingAssetsPath, fileName);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        Debug.Log("Detected platform: Windows/Editor");
        filePath = streamingFilePath;
#elif UNITY_ANDROID
        Debug.Log("Detected platform: Android");
        filePath = await CopyParquetToPersistentPath(streamingFilePath);
#endif
        df = await ReadParquet(filePath);
        Debug.Log($"Parquet successfully read into DataFrame. DataFrame length: {df.Rows.Count}");
        //textGUI.SetText($"Columns: {dfFiltered.Rows.Count}");
    }

    private async UniTask<string> CopyParquetToPersistentPath(string streamingFilePath)
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

        return persistentFilePath;
    }

    private async UniTask<DataFrame> ReadParquet(string filePath)
    {
        Debug.Log($"Reading parquet from {filePath}");

        DataFrame df = new DataFrame();
        await Task.Run(async () =>
        {
            using (var stream = File.OpenRead(filePath))
            {
                df = await stream.ReadParquetAsDataFrameAsync();
            }
        });
        return df;
    }

    public async UniTask<Vector2> GetCoordinateBoundMin(Vector2 chunkIndex, float plantDistributionScale)
    {
        var xMin = (chunkIndex.x * plantDistributionScale) - (plantDistributionScale / 2);
        var yMin = (chunkIndex.y * plantDistributionScale) - (plantDistributionScale / 2);
        Vector2 rangeMin = new Vector2(xMin+5, yMin+5);
        return rangeMin;
    }

    public async UniTask<Vector2> GetCoordinateBoundMax(Vector2 chunkIndex, float plantDistributionScale)
    {
        var xMax = (chunkIndex.x * plantDistributionScale) + (plantDistributionScale / 2);
        var yMax = (chunkIndex.y * plantDistributionScale) + (plantDistributionScale / 2);
        Vector2 rangeMin = new Vector2(xMax+5, yMax+5);
        return rangeMin;
    }

    public async UniTask<DataFrame> GetTerrainChunkDataFrame(Vector2 min, Vector2 max)
    {
        //Debug.Log("Getting DataFrame filtered by coordinate");
        if (df.Rows.Count <= 0)
        {
            //Debug.LogError("DataFrame is empty, returning null");
            return null;
        }

        DataFrame chunkDf = df;
        await Task.Run(() =>
        {
            chunkDf = chunkDf[chunkDf["umap_x"].ElementwiseGreaterThan(min.x)];
            chunkDf = chunkDf[chunkDf["umap_x"].ElementwiseLessThan(max.x)];
            chunkDf = chunkDf[chunkDf["umap_y"].ElementwiseGreaterThan(min.y)];
            chunkDf = chunkDf[chunkDf["umap_y"].ElementwiseLessThan(max.y)];
        });
        //Debug.Log("Filtering complete, returning DataFrame");
        return chunkDf;
    }

    public async UniTask<NativeArray<Vector3>> GetCoordinatesAsNativeArray(DataFrame df)
    {
        //Debug.Log("Converting coordinates from DataFrame into NativeArray");
        NativeArray<Vector3> array = new NativeArray<Vector3>((int)df.Rows.Count, Allocator.Persistent);
        int xIndex = 10;
        int zIndex = 11;

        await Task.Run(() =>
        {
            for (int i = 0; i < df.Rows.Count; i++)
            {
                float x = System.Convert.ToSingle(df[i, xIndex]);
                float z = System.Convert.ToSingle(df[i, zIndex]);
                array[i] = new Vector3(x, 0f, z);
            }
        });
        //Debug.Log($"Returning new NativeArray of length {array.Length}");
        return array;
    }

    public async UniTask<NativeArray<Vector3>> LocalizeCoordinateArray(Vector2 chunkIndex, NativeArray<Vector3> array, Vector2 min, Vector2 max, float chunkScale)
    {
        Debug.Log("Converting NativeArray of coordinates to local terrain chunk space");
        NativeArray<Vector3> newArray = new NativeArray<Vector3>(array.Length, Allocator.Persistent);

        await Task.Run(() =>
        {
            for (int i = 0; i < array.Length; i++)
            {
                var xNorm = (array[i].x - min.x) / (max.x - min.x);
                var zNorm = (array[i].z - min.y) / (max.y - min.y);
                newArray[i] = new Vector3((xNorm - 0.5f + chunkIndex.x) * chunkScale, array[i].y, (zNorm - 0.5f + chunkIndex.y) * chunkScale);
            }
        });
        //Debug.Log($"Returning NativeArray of localized coordinates of length {newArray.Length}");
        return newArray;
    }
}
