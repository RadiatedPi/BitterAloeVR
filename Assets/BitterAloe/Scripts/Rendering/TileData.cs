using BitterAloe;
using Cysharp.Threading.Tasks;
using DataStructures.ViliWonka.KDTree;
using GPUInstancerPro;
using Microsoft.Data.Analysis;
using ProceduralToolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;


public class TileData : MonoBehaviour
{
    private LevelData level;

    public Vector2 tileIndex;

    //public List<Testimony> testimonies;
    //public List<Vector3> kdTreeAloePositions;
    //public RenderTransformList aloePlants = new RenderTransformList();
    public List<RenderTransformList> objects = new List<RenderTransformList>();
    //public KDTreeOld kdTree;
    private bool levelFound = false;
    public bool dataFound = false;


    public void Start()
    {
        level = transform.parent.GetComponent<LevelData>();
        levelFound = true;
    }

    //static readonly ProfilerMarker GetAloeDataProfiler = new ProfilerMarker("GetAloeData");
    //public async UniTask<bool> GetAloeData()
    //{
    //    while (levelFound == false || level.parq.parquetRead == false)
    //        await UniTask.Yield();

    //    using (GetAloeDataProfiler.Auto())
    //    {
    //        //Vector2 rangeMin = level.parq.GetCoordinateBoundMin(tileIndex, 1 / level.parq.dataScalar);
    //        //Vector2 rangeMax = level.parq.GetCoordinateBoundMax(tileIndex, 1 / level.parq.dataScalar);
    //        //level.debug.Log($"rangeMin = {rangeMin}, rangeMax = {rangeMax}");

    //        //testimonies = level.parq.TestimonySearchByUmapRange(level.parq.testimonies, rangeMin, rangeMax);
    //        testimonies = level.parq.GetTestimoniesInTile(tileIndex);
    //        if (testimonies.Count >= 1)
    //        {
    //            //List<Vector3> localPositions = level.parq.GetDataCoordinateList(testimonies);
    //            //localPositions = ScalePositionsToLocal(localPositions, rangeMin, rangeMax);

    //            //localPositions = GetHeights(localPositions);

    //            //localPositions = localPositions.Where(pos =>
    //            //    pos.x <= level.tc.tileSize.x / 2 &&
    //            //    pos.x >= -level.tc.tileSize.x / 2 &&
    //            //    pos.z <= level.tc.tileSize.x / 2 &&
    //            //    pos.z >= -level.tc.tileSize.x / 2
    //            //).ToList();

    //            //aloePlants.AddTransforms(tileIndex, localPositions);
    //        }
    //        dataFound = true;
    //        //level.debug.Log($"Tile {tileIndex}: Local positions found, {aloePlants.transforms.Count} total.");
    //    }
    //    return true;
    //}

    //static readonly ProfilerMarker GetObjectDataProfiler = new ProfilerMarker("GetObjectData");
    public async UniTask GetObjectData()
    {
        //using (GetObjectDataProfiler.Auto())
        //{
        while (levelFound == false || level.parq.parquetRead == false)
            await UniTask.Yield();
        //LoadUtilities loadUtil = new LoadUtilities(0.05f);
        PlaceObjectSettings placeSettings;
        RenderTransformList renderTransformList = new RenderTransformList();
        RaycastHit hit;
        Quaternion orientation;
        RaycastHit boxHit;
        RenderTransform objectTransform;
        for (int i = 0; i < level.gpui.objectRenderers.Count; i++)
        {
            //await loadUtil.YieldForFrameBudget();
            placeSettings = level.gpui.objectRenderers[i].prototype.prefabObject.GetComponent<PlaceObjectSettings>();

            int objectInstanceCount = UnityEngine.Random.Range(placeSettings.countPerTileRange.x, placeSettings.countPerTileRange.y);
            //await UniTask.RunOnThreadPool(async () =>
            //{
            renderTransformList.transforms.Clear();
            for (int j = 0; j < objectInstanceCount; j++)
            {
                //await loadUtil.YieldForFrameBudget();
                Vector3 startPoint = RandomPointAboveTerrain();
                if (Physics.Raycast(startPoint, Vector3.down, out hit, float.MaxValue, LayerMask.GetMask("Terrain")))
                {
                    orientation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(Vector3.up * UnityEngine.Random.Range(0f, 360f));
                    if (Physics.BoxCast(startPoint, Vector3.one, Vector3.down, out boxHit, orientation, float.MaxValue, LayerMask.GetMask("Terrain")))
                    {
                        objectTransform = new RenderTransform(
                            new Vector3(startPoint.x - transform.position.x, hit.point.y + placeSettings.heightOffset, startPoint.z - transform.position.z),
                            orientation,
                            UnityEngine.Random.Range(placeSettings.sizeRange.x, placeSettings.sizeRange.y)
                        );
                        renderTransformList.transforms.Add(objectTransform);
                    }
                }
            }
            renderTransformList.tileIndex = tileIndex;
            objects.Add(renderTransformList);
            //});
        }
        //}
    }

    private Vector3 RandomPointAboveTerrain()
    {
        return new Vector3(
            UnityEngine.Random.Range(transform.position.x - level.tc.TerrainSize.x / 2, transform.position.x + level.tc.TerrainSize.x / 2),
            transform.position.y + level.tc.TerrainSize.y * 2,
            UnityEngine.Random.Range(transform.position.z - level.tc.TerrainSize.z / 2, transform.position.z + level.tc.TerrainSize.z / 2)
        );
    }



    //// converts localPositions from localspace to levelspace
    public List<Vector3> GetLevelPositions(List<Vector3> localPositions)
    {
        List<Vector3> levelPositions = new List<Vector3>();

        Vector3 levelOffset = new Vector3(tileIndex.x * level.tc.tileSize.x, 0, tileIndex.y * level.tc.tileSize.z);

        for (int i = 0; i < localPositions.Count; i++)
            levelPositions.Add(localPositions[i] + levelOffset);

        return levelPositions;
    }


    // converts raw data positions to localspace relative to tile
    static readonly ProfilerMarker ScalePositionsToLocalProfiler = new ProfilerMarker("ScalePositionsToLocal");
    public List<Vector3> ScalePositionsToLocal(List<Vector3> positions, Vector2 min, Vector2 max)
    {
        using (ScalePositionsToLocalProfiler.Auto())
        {

            //LoadUtilities loadUtil = new LoadUtilities(0.05f);

            //List<Vector3> rescaledPositions = new List<Vector3>();

            //await UniTask.RunOnThreadPool(() =>
            //{
            for (int i = 0; i < positions.Count; i++)
            {
                //await loadUtil.YieldForFrameBudget();
                // normalizes coordinates from 0 to 1
                var xNorm = (positions[i].x - min.x) / (max.x - min.x);
                var zNorm = (positions[i].z - min.y) / (max.y - min.y);

                // scales up coordinates to match the size and position of the chunk
                //rescaledPositions.Add(new Vector3((xNorm - 0.5f) * level.tc.tileSize.x, rawPositions[i].y, (zNorm - 0.5f) * level.tc.tileSize.z));
                positions[i] = new Vector3((xNorm - 0.5f) * level.tc.tileSize.x, positions[i].y, (zNorm - 0.5f) * level.tc.tileSize.z);
            }



            //});
            //level.debug.Log($"Tile {tileIndex}: Positions rescaled.");
            return positions;
        }
    }

    public async UniTask<List<Vector3>> NormalizePositionsToLocal(List<Vector3> rawPositions, Vector2 min, Vector2 max)
    {
        LoadUtilities loadUtil = new LoadUtilities(0.05f);

        List<Vector3> normalizedPositions = new List<Vector3>();

        //await UniTask.RunOnThreadPool(() =>
        //{
        for (int i = 0; i < rawPositions.Count; i++)
        {
            await loadUtil.YieldForFrameBudget();
            // normalizes coordinates from 0.5 to -0.5
            float xNorm = ((rawPositions[i].x - min.x) / (max.x - min.x)) - 0.5f;
            float zNorm = ((rawPositions[i].z - min.y) / (max.y - min.y)) - 0.5f;
            normalizedPositions.Add(new Vector3(xNorm, rawPositions[i].y, zNorm));
        }
        //});
        Debug.Log($"Tile {tileIndex}: Positions normalized.");
        return normalizedPositions;
    }

    public async UniTask<List<Vector3>> RescaleNormalizedPositions(List<Vector3> normalizedPositions, float scale)
    {
        List<Vector3> scaledPositions = new List<Vector3>();

        await UniTask.RunOnThreadPool(() =>
        {
            for (int i = 0; i < normalizedPositions.Count; i++)
            {
                scaledPositions.Add(new Vector3(normalizedPositions[i].x * scale, normalizedPositions[i].y, normalizedPositions[i].x * scale));
            }
        });
        Debug.Log($"Tile {tileIndex}: Positions rescaled by {scale}x.");
        return scaledPositions;
    }



    static readonly ProfilerMarker GetHeightsProfiler = new ProfilerMarker("GetHeights");
    private List<Vector3> GetHeights(List<Vector3> localPositions)
    {
        using (GetHeightsProfiler.Auto())
        {
            //LoadUtilities loadUtil = new LoadUtilities(0.05f);
            List<Vector3> levelPositions = GetLevelPositions(localPositions);//aloePlants.GetLevelPositions(level.tc.tileSize.x);

            for (int i = 0; i < levelPositions.Count; i++)
            {
                //await loadUtil.YieldForFrameBudget();
                // adjusts for offset caused by the temporary terrain tile border quads generated to calculate normals
                float quadLength = level.tc.tileSize.x / Mathf.FloorToInt(level.tc.tileSize.x / level.tc.cellSize);

                // +0.5f to compensate for terrain tile origins being at center
                float noiseX = (levelPositions[i].x + quadLength) / level.tc.tileSize.x + 0.5f + level.tc.startOffset.x;
                float noiseZ = (levelPositions[i].z + quadLength) / level.tc.tileSize.z + 0.5f + level.tc.startOffset.y;

                noiseX = (noiseX) % level.tc.noiseRange.x;
                noiseZ = (noiseZ) % level.tc.noiseRange.y;

                //account for negatives (ex. -1 % 256 = -1, needs to loop around to 255)
                if (noiseX < 0)
                    noiseX = noiseX + level.tc.noiseRange.x;
                if (noiseZ < 0)
                    noiseZ = noiseZ + level.tc.noiseRange.y;

                float height = Mathf.PerlinNoise(noiseX, noiseZ);

                localPositions[i] = new Vector3(localPositions[i].x, height * level.tc.tileSize.y, localPositions[i].z);
            }

            //level.debug.Log($"Tile {tileIndex}: Plant heights calculated.");
        }
        return localPositions;
    }



    //public async UniTask<Testimony> GetTileDatapointUsingKDTree(Vector3 coordinates)
    //{
    //    var chunkPlantIndex = kdTree.FindNearest(coordinates);
    //    //var datasetIndex = (int)df.Rows[chunkPlantIndex]["index"];
    //    return testimonies[chunkPlantIndex];
    //}
    //private async UniTask<KDTreeOld> MakeKDTree(List<Vector3> coordinates)
    //{
    //    var kdTree = KDTreeOld.MakeFromPoints(coordinates.ToArray());
    //    //level.debug.Log($"Tile {tileIndex}: KDTree created.");
    //    return kdTree;
    //}
}
