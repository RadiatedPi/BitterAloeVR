// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace GPUInstancerPro.PrefabModule
{
    [Unity.Burst.BurstCompile]
    public struct GPUIAutoUpdateTransformsJob : IJobParallelForTransform
    {
        [ReadOnly] public int instanceCount;
        [ReadOnly] public Matrix4x4 zeroMatrix;
        [WriteOnly] public NativeArray<Matrix4x4> instanceDataNativeArray;

        public void Execute(int index, TransformAccess transform)
        {
            if (index >= instanceCount)
                return;

            if (transform.isValid)
                instanceDataNativeArray[index] = transform.localToWorldMatrix;
            else
                instanceDataNativeArray[index] = zeroMatrix;
        }
    }
}