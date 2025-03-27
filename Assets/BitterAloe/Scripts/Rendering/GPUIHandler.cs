using Cysharp.Threading.Tasks;
using GPUInstancerPro;
using GPUInstancerPro.PrefabModule;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.PlasticSCM.Editor.UI;
using UnityEngine;

public class GPUIHandler : MonoBehaviour
{
    public LevelData level;

    public GameObject prefab;
    private int _rendererKey;
    public List<TileData> activeTiles;
    public List<Vector3> renderCoordinates;
    public GPUIPrefabManager gpuiPrefabManager;
    public int instanceCount = 2000;
    public Vector3 previousPosition;


    public void Start()
    {
        GPUICoreAPI.RegisterRenderer(this, gpuiPrefabManager.GetPrototype(0), out _rendererKey); // Register the prefab as renderer
    }

    public void Update()
    {
        if (previousPosition != transform.position)
        {
            level.rdc.Log($"Position changed ----------------------------------------------------");
            UpdatePlantTransforms();
            previousPosition = transform.position;
        }
    }




    public async UniTask<bool> GetActiveTiles()
    {
        activeTiles.Clear();

        while (transform.childCount <= 0 )
        {
            await UniTask.Yield();
        }

        activeTiles.AddRange(GetComponentsInChildren<TileData>());
        level.rdc.Log($"{activeTiles.Count} active tiles found.");
        return true;
    }


    public async UniTask<bool> UpdatePlantTransforms()
    {
        await GetActiveTiles();
        List<Vector3> updatedCoordinates = new List<Vector3>();

        for (int i = 0; i < activeTiles.Count; i++)
        {
            while (!activeTiles[i].dataFound)
            {
                await UniTask.Yield();
            }

            updatedCoordinates.AddRange(activeTiles[i].GetGlobalPositions());
        }
        renderCoordinates = updatedCoordinates;
        GPUICoreAPI.SetTransformBufferData(_rendererKey, GenerateMatrixArray(updatedCoordinates));
        //GPUIPrefabAPI.UpdateTransformData(gpuiPrefabManager);
        level.rdc.Log($"Updated plant transforms. {updatedCoordinates.Count} plants total.");
        return true;
    }

    public Matrix4x4[] GenerateMatrixArray(List<Vector3> coordinates)
    {
        Matrix4x4[] matrix4X4s = new Matrix4x4[instanceCount];
        for (int i = 0; i < instanceCount; i++)
        {
            matrix4X4s[i] = Matrix4x4.zero;

            if (i < coordinates.Count)
            {
                var random = new Unity.Mathematics.Random((uint)((coordinates[i].x * coordinates[i].z) * 100000));
                matrix4X4s[i] = Matrix4x4.TRS(coordinates[i], Quaternion.Euler(-90, random.NextFloat(0, 360), 0), Vector3.one * random.NextFloat(0.9f, 1.1f));
            }
        }
        return matrix4X4s;
    }
}
