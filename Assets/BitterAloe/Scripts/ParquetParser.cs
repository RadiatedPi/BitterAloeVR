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

    private string filePath;
    public DataFrame df = null;
    public DataFrame dfFiltered;
    public KDTree kdTree;
    public Vector2 plantMapCenterOffset = new(5f, 5f);
    public float plantMapSampleScale = 0.05f;

    // Start is called before the first frame update
    void Start()
    {
        GetParquetAsDataFrame(fileName);
    }

    public async void GetParquetAsDataFrame(string fileName)
    {
        Debug.Log("Fetching parquet file");
        string streamingFilePath = Path.Combine(Application.streamingAssetsPath, fileName);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        Debug.Log("Detected platform: Windows/Editor");
        filePath = streamingFilePath;
#elif UNITY_ANDROID
        Debug.Log("Detected platform: Android");
        filePath = await CopyParquetToPersistentPath(streamingFilePath);
#endif
        df = await ReadParquetIntoDataFrame(filePath);
        df = await AddIndexColumnToDataFrame(df);
        kdTree = await CreateKDTree(df);
        Debug.Log($"Parquet successfully read into DataFrame");
    }

    // this function is only used if running on android (meta quest)
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

    // this makes the dataset workable
    private async UniTask<DataFrame> ReadParquetIntoDataFrame(string filePath)
    {
        Debug.Log($"Opening parquet file from {filePath}");

        DataFrame df = new DataFrame();
        await UniTask.RunOnThreadPool(async () =>
        {
            using (var stream = File.OpenRead(filePath))
            {
                Debug.Log($"Parquet file successfully opened. Converting parquet to DataFrame");
                df = await stream.ReadParquetAsDataFrameAsync();
            }
        });
        Debug.Log($"Parquet successfully made from DataFrame");
        return df;
    }

    // this allows you to know the index of the results of KDTree queries
    private async UniTask<DataFrame> AddIndexColumnToDataFrame(DataFrame df)
    {
        int[] indexes = new int[df.Rows.Count];

        for (int i = 0; i < indexes.Length; i++)
            indexes[i] = i;

        PrimitiveDataFrameColumn<int> indexCol = new("index", indexes);
        df.Columns.Add(indexCol);

        Debug.Log($"Added index column to DataFrame");

        return df;
    }

    // meant to reduce loading time for plant selection
    private async UniTask<KDTree> CreateKDTree(DataFrame df)
    {
        // for some reason the KDTree algorithm only accepts Vector3[] inputs
        var coordinateNativeArray = await GetCoordinatesAsNativeArray(df);
        // rescales coordinates so plant density isn't too high, scale value can be changed on the ParquetParser component
        for (int i = 0; i < coordinateNativeArray.Length; i++)
        {
            var x = coordinateNativeArray[i].x / plantMapSampleScale;
            var z = coordinateNativeArray[i].z / plantMapSampleScale;
            coordinateNativeArray[i] = new Vector3(x, coordinateNativeArray[i].y, z);
        }
        var kdTree = KDTree.MakeFromPoints(coordinateNativeArray.ToArray());
        return kdTree;
    }

    public async UniTask<NativeArray<Vector3>> GetCoordinatesAsNativeArray(DataFrame df)
    {
        Debug.Log("Converting coordinates from DataFrame into NativeArray");
        NativeArray<Vector3> array = new NativeArray<Vector3>((int)df.Rows.Count, Allocator.Persistent);
        // these index values are specific to the trctestimonies.parquet dataset
        // TODO: make these values less arbitrary somehow
        int xColumnIndex = 10;
        int zColumnIndex = 11;

        for (int i = 0; i < df.Rows.Count; i++)
        {
            // System.Convert.ToSingle firmly tells unity that this var is in fact a float so it doesn't panic
            float x = System.Convert.ToSingle(df[i, xColumnIndex]);
            float z = System.Convert.ToSingle(df[i, zColumnIndex]);
            // "0f" is a placeholder for the y axis coordinate, which is calculated later
            array[i] = new Vector3(x, 0f, z);
        }

        Debug.Log($"Returning NativeArray of XYZ coordinates (y is still 0)");
        return array;
    }


    // TODO: everything from here down is only used in SampleRenderMeshIndirect.cs, should maybe be relocated


    // finds min bounds of a given chunk, used for making chunk-specific DataFrame
    public async UniTask<Vector2> GetCoordinateBoundMin(Vector2 chunkIndex, float plantMapScale)
    {
        var xMin = (chunkIndex.x * plantMapScale) - (plantMapScale / 2);
        var yMin = (chunkIndex.y * plantMapScale) - (plantMapScale / 2);
        Vector2 rangeMin = new Vector2(xMin, yMin) + plantMapCenterOffset;
        return rangeMin;
    }

    // finds min bounds of a given chunk, used for making chunk-specific DataFrame
    public async UniTask<Vector2> GetCoordinateBoundMax(Vector2 chunkIndex, float plantMapScale)
    {
        var xMax = (chunkIndex.x * plantMapScale) + (plantMapScale / 2);
        var yMax = (chunkIndex.y * plantMapScale) + (plantMapScale / 2);
        Vector2 rangeMin = new Vector2(xMax, yMax) + plantMapCenterOffset;
        return rangeMin;
    }

    // filters DataFrame by given bounds to determine which plants are on a chunk
    public async UniTask<DataFrame> GetTerrainChunkDataFrame(Vector2 min, Vector2 max)
    {
        Debug.Log($"Creating chunk-specific DataFrame");

        if (df.Rows.Count <= 0)
        {
            Debug.LogError("DataFrame is empty (no plants are on this chunk), returning null");
            return null;
        }

        // TODO: There's gotta be a faster way to do this
        DataFrame chunkDf = df;
        await UniTask.RunOnThreadPool(() =>
        {
            chunkDf = chunkDf[chunkDf["umap_x"].ElementwiseGreaterThan(min.x)];
            UniTask.Yield();
            chunkDf = chunkDf[chunkDf["umap_x"].ElementwiseLessThan(max.x)];
            UniTask.Yield();
            chunkDf = chunkDf[chunkDf["umap_y"].ElementwiseGreaterThan(min.y)];
            UniTask.Yield();
            chunkDf = chunkDf[chunkDf["umap_y"].ElementwiseLessThan(max.y)];
            UniTask.Yield();
        });

        Debug.Log("Chunk-specific DataFrame created");
        return chunkDf;
    }

    // converts coordinates from global to local relative to a given chunk
    public async UniTask<NativeArray<Vector3>> ScaleCoordinateArray(Vector2 chunkIndex, NativeArray<Vector3> array, Vector2 min, Vector2 max, Vector3 chunkScale)
    {
        //Debug.Log("Converting NativeArray of coordinates to local terrain chunk space");
        NativeArray<Vector3> newArray = new NativeArray<Vector3>(array.Length, Allocator.Persistent);

        await UniTask.RunOnThreadPool(() =>
        {
            for (int i = 0; i < array.Length; i++)
            {
                // normalizes coordinates from 0 to 1
                var xNorm = (array[i].x - min.x) / (max.x - min.x);
                var zNorm = (array[i].z - min.y) / (max.y - min.y);
                // scales up coordinates to match the size and position of the chunk
                newArray[i] = new Vector3((xNorm - 0.5f + chunkIndex.x) * chunkScale.x, array[i].y, (zNorm - 0.5f + chunkIndex.y) * chunkScale.z);
                //Debug.Log($"{newArray[i]}");
            }
        });
        //Debug.Log($"NativeArray coordinates localized");
        return newArray;
    }
}
