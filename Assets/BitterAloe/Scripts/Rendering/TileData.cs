using Cysharp.Threading.Tasks;
using Microsoft.Data.Analysis;
using ProceduralToolkit;
using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class TileData : MonoBehaviour
{
    private LevelData level;

    public Vector2 tileIndex;

    private DataFrame df;
    public NativeArray<Vector3> localPositions;
    public KDTree kdTree;
    private bool levelFound = false;
    public bool dataFound = false;


    public void Start()
    {
        level = transform.parent.GetComponent<LevelData>();
        levelFound = true;
    }

    public async UniTask<bool> GetPlantData()
    {
        while (levelFound == false || level.parq.parquetRead == false)
            await UniTask.Yield();

        Vector2 rangeMin = await level.parq.GetCoordinateBoundMin(tileIndex, level.parq.plantMapSampleScale);
        Vector2 rangeMax = await level.parq.GetCoordinateBoundMax(tileIndex, level.parq.plantMapSampleScale);

        df = await level.parq.GetDataFrameWithinBounds(level.parq.df, rangeMin, rangeMax);
        if (df.Rows.Count >= 1)
        {
            NativeArray<Vector3> positions = await level.parq.GetCoordinatesAsNativeArray(df);
            positions = await ScalePositionsToLocal(positions, rangeMin, rangeMax);
            positions = await GetPlantHeights(positions);
            localPositions = positions;

            kdTree = await MakeKDTree(localPositions);

        }
        dataFound = true;
        level.rdc.Log($"Tile {tileIndex}: Local coordinates calculated, {localPositions.Length} total.");
        return true;
    }

    // converts localPositions from localspace to levelspace
    public NativeArray<Vector3> GetLevelPositions(NativeArray<Vector3> localPositions)
    {
        NativeArray<Vector3> levelPositions = new NativeArray<Vector3>(localPositions.Length, Allocator.TempJob);

        Vector3 levelOffset = new Vector3(tileIndex.x * level.tc.tileSize.x, 0, tileIndex.y * level.tc.tileSize.z);

        for (int i = 0; i < localPositions.Length; i++)
            levelPositions[i] = localPositions[i] + levelOffset;

        return levelPositions;
    }

    // converts localPositions from localspace to worldspace
    public NativeArray<Vector3> GetGlobalPositions()
    {
        NativeArray<Vector3> globalPositions = new NativeArray<Vector3>(localPositions.Length, Allocator.TempJob);

        for (int i = 0; i < localPositions.Length; i++)
            globalPositions[i] = localPositions[i] + transform.position; 

        return globalPositions;
    }

    // converts raw data positions to localspace relative to tile
    public async UniTask<NativeArray<Vector3>> ScalePositionsToLocal(NativeArray<Vector3> rawPositions, Vector2 min, Vector2 max)
    {
        NativeArray<Vector3> rescaledCoordinates = new NativeArray<Vector3>(rawPositions.Length, Allocator.TempJob);

        await UniTask.RunOnThreadPool(() =>
        {
            for (int i = 0; i < rawPositions.Length; i++)
            {
                // normalizes coordinates from 0 to 1
                var xNorm = (rawPositions[i].x - min.x) / (max.x - min.x);
                var zNorm = (rawPositions[i].z - min.y) / (max.y - min.y);

                // scales up coordinates to match the size and position of the chunk
                rescaledCoordinates[i] = new Vector3((xNorm - 0.5f) * level.tc.tileSize.x, rawPositions[i].y, (zNorm - 0.5f) * level.tc.tileSize.z);
            }
        });
        level.rdc.Log($"Tile {tileIndex}: Coordinates rescaled.");
        return rescaledCoordinates;
    }


    private async UniTask<NativeArray<Vector3>> GetPlantHeights(NativeArray<Vector3> localPositions)
    {
        NativeArray<Vector3> levelPositions = GetLevelPositions(localPositions);

        for (int i = 0; i < levelPositions.Length; i++)
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

            localPositions[i] = new Vector3(localPositions[i].x, height * level.tc.tileSize.y + 0.2f, localPositions[i].z);
        }

        level.rdc.Log($"Tile {tileIndex}: Plant heights calculated.");
        return localPositions;
    }

    public async UniTask<DataFrameRow> GetDatapointUsingKDTree(Vector3 coordinates)
    {
        var chunkPlantIndex = kdTree.FindNearest(coordinates);
        var datasetIndex = df.Rows[chunkPlantIndex]["index"];
        return df.Rows[chunkPlantIndex];
    }

    private async UniTask<KDTree> MakeKDTree(NativeArray<Vector3> coordinates)
    {
        var kdTree = KDTree.MakeFromPoints(coordinates.ToArray());
        level.rdc.Log($"Tile {tileIndex}: KDTree created.");
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
