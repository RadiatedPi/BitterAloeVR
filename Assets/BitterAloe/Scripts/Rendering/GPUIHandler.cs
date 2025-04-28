using Cysharp.Threading.Tasks;
using GPUInstancerPro;
using GPUInstancerPro.PrefabModule;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

public class RenderTransform
{
    public Vector3 position;
    public Quaternion orientation;
    public float scale;

    public RenderTransform(Vector3 position)
    {
        this.position = position;
        orientation = Quaternion.Euler(Vector3.up * UnityEngine.Random.Range(0f, 360f));
        scale = 1.0f;
    }

    public RenderTransform(Vector3 position, Quaternion orientation)
    {
        this.position = position;
        this.orientation = orientation;
        scale = 1.0f;
    }

    public RenderTransform(Vector3 position, Quaternion orientation, float scale)
    {
        this.position = position;
        this.orientation = orientation;
        this.scale = scale;
    }
}

public class RenderTransformList
{
    public Vector2 tileIndex;
    public List<RenderTransform> transforms = new List<RenderTransform>();


    public void AddTransforms(List<Vector3> positions)
    {
        for (int i = 0; i < positions.Count; i++)
        {
            //Debug.Log($"{positions[i].y}");
            transforms.Add(new RenderTransform(positions[i]));
        }
    }
    public void AddTransforms(Vector2 tileIndex, List<Vector3> localPositions)
    {
        this.tileIndex = tileIndex;
        for (int i = 0; i < localPositions.Count; i++)
            transforms.Add(new RenderTransform(localPositions[i]));
    }
    public void AddTransforms(RenderTransformList transformList)
    {
        this.transforms.AddRange(transformList.transforms);
    }

    public List<Vector3> GetPositions()
    {
        List<Vector3> localPositions = new List<Vector3>();

        for (int i = 0; i < transforms.Count; i++)
            localPositions.Add(transforms[i].position);

        return localPositions;
    }

    //public List<Vector3> GetLevelPositions(float tileSize)
    //{
    //    Vector3 levelOffset = new Vector3(tileIndex.x * tileSize, 0, tileIndex.y * tileSize);
    //    List<Vector3> levelPositions = new List<Vector3>();

    //    for (int i = 0; i < transforms.Count; i++)
    //        levelPositions.Add(transforms[i].position + levelOffset);

    //    return levelPositions;
    //}

    public RenderTransformList GetLocalToGlobalTransforms(Transform tileTransform)
    {
        RenderTransformList globalTransforms = this;

        for (int i = 0; i < globalTransforms.transforms.Count; i++)
            globalTransforms.transforms[i].position += tileTransform.position;

        return globalTransforms;
    }

    //public List<Vector3> GetGlobalPositions()
    //{
    //    List<Vector3> globalPositions = new List<Vector3>();

    //    for (int i = 0; i < transforms.Count; i++)
    //        globalPositions.Add(transforms[i].position + tileTransform.position);

    //    return globalPositions;
    //}


    public RenderTransformList GetGlobalTransforms(Vector3 levelPosition, Vector3 tileSize)
    {
        RenderTransformList renderTransformList = new RenderTransformList();

        for (int i = 0; i < transforms.Count; i++)
        {
            renderTransformList.transforms.Add(new RenderTransform(
                GetGlobalPosition(transforms[i].position, levelPosition, tileSize),
                //transforms[i].position + levelOffset + levelPosition,
                transforms[i].orientation,
                transforms[i].scale
            ));
        }


        return renderTransformList;
    }

    public Vector3 GetGlobalPosition(Vector3 localPosition, Vector3 levelPosition, Vector3 tileSize)
    {
        Vector3 levelOffset = new Vector3(tileIndex.x * tileSize.x, 0, tileIndex.y * tileSize.z);
        return localPosition + levelOffset + levelPosition;
    }
}

public class ObjectRenderer
{
    public RenderTransformList localTransforms;
    public GPUIPrototype prototype;
    public int key;

    public void AddLocalTransforms(Vector2 tileIndex, List<Vector3> localPositions)
    {
        for (int i = 0; i < localPositions.Count; i++)
            localTransforms.AddTransforms(tileIndex, localPositions);
    }

    public List<Vector3> GetLocalPositions()
    {
        List<Vector3> localPositions = new List<Vector3>();

        for (int i = 0; i < localTransforms.transforms.Count; i++)
            localPositions.Add(localTransforms.transforms[i].position);

        return localPositions;
    }

    public List<Vector3> GetLevelPositions(Vector2 tileIndex, float tileSize)
    {
        Vector3 levelOffset = new Vector3(tileIndex.x * tileSize, 0, tileIndex.y * tileSize);
        List<Vector3> levelPositions = new List<Vector3>();

        for (int i = 0; i < localTransforms.transforms.Count; i++)
            levelPositions.Add(localTransforms.transforms[i].position + levelOffset);

        return levelPositions;
    }

    public List<RenderTransform> GetGlobalTransforms(Vector3 tilePosition)
    {
        List<RenderTransform> globalPositions = localTransforms.transforms;

        for (int i = 0; i < globalPositions.Count; i++)
            globalPositions[i].position = GetGlobalPosition(i, tilePosition);

        return globalPositions;
    }

    public List<Vector3> GetGlobalPositions(Vector3 tilePosition)
    {
        List<Vector3> globalPositions = new List<Vector3>();

        for (int i = 0; i < localTransforms.transforms.Count; i++)
            globalPositions.Add(GetGlobalPosition(i, tilePosition));

        return globalPositions;
    }

    public Vector3 GetGlobalPosition(int index, Vector3 tilePosition)
    {
        return localTransforms.transforms[index].position - tilePosition;
    }
}

public class GPUIHandler : MonoBehaviour
{
    public LevelData level;

    public GameObject prefab;
    public List<ObjectRenderer> objectRenderers = new List<ObjectRenderer>();
    public ObjectRenderer aloeRenderer = new ObjectRenderer();
    public List<TileData> activeTiles;
    //public List<Vector3> renderCoordinates;
    public GPUIPrefabManager gpuiPrefabManager;
    public int instanceCount = 1000;
    public Vector3 previousPosition;


    public void Start()
    {
        for (int i = 0; i < gpuiPrefabManager.GetPrototypeCount(); i++)
        {
            GPUIPrototype prototype = gpuiPrefabManager.GetPrototype(i);
            GPUICoreAPI.RegisterRenderer(this, prototype, out int key);

            Debug.Log($"Prototype {i} name: {prototype.name}");

            if (prototype.name.Contains("Aloe"))
                aloeRenderer.key = key;
            else
            {
                ObjectRenderer renderObject = new ObjectRenderer();
                renderObject.key = key;
                renderObject.prototype = prototype;
                objectRenderers.Add(renderObject);
            }
        }

        WaitForLoad();
        UpdateTransforms();
    }

    private async UniTask<bool> WaitForLoad()
    {
        while (transform.childCount < (level.tc.radiusToRender * 2 + 1) * (level.tc.radiusToRender * 2 + 1))
        {
            await UniTask.Yield();
        }
        return true;
    }

    public void Update()
    {
        if (previousPosition != transform.position)
        {
            Debug.Log($"Level position recentered.");
            UpdateTransforms();
            previousPosition = transform.position;
        }
    }

    public async UniTask<bool> GetActiveTiles()
    {
        activeTiles.Clear();

        while (transform.childCount <= 0)
        {
            await UniTask.Yield();
        }

        activeTiles.AddRange(GetComponentsInChildren<TileData>());
        Debug.Log($"{activeTiles.Count} active tiles found.");
        return true;
    }

    public async UniTask UpdateTransforms()
    {
        await GetActiveTiles();

        RenderTransformList aloeTransforms = new RenderTransformList();
        for (int i = 0; i < activeTiles.Count; i++)//each (var tile in activeTiles)
        {
            //aloeTransforms.AddTransforms(activeTiles[i].aloePlants.GetGlobalTransforms(transform.position, level.tc.tileSize));
            aloeTransforms.AddTransforms(level.parq.GetWorldPositionList(level.parq.GetTestimoniesInTile(activeTiles[i].tileIndex)));
        }
        GPUICoreAPI.SetTransformBufferData(aloeRenderer.key, GenerateMatrixArray(aloeTransforms, instanceCount));



        for (int i = 0; i < objectRenderers.Count; i++)//each (var objectRenderer in objectRenderers)
        {
            RenderTransformList objectTransforms = new RenderTransformList();
            for (int j = 0; j < activeTiles.Count; j++)//each (var tile in activeTiles)
            {
                while (activeTiles[j].objects.Count < objectRenderers.Count)
                {
                    await UniTask.Yield();
                }
                //objectTransforms.AddTransforms(activeTiles[j].objects[i].GetLocalToGlobalTransforms(activeTiles[j].transform));
                objectTransforms.AddTransforms(activeTiles[j].objects[i].GetGlobalTransforms(transform.position, level.tc.tileSize));
            }
            GPUICoreAPI.SetTransformBufferData(objectRenderers[i].key, GenerateMatrixArray(objectTransforms, instanceCount));
        }

        //GPUIPrefabAPI.UpdateTransformData(gpuiPrefabManager);
        //level.debug.Log($"Updated transforms. {aloeTransforms.transforms.Count} aloe plants total.");
    }

    private Vector3 RandomPointAboveTerrain()
    {
        return new Vector3(
            UnityEngine.Random.Range(transform.position.x - level.tc.TerrainSize.x / 2, transform.position.x + level.tc.TerrainSize.x / 2),
            transform.position.y + level.tc.TerrainSize.y * 2,
            UnityEngine.Random.Range(transform.position.z - level.tc.TerrainSize.z / 2, transform.position.z + level.tc.TerrainSize.z / 2)
        );
    }

    public Matrix4x4[] GenerateMatrixArray(RenderTransformList globalTransforms, int maxInstances)
    {
        Matrix4x4[] matrix4X4s = new Matrix4x4[maxInstances];
        for (int i = 0; i < maxInstances; i++)
        {
            matrix4X4s[i] = Matrix4x4.zero;

            if (i < globalTransforms.transforms.Count)
            {
                //var random = new Unity.Mathematics.Random((uint)((globalTransforms[i].position.x * globalTransforms[i].position.z) * 100000));
                matrix4X4s[i] = Matrix4x4.TRS(globalTransforms.transforms[i].position, globalTransforms.transforms[i].orientation, Vector3.one * globalTransforms.transforms[i].scale);
            }
        }
        return matrix4X4s;
    }
}
