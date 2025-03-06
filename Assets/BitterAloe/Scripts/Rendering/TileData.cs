using Cysharp.Threading.Tasks;
using Microsoft.Data.Analysis;
using ProceduralToolkit;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class TileData : MonoBehaviour
{
    private GlobalReferences gr;

    public Vector2 tileIndex;

    private DataFrame df;
    public NativeArray<Vector3> coordinates;
    public NativeArray<Vector3> globalCoordinates;
    public KDTree kdTree;
    private bool grFound = false;


    public void Start()
    {
        gr = GameObject.FindWithTag("Reference").GetComponent<GlobalReferences>();
        grFound = true;
    }

    public async UniTask<bool> GetPlantData()
    {
        while (grFound == false)
        {
            await UniTask.Yield(); 
        }
        while (gr.parq.parquetRead == false)
        {
            await UniTask.Yield(); 
        }
        Vector2 rangeMin = await gr.parq.GetCoordinateBoundMin(tileIndex, gr.parq.plantMapSampleScale);
        Vector2 rangeMax = await gr.parq.GetCoordinateBoundMax(tileIndex, gr.parq.plantMapSampleScale);
        df = await gr.parq.GetDataFrameWithinBounds(gr.parq.df, rangeMin, rangeMax);
        if (df.Rows.Count >= 1)
        {
            globalCoordinates = await gr.parq.GetCoordinatesAsNativeArray(df);
            globalCoordinates = await ScaleCoordinates(globalCoordinates, tileIndex, rangeMin, rangeMax, gr.tc.tileSize);
            globalCoordinates = await GetPlantHeights(globalCoordinates);
             
            kdTree = await MakeKDTree(globalCoordinates);

            coordinates = await LocalizeCoordinates(globalCoordinates, tileIndex, gr.tc.tileSize);
            coordinates = await DoubleArray(coordinates);
        } 
        return true;
    }

    // converts coordinates from global to local relative to a given chunk
    public async UniTask<NativeArray<Vector3>> ScaleCoordinates(NativeArray<Vector3> rawCoordinates, Vector2 tileIndex, Vector2 min, Vector2 max, Vector3 tileScale)
    {
        //Debug.Log("Converting NativeArray of coordinates to local terrain chunk space");
        NativeArray<Vector3> scaledCoordinates = new NativeArray<Vector3>(rawCoordinates.Length, Allocator.TempJob);

        await UniTask.RunOnThreadPool(() =>
        {
            for (int i = 0; i < rawCoordinates.Length; i++)
            {
                // normalizes coordinates from 0 to 1
                var xNorm = (rawCoordinates[i].x - min.x) / (max.x - min.x);
                var zNorm = (rawCoordinates[i].z - min.y) / (max.y - min.y);
                // scales up coordinates to match the size and position of the chunk
                //newArray[i] = new Vector3((xNorm - 0.5f + chunkIndex.x) * chunkScale.x, array[i].y, (zNorm - 0.5f + chunkIndex.y) * chunkScale.z);
                scaledCoordinates[i] = new Vector3((xNorm - 0.5f + tileIndex.x) * tileScale.x, rawCoordinates[i].y, (zNorm - 0.5f + tileIndex.y) * tileScale.z);
                //Debug.Log($"Scaled coordinate {i}: {scaledCoordinates[i]}");
            }
        });
        //Debug.Log($"NativeArray coordinates localized");
        return scaledCoordinates;
    }

    public async UniTask<NativeArray<Vector3>> LocalizeCoordinates(NativeArray<Vector3> globalCoordinates, Vector2 tileIndex, Vector3 tileSize)
    {
        NativeArray<Vector3> localCoordinates = new NativeArray<Vector3>(globalCoordinates.Length, Allocator.TempJob);

        await UniTask.RunOnThreadPool(() =>
        {
            for (int i = 0; i < globalCoordinates.Length; i++)
            {
                localCoordinates[i] = new Vector3(
                    globalCoordinates[i].x - (tileIndex.x * tileSize.x),
                    globalCoordinates[i].y,
                    globalCoordinates[i].z - (tileIndex.y * tileSize.z));
                Debug.Log($"global: {globalCoordinates[i]}, local: {localCoordinates[i]}");
            }
        });
        return localCoordinates;
    }



    public async UniTask<DataFrameRow> GetDatapointUsingKDTree(Vector3 coordinates) 
    {
        var chunkPlantIndex = kdTree.FindNearest(coordinates);
        var datasetIndex = df.Rows[chunkPlantIndex]["index"];
        return df.Rows[chunkPlantIndex];
    }


    private async UniTask<NativeArray<Vector3>> GetPlantHeights(NativeArray<Vector3> coordinateArray)
    {
        for (int i = 0; i < coordinateArray.Length; i++)
        {
            float noiseX = coordinateArray[i].x / gr.tc.tileSize.x + 0.5f + gr.tc.startOffset.x;
            float noiseZ = coordinateArray[i].z / gr.tc.tileSize.z + 0.5f + gr.tc.startOffset.y;

            noiseX = (noiseX) % gr.tc.noiseRange.x;
            noiseZ = (noiseZ) % gr.tc.noiseRange.y;

            //account for negatives (ex. -1 % 256 = -1, needs to loop around to 255)
            if (noiseX < 0)
                noiseX = noiseX + gr.tc.noiseRange.x;
            if (noiseZ < 0)
                noiseZ = noiseZ + gr.tc.noiseRange.y;

            float height = Mathf.PerlinNoise(noiseX, noiseZ);

            coordinateArray[i] = new Vector3(coordinateArray[i].x, height * gr.tc.tileSize.y + 0.2f, coordinateArray[i].z);
        }

        return coordinateArray;
    }

    private async UniTask<KDTree> MakeKDTree(NativeArray<Vector3> coordinates)
    {
        var kdTree = KDTree.MakeFromPoints(coordinates.ToArray());
        return kdTree;
    }

    private async UniTask<NativeArray<Vector3>> DoubleArray(NativeArray<Vector3> array)
    {
        Debug.Log("Doubling the instance count of the coordinate array to render in both eyes in VR");
        NativeArray<Vector3> doubledArray = new NativeArray<Vector3>(array.Length * 2, Allocator.TempJob);

        for (int i = 0; i < array.Length * 2; i += 2)
        {
            doubledArray[i] = array[i / 2];
            doubledArray[i + 1] = array[i / 2];
        }
        Debug.Log($"Returning doubled coordinate array of length {doubledArray.Length}");
        return doubledArray;
    }
}
