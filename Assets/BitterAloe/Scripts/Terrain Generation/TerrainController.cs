using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using GPUInstancerPro;
using GPUInstancerPro.PrefabModule;
using GPUInstancerPro.TerrainModule;

public class TerrainController : MonoBehaviour
{
    private LevelData level;
    [SerializeField]
    private GameObject terrainTilePrefab = null;
    [SerializeField] private Mesh plantMesh;
    public Material material;
    public Vector3 tileSize = new Vector3(32, 8, 32);
    public Vector3 TerrainSize { get { return tileSize; } }
    public float noiseScale = 1, cellSize = 4;
    public int radiusToRender = 2;
    [SerializeField]
    private Transform[] gameTransforms;
    [SerializeField]
    private Transform playerTransform;
    [SerializeField]
    private Transform playerContainer;
    [SerializeField]
    private GameObject playerLoadingBox;


    [SerializeField]
    private int seed;

    [SerializeField]
    private GameObject rockObjectsPrefab, plantObjectsPrefab;
    public GameObject RockObjects { get { return rockObjectsPrefab; } }
    public GameObject PlantObjects { get { return plantObjectsPrefab; } }

    [SerializeField]
    private Vector2Int rocksPerTileRange = new Vector2Int(0, 20);
    public int MinRocksPerTile { get { return rocksPerTileRange.x; } }
    public int MaxRocksPerTile { get { return rocksPerTileRange.y; } }
    [SerializeField]
    private Vector2Int plantsPerTileRange = new Vector2Int(0, 20);
    public int MinPlantsPerTile { get { return plantsPerTileRange.x; } }
    public int MaxPlantsPerTile { get { return plantsPerTileRange.y; } }


    [SerializeField]
    private float destroyDistance = 200;
    [SerializeField]
    private bool usePerlinNoise = true;
    [SerializeField]
    private Texture2D noise;
    public static float[][] noisePixels;
    [HideInInspector]
    public Vector2 startOffset;
    [HideInInspector]
    public Vector2 noiseRange;

    private Dictionary<Vector2, GameObject> terrainTiles = new Dictionary<Vector2, GameObject>();

    private Vector2[] previousCenterTiles;
    private List<GameObject> previousTileObjects = new List<GameObject>();

    public GameObject aloePrefab;
    public List<Vector3> aloeCoordinates;


    DateTime startTime;
    float frameBudget = 0.05f; // max amount of time to do work per frame

    private void Awake()
    {
        if (noise)
            noisePixels = GetGrayScalePixels(noise);
        GenerateMesh.UsePerlinNoise = usePerlinNoise;
        noiseRange = usePerlinNoise ? Vector2.one * 256 : new Vector2(noisePixels.Length, noisePixels[0].Length);
        startOffset = new Vector2(256, 256);
    }

    private void Start()
    {
        startTime = DateTime.Now;
        InitialLoad();
    }

    public void InitialLoad()
    {
        level = GetComponent<LevelData>();

        foreach (Transform t in gameTransforms)
            t.parent = transform;

        UnityEngine.Random.InitState(seed);
        RandomizeInitState();
    }

    private void Update()
    {
        if (level.parq.df != null)
            LoadTileLoop().Forget();
    }


    private async UniTaskVoid LoadTileLoop()
    {
        //save the tile the player is on
        Vector2 playerTile = TileFromPosition(playerTransform.position - transform.position);
        //save the tiles of all tracked objects in gameTransforms (including the player)
        List<Vector2> centerTiles = new List<Vector2>();
        centerTiles.Add(playerTile);
        foreach (Transform t in gameTransforms)
            centerTiles.Add(TileFromPosition(t.localPosition));

        //if no tiles exist yet or tiles should change
        if (previousCenterTiles == null || HaveTilesChanged(centerTiles))
        {
            List<GameObject> tileObjects = new List<GameObject>();
            //activate new tiles
            foreach (Vector2 tile in centerTiles)
            {
                bool isPlayerTile = tile == playerTile;
                int radius = isPlayerTile ? radiusToRender : 1;
                for (int i = -radius; i <= radius; i++)
                    for (int j = -radius; j <= radius; j++)
                    {
                        ActivateOrCreateTile((int)tile.x + i, (int)tile.y + j, tileObjects).Forget();
                        await UniTask.Yield();
                    }
            }

            //deactivate old tiles
            foreach (GameObject g in previousTileObjects)
                if (!tileObjects.Contains(g))
                {
                    g.SetActive(false);
                    //level.gpui.RemoveFromCoordinateList(g.GetComponent<TileData>().globalCoordinates);
                }

            //destroy inactive tiles if they're too far away
            List<Vector2> keysToRemove = new List<Vector2>();//can't remove item when inside a foreach loop
            foreach (KeyValuePair<Vector2, GameObject> kv in terrainTiles)
            {
                if (Vector3.Distance(playerTransform.position, kv.Value.transform.position) > destroyDistance && !kv.Value.activeSelf)
                {
                    keysToRemove.Add(kv.Key);
                    Destroy(kv.Value); 
                }
            }
            foreach (Vector2 key in keysToRemove)
                terrainTiles.Remove(key);

            previousTileObjects = new List<GameObject>(tileObjects);

            level.gpui.UpdatePlantTransforms();
        }

        previousCenterTiles = centerTiles.ToArray();
    }


    //Helper methods below

    private async UniTaskVoid ActivateOrCreateTile(int xIndex, int yIndex, List<GameObject> tileObjects)
    {
        if (!terrainTiles.ContainsKey(new Vector2(xIndex, yIndex)))
        {
            GameObject t = await CreateTile(xIndex, yIndex);
            tileObjects.Add(t);

            //level.gpui.AddToCoordinateList(t.GetComponent<TileData>().globalCoordinates);

            // drops player into world once all initial tiles have been loaded
            if (playerLoadingBox.activeSelf == true && tileObjects.Count >= (radiusToRender + 1) * (radiusToRender + 1))
                playerLoadingBox.SetActive(false);
        }
        else
        {
            GameObject t = terrainTiles[new Vector2(xIndex, yIndex)];
            tileObjects.Add(t);
            if (!t.activeSelf)
            {
                t.SetActive(true);
                //level.gpui.AddToCoordinateList(t.GetComponent<TileData>().globalCoordinates);
            }
        }
    }

    private async UniTask<GameObject> CreateTile(int xIndex, int yIndex)
    {
        GameObject terrain = Instantiate(
            terrainTilePrefab,
            new Vector3(tileSize.x * xIndex, 0, tileSize.z * yIndex),
            Quaternion.identity,
            transform
        );

        terrain.name = TrimEnd(terrain.name, "(Clone)") + " [" + xIndex + " , " + yIndex + "]";
        terrain.GetComponent<TileData>().tileIndex = new Vector2(xIndex, yIndex);

        terrainTiles.Add(new Vector2(xIndex, yIndex), terrain);

        await terrain.GetComponent<TileData>().GetPlantData();

        terrain.transform.localPosition = new Vector3(tileSize.x * xIndex, 0, tileSize.z * yIndex);

        GenerateMesh gm = terrain.GetComponent<GenerateMesh>();
        gm.TerrainSize = tileSize;
        gm.NoiseScale = noiseScale;
        gm.CellSize = cellSize;
        gm.NoiseOffset = NoiseOffset(xIndex, yIndex);
        gm.TileIndex = new Vector2(xIndex, yIndex);
        gm.Generate();

        UnityEngine.Random.InitState((int)(seed + (long)xIndex * 100 + yIndex));//so it doesn't form a (noticable) pattern of similar tiles
        RandomizeInitState();


        //GPUIProfile gpuiProfile = new GPUIProfile();


        // Pre GPUI plant rendering method:
        //SampleRenderMeshIndirect plantRenderer = terrain.AddComponent<SampleRenderMeshIndirect>();
        //plantRenderer.td = terrain.GetComponent<TileData>();
        //plantRenderer._mesh = plantMesh;
        //plantRenderer._material = new Material(material);
        //plantRenderer._shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        //plantRenderer._receiveShadows = true;
        //await plantRenderer.StartRender();

        PlaceObjects po = terrain.GetComponent<PlaceObjects>();
        await po.Place(PlantObjects);
        await po.Place(RockObjects);

        return terrain;
    }







    public Vector2 NoiseOffset(int xIndex, int yIndex)
    {
        Vector2 noiseOffset = new Vector2(
            (xIndex * noiseScale + startOffset.x) % noiseRange.x,
            (yIndex * noiseScale + startOffset.y) % noiseRange.y
        );
        //account for negatives (ex. -1 % 256 = -1, needs to loop around to 255)
        if (noiseOffset.x < 0)
            noiseOffset = new Vector2(noiseOffset.x + noiseRange.x, noiseOffset.y);
        if (noiseOffset.y < 0)
            noiseOffset = new Vector2(noiseOffset.x, noiseOffset.y + noiseRange.y);
        return noiseOffset;
    }

    private Vector2 TileFromPosition(Vector3 position)
    {
        return new Vector2(Mathf.FloorToInt(position.x / tileSize.x + .5f), Mathf.FloorToInt(position.z / tileSize.z + .5f));
    }

    private void RandomizeInitState()
    {
        UnityEngine.Random.InitState((int)System.DateTime.UtcNow.Ticks);//casting a long to an int "loops" it (like modulo)
    }

    private bool HaveTilesChanged(List<Vector2> centerTiles)
    {
        if (previousCenterTiles.Length != centerTiles.Count)
            return true;
        for (int i = 0; i < previousCenterTiles.Length; i++)
            if (previousCenterTiles[i] != centerTiles[i])
                return true;
        return false;
    }



    // TODO: rework, since level root object now uses important components
    //public void DestroyTerrain()
    //{
    //    //water.parent = null;
    //    //playerContainer.parent = null;
    //    foreach (Transform t in gameTransforms)
    //        t.parent = level;
    //    Destroy(level);
    //    terrainTiles.Clear();
    //}

    private static string TrimEnd(string str, string end)
    {
        if (str.EndsWith(end))
            return str.Substring(0, str.LastIndexOf(end));
        return str;
    }

    public static float[][] GetGrayScalePixels(Texture2D texture2D)
    {
        List<float> grayscale = texture2D.GetPixels().Select(c => c.grayscale).ToList();

        List<List<float>> grayscale2d = new List<List<float>>();
        for (int i = 0; i < grayscale.Count; i += texture2D.width)
            grayscale2d.Add(grayscale.GetRange(i, texture2D.width));

        return grayscale2d.Select(a => a.ToArray()).ToArray();
    }

}