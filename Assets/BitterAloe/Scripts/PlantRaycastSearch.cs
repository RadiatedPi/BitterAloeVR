using Autohand;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlantRaycastSearch : MonoBehaviour
{
    public ParquetParser parquetParser;
    public PlantUIManager plantUIManager;
    public Transform aimer;
    public float aimerSmoothingSpeed = 5f;
    public LayerMask layer;
    public float curveStrength = 1;
    public LineRenderer line;
    public int lineSegments = 50;

    public Gradient canSelectColor = new Gradient() { colorKeys = new GradientColorKey[] { new GradientColorKey() { color = Color.green, time = 0 } } };
    public Gradient cantSelectColor = new Gradient() { colorKeys = new GradientColorKey[] { new GradientColorKey() { color = Color.red, time = 0 } } };

    public GameObject indicator;

    public UnityEvent OnStartSelect;
    public UnityEvent OnStopSelect;
    public UnityEvent OnSelect;

    Vector3[] lineArr;
    bool aiming;
    bool hitting;
    RaycastHit aimHit;
    HandTeleportGuard[] selectGuards;
    AutoHandPlayer playerBody;

    Vector3 currentSelectSmoothForward;

    Vector3 currentSelectForward;
    Vector3 currentSelectPosition;

    private void Awake()
    {
        line.enabled = false;
    }

    private void Start()
    {
        playerBody = AutoHandExtensions.CanFindObjectOfType<AutoHandPlayer>();

        lineArr = new Vector3[lineSegments];
        selectGuards = AutoHandExtensions.CanFindObjectsOfType<HandTeleportGuard>();
    }
    void LateUpdate()
    {
        SmoothTargetValues();

        if (aiming)
            CalculateSelect();
        else
            line.positionCount = 0;

        DrawIndicator();
    }
    void SmoothTargetValues()
    {
        currentSelectForward = aimer.forward;
        currentSelectPosition = aimer.position;
        currentSelectSmoothForward = Vector3.Lerp(currentSelectSmoothForward, currentSelectForward, Time.deltaTime * aimerSmoothingSpeed);
    }

    void CalculateSelect()
    {
        line.colorGradient = cantSelectColor;
        var lineList = new List<Vector3>();
        int i;
        hitting = false;
        for (i = 0; i < lineSegments; i++)
        {
            var time = i / 60f;
            lineArr[i] = currentSelectPosition;
            lineArr[i] += currentSelectSmoothForward * time * 15;
            lineArr[i].y += curveStrength * (time - Mathf.Pow(9.8f * 0.5f * time, 2));
            lineList.Add(lineArr[i]);
            if (i != 0)
            {
                if (Physics.Raycast(lineArr[i - 1], lineArr[i] - lineArr[i - 1], out aimHit, Vector3.Distance(lineArr[i], lineArr[i - 1]), ~Hand.GetHandsLayerMask(), QueryTriggerInteraction.Ignore))
                {
                    //Makes sure the angle isnt too steep
                    if (layer == (layer | (1 << aimHit.collider.gameObject.layer)))
                    {
                        line.colorGradient = canSelectColor;
                        lineList.Add(aimHit.point);
                        hitting = true;
                        break;
                    }
                    break;
                }
            }
        }
        line.enabled = true;
        line.positionCount = i;
        line.SetPositions(lineArr);
    }
    void DrawIndicator()
    {
        if (indicator != null)
        {
            if (hitting)
            {
                indicator.gameObject.SetActive(true);
                indicator.transform.position = aimHit.point;
                indicator.transform.up = aimHit.normal;
            }
            else
                indicator.gameObject.SetActive(false);
        }
    }

    public void StartSelect()
    {
        aiming = true;
        OnStartSelect?.Invoke();
    }

    public void CancelSelect()
    {
        line.positionCount = 0;
        line.enabled = false;
        hitting = false;
        aiming = false;
        OnStopSelect?.Invoke();
    }

    public async UniTaskVoid Select()
    {
        Queue<Vector3> fromPos = new Queue<Vector3>();
        foreach (var guard in selectGuards)
        {
            if (guard.gameObject.activeInHierarchy)
                fromPos.Enqueue(guard.transform.position);
        }

        if (hitting)
        {
            var terrainTile = aimHit.transform.gameObject.GetComponentInParent<TileData>();

            var datapoint = await terrainTile.GetDatapointUsingKDTree(aimHit.point);
            Debug.Log(datapoint[13]);
            plantUIManager.GetTranscript(Convert.ToInt32(datapoint[13]));
            plantUIManager.DisplayTranscriptPage(0);
            plantUIManager.SpawnPlantUI(terrainTile.aloePlants.transforms[terrainTile.kdTree.FindNearest(aimHit.point)].position);

            //Debug.Log($"DF index 0: ({parquetParser.df[0, 10]}, {parquetParser.df[0, 11]})");
            //Debug.Log($": ({parquetParser.df[0, 10]}, {parquetParser.df[0, 11]})");
            //Debug.Log($"Hit point: ({aimHit.point.x}, {aimHit.point.z})");
            //Debug.Log($"Plant index found from hit: {selectedPlantIndex}");
            //var selectedPlant = parquetParser.df.Rows[selectedPlantIndex];
            //Debug.Log($"Selected datapoint coordinates: ({selectedPlant[10]}, {selectedPlant[11]})");
            //Debug.Log($"Datapoint coordinates scaled to map: " +
            //    $"({System.Convert.ToSingle(selectedPlant[10]) / parquetParser.plantMapScale}, " +
            //    $"{System.Convert.ToSingle(selectedPlant[11]) / parquetParser.plantMapScale})");
            //Debug.Log(selectedPlant);

            OnSelect?.Invoke();


            foreach (var guard in selectGuards)
            {
                if (guard.gameObject.activeInHierarchy)
                {
                    guard.TeleportProtection(fromPos.Dequeue(), guard.transform.position);
                }
            }
        }

        CancelSelect();
    }
}

