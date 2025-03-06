using ProceduralToolkit;
using UnityEngine;

public class PlayerZero : MonoBehaviour {

    [SerializeField]
    private TerrainController terrainController;

    //[SerializeField]
    //private float distance = 10;

    private float distance = 32;

    private void Start()
    {
        distance = terrainController.tileSize.x;
    }

    private void Update() {
        //if (Vector2.Distance(Vector2.zero, new Vector2(transform.position.x,transform.position.z)) > distance) {
        if (transform.localPosition.x > distance)
        {
            terrainController.Level.position -= new Vector3(distance, 0, 0);
            //transform.parent.position = terrainController.Level.position;
            //transform.position = new Vector3(0, transform.position.y, transform.position.z);//only necessary if player isn't a child of the level
        }
        if (transform.localPosition.x < -distance)
        {
            terrainController.Level.position -= new Vector3(-distance, 0, 0);
            //transform.parent.position = terrainController.Level.position;
            //transform.position = new Vector3(0, transform.position.y, transform.position.z);//only necessary if player isn't a child of the level
        }
        if (transform.localPosition.z > distance)
        {
            terrainController.Level.position -= new Vector3(0, 0, distance);
            //transform.parent.position = terrainController.Level.position;
            //transform.position = new Vector3(transform.position.x, transform.position.y, 0);//only necessary if player isn't a child of the level
        }
        if (transform.localPosition.z < -distance)
        {
            terrainController.Level.position -= new Vector3(0, 0, -distance);
            //transform.parent.position = terrainController.Level.position;
            //transform.position = new Vector3(transform.position.x, transform.position.y, 0);//only necessary if player isn't a child of the level
        }
    }
}