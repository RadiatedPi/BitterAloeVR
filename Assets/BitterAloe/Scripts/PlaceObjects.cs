﻿using Cysharp.Threading.Tasks;
using GPUInstancerPro;
using GPUInstancerPro.PrefabModule;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GenerateMesh))]
public class PlaceObjects : MonoBehaviour
{
    private LevelData level;
    private GPUIPrefabManager gpuiPrefabManager;



    //public TerrainController TerrainController { get; set; }

    public void Start()
    {
        level = transform.parent.GetComponent<LevelData>();
        gpuiPrefabManager = level.gpui.gpuiPrefabManager;
    }

    //public async UniTask<bool> Place(GameObject objectsPrefab)
    //{

    //    for (int i = 0; i < gpuiPrefabManager.GetPrototypeCount(); i++)
    //    {
    //        GPUIPrototype prototype = gpuiPrefabManager.GetPrototype(i);
    //        int key;
    //        GPUICoreAPI.RegisterRenderer(this, prototype, out key);

    //        level.debug.Log($"Prototype {i} name: {prototype.name}");

    //        if (prototype.name.Contains("Aloe"))
    //            _aloeRendererKey = key;
    //        else if (prototype.name.Contains("Rock"))
    //        {
    //            rockRendererKeys.Add(key);
    //            rockPrototypes.Add(prototype);
    //        }
    //        else if (prototype.name.Contains("Foliage"))
    //        {
    //            foliageRendererKeys.Add(key);
    //            foliagePrototypes.Add(prototype);
    //        }
    //    }

    //    int prefabNum = objectsPrefab.transform.childCount;

    //    if (prefabNum < 1)
    //    {
    //        return false;
    //    }

    //    for (int i = 0; i < prefabNum; i++)
    //    {
    //        Transform objectType = objectsPrefab.transform.GetChild(i);
    //        PlaceObjectSettings placeSettings = objectType.GetComponent<PlaceObjectSettings>();

    //        int numObjects = Random.Range(placeSettings.countPerTileRange.x, placeSettings.countPerTileRange.y);
    //        await UniTask.RunOnThreadPool(async () =>
    //        {
    //            for (int j = 0; j < numObjects; j++)
    //            {
    //                await UniTask.Yield();
    //                Vector3 startPoint = RandomPointAboveTerrain();

    //                RaycastHit hit;
    //                if (Physics.Raycast(startPoint, Vector3.down, out hit, float.MaxValue, LayerMask.GetMask("Terrain")))
    //                {
    //                    Quaternion orientation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(Vector3.up * Random.Range(0f, 360f));
    //                    RaycastHit boxHit;
    //                    if (Physics.BoxCast(startPoint, Vector3.one, Vector3.down, out boxHit, orientation, float.MaxValue, LayerMask.GetMask("Terrain")))
    //                    {
    //                        Transform placedObject = Instantiate(objectType, new Vector3(startPoint.x, hit.point.y + placeSettings.heightOffset, startPoint.z), orientation, transform);
    //                        placedObject.transform.localScale *= Random.Range(placeSettings.sizeRange.x, placeSettings.sizeRange.y);
    //                    }
    //                    //Debug code. To use, uncomment the giant thingy below

    //                    //Debug.DrawRay(startPoint, Vector3.down * 10000, Color.blue);
    //                    //DrawBoxCastBox(startPoint, Vector3.one, orientation, Vector3.down, 10000, Color.red);
    //                    //UnityEditor.EditorApplication.isPaused = true;
    //                }
    //            }
    //        });
    //    }
    //    return true;
    //}

    private Vector3 RandomPointAboveTerrain()
    {
        return new Vector3(
            Random.Range(transform.position.x - level.tc.TerrainSize.x / 2, transform.position.x + level.tc.TerrainSize.x / 2),
            transform.position.y + level.tc.TerrainSize.y * 2,
            Random.Range(transform.position.z - level.tc.TerrainSize.z / 2, transform.position.z + level.tc.TerrainSize.z / 2)
        );
    }

    //code to help visualize the boxcast
    //source: https://answers.unity.com/questions/1156087/how-can-you-visualize-a-boxcast-boxcheck-etc.html 

    //Draws just the box at where it is currently hitting. 
    public static void DrawBoxCastOnHit(Vector3 origin, Vector3 halfExtents, Quaternion orientation, Vector3 direction, float hitInfoDistance, Color color)
    {
        origin = CastCenterOnCollision(origin, direction, hitInfoDistance);
        DrawBox(origin, halfExtents, orientation, color);
    }

    //Draws the full box from start of cast to its end distance. Can also pass in hitInfoDistance instead of full distance
    public static void DrawBoxCastBox(Vector3 origin, Vector3 halfExtents, Quaternion orientation, Vector3 direction, float distance, Color color)
    {
        direction.Normalize();
        Box bottomBox = new Box(origin, halfExtents, orientation);
        Box topBox = new Box(origin + (direction * distance), halfExtents, orientation);

        Debug.DrawLine(bottomBox.backBottomLeft, topBox.backBottomLeft, color);
        Debug.DrawLine(bottomBox.backBottomRight, topBox.backBottomRight, color);
        Debug.DrawLine(bottomBox.backTopLeft, topBox.backTopLeft, color);
        Debug.DrawLine(bottomBox.backTopRight, topBox.backTopRight, color);
        Debug.DrawLine(bottomBox.frontTopLeft, topBox.frontTopLeft, color);
        Debug.DrawLine(bottomBox.frontTopRight, topBox.frontTopRight, color);
        Debug.DrawLine(bottomBox.frontBottomLeft, topBox.frontBottomLeft, color);
        Debug.DrawLine(bottomBox.frontBottomRight, topBox.frontBottomRight, color);

        DrawBox(bottomBox, color);
        DrawBox(topBox, color);
    }

    public static void DrawBox(Vector3 origin, Vector3 halfExtents, Quaternion orientation, Color color)
    {
        DrawBox(new Box(origin, halfExtents, orientation), color);
    }
    public static void DrawBox(Box box, Color color)
    {
        Debug.DrawLine(box.frontTopLeft, box.frontTopRight, color);
        Debug.DrawLine(box.frontTopRight, box.frontBottomRight, color);
        Debug.DrawLine(box.frontBottomRight, box.frontBottomLeft, color);
        Debug.DrawLine(box.frontBottomLeft, box.frontTopLeft, color);

        Debug.DrawLine(box.backTopLeft, box.backTopRight, color);
        Debug.DrawLine(box.backTopRight, box.backBottomRight, color);
        Debug.DrawLine(box.backBottomRight, box.backBottomLeft, color);
        Debug.DrawLine(box.backBottomLeft, box.backTopLeft, color);

        Debug.DrawLine(box.frontTopLeft, box.backTopLeft, color);
        Debug.DrawLine(box.frontTopRight, box.backTopRight, color);
        Debug.DrawLine(box.frontBottomRight, box.backBottomRight, color);
        Debug.DrawLine(box.frontBottomLeft, box.backBottomLeft, color);
    }

    public struct Box
    {
        public Vector3 localFrontTopLeft { get; private set; }
        public Vector3 localFrontTopRight { get; private set; }
        public Vector3 localFrontBottomLeft { get; private set; }
        public Vector3 localFrontBottomRight { get; private set; }
        public Vector3 localBackTopLeft { get { return -localFrontBottomRight; } }
        public Vector3 localBackTopRight { get { return -localFrontBottomLeft; } }
        public Vector3 localBackBottomLeft { get { return -localFrontTopRight; } }
        public Vector3 localBackBottomRight { get { return -localFrontTopLeft; } }

        public Vector3 frontTopLeft { get { return localFrontTopLeft + origin; } }
        public Vector3 frontTopRight { get { return localFrontTopRight + origin; } }
        public Vector3 frontBottomLeft { get { return localFrontBottomLeft + origin; } }
        public Vector3 frontBottomRight { get { return localFrontBottomRight + origin; } }
        public Vector3 backTopLeft { get { return localBackTopLeft + origin; } }
        public Vector3 backTopRight { get { return localBackTopRight + origin; } }
        public Vector3 backBottomLeft { get { return localBackBottomLeft + origin; } }
        public Vector3 backBottomRight { get { return localBackBottomRight + origin; } }

        public Vector3 origin { get; private set; }

        public Box(Vector3 origin, Vector3 halfExtents, Quaternion orientation) : this(origin, halfExtents)
        {
            Rotate(orientation);
        }
        public Box(Vector3 origin, Vector3 halfExtents)
        {
            this.localFrontTopLeft = new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z);
            this.localFrontTopRight = new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z);
            this.localFrontBottomLeft = new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
            this.localFrontBottomRight = new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z);

            this.origin = origin;
        }


        public void Rotate(Quaternion orientation)
        {
            localFrontTopLeft = RotatePointAroundPivot(localFrontTopLeft, Vector3.zero, orientation);
            localFrontTopRight = RotatePointAroundPivot(localFrontTopRight, Vector3.zero, orientation);
            localFrontBottomLeft = RotatePointAroundPivot(localFrontBottomLeft, Vector3.zero, orientation);
            localFrontBottomRight = RotatePointAroundPivot(localFrontBottomRight, Vector3.zero, orientation);
        }
    }

    //This should work for all cast types
    static Vector3 CastCenterOnCollision(Vector3 origin, Vector3 direction, float hitInfoDistance)
    {
        return origin + (direction.normalized * hitInfoDistance);
    }

    static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
    {
        Vector3 direction = point - pivot;
        return pivot + rotation * direction;
    }
}