using NUnit.Framework.Constraints;
using ProceduralToolkit;
using UnityEngine;

public class PlayerZero : MonoBehaviour
{
    [SerializeField]
    private LevelData level;

    private float distance = 16;

    private void Start()
    {
        distance = level.tc.tileSize.x / 2;
    }

    private void Update()
    {
        if (transform.position.x > distance)
        {
            level.transform.position -= new Vector3(distance, 0, 0);
            level.uim.transform.position -= new Vector3(distance, 0, 0);
            level.gpui.UpdateTransforms();
        }
        if (transform.position.x < -distance)
        {
            level.transform.position -= new Vector3(-distance, 0, 0);
            level.uim.transform.position -= new Vector3(-distance, 0, 0);
            level.gpui.UpdateTransforms();
        }
        if (transform.position.z > distance)
        {
            level.transform.position -= new Vector3(0, 0, distance);
            level.uim.transform.position -= new Vector3(0, 0, distance);
            level.gpui.UpdateTransforms();
        }
        if (transform.position.z < -distance)
        {
            level.transform.position -= new Vector3(0, 0, -distance);
            level.uim.transform.position -= new Vector3(0, 0, -distance);
            level.gpui.UpdateTransforms();
        }
    }
}