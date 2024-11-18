using UnityEngine;

public class PlayerZero : MonoBehaviour {

    [SerializeField]
    private TerrainController terrainController;

    [SerializeField]
    private float distance = 10;

    private void Update() {
        if (Vector2.Distance(Vector2.zero, new Vector2(transform.position.x,transform.position.z)) > distance) {
            terrainController.Level.position -= transform.position;
            transform.position = new Vector3(0,transform.position.y,0);//only necessary if player isn't a child of the level
        }
    }

}