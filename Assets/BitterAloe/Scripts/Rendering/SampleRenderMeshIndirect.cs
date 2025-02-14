using Cysharp.Threading.Tasks;
using Microsoft.Data.Analysis;
using ProceduralToolkit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Rendering.HableCurve;

public class SampleRenderMeshIndirect : MonoBehaviour
{
    public ParquetParser parquetParser;
    public GenerateMesh gm;
    public Vector2 chunkIndex;
    public Vector3 chunkScale;
    private DataFrame df;
    NativeArray<Vector3> coordinatesForRendering;
    public NativeArray<Vector3> rawCoordinates;
    public KDTree kdTree;

    [SerializeField] private int _count;
    [SerializeField] private Mesh _mesh;
    public Material _material;
    [SerializeField] private ShadowCastingMode _shadowCastingMode;
    [SerializeField] private bool _receiveShadows;

    private GraphicsBuffer _drawArgsBuffer;
    private GraphicsBuffer _dataBuffer;

    public void Start()
    {
        gm = GetComponent<GenerateMesh>();
    }

    public async UniTaskVoid GetChunkPlantData()
    {
        //Debug.Log("Waiting for DataFrame to exist before getting plant DataFrame and Array for terrain chunk");
        while (parquetParser.kdTree == null)
        {
            //Debug.Log("Waiting...");
            await UniTask.Yield();
            //yield return null;
        }
        //Debug.Log("Getting plant DataFrame and Array for terrain chunk");
        //yield return new WaitUntil(() => parquetParser.df != null);
        Vector2 rangeMin = await parquetParser.GetCoordinateBoundMin(chunkIndex, parquetParser.plantMapSampleScale);
        Vector2 rangeMax = await parquetParser.GetCoordinateBoundMax(chunkIndex, parquetParser.plantMapSampleScale);
        df = await parquetParser.GetTerrainChunkDataFrame(rangeMin, rangeMax);
        if (df.Rows.Count >= 1)
        {
            rawCoordinates = await parquetParser.GetCoordinatesAsNativeArray(df);
            rawCoordinates = await parquetParser.ScaleCoordinateArray(chunkIndex, rawCoordinates, rangeMin, rangeMax, chunkScale);
            rawCoordinates = await GetPlantHeights(chunkIndex, rawCoordinates);
            kdTree = await MakeChunkKDTree(rawCoordinates);

            coordinatesForRendering = await DoubleInstanceCount(rawCoordinates);
            //Debug.Log(coordinates.Length);

            UniTask.Void(StartRender);
        }
    }

    private async UniTask<KDTree> MakeChunkKDTree(NativeArray<Vector3> coordinates)
    {
        var kdTree = KDTree.MakeFromPoints(coordinates.ToArray());
        return kdTree;
    }

    public async UniTask<DataFrameRow> GetDatapointUsingKDTree(Vector3 coordinates)
    {
        Debug.Log($"Input coordinates: {coordinates}");
        var chunkPlantIndex = kdTree.FindNearest(coordinates);
        //Debug.Log($"chunkPlantIndex: {chunkPlantIndex}");
        //Debug.Log(df.Rows.Count);
        var datasetIndex = df.Rows[chunkPlantIndex]["index"];
        //Debug.Log($"datasetIndex: {datasetIndex}");

        Debug.Log($"Coordinates of plant in df: (" +
            $"{(System.Convert.ToSingle(df[chunkPlantIndex, 10]) - 5) * chunkScale.x / parquetParser.plantMapSampleScale}, " +
            $"{(System.Convert.ToSingle(df[chunkPlantIndex, 11]) - 5) * chunkScale.z / parquetParser.plantMapSampleScale})");
        
        Debug.Log(df.Rows[chunkPlantIndex]);

        return df.Rows[chunkPlantIndex];
    }

    private async UniTask<NativeArray<Vector3>> GetPlantHeights(Vector2 tileIndex, NativeArray<Vector3> coordinateArray)
    {
        // TODO: Grab the actual values for these variables to make sure changes to terrain generation parameters don't
        // mess this function up
        Vector3 terrainSize = new Vector3(32, 8, 32);

        float cellSize = 8;

        Vector2 startOffset = new Vector2(0, 0);

        Vector2 noiseRange = Vector2.one * 256;
        
        int xSegments = Mathf.FloorToInt(terrainSize.x / cellSize);
        int zSegments = Mathf.FloorToInt(terrainSize.z / cellSize);

        float xStep = terrainSize.x / xSegments;
        float zStep = terrainSize.z / zSegments;

        for (int i = 0; i < coordinateArray.Length; i++)
        {
            float noiseX = coordinateArray[i].x / xStep / xSegments + 0.5f + startOffset.x / xStep;
            float noiseZ = coordinateArray[i].z / zStep / zSegments + 0.5f + startOffset.y / zStep;

            noiseX = (noiseX) % noiseRange.x;
            noiseZ = (noiseZ) % noiseRange.y;
            
            //account for negatives (ex. -1 % 256 = -1, needs to loop around to 255)
            if (noiseX < 0)
                noiseX = noiseX + noiseRange.x;
            if (noiseZ < 0)
                noiseZ = noiseZ + noiseRange.y;

            float height = Mathf.PerlinNoise(noiseX, noiseZ);
            Debug.Log($"For render, tile is {chunkIndex.x}, {chunkIndex.y}, noise is {noiseX}, {noiseZ}");
            coordinateArray[i] = new Vector3(coordinateArray[i].x, height * terrainSize.y, coordinateArray[i].z);
        }

        return coordinateArray;
    }

    //private async UniTask<NativeArray<Vector3>> GetYCoordinates(NativeArray<Vector3> array)
    //{
    //    QueryParameters queryParameters = new QueryParameters(LayerMask.GetMask("Terrain Interact"), false, QueryTriggerInteraction.Ignore);

    //    Debug.Log("Using raycasts to find appropriate Y-axis value of each plant");
    //    NativeArray<Vector3> newArray = new NativeArray<Vector3>(array.Length, Allocator.Persistent);

    //    for (int i = 0; i < array.Length; i++)
    //    {
    //        //Debug.Log("Attempting raycast...");
    //        //Physics.Raycast(new Vector3(array[i].x, 20, array[i].z), -transform.up, out RaycastHit hit, 200f, LayerMask.GetMask("Terrain Interact"), QueryTriggerInteraction.Ignore);
    //        //newArray[i] = new Vector3(array[i].x, hit.point.y - 0.43f, array[i].z);
    //        //Debug.Log("Raycast success");

    //        //newArray[i] = new Vector3(array[i].x, GetHeight(array[i].x, array[i].z, gm) - 0.43f, array[i].z);
    //        newArray[i] = new Vector3(array[i].x, 0, array[i].z);

    //        //await UniTask.Yield();
    //    }


    //    Debug.Log($"Returning NativeArray of coordinates with appropriate Y-axis values of length {newArray.Length}");
    //    return newArray;
    //}

    private async UniTask<NativeArray<Vector3>> DoubleInstanceCount(NativeArray<Vector3> array)
    {
        Debug.Log("Doubling the instance count of the coordinate array to render in both eyes in VR");
        NativeArray<Vector3> doubledArray = new NativeArray<Vector3>(array.Length * 2, Allocator.Persistent);

        for (int i = 0; i < array.Length * 2; i += 2)
        {
            doubledArray[i] = array[i / 2];
            doubledArray[i + 1] = array[i / 2];
        }
        Debug.Log($"Returning doubled coordinate array of length {doubledArray.Length}");
        return doubledArray;
    }

    private async UniTaskVoid StartRender()
    {
        Debug.Log("Starting terrain chunk plant rendering");

        _drawArgsBuffer = await CreateDrawArgsBufferForRenderMeshIndirect(_mesh, coordinatesForRendering.Length);
        _dataBuffer = await CreateDataBuffer<Matrix4x4>(coordinatesForRendering.Length);

        var transformMatrixArray = await TransformMatrixArrayFactory.Create(coordinatesForRendering);
        _dataBuffer.SetData(transformMatrixArray);

        _material.SetBuffer("_TransformMatrixArray", _dataBuffer);
        _material.SetVector("_BoundsOffset", transform.position);

        transformMatrixArray.Dispose();

    }


    private void Update()
    {
        var renderParams = new RenderParams(_material)
        {
            receiveShadows = _receiveShadows,
            shadowCastingMode = _shadowCastingMode,
            worldBounds = new Bounds(transform.position, chunkScale * 1.25f)
        };

        Graphics.RenderMeshIndirect(
            renderParams,
            _mesh,
            _drawArgsBuffer
        );
    }

    private void OnDestroy()
    {
        _drawArgsBuffer?.Dispose();
        _dataBuffer?.Dispose();
    }

    private static async UniTask<GraphicsBuffer> CreateDrawArgsBufferForRenderMeshIndirect(Mesh mesh, int instanceCount)
    {
        var commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        commandData[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
        {
            indexCountPerInstance = mesh.GetIndexCount(0),
            instanceCount = (uint)instanceCount,
            startIndex = mesh.GetIndexStart(0),
            baseVertexIndex = mesh.GetBaseVertex(0),
        };

        var drawArgsBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments,
            1,
            GraphicsBuffer.IndirectDrawIndexedArgs.size
        );
        drawArgsBuffer.SetData(commandData);

        return drawArgsBuffer;
    }

    private static async UniTask<GraphicsBuffer> CreateDataBuffer<T>(int instanceCount) where T : struct
    {
        return new GraphicsBuffer(
            GraphicsBuffer.Target.Structured, instanceCount,
            Marshal.SizeOf(typeof(T))
        );
    }

    private static float GetHeight(float x, float z, GenerateMesh gm)
    {
        //private static MeshDraft TerrainDraft(Vector3 terrainSize, float cellSize, Vector2 noiseOffset, float noiseScale)
        //{
        //    int xSegments = Mathf.FloorToInt(terrainSize.x / cellSize);

        Vector3 terrainSize = new Vector3(32, 8, 32);
        float cellSize = 8;
        float noiseScale = 1;

        int xSegments = Mathf.FloorToInt(terrainSize.x / cellSize);
        int zSegments = Mathf.FloorToInt(terrainSize.z / cellSize);

        float noiseX = gm.NoiseOffset.x + x/xSegments;
        float noiseZ = gm.NoiseOffset.x + z/zSegments;
        return Mathf.PerlinNoise(noiseX, noiseZ) * terrainSize.y;
    }
}
