using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BigAsteroidSystem : MonoBehaviour
{
    public GameObject AsteroidPrefab;
    public int nAsteroids = 10;
    public float SpawnRadius = 100;
    public float SpawnHeight;
    public float MinSize = 10;
    public float MaxSize = 16;
    public Transform BoundingBox;
    GameObject[] Asteroids;
    public float MaxRotationSpeed = 45;

    // Start is called before the first frame update
    void Start()
    {
        var Scale = BoundingBox.localScale;
        Asteroids = new GameObject[nAsteroids];
        for (int i = 0; i < nAsteroids; i++)
        {
            var go = Instantiate(AsteroidPrefab);
            Vector2 p = Random.insideUnitCircle * SpawnRadius;
            go.transform.position = new Vector3(p.x, 0, p.y);
            go.transform.rotation = Random.rotation;
            go.transform.localScale = Vector3.one * Random.Range(MinSize, MaxSize);
            Asteroids[i] = go;
        }
    }

    // Update is called once per frame
    void Update()
    {
        /*for (int i = 0; i < nAsteroids; i++)
        {
            // Random rotation
            Random.InitState(i);
            var Rotation = Random.rotation;
            var Speed = Random.value * MaxRotationSpeed;
            Asteroids[i].transform.rotation = Rotation * Quaternion.AngleAxis(Time.time * Speed, Vector3.up);
        }*/
    }
}
