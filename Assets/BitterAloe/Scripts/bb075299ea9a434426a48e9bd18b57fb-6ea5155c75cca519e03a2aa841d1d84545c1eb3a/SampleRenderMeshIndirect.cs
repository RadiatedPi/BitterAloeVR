using Cysharp.Threading.Tasks;
using Microsoft.Data.Analysis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public class SampleRenderMeshIndirect : MonoBehaviour
{
    public ParquetParser parquetParser;
    public Vector2 chunkIndex;
    private DataFrame df;
    NativeArray<Vector3> coordinates;

    [SerializeField] private int _count;
    [SerializeField] private Mesh _mesh;
    public Material _material;
    [SerializeField] private ShadowCastingMode _shadowCastingMode;
    [SerializeField] private bool _receiveShadows;

    private GraphicsBuffer _drawArgsBuffer;
    private GraphicsBuffer _dataBuffer;

    public async UniTaskVoid GetChunkPlantData()
    {
        //Debug.Log("Waiting for DataFrame to exist before getting plant DataFrame and Array for terrain chunk");
        while (parquetParser.df == null)
        {
            //Debug.Log("Waiting...");
            await UniTask.Yield();
            //yield return null;
        }
        //Debug.Log("Getting plant DataFrame and Array for terrain chunk");
        //yield return new WaitUntil(() => parquetParser.df != null);
        var plantDistributionScale = 0.125f;
        Vector2 rangeMin = await parquetParser.GetCoordinateBoundMin(chunkIndex, plantDistributionScale);
        Vector2 rangeMax = await parquetParser.GetCoordinateBoundMax(chunkIndex, plantDistributionScale);
        df = await parquetParser.GetTerrainChunkDataFrame(rangeMin, rangeMax);
        if (df.Rows.Count >= 1)
        {
            NativeArray<Vector3> rawCoordinates = await parquetParser.GetCoordinatesAsNativeArray(df);
            rawCoordinates = await parquetParser.LocalizeCoordinateArray(chunkIndex, rawCoordinates, rangeMin, rangeMax, 32);

            //coordinates = DoubleInstanceCount(rawCoordinates);
            coordinates = await DoubleInstanceCount(await GetYCoordinates(rawCoordinates));
            //Debug.Log(coordinates.Length);
            UniTask.Void(StartRender);
        }
    }

    private async UniTask<NativeArray<Vector3>> GetYCoordinates(NativeArray<Vector3> array)
    {
        QueryParameters queryParameters = new QueryParameters(LayerMask.GetMask("Terrain Interact"), false, QueryTriggerInteraction.Ignore);

        Debug.Log("Using raycasts to find appropriate Y-axis value of each plant");
        NativeArray<Vector3> newArray = new NativeArray<Vector3>(array.Length, Allocator.Persistent);

        for (int i = 0; i < array.Length; i++)
        {
            //var results = new NativeArray<RaycastHit>(2, Allocator.TempJob);
            //var commands = new NativeArray<RaycastCommand>(1, Allocator.TempJob);
            //commands[0] = new RaycastCommand(new Vector3(array[i].x, 20, array[i].z), -transform.up, queryParameters);
            Debug.Log("Attempting raycast...");
            //JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 1, 2, default(JobHandle));
            Physics.Raycast(new Vector3(array[i].x, 20, array[i].z), -transform.up, out RaycastHit hit, 200f, LayerMask.GetMask("Terrain Interact"), QueryTriggerInteraction.Ignore);
            newArray[i] = new Vector3(array[i].x, hit.point.y - 0.43f, array[i].z);
            //handle.Complete();
            Debug.Log("Raycast success");
            //foreach (var hit in results)
            //{
                //newArray[i] = new Vector3(array[i].x, hit.point.y - 0.43f, array[i].z);
            //}
            //results.Dispose();
            //commands.Dispose();
        }


        Debug.Log($"Returning NativeArray of coordinates with appropriate Y-axis values of length {newArray.Length}");
        return newArray;
    }

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

        _drawArgsBuffer = await CreateDrawArgsBufferForRenderMeshIndirect(_mesh, coordinates.Length);
        _dataBuffer = await CreateDataBuffer<Matrix4x4>(coordinates.Length);

        var transformMatrixArray = await TransformMatrixArrayFactory.Create(coordinates);
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
            worldBounds = new Bounds(transform.position, transform.localScale)
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

}
