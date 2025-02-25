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
    //private GlobalReferences globalRef;
    public ParquetParser parq;
    public TerrainController tc;
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
        //globalRef = GameObject.FindWithTag("Reference").GetComponent<GlobalReferences>();
        gm = GetComponent<GenerateMesh>();
    }

    public async UniTaskVoid GetChunkPlantData()
    {
        while (parq.kdTree == null)
        {
            await UniTask.Yield();
        }
        Vector2 rangeMin = await parq.GetCoordinateBoundMin(chunkIndex, parq.plantMapSampleScale);
        Vector2 rangeMax = await parq.GetCoordinateBoundMax(chunkIndex, parq.plantMapSampleScale);
        df = await parq.GetDataFrameWithinBounds(rangeMin, rangeMax);
        if (df.Rows.Count >= 1)
        {
            rawCoordinates = await parq.GetCoordinatesAsNativeArray(df);
            rawCoordinates = await parq.ScaleCoordinateArray(chunkIndex, rawCoordinates, rangeMin, rangeMax, chunkScale);
            rawCoordinates = await GetPlantHeights(rawCoordinates);
            kdTree = await MakeChunkKDTree(rawCoordinates);

            coordinatesForRendering = await DoubleInstanceCount(rawCoordinates);

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
        var chunkPlantIndex = kdTree.FindNearest(coordinates);
        var datasetIndex = df.Rows[chunkPlantIndex]["index"];
        return df.Rows[chunkPlantIndex];
    }

    private async UniTask<NativeArray<Vector3>> GetPlantHeights(NativeArray<Vector3> coordinateArray)
    {
        Vector3 terrainSize = tc.TerrainSize;
        Vector2 startOffset = tc.startOffset;
        Vector2 noiseRange = tc.noiseRange;
        float cellSize = tc.cellSize;

        int xSegments = Mathf.FloorToInt(terrainSize.x / cellSize);
        int zSegments = Mathf.FloorToInt(terrainSize.z / cellSize);

        float xStep = terrainSize.x / xSegments;
        float zStep = terrainSize.z / zSegments;

        for (int i = 0; i < coordinateArray.Length; i++)
        {
            float noiseX = coordinateArray[i].x / xStep / xSegments + 0.5f + startOffset.x;
            float noiseZ = coordinateArray[i].z / zStep / zSegments + 0.5f + startOffset.y;

            noiseX = (noiseX) % noiseRange.x;
            noiseZ = (noiseZ) % noiseRange.y;
            
            //account for negatives (ex. -1 % 256 = -1, needs to loop around to 255)
            if (noiseX < 0)
                noiseX = noiseX + noiseRange.x;
            if (noiseZ < 0)
                noiseZ = noiseZ + noiseRange.y;

            float height = Mathf.PerlinNoise(noiseX, noiseZ);
            //Debug.Log($"For render, tile is {chunkIndex.x}, {chunkIndex.y}, noise is {noiseX}, {noiseZ}");
            coordinateArray[i] = new Vector3(coordinateArray[i].x, height * terrainSize.y, coordinateArray[i].z);
        }

        return coordinateArray;
    }

    private async UniTask<NativeArray<Vector3>> DoubleInstanceCount(NativeArray<Vector3> array)
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
        if (df != null)
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
}
