using UnityEngine;

public class PlayerZero : MonoBehaviour {

    [SerializeField]
    private TerrainController terrainController;

    [SerializeField]
    private float distance = 10;

    private void Update() {
        if (Vector3.Distance(Vector3.zero, transform.position) > distance) {
            terrainController.Level.position -= transform.position;
            transform.position = new Vector3(0,transform.position.y,0);//only necessary if player isn't a child of the level
        }
    }

}