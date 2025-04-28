using Apache.Arrow;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
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
using BitterAloe;
using UnityEngine.UIElements;
using System.Reflection;
using UnityEngine.SocialPlatforms;
using Unity.Profiling;
using static UnityEngine.Rendering.DebugUI;
using DataStructures.ViliWonka.KDTree;


public class ParquetParser : MonoBehaviour
{
    #region debug
    static readonly ProfilerMarker CoordinateSearchFirstLoop = new ProfilerMarker("CoordinateSearch.Loop1");
    static readonly ProfilerMarker CoordinateSearchSecondLoop = new ProfilerMarker("CoordinateSearch.Loop2");
    static readonly ProfilerMarker CoordinateSearchThirdLoop = new ProfilerMarker("CoordinateSearch.Loop3");
    static readonly ProfilerMarker CoordinateSearchFourthLoop = new ProfilerMarker("CoordinateSearch.Loop4");
    static readonly ProfilerMarker CoordinateSearchFifthLoop = new ProfilerMarker("CoordinateSearch.Loop5");
    static readonly ProfilerMarker CoordinateSearchLinq = new ProfilerMarker("CoordinateSearch.Linq");
    #endregion

    const int worldTileLength = 2000;

    private LevelData level;
    public string fileName = "trctestimonies.parquet";

    private string filePath;
    //public DataFrame df = new DataFrame();
    //public DataFrame dfFiltered;
    public List<Testimony> testimonyList;
    public Testimonies testimonies;
    public KDTree kdTree;
    public float plantMapSampleScale = 0.025f;

    public bool parquetRead = false;

    // adjusts data to make all values positive
    public Vector2 dataRecenter = new Vector2(18f, 17f);
    // offset from middle of dataset
    public Vector2 dataCenterOffset = new(5f, 5f);
    // value datapoints are scaled by after offset adjustments
    //public float dataScalar = 40f;

    //public int[,,] testimonyTileIndices = new int[2000, 2000, 500];
    //public int[,] tileIndiceLengths = new int[2000, 2000];
    public Vector2 testimonyTileCenterOffset = new(920f, 880f);
    //public Vector2 testimonyTileCenterOffset = new(1300, 1474);

    public List<int>[,] tileIndices = new List<int>[worldTileLength, worldTileLength];
    public List<Vector3> testimonyLevelPositions;
    //private List<Testimony> testimonyBuffer;


    // Start is called before the first frame update
    async void Start()
    {
        level = GetComponent<LevelData>();
        await GetParquetAsDataFrame(fileName);
    }


    //static readonly ProfilerMarker GetParquetAsDataFrameProfiler = new ProfilerMarker("GetParquetAsDataFrame");
    public async UniTask GetParquetAsDataFrame(string fileName)
    {
        //using (GetParquetAsDataFrameProfiler.Auto())
        //{

        //level.debug.Log("Fetching parquet file");
        string streamingFilePath = Path.Combine(Application.streamingAssetsPath, fileName);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        //level.debug.Log("Detected platform: Windows/Editor");
        filePath = streamingFilePath;
#elif UNITY_ANDROID
        //Debug.Log("Detected platform: Android");
        filePath = await CopyParquetToPersistentPath(streamingFilePath);
#endif
        DataFrame tempDf = await ReadParquetIntoDataFrame(filePath);

        testimonyList = ConvertDataFrameToTestimonyList(tempDf);
        testimonyList = GetTestimonyLevelPositions(testimonyList);
        testimonyList = GetHeights(testimonyList);

        //testimonyBuffer = new List<Testimony>(testimonyList.Count);

        testimonies = new Testimonies(testimonyList);
        await testimonies.SortTestimonies();

        //testimonyList = await TestimonySearchByHearingType(testimonies, "Human Rights Violation Hearings");
        //testimonyList = await TestimonySearchByLocation(testimonies, "Cape Town");


        testimonyLevelPositions = GetLevelPositionList(testimonyList);
        //Debug.Log($"height = {testimonyLevelPositions[0].y}");
        await PopulateTiles(testimonyLevelPositions);

        kdTree = MakeKDTree(testimonyLevelPositions);

        //Testimony filter = new Testimony();
        //filter.location = "Cape Town";
        //testimonyList = await TestimonyElementwiseEquals(filter, testimonyList);

        // REMOVE ONCE QUERY PERFORMANCE ISSUES ARE FIXED
        // ---------------------------------------------------------------------------------------
        //tempDf = tempDf.Filter(tempDf["location"].ElementwiseEquals("Cape Town"));
        //Vector2 min = await GetCoordinateBoundMin(new Vector2(-10, -10), plantMapSampleScale);
        //Vector2 max = await GetCoordinateBoundMax(new Vector2(10, 10), plantMapSampleScale);
        //tempDf = await GetDataFrameWithinBounds(tempDf, min, max);
        // ---------------------------------------------------------------------------------------

        parquetRead = true;
        level.debug.Log($"Parquet successfully read into DataFrame");
        //}
    }

    // only used if running on android (meta quest)
    private async UniTask<string> CopyParquetToPersistentPath(string streamingFilePath)
    {
        level.debug.Log("Getting persistent asset path location for parquet");
        string persistentFilePath = streamingFilePath.Replace(Application.streamingAssetsPath, Application.persistentDataPath);

        var persistentFileDirectory = Path.GetDirectoryName(persistentFilePath);
        if (!Directory.Exists(persistentFileDirectory))
        {
            level.debug.Log("Parquet persistent path directory does not exist, creating new directory");
            Directory.CreateDirectory(persistentFileDirectory);
        }

        UnityWebRequest loader = UnityWebRequest.Get(streamingFilePath);
        level.debug.Log("Sending parquet web request...");
        await loader.SendWebRequest();
        if (loader.result == UnityWebRequest.Result.Success)
        {
            level.debug.Log("Parquet web request succeeded, copying parquet to persistent asset path");
            File.WriteAllBytes(persistentFilePath, loader.downloadHandler.data);
        }
        else
        {
            Debug.LogError("Cannot load parquet at " + streamingFilePath);
        }

        return persistentFilePath;
    }


    static readonly ProfilerMarker ConvertDataFrameToTestimonyListProfiler = new ProfilerMarker("ConvertDataFrameToTestimonyList");
    private List<Testimony> ConvertDataFrameToTestimonyList(DataFrame df)
    {
        using (ConvertDataFrameToTestimonyListProfiler.Auto())
        {

            //LoadUtilities loadUtil = new LoadUtilities(0.05f);

            List<Testimony> testimonies = new List<Testimony>();
            Testimony testimony = new Testimony();

            for (int i = 0; i < df.Rows.Count; i++)
            {
                //await loadUtil.YieldForFrameBudget();
                testimony = ConvertRowToTestimony(df.Rows[i], i);
                testimonies.Add(testimony);
            }

            return testimonies;
        }
    }

    static readonly ProfilerMarker GetTestimonyLevelPositionsProfiler = new ProfilerMarker("GetTestimonyLevelPositions");
    public List<Testimony> GetTestimonyLevelPositions(List<Testimony> testimonies)
    {
        using (GetTestimonyLevelPositionsProfiler.Auto())
        {
            Vector2 max = Vector2.zero;
            Vector2 min = Vector2.one;
            for (int i = 0; i < testimonies.Count; i++)
            {
                if (max.x < (float)testimonies[i].umap_x)
                    max.x = (float)testimonies[i].umap_x;
                if (max.y < (float)testimonies[i].umap_y)
                    max.y = (float)testimonies[i].umap_y;

                if (min.x > (float)testimonies[i].umap_x)
                    min.x = (float)testimonies[i].umap_x;
                if (min.y > (float)testimonies[i].umap_y)
                    min.y = (float)testimonies[i].umap_y;
            }

            for (int i = 0; i < testimonies.Count; i++)
            {
                // normalizes coordinates from 0 to 1
                var xNorm = ((float)testimonies[i].umap_x - min.x) / (max.x - min.x);
                var zNorm = ((float)testimonies[i].umap_y - min.y) / (max.y - min.y);

                // scales up coordinates to match the size and position of the chunk
                testimonies[i].levelPosition = new Vector3(
                    (xNorm - 0.5f) * level.tc.tileSize.x * (worldTileLength - 2),
                    testimonies[i].levelPosition.y,
                    (zNorm - 0.5f) * level.tc.tileSize.z * (worldTileLength - 2));
            }

            return testimonies;
        }
    }

    static readonly ProfilerMarker GetHeightProfiler = new ProfilerMarker("GetHeight");
    private float GetHeight(Testimony testimony)
    {
        using (GetHeightProfiler.Auto())
        {
            // adjusts for offset caused by the temporary terrain tile border quads generated to calculate normals
            float quadLength = level.tc.tileSize.x / Mathf.FloorToInt(level.tc.tileSize.x / level.tc.cellSize);

            // +0.5f to compensate for terrain tile origins being at center
            float noiseX = ((float)testimony.umap_x + quadLength) / level.tc.tileSize.x + 0.5f + level.tc.startOffset.x;
            float noiseZ = ((float)testimony.umap_y + quadLength) / level.tc.tileSize.z + 0.5f + level.tc.startOffset.y;

            noiseX = (noiseX) % level.tc.noiseRange.x;
            noiseZ = (noiseZ) % level.tc.noiseRange.y;

            //account for negatives (ex. -1 % 256 = -1, needs to loop around to 255)
            if (noiseX < 0)
                noiseX = noiseX + level.tc.noiseRange.x;
            if (noiseZ < 0)
                noiseZ = noiseZ + level.tc.noiseRange.y;

            return Mathf.PerlinNoise(noiseX, noiseZ);
        }
    }


    static readonly ProfilerMarker GetHeightsProfiler = new ProfilerMarker("GetHeights");
    private List<Testimony> GetHeights(List<Testimony> testimonies)
    {
        using (GetHeightsProfiler.Auto())
        {
            for (int i = 0; i < testimonies.Count; i++)
            {
                // adjusts for offset caused by the temporary terrain tile border quads generated to calculate normals
                float quadLength = level.tc.tileSize.x / Mathf.FloorToInt(level.tc.tileSize.x / level.tc.cellSize);

                //Debug.Log($"umap_x = {(float)testimonies[i].umap_x}, umap_y = {(float)testimonies[i].umap_y}");

                // +0.5f to compensate for terrain tile origins being at center
                float noiseX = ((float)testimonies[i].levelPosition.x + quadLength) / level.tc.tileSize.x + 0.5f + level.tc.startOffset.x;
                float noiseZ = ((float)testimonies[i].levelPosition.z + quadLength) / level.tc.tileSize.z + 0.5f + level.tc.startOffset.y;

                noiseX = (noiseX) % level.tc.noiseRange.x;
                noiseZ = (noiseZ) % level.tc.noiseRange.y;

                //account for negatives (ex. -1 % 256 = -1, needs to loop around to 255)
                if (noiseX < 0)
                    noiseX = noiseX + level.tc.noiseRange.x;
                if (noiseZ < 0)
                    noiseZ = noiseZ + level.tc.noiseRange.y;

                testimonies[i].levelPosition = new Vector3(
                    testimonies[i].levelPosition.x,
                    Mathf.PerlinNoise(noiseX, noiseZ) * level.tc.tileSize.y,
                    testimonies[i].levelPosition.z);
            }
            return testimonies;
        }
    }


    //static readonly ProfilerMarker GetHeightsProfiler = new ProfilerMarker("GetHeights");
    //private List<Vector3> GetHeights(List<Vector3> localPositions)
    //{
    //    using (GetHeightsProfiler.Auto())
    //    {
    //        //LoadUtilities loadUtil = new LoadUtilities(0.05f);
    //        List<Vector3> levelPositions = GetLevelPositions(localPositions);//aloePlants.GetLevelPositions(level.tc.tileSize.x);

    //        for (int i = 0; i < levelPositions.Count; i++)
    //        {
    //            //await loadUtil.YieldForFrameBudget();
    //            // adjusts for offset caused by the temporary terrain tile border quads generated to calculate normals
    //            float quadLength = level.tc.tileSize.x / Mathf.FloorToInt(level.tc.tileSize.x / level.tc.cellSize);

    //            // +0.5f to compensate for terrain tile origins being at center
    //            float noiseX = (levelPositions[i].x + quadLength) / level.tc.tileSize.x + 0.5f + level.tc.startOffset.x;
    //            float noiseZ = (levelPositions[i].z + quadLength) / level.tc.tileSize.z + 0.5f + level.tc.startOffset.y;

    //            noiseX = (noiseX) % level.tc.noiseRange.x;
    //            noiseZ = (noiseZ) % level.tc.noiseRange.y;

    //            //account for negatives (ex. -1 % 256 = -1, needs to loop around to 255)
    //            if (noiseX < 0)
    //                noiseX = noiseX + level.tc.noiseRange.x;
    //            if (noiseZ < 0)
    //                noiseZ = noiseZ + level.tc.noiseRange.y;

    //            float height = Mathf.PerlinNoise(noiseX, noiseZ);

    //            localPositions[i] = new Vector3(localPositions[i].x, height * level.tc.tileSize.y, localPositions[i].z);
    //        }

    //        //level.debug.Log($"Tile {tileIndex}: Plant heights calculated.");
    //    }
    //    return localPositions;
    //}



    static readonly ProfilerMarker PopulateTilesProfiler = new ProfilerMarker("PopulateTilesWithTestimonies");
    private async UniTask PopulateTiles(List<Vector3> testimonyPositions)
    {
        await UniTask.RunOnThreadPool(() =>
        {
            using (PopulateTilesProfiler.Auto())
            {
                for (int i = 0; i < testimonyPositions.Count; i++)
                {
                    int xTileIndex = (int)Math.Round(testimonyPositions[i].x / level.tc.tileSize.x + (worldTileLength / 2 - 1), MidpointRounding.AwayFromZero);
                    int yTileIndex = (int)Math.Round(testimonyPositions[i].z / level.tc.tileSize.z + (worldTileLength / 2 - 1), MidpointRounding.AwayFromZero);

                    //Debug.Log($"x = {xTileIndex}, y = {yTileIndex}");
                    List<int> tile = tileIndices[xTileIndex, yTileIndex] ??= new List<int>();
                    tile.Add((int)testimonyList[i].index);
                }
            }
        });
    }

    static readonly ProfilerMarker GetTestimoniesInTileProfiler = new ProfilerMarker("GetTestimoniesInTile");
    public List<Testimony> GetTestimoniesInTile(Vector2 tileIndex)
    {
        using (GetTestimoniesInTileProfiler.Auto())
        {
            List<int> tile = tileIndices[
                (int)(tileIndex.x + (worldTileLength / 2 - 1)),
                (int)(tileIndex.y + (worldTileLength / 2 - 1))];

            List<Testimony> testimonies = new List<Testimony>();

            if (tile != null)
            {
                for (int i = 0; i < tile.Count; i++)
                {
                    testimonies.Add(this.testimonies.testimonyArray[tile[i]]);
                }
            }
            //Debug.Log($"testimony length: {testimonies.Count}");
            return testimonies;
        }
    }





    private KDTree MakeKDTree(List<Vector3> coordinates)
    {
        KDTree kdTree = new KDTree();
        kdTree.Build(coordinates, 1);
        return kdTree;
    }
    public int FindNearestDatapointKDTreeIndex(Vector3 coordinates)
    {
        KDQuery query = new KDQuery();
        List<int> results = new List<int>();

        query.ClosestPoint(kdTree, coordinates, results);
        return results[0];
    }




















    static readonly ProfilerMarker ConvertRowToTestimonyProfiler = new ProfilerMarker("ConvertRowToTestimony");
    private Testimony ConvertRowToTestimony(DataFrameRow row, int index)
    {
        using (ConvertRowToTestimonyProfiler.Auto())
        {

            Testimony testimony = new Testimony();

            testimony.speaker = (string)row["speaker"];
            testimony.dialogue = (string)row["dialogue"];
            testimony.file = (string)row["file"];
            testimony.file_index = System.Convert.ToInt32(row["file_index"]);
            testimony.saha_page = (string)row["saha_page"];
            testimony.saha_loc = System.Convert.ToInt32(row["saha_loc"]);
            testimony.hearing_type = (string)row["hearing_type"];
            testimony.location = (string)row["location"];
            testimony.file_num = System.Convert.ToInt32(row["file_num"]);
            testimony.date = (string)row["date"];
            testimony.umap_x = System.Convert.ToSingle(row["umap_x"]);
            testimony.umap_y = System.Convert.ToSingle(row["umap_y"]);
            testimony.hdbscan_label = System.Convert.ToInt32(row["hdbscan_label"]);
            testimony.index = index;
            testimony.levelPosition = Vector3.zero;

            return testimony;
        }
    }

    static readonly ProfilerMarker TestimonyElementwiseEqualsProfiler = new ProfilerMarker("TestimonyElementwiseEquals");
    public List<Testimony> TestimonyElementwiseEquals(Testimony filter, List<Testimony> testimonies)
    {
        using (TestimonyElementwiseEqualsProfiler.Auto())
        {

            //LoadUtilities loadUtil = new LoadUtilities(0.1f);

            List<Testimony> filteredTestimonies = new List<Testimony>();
            List<bool> propertyInFilter = new List<bool>();

            foreach (PropertyInfo property in filter.GetType().GetProperties())
            {
                bool propertyToFilter = false;

                if (property.GetValue(filter) != null)
                    propertyToFilter = true;

                propertyInFilter.Add(propertyToFilter);
            }
            level.debug.Log($"Properties to filter: {propertyInFilter.ToString()}");

            for (int i = 0; i < testimonies.Count; i++)
            {
                Testimony testimony = testimonies[i];

                //await loadUtil.YieldForFrameBudget();

                PropertyInfo[] properties = testimony.GetType().GetProperties();
                bool match = true;

                for (int j = 0; j < properties.Length; j++)
                {
                    if (propertyInFilter[j])
                    {
                        if (!properties[j].GetValue(testimony).Equals(filter.GetType().GetProperties()[j].GetValue(filter)))
                        {
                            match = false;
                        }
                    }
                }

                if (match)
                    filteredTestimonies.Add(testimony);
            }

            return filteredTestimonies;
        }
    }

    static readonly ProfilerMarker TestimonySearchByHearingTypeProfiler = new ProfilerMarker("TestimonySearchByHearingType");
    public List<Testimony> TestimonySearchByHearingType(Testimonies testimonies, string type)
    {
        using (TestimonySearchByHearingTypeProfiler.Auto())
        {
            //LoadUtilities loadUtil = new LoadUtilities(0.05f);

            level.debug.Log($"Creating chunk-specific Testimony lists");

            if (testimonies.testimonyArray.Length <= 0)
            {
                Debug.LogError("Testimony list is empty (no plants are on this chunk), returning null");
                return null;
            }

            List<Testimonies.Hearing_type> types = new List<Testimonies.Hearing_type>(testimonies.testimonyArray.Length);
            for (int i = 0; i < testimonies.hearing_typeArray.Count(); i++)
            {
                //await loadUtil.YieldForFrameBudget();
                bool matchFound = false;
                if (type.Equals(testimonies.hearing_typeArray[i].type))
                {
                    matchFound = true;
                    types.Add(testimonies.hearing_typeArray[i]);
                }
                else if (matchFound)
                {
                    break;
                }
            }

            List<Testimony> typeList = new List<Testimony>(testimonies.testimonyArray.Length);
            for (int i = 0; i < types.Count(); i++)
            {
                //await loadUtil.YieldForFrameBudget();
                typeList.Add(testimonies.testimonyArray[types.ElementAt(i).index]);
            }

            return typeList;
        }
    }

    static readonly ProfilerMarker TestimonySearchByLocationProfiler = new ProfilerMarker("TestimonySearchByLocation");
    public List<Testimony> TestimonySearchByLocation(Testimonies testimonies, string location)
    {
        using (TestimonySearchByLocationProfiler.Auto())
        {
            //LoadUtilities loadUtil = new LoadUtilities(0.05f);

            level.debug.Log($"Creating chunk-specific Testimony lists");

            if (testimonies.testimonyArray.Length <= 0)
            {
                Debug.LogError("Testimony list is empty (no plants are on this chunk), returning null");
                return null;
            }

            List<Testimonies.Location> locations = new List<Testimonies.Location>(testimonies.testimonyArray.Length);
            for (int i = 0; i < testimonies.locationArray.Count(); i++)
            {
                //await loadUtil.YieldForFrameBudget();
                bool matchFound = false;
                if (location.Equals(testimonies.locationArray[i].loc))
                {
                    matchFound = true;
                    locations.Add(testimonies.locationArray[i]);
                }
                else if (matchFound)
                {
                    break;
                }
            }

            List<Testimony> locationList = new List<Testimony>(testimonies.testimonyArray.Length);
            for (int i = 0; i < locations.Count(); i++)
            {
                //await loadUtil.YieldForFrameBudget();
                locationList.Add(testimonies.testimonyArray[locations.ElementAt(i).index]);
            }
            return locationList;
        }
    }

    static readonly ProfilerMarker TestimonySearchByHearingProfiler = new ProfilerMarker("TestimonySearchByHearing");
    public List<Testimony> TestimonySearchByFile(Testimonies testimonies, int file_num)
    {
        using (TestimonySearchByHearingProfiler.Auto())
        {
            if (testimonies.testimonyArray.Length <= 0)
            {
                Debug.LogError("Testimony list is empty (no plants are on this chunk), returning null");
                return null;
            }

            List<Testimonies.File> files = new List<Testimonies.File>(testimonies.testimonyArray.Length);
            for (int i = 0; i < testimonies.fileArray.Count(); i++)
            {
                bool matchFound = false;
                if (file_num.Equals(testimonies.fileArray[i].file_num))
                {
                    matchFound = true;
                    files.Add(testimonies.fileArray[i]);
                }
                else if (matchFound)
                {
                    break;
                }
            }

            List<Testimony> locationList = new List<Testimony>(testimonies.testimonyArray.Length);
            for (int i = 0; i < files.Count(); i++)
            {
                locationList.Add(testimonies.testimonyArray[files.ElementAt(i).index]);
            }
            return locationList;
        }
    }




    //public async UniTask<List<Testimony>> TestimonyUmapRangeLINQSearch(Testimonies testimonies, Vector2 min, Vector2 max)
    //{
    //    testimonies.umap_xArray.Where(x => x.value > min.x );
    //}




    // TODO: fix this abomination
    //public List<Testimony> TestimonySearchByUmapRange(Testimonies testimonies, Vector2 min, Vector2 max)
    //{
    //    //LoadUtilities loadUtil = new LoadUtilities(0.1f);

    //    level.debug.Log($"Creating chunk-specific Testimony lists");

    //    if (testimonies.testimonyArray.Length <= 0)
    //    {
    //        Debug.LogError("Testimony list is empty (no plants are on this chunk), returning null");
    //        return null;
    //    }

    //    // x search
    //    int first = 0;
    //    int last = testimonies.umap_xArray.Length - 1;
    //    int mid = 0;
    //    CoordinateSearchFirstLoop.Begin();
    //    do
    //    {
    //        //await loadUtil.YieldForFrameBudget();
    //        mid = first + (last - first) / 2;
    //        if (min.x > testimonies.umap_xArray[mid].value)
    //            first = mid + 1;
    //        else
    //            last = mid - 1;
    //        if (testimonies.umap_xArray[mid].value == min.x)
    //            break;
    //    } while (first <= last);
    //    CoordinateSearchFirstLoop.End();
    //    xRange.Clear();
    //    CoordinateSearchSecondLoop.Begin();
    //    for (int i = mid; i < testimonies.umap_xArray.Length; i++)
    //    {
    //        //await loadUtil.YieldForFrameBudget();
    //        xRange.Add(testimonies.umap_xArray[i]);
    //        if (testimonies.umap_xArray[i].value >= max.x)
    //            break;
    //    }
    //    CoordinateSearchSecondLoop.End();

    //    // y search
    //    first = 0;
    //    last = testimonies.umap_yArray.Length - 1;
    //    mid = 0;
    //    CoordinateSearchThirdLoop.Begin();
    //    do
    //    {
    //        //await loadUtil.YieldForFrameBudget();
    //        mid = first + (last - first) / 2;
    //        if (min.y > testimonies.umap_yArray[mid].value)
    //            first = mid + 1;
    //        else
    //            last = mid - 1;
    //        if (testimonies.umap_yArray[mid].value == min.y)
    //            break;
    //    } while (first <= last);
    //    CoordinateSearchThirdLoop.End();
    //    yRange.Clear();
    //    CoordinateSearchFourthLoop.Begin();
    //    for (int i = mid; i < testimonies.umap_yArray.Length; i++)
    //    {
    //        //await loadUtil.YieldForFrameBudget();
    //        yRange.Add(testimonies.umap_yArray[i]);
    //        if (testimonies.umap_yArray[i].value >= max.y)
    //            break;
    //    }
    //    CoordinateSearchFourthLoop.End();

    //    xyRange.Clear();
    //    CoordinateSearchLinq.Begin();
    //    //xyRange = (from x in xRange
    //    //              join y in yRange
    //    //              on x.value equals y.value
    //    //              into matches
    //    //              where matches.Any()
    //    //              select x).ToList();
    //    foreach (var x in xRange)
    //    {
    //        foreach (var y in yRange)
    //        {
    //            if (x.index == y.index)
    //            {
    //                xyRange.Add(x);
    //                break;
    //            }
    //        }
    //    }
    //    CoordinateSearchLinq.End();

    //    xyList.Clear();
    //    CoordinateSearchFifthLoop.Begin();
    //    for (int i = 0; i < xyRange.Count(); i++)
    //    {
    //        xyList.Add(testimonies.testimonyArray[xyRange.ElementAt(i).index]);
    //    }
    //    CoordinateSearchFifthLoop.End();
    //    return xyList;
    //}


    // credit: https://stackoverflow.com/questions/49843710/binary-search-closest-value-c-sharp
    // returns index of matching value, or closest index less than search value
    public async UniTask<int> BinarySearch(float[] array, float item)
    {
        //LoadUtilities loadUtil = new LoadUtilities(0.05f);
        int first = 0;
        int last = array.Length - 1;
        int mid = 0;
        do
        {
            //await loadUtil.YieldForFrameBudget();
            mid = first + (last - first) / 2;
            if (item > array[mid])
                first = mid + 1;
            else
                last = mid - 1;
            if (array[mid] == item)
                return mid;
        } while (first <= last);
        return mid;
    }


    //// meant to reduce loading time for plant selection
    //static readonly ProfilerMarker CreateKDTreeFromTestimonyListProfiler = new ProfilerMarker("CreateKDTreeFromTestimonyList");
    //private KDTreeOld CreateKDTreeFromTestimonyList(List<Testimony> testimonies)
    //{
    //    using (CreateKDTreeFromTestimonyListProfiler.Auto())
    //    {

    //        //LoadUtilities loadUtil = new LoadUtilities(0.05f);

    //        // for some reason the KDTree algorithm only accepts Vector3[] inputs
    //        var coordinateNativeArray = GetCoordinatesAsNativeArray(testimonies);
    //        // rescales coordinates so plant density isn't too high, scale value can be changed on the ParquetParser component
    //        for (int i = 0; i < coordinateNativeArray.Length; i++)
    //        {
    //            var x = coordinateNativeArray[i].x * dataScalar;
    //            var z = coordinateNativeArray[i].z * dataScalar;
    //            coordinateNativeArray[i] = new Vector3(x, coordinateNativeArray[i].y, z);

    //            //await loadUtil.YieldForFrameBudget();
    //        }
    //        var kdTree = KDTreeOld.MakeFromPoints(coordinateNativeArray.ToArray());
    //        return kdTree;
    //    }
    //}


    static readonly ProfilerMarker GetCoordinatesAsNativeArrayProfiler = new ProfilerMarker("GetCoordinatesAsNativeArray");
    public NativeArray<Vector3> GetCoordinatesAsNativeArray(List<Testimony> testimonies)
    {
        using (GetCoordinatesAsNativeArrayProfiler.Auto())
        {
            //LoadUtilities loadUtil = new LoadUtilities(0.05f);

            level.debug.Log("Converting coordinates from DataFrame into NativeArray");
            NativeArray<Vector3> array = new NativeArray<Vector3>(testimonies.Count, Allocator.TempJob);

            for (int i = 0; i < testimonies.Count; i++)
            {
                // "0f" is a placeholder for the y axis coordinate, which is calculated later
                // System.Convert.ToSingle firmly tells unity that this object var is in fact a float so it doesn't panic
                array[i] = new Vector3(System.Convert.ToSingle(testimonies[i].umap_x), 0f, System.Convert.ToSingle(testimonies[i].umap_y));

                //await loadUtil.YieldForFrameBudget();
            }

            level.debug.Log($"NativeArray<Vector3> of XZ coordinates created");
            return array;
        }
    }





    static readonly ProfilerMarker GetLevelPositionProfiler = new ProfilerMarker("GetLevelPosition");
    public Vector3 GetLevelPosition(Testimony testimony)
    {
        using (GetLevelPositionProfiler.Auto())
        {
            return testimony.levelPosition;
        }
    }
    static readonly ProfilerMarker GetWorldPositionProfiler = new ProfilerMarker("GetWorldPosition");
    public Vector3 GetWorldPosition(Testimony testimony)
    {
        using (GetWorldPositionProfiler.Auto())
        {
            return testimony.levelPosition + level.transform.position;
        }
    }

    static readonly ProfilerMarker GetLevelPositionListProfiler = new ProfilerMarker("GetLevelPositionList");
    public List<Vector3> GetLevelPositionList(List<Testimony> testimonies)
    {
        using (GetLevelPositionListProfiler.Auto())
        {
            List<Vector3> coordinateList = new List<Vector3>(testimonies.Count);
            foreach (Testimony testimony in testimonies)
            {
                coordinateList.Add(testimony.levelPosition);
            }
            return coordinateList;
        }
    }
    static readonly ProfilerMarker GetWorldPositionListProfiler = new ProfilerMarker("GetWorldPositionList");
    public List<Vector3> GetWorldPositionList(List<Testimony> testimonies)
    {
        using (GetWorldPositionListProfiler.Auto())
        {
            List<Vector3> coordinateList = new List<Vector3>(testimonies.Count);
            foreach (Testimony testimony in testimonies)
            {
                coordinateList.Add(GetWorldPosition(testimony));
            }
            return coordinateList;
        }
    }













    // this makes the dataset workable
    private async UniTask<DataFrame> ReadParquetIntoDataFrame(string filePath)
    {
        level.debug.Log($"Opening parquet file from {filePath}");

        DataFrame df = new DataFrame();
        await UniTask.RunOnThreadPool(async () =>
        {
            using (var stream = File.OpenRead(filePath))
            {
                level.debug.Log($"Parquet file successfully opened. Converting parquet to DataFrame");
                df = await stream.ReadParquetAsDataFrameAsync();
            }
        });
        level.debug.Log($"DataFrame successfully made from Parquet");
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

        level.debug.Log($"Added index column to DataFrame");

        return df;
    }

    //// meant to reduce loading time for plant selection
    //private async UniTask<KDTreeOld> CreateKDTreeFromDF(DataFrame df)
    //{
    //    DateTime startTime = DateTime.Now;
    //    float frameBudget = 0.05f; // max amount of time to do work per frame

    //    // for some reason the KDTree algorithm only accepts Vector3[] inputs
    //    var coordinateNativeArray = await GetCoordinatesAsNativeArray(df);
    //    // rescales coordinates so plant density isn't too high, scale value can be changed on the ParquetParser component
    //    for (int i = 0; i < coordinateNativeArray.Length; i++)
    //    {
    //        var x = coordinateNativeArray[i].x * dataScalar;
    //        var z = coordinateNativeArray[i].z * dataScalar;
    //        coordinateNativeArray[i] = new Vector3(x, coordinateNativeArray[i].y, z);

    //        TimeSpan timeElapsed = DateTime.Now - startTime;
    //        if (timeElapsed.TotalSeconds > frameBudget)
    //        {
    //            // reset the start time and wait a frame
    //            startTime = DateTime.Now;
    //            await UniTask.Yield();
    //        }
    //    }
    //    var kdTree = KDTreeOld.MakeFromPoints(coordinateNativeArray.ToArray());
    //    return kdTree;
    //}

    public async UniTask<NativeArray<Vector3>> GetCoordinatesAsNativeArray(DataFrame df)
    {
        DateTime startTime = DateTime.Now;
        float frameBudget = 0.05f; // max amount of time to do work per frame

        level.debug.Log("Converting coordinates from DataFrame into NativeArray");
        NativeArray<Vector3> array = new NativeArray<Vector3>((int)df.Rows.Count, Allocator.TempJob);
        // these index values are specific to the trctestimonies.parquet dataset
        // TODO: make these values less arbitrary somehow
        int xColumnIndex = 10;
        int zColumnIndex = 11;
        TimeSpan timeElapsed;
        for (int i = 0; i < df.Rows.Count; i++)
        {
            // "0f" is a placeholder for the y axis coordinate, which is calculated later
            // System.Convert.ToSingle firmly tells unity that this object var is in fact a float so it doesn't panic
            array[i] = new Vector3(System.Convert.ToSingle(df[i, xColumnIndex]), 0f, System.Convert.ToSingle(df[i, zColumnIndex]));

            timeElapsed = DateTime.Now - startTime;
            if (timeElapsed.TotalSeconds > frameBudget)
            {
                // reset the start time and wait a frame
                startTime = DateTime.Now;
                await UniTask.Yield();
            }
        }

        level.debug.Log($"NativeArray<Vector3> of XZ coordinates created");
        return array;
    }


    // TODO: everything from here down is only used in SampleRenderMeshIndirect.cs, should maybe be relocated


    // finds min bounds of a given chunk, used for making chunk-specific DataFrame
    public Vector2 GetCoordinateBoundMin(Vector2 tileIndex, float plantMapScale)
    {
        var xMin = (tileIndex.x * plantMapScale) - (plantMapScale / 2);
        var yMin = (tileIndex.y * plantMapScale) - (plantMapScale / 2);
        Vector2 rangeMin = new Vector2(xMin, yMin) + dataCenterOffset;
        return rangeMin;
    }

    // finds min bounds of a given chunk, used for making chunk-specific DataFrame
    public Vector2 GetCoordinateBoundMax(Vector2 tileIndex, float plantMapScale)
    {
        var xMax = (tileIndex.x * plantMapScale) + (plantMapScale / 2);
        var yMax = (tileIndex.y * plantMapScale) + (plantMapScale / 2);
        Vector2 rangeMin = new Vector2(xMax, yMax) + dataCenterOffset;
        return rangeMin;
    }


    // finds min bounds of a given chunk, used for making chunk-specific DataFrame
    public Vector2 GetKDTreeMin(Vector2 tileIndex, float plantMapScale)
    {
        var xMin = (tileIndex.x * plantMapScale) - (plantMapScale * 3 / 2);
        var yMin = (tileIndex.y * plantMapScale) - (plantMapScale * 3 / 2);
        Vector2 rangeMin = new Vector2(xMin, yMin) + dataCenterOffset;
        return rangeMin;
    }

    // finds min bounds of a given chunk, used for making chunk-specific DataFrame
    public Vector2 GetKDTreeMax(Vector2 tileIndex, float plantMapScale)
    {
        var xMax = (tileIndex.x * plantMapScale) + (plantMapScale * 3 / 2);
        var yMax = (tileIndex.y * plantMapScale) + (plantMapScale * 3 / 2);
        Vector2 rangeMin = new Vector2(xMax, yMax) + dataCenterOffset;
        return rangeMin;
    }









    // filters DataFrame by given bounds to determine which plants are on a chunk
    public async UniTask<DataFrame> GetDataFrameRange(DataFrame df, Vector2 min, Vector2 max)
    {
        level.debug.Log($"Creating chunk-specific DataFrame");

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


        level.debug.Log("Chunk-specific DataFrame created");
        return tileDf;
    }






    public async void GetParquetAsIList(string fileName)
    {
        level.debug.Log("Fetching parquet file");
        string streamingFilePath = Path.Combine(Application.streamingAssetsPath, fileName);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        level.debug.Log("Detected platform: Windows/Editor");
        filePath = streamingFilePath;
#elif UNITY_ANDROID
        //gr.rdc.Log("Detected platform: Android");
        filePath = await CopyParquetToPersistentPath(streamingFilePath);
#endif
        //testimonies = await ReadParquetIntoIList(filePath);

        level.debug.Log($"Parquet successfully read into IList");
    }

    // this makes the dataset workable
    private async UniTask<IList<Testimony>> ReadParquetIntoIList(string filePath)
    {
        level.debug.Log($"Opening parquet file from {filePath}");

        IList<Testimony> testimonies = new List<Testimony>();
        await UniTask.RunOnThreadPool(async () =>
        {
            using (var stream = File.OpenRead(filePath))
            {
                level.debug.Log($"Parquet file successfully opened. Converting parquet to IList");
                //testimonies = await ParquetSerializer.DeserializeAsync<Testimony>(stream);
                var reader = await ParquetReader.CreateAsync(stream);
                List<DataField> readableFields = (from df in reader.Schema.Fields
                                                  select df as DataField into df
                                                  where df != null
                                                  select df).Cast<DataField>().ToList();

                ParquetSchema schema = new ParquetSchema(
                    new DataField<string>("speaker"),
                    new DataField<string>("dialogue"),
                    new DataField<string>("file"),
                    new DataField<Int64>("file_index"),
                    new DataField<string>("saha_page"),
                    new DataField<Int64>("saha_loc"),
                    new DataField<string>("hearing_type"),
                    new DataField<string>("location"),
                    new DataField<Int64>("file_num"),
                    new DataField<string>("date"),
                    new DataField<float>("umap_x"),
                    new DataField<float>("umap_y"),
                    new DataField<Int64>("hdbscan_label"),
                    new DataField<int>("index")
                    );


                for (int rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount; rowGroupIndex++)
                {
                    using var rgr = reader.OpenRowGroupReader(rowGroupIndex);
                    testimonies = await ParquetSerializer.DeserializeAsync<Testimony>(rgr, schema);
                }

            }
        });
        level.debug.Log($"IList successfully made from Parquet");

        return testimonies;
    }

    // this allows you to know the index of the results of KDTree queries
    //private async UniTask<DataFrame> AddIndexColumnToIList(IList<Testimony> ilist)
    //{
    //    int[] indexes = new int[ilist.Count];

    //    for (int i = 0; i < indexes.Length; i++)
    //        indexes[i] = i;

    //    PrimitiveDataFrameColumn<int> indexCol = new("index", indexes);
    //    df.Columns.Add(indexCol);

    //    level.debug.Log($"Added index column to DataFrame");

    //    return df;
    //}


}