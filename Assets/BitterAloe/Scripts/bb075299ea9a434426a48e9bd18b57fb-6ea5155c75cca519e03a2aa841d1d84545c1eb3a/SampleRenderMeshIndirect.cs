using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class SampleRenderMeshIndirect : MonoBehaviour
{
    public ParquetParser parquetParser;

    [SerializeField] private int _count;
    [SerializeField] private Mesh _mesh;
    [SerializeField] private Material _material;
    [SerializeField] private ShadowCastingMode _shadowCastingMode;
    [SerializeField] private bool _receiveShadows;

    private GraphicsBuffer _drawArgsBuffer;
    private GraphicsBuffer _dataBuffer;

    private void Start()
    {
        StartRender();
    }

    private async void StartRender()
    {
        NativeArray<Vector3> rawCoordinates = await parquetParser.GetParquetDataset(parquetParser.fileName);

        NativeArray<Vector3> coordinates = DoubleInstanceCount(GetYCoordinates(rawCoordinates));

        Debug.Log(coordinates.Length);

        _drawArgsBuffer = CreateDrawArgsBufferForRenderMeshIndirect(_mesh, coordinates.Length);
        _dataBuffer = CreateDataBuffer<Matrix4x4>(coordinates.Length);

        var transformMatrixArray = TransformMatrixArrayFactory.Create(coordinates);
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

    private NativeArray<Vector3> GetYCoordinates(NativeArray<Vector3> array)
    {
        NativeArray<Vector3> newArray = new NativeArray<Vector3>(array.Length, Allocator.Persistent);

        for (int i = 0; i < array.Length; i++)
        {
            Physics.Raycast(new Vector3(array[i].x, 20, array[i].z), -transform.up, out RaycastHit hit, float.MaxValue, LayerMask.GetMask("Terrain Interact"), QueryTriggerInteraction.Ignore);
            newArray[i] = new Vector3(array[i].x, hit.point.y - 0.43f, array[i].z);
        }

        return newArray;
    }


    private NativeArray<Vector3> DoubleInstanceCount(NativeArray<Vector3> array)
    {
        NativeArray<Vector3> doubledArray = new NativeArray<Vector3>(array.Length * 2, Allocator.Persistent);

        for (int i = 0; i < array.Length * 2; i += 2)
        {
            doubledArray[i] = array[i / 2];
            doubledArray[i + 1] = array[i / 2];
        }

        return doubledArray;
    }
}
