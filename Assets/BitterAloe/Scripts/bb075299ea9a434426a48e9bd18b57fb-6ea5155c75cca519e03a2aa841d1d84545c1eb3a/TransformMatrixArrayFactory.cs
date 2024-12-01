using Cysharp.Threading.Tasks;
using Microsoft.Data.Analysis;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ZstdSharp.Unsafe;

public static class TransformMatrixArrayFactory
{
    public static NativeArray<Matrix4x4> Create(int count, float3 maxPosition, float3 minPosition)
    {
        var transformMatrixArray = new NativeArray<Matrix4x4>(count, Allocator.Persistent);
        var job = new InitializeMatrixJob
        {
            _transformMatrixArray = transformMatrixArray,
            _maxPosition = maxPosition,
            _minPosition = minPosition
        };

        var jobHandle = job.Schedule(count, 64);
        jobHandle.Complete();

        return transformMatrixArray;
    }

    [BurstCompile]
    private struct InitializeMatrixJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<Matrix4x4> _transformMatrixArray;
        [ReadOnly] public float3 _maxPosition;
        [ReadOnly] public float3 _minPosition;

        public void Execute(int index)
        {
            var random = new Unity.Mathematics.Random((uint)index + 1);
            var x = random.NextFloat(_minPosition.x, _maxPosition.x);
            var y = random.NextFloat(_minPosition.y, _maxPosition.y);
            var z = random.NextFloat(_minPosition.z, _maxPosition.z);

            _transformMatrixArray[index] = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(random.NextFloat(0, 360), 0, 90), Vector3.one * random.NextFloat(0.9f, 1.1f));
        }
    }

    public static async UniTask<NativeArray<Matrix4x4>> Create(NativeArray<Vector3> coordinates)
    {
        var transformMatrixArray = new NativeArray<Matrix4x4>(coordinates.Length, Allocator.Persistent);
        var job = new InitializeDataFrameMatrixJob
        {
            _transformMatrixArray = transformMatrixArray,
            _coordinates = coordinates
        };

        Debug.Log(coordinates.Length);
        var jobHandle = job.Schedule(coordinates.Length, 64);
        jobHandle.Complete();

        return transformMatrixArray;
    }

    [BurstCompile]
    private struct InitializeDataFrameMatrixJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<Matrix4x4> _transformMatrixArray;
        [ReadOnly] public NativeArray<Vector3> _coordinates;

        public void Execute(int index)
        {
            Debug.Log(_coordinates[index]);
            var random = new Unity.Mathematics.Random((uint)index + 1);
            _transformMatrixArray[index] = Matrix4x4.TRS(_coordinates[index], Quaternion.Euler(0, random.NextFloat(0, 360), 0), Vector3.one /** random.NextFloat(0.9f, 1.1f)*/);
        }
    }

}
