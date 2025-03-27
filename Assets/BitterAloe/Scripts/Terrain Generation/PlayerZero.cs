using ProceduralToolkit;
using UnityEngine;

public class PlayerZero : MonoBehaviour
{
    [SerializeField]
    private LevelData level;

    private float distance = 32;

    private void Start()
    {
        distance = level.tc.tileSize.x / 2;
    }

    private void Update()
    {
        if (transform.position.x > distance)
            level.transform.position -= new Vector3(distance, 0, 0);
        if (transform.position.x < -distance)
            level.transform.position -= new Vector3(-distance, 0, 0);
        if (transform.position.z > distance)
            level.transform.position -= new Vector3(0, 0, distance);
        if (transform.position.z < -distance)
            level.transform.position -= new Vector3(0, 0, -distance);
    }
}