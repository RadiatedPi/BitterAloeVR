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
    private GlobalReferences gr;
    public TileData td;

    //[SerializeField] private int _count;
    public Mesh _mesh;
    public Material _material;
    public ShadowCastingMode _shadowCastingMode;
    public bool _receiveShadows;

    private GraphicsBuffer _drawArgsBuffer;
    private GraphicsBuffer _dataBuffer;
    bool renderStarted = false;
    private bool tdFound = false;

    public void Start()
    {
        gr = transform.parent.GetComponent<GlobalReferences>();
        //td = GetComponentInParent<TileData>();
        tdFound = true;
    }

    private void Update() 
    {
        if (renderStarted)
        {
            var renderParams = new RenderParams(_material)
            { 
                receiveShadows = _receiveShadows,
                shadowCastingMode = _shadowCastingMode,
                worldBounds = new Bounds(
                    //new Vector3(
                    //    td.tileIndex.x*gr.tc.tileSize.x - gr.tc.Level.position.x,
                    //    0,
                    //    td.tileIndex.y * gr.tc.tileSize.z - gr.tc.Level.position.z),
                    transform.position,
                    gr.tc.tileSize * 1.25f)
            }; 

            Graphics.RenderMeshIndirect(
                renderParams,
                _mesh,
                _drawArgsBuffer
            );
        }
    }

    //public async UniTask<bool> StartRender()
    //{
    //    while( tdFound == false )
    //        await UniTask.Yield();

    //    gr.rdc.Log("Starting terrain chunk plant rendering");
    //    //Debug.Log(td.coordinates.Length);

    //    _drawArgsBuffer = CreateDrawArgsBufferForRenderMeshIndirect(_mesh, td.aloeCoordinates.Length);
    //    _dataBuffer = CreateDataBuffer<Matrix4x4>(td.aloeCoordinates.Length);

    //    var transformMatrixArray = TransformMatrixArrayFactory.Create(td.aloeCoordinates);
    //    _dataBuffer.SetData(transformMatrixArray);
    //    transformMatrixArray.Dispose();
    //    _material.SetBuffer("_TransformMatrixArray", _dataBuffer);

    //    //_material.SetVector("_BoundsOffset", new Vector3(td.tileIndex.x * gr.tc.tileSize.x, 0, td.tileIndex.y * gr.tc.tileSize.z));
    //    //_material.SetVector("_BoundsOffset", new Vector3(
    //    //                td.tileIndex.x * gr.tc.tileSize.x - gr.tc.Level.position.x,
    //    //                0,
    //    //                td.tileIndex.y * gr.tc.tileSize.z - gr.tc.Level.position.z));
    //    _material.SetVector("_BoundsOffset", transform.position);
         

    //    renderStarted = true; 

    //    return true;
    //}

    private void OnDestroy()
    {  
        _drawArgsBuffer?.Dispose();
        _dataBuffer?.Dispose();
    }

    private static GraphicsBuffer CreateDrawArgsBufferForRenderMeshIndirect(Mesh mesh, int instanceCount)
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

    private static GraphicsBuffer CreateDataBuffer<T>(int instanceCount) where T : struct
    {
        return new GraphicsBuffer(
            GraphicsBuffer.Target.Structured, instanceCount,
            Marshal.SizeOf(typeof(T))
        );
    }
}
