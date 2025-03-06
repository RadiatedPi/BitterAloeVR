using Apache.Arrow;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Data.Analysis;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using Parquet.Serialization;
using ProceduralToolkit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Networking;
using static Unity.Collections.AllocatorManager;

public class Testimony
{
    public string speaker { get; set; }
    public string dialogue { get; set; }
    public string file { get; set; }
    public int file_index { get; set; }
    public string saha_page { get; set; }
    public int saha_loc { get; set; }
    public string hearing_type { get; set; }
    public string location { get; set; }
    public int file_num { get; set; }
    public string date { get; set; }
    public int hdbscan_label { get; set; }
    public int index { get; set; }
}


public class ParquetParser : MonoBehaviour
{
    public TextMeshPro textGUI;
    public string fileName = "trctestimonies.parquet";

    private string filePath;
    public DataFrame df = new DataFrame();
    public DataFrame dfFiltered;
    public IList<Testimony> testimonies;
    public KDTree kdTree;
    public Vector2 plantMapCenterOffset = new(5f, 5f);
    public float plantMapSampleScale = 0.05f;

    public bool parquetRead = false;

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
        DataFrame tempDf = await ReadParquetIntoDataFrame(filePath);
         
        // REMOVE ONCE QUERY PERFORMANCE ISSUES ARE FIXED
        // ---------------------------------------------------------------------------------------
        tempDf = tempDf.Filter(tempDf["location"].ElementwiseEquals("Cape Town"));
        //Vector2 min = await GetCoordinateBoundMin(new Vector2(-10, -10), plantMapSampleScale);
        //Vector2 max = await GetCoordinateBoundMax(new Vector2(10, 10), plantMapSampleScale);
        //tempDf = await GetDataFrameWithinBounds(tempDf, min, max);
        // ---------------------------------------------------------------------------------------

        df = await AddIndexColumnToDataFrame(tempDf);
        kdTree = await CreateKDTree(df);
          
        parquetRead = true;
        Debug.Log($"Parquet successfully read into DataFrame");
    } 
     

    





    // only used if running on android (meta quest)
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
        Debug.Log($"DataFrame successfully made from Parquet");
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
        DateTime startTime = DateTime.Now;
        float frameBudget = 0.01f; // max amount of time to do work per frame

        // for some reason the KDTree algorithm only accepts Vector3[] inputs
        var coordinateNativeArray = await GetCoordinatesAsNativeArray(df);
        // rescales coordinates so plant density isn't too high, scale value can be changed on the ParquetParser component
        for (int i = 0; i < coordinateNativeArray.Length; i++)
        {
            var x = coordinateNativeArray[i].x / plantMapSampleScale;
            var z = coordinateNativeArray[i].z / plantMapSampleScale;
            coordinateNativeArray[i] = new Vector3(x, coordinateNativeArray[i].y, z);

            TimeSpan timeElapsed = DateTime.Now - startTime;
            if (timeElapsed.TotalSeconds > frameBudget)
            {
                // reset the start time and wait a frame
                startTime = DateTime.Now;
                await UniTask.Yield();
            }
        }
        var kdTree = KDTree.MakeFromPoints(coordinateNativeArray.ToArray());
        return kdTree;
    }

    public async UniTask<NativeArray<Vector3>> GetCoordinatesAsNativeArray(DataFrame df)
    {
        DateTime startTime = DateTime.Now;
        float frameBudget = 0.01f; // max amount of time to do work per frame

        Debug.Log("Converting coordinates from DataFrame into NativeArray");
        NativeArray<Vector3> array = new NativeArray<Vector3>((int)df.Rows.Count, Allocator.TempJob);
        // these index values are specific to the trctestimonies.parquet dataset
        // TODO: make these values less arbitrary somehow
        int xColumnIndex = 10;
        int zColumnIndex = 11;
        TimeSpan timeElapsed;
        for (int i = 0; i < df.Rows.Count; i++)
        {
            // System.Convert.ToSingle firmly tells unity that this object var is in fact a float so it doesn't panic
            //float x = System.Convert.ToSingle(df[i, xColumnIndex]);
            //float z = System.Convert.ToSingle(df[i, zColumnIndex]);
            // "0f" is a placeholder for the y axis coordinate, which is calculated later
            array[i] = new Vector3(System.Convert.ToSingle(df[i, xColumnIndex]), 0f, System.Convert.ToSingle(df[i, zColumnIndex]));

            timeElapsed = DateTime.Now - startTime;
            if (timeElapsed.TotalSeconds > frameBudget)
            {
                // reset the start time and wait a frame
                startTime = DateTime.Now;
                await UniTask.Yield();
            }
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
    public async UniTask<DataFrame> GetDataFrameWithinBounds(DataFrame df, Vector2 min, Vector2 max)
    {
        Debug.Log($"Creating chunk-specific DataFrame");

        if (df.Rows.Count <= 0)
        {
            Debug.LogError("DataFrame is empty (no plants are on this chunk), returning null");
            return null;
        }

        // TODO: There's gotta be a faster way to do this
        DataFrame tileDf = df;
        await UniTask.RunOnThreadPool(() =>
            { tileDf = tileDf.Filter(tileDf["umap_x"].ElementwiseGreaterThan(min.x)); });
        await UniTask.RunOnThreadPool(() =>
            { tileDf = tileDf.Filter(tileDf["umap_x"].ElementwiseLessThan(max.x)); });
        await UniTask.RunOnThreadPool(() =>
            { tileDf = tileDf.Filter(tileDf["umap_y"].ElementwiseGreaterThan(min.y)); });
        await UniTask.RunOnThreadPool(() =>
            { tileDf = tileDf.Filter(tileDf["umap_y"].ElementwiseLessThan(max.y)); });

        


        Debug.Log("Chunk-specific DataFrame created");
        return tileDf;
    }



    public async void GetParquetAsIList(string fileName)
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
        testimonies = await ReadParquetIntoIList(filePath);
        //df = await AddIndexColumnToDataFrame(df);
        //kdTree = await CreateKDTree(df);
        Debug.Log($"Parquet successfully read into IList");
    }

    // this makes the dataset workable
    private async UniTask<IList<Testimony>> ReadParquetIntoIList(string filePath)
    {
        Debug.Log($"Opening parquet file from {filePath}");

        IList<Testimony> testimonies = new List<Testimony>();
        await UniTask.RunOnThreadPool(async () =>
        {
            using (var stream = File.OpenRead(filePath))
            {
                Debug.Log($"Parquet file successfully opened. Converting parquet to IList");
                //testimonies = await ParquetSerializer.DeserializeAsync<Testimony>(stream);
                var reader = await ParquetReader.CreateAsync(stream);
                List<DataField> readableFields = (from df in reader.Schema.Fields
                                                  select df as DataField into df
                                                  where df != null
                                                  select df).Cast<DataField>().ToList();

                ParquetSchema schema = new ParquetSchema(
                    new DataField<string>   ("speaker"),
                    new DataField<string>   ("dialogue"),
                    new DataField<string>   ("file"),
                    new DataField<Int64>     ("file_index"),
                    new DataField<string>   ("saha_page"),
                    new DataField<Int64>     ("saha_loc"),
                    new DataField<string>   ("hearing_type"),
                    new DataField<string>   ("location"),
                    new DataField<Int64>     ("file_num"),
                    new DataField<string>   ("date"),
                    new DataField<float>    ("umap_x"),
                    new DataField<float>    ("umap_y"),
                    new DataField<Int64>     ("hdbscan_label"),
                    new DataField<int>      ("index")
                    );


                for (int rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount; rowGroupIndex++)
                {
                    using var rgr = reader.OpenRowGroupReader(rowGroupIndex);
                    testimonies = await ParquetSerializer.DeserializeAsync<Testimony>(rgr, schema);
                }

            }
        });
        Debug.Log($"IList successfully made from Parquet");

        return testimonies;
    }

    // this allows you to know the index of the results of KDTree queries
    private async UniTask<DataFrame> AddIndexColumnToIList(IList<Testimony> ilist)
    {
        int[] indexes = new int[ilist.Count];

        for (int i = 0; i < indexes.Length; i++)
            indexes[i] = i;

        PrimitiveDataFrameColumn<int> indexCol = new("index", indexes);
        df.Columns.Add(indexCol);

        Debug.Log($"Added index column to DataFrame");

        return df;
    }
}