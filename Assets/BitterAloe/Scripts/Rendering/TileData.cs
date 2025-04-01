using Cysharp.Threading.Tasks;
using GPUInstancerPro;
using Microsoft.Data.Analysis;
using ProceduralToolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


public class TileData : MonoBehaviour
{
    private LevelData level;

    public Vector2 tileIndex;

    private DataFrame df;
    public RenderTransformList aloePlants = new RenderTransformList();
    public List<RenderTransformList> objects = new List<RenderTransformList>();
    //public List<Vector3> localAloePositions;
    public KDTree kdTree;
    private bool levelFound = false;
    public bool dataFound = false;


    public void Start()
    {
        level = transform.parent.GetComponent<LevelData>();
        levelFound = true;
    }

    public async UniTask<bool> GetAloeData()
    {
        while (levelFound == false || level.parq.parquetRead == false)
            await UniTask.Yield();

        Vector2 rangeMin = await level.parq.GetCoordinateBoundMin(tileIndex, level.parq.plantMapSampleScale);
        Vector2 rangeMax = await level.parq.GetCoordinateBoundMax(tileIndex, level.parq.plantMapSampleScale);

        df = await level.parq.GetDataFrameWithinBounds(level.parq.df, rangeMin, rangeMax);
        if (df.Rows.Count >= 1)
        {
            List<Vector3> localPositions = (await level.parq.GetCoordinatesAsNativeArray(df)).ToList();
            localPositions = await ScalePositionsToLocal(localPositions, rangeMin, rangeMax);
            localPositions = await GetHeights(localPositions);

            aloePlants.AddTransforms(tileIndex, localPositions);

            kdTree = await MakeKDTree(aloePlants.GetPositions());
        }
        dataFound = true;
        level.debug.Log($"Tile {tileIndex}: Local positions found, {aloePlants.transforms.Count} total.");
        return true;
    }

    public async UniTask<bool> GetObjectData()
    {
        for (int i = 0; i < level.gpui.objectRenderers.Count; i++)
        {
            PlaceObjectSettings placeSettings = level.gpui.objectRenderers[i].prototype.prefabObject.GetComponent<PlaceObjectSettings>();

            int objectInstanceCount = UnityEngine.Random.Range(placeSettings.countPerTileRange.x, placeSettings.countPerTileRange.y);
            await UniTask.RunOnThreadPool(async () =>
            {
                RenderTransformList renderTransformList = new RenderTransformList();
                for (int j = 0; j < objectInstanceCount; j++)
                {
                    await UniTask.Yield();
                    Vector3 startPoint = RandomPointAboveTerrain();

                    RaycastHit hit;
                    if (Physics.Raycast(startPoint, Vector3.down, out hit, float.MaxValue, LayerMask.GetMask("Terrain")))
                    {
                        Quaternion orientation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(Vector3.up * UnityEngine.Random.Range(0f, 360f));
                        RaycastHit boxHit;
                        if (Physics.BoxCast(startPoint, Vector3.one, Vector3.down, out boxHit, orientation, float.MaxValue, LayerMask.GetMask("Terrain")))
                        {
                            RenderTransform objectTransform = new RenderTransform(
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
            });

        }
        return true;
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

    //// converts localPositions from localspace to worldspace
    //public List<Vector3> GetGlobalPositions()
    //{
    //    List<Vector3> globalPositions = new List<Vector3>();

    //    for (int i = 0; i < aloePlants.transforms.Count; i++)
    //        globalPositions[i] = aloePlants.transforms[i].position + aloePlants.tileTransform.position;

    //    return globalPositions;
    //}

    // converts raw data positions to localspace relative to tile
    public async UniTask<List<Vector3>> ScalePositionsToLocal(List<Vector3> rawPositions, Vector2 min, Vector2 max)
    {
        List<Vector3> rescaledCoordinates = new List<Vector3>();

        await UniTask.RunOnThreadPool(() =>
        {
            for (int i = 0; i < rawPositions.Count; i++)
            {
                // normalizes coordinates from 0 to 1
                var xNorm = (rawPositions[i].x - min.x) / (max.x - min.x);
                var zNorm = (rawPositions[i].z - min.y) / (max.y - min.y);

                // scales up coordinates to match the size and position of the chunk
                rescaledCoordinates.Add(new Vector3((xNorm - 0.5f) * level.tc.tileSize.x, rawPositions[i].y, (zNorm - 0.5f) * level.tc.tileSize.z));
            }
        });
        level.debug.Log($"Tile {tileIndex}: Coordinates rescaled.");
        return rescaledCoordinates;
    }


    private async UniTask<List<Vector3>> GetHeights(List<Vector3> localPositions)
    {
        List<Vector3> levelPositions = GetLevelPositions(localPositions);//aloePlants.GetLevelPositions(level.tc.tileSize.x);

        for (int i = 0; i < levelPositions.Count; i++)
        {
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

        level.debug.Log($"Tile {tileIndex}: Plant heights calculated.");
        return localPositions;
    }

    public async UniTask<DataFrameRow> GetDatapointUsingKDTree(Vector3 coordinates)
    {
        var chunkPlantIndex = kdTree.FindNearest(coordinates);
        var datasetIndex = df.Rows[chunkPlantIndex]["index"];
        return df.Rows[chunkPlantIndex];
    }

    private async UniTask<KDTree> MakeKDTree(List<Vector3> coordinates)
    {
        var kdTree = KDTree.MakeFromPoints(coordinates.ToArray());
        level.debug.Log($"Tile {tileIndex}: KDTree created.");
        return kdTree;
    }

    //public async UniTask<NativeArray<Vector3>> LocalizeCoordinates(NativeArray<Vector3> globalCoordinates, Vector2 tileIndex, Vector3 tileSize)
    //{
    //    NativeArray<Vector3> localCoordinates = new NativeArray<Vector3>(globalCoordinates.Length, Allocator.TempJob);

    //    await UniTask.RunOnThreadPool(() =>
    //    {
    //        for (int i = 0; i < globalCoordinates.Length; i++)
    //        {
    //            localCoordinates[i] = new Vector3(
    //                globalCoordinates[i].x - (tileIndex.x * tileSize.x),
    //                globalCoordinates[i].y,
    //                globalCoordinates[i].z - (tileIndex.y * tileSize.z));
    //            //Debug.Log($"global: {globalCoordinates[i]}, local: {localCoordinates[i]}");
    //        }
    //    });
    //    return localCoordinates;
    //}

    //private async UniTask<NativeArray<Vector3>> DoubleArray(NativeArray<Vector3> array)
    //{
    //    //level.rdc.Log("Doubling the instance count of the coordinate array to render in both eyes in VR");
    //    NativeArray<Vector3> doubledArray = new NativeArray<Vector3>(array.Length * 2, Allocator.TempJob);

    //    for (int i = 0; i < array.Length * 2; i += 2)
    //    {
    //        doubledArray[i] = array[i / 2];
    //        doubledArray[i + 1] = array[i / 2];
    //    }
    //    //level.rdc.Log($"Returning doubled coordinate array of length {doubledArray.Length}");
    //    return doubledArray;
    //}
}
