using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BigAsteroidSystem : MonoBehaviour
{
    public static BigAsteroidSystem Instance;

    public GameObject AsteroidPrefab;
    public int nAsteroids = 10;
    public float SpawnRadius = 100;
    public float SpawnHeight;
    public float MinSize = 10;
    public float MaxSize = 16;
    public float MinMass = 600000;
    public Transform BoundingBox;
    public GameObject[] Asteroids;

    // Start is called before the first frame update
    void Start()
    {
        Instance = this;

        var Scale = BoundingBox.localScale;
        float Diff = MaxSize - MinSize;
        Asteroids = new GameObject[nAsteroids];
        for (int i = 0; i < nAsteroids; i++)
        {
            float size = Random.Range(MinSize, MaxSize);
            float mass = (MaxSize - size) * MinMass;
            Vector2 p = Random.insideUnitCircle * SpawnRadius;
            p += p.normalized * 20.0f; // Make a clearing in the center.
            Vector3 Torque = Random.rotation * (Vector3.up * Random.value) / size;

            var go = Instantiate(AsteroidPrefab);
            go.transform.position = new Vector3(p.x, 0, p.y);
            go.transform.rotation = Random.rotation;
            go.transform.localScale = Vector3.one * size;

            var rb = go.GetComponent<Rigidbody>();
            rb.mass = mass;
            rb.AddTorque(Torque, ForceMode.VelocityChange);

            Asteroids[i] = go;
        }
        //StartCoroutine(SetInterpolateMode());
    }

    IEnumerator SetInterpolateMode()
    {
        yield return new WaitForFixedUpdate();
        for (int i = 0; i < nAsteroids; i++)
        {
            // Only interpolate after setting spawn position (otherwise the asteroids will all spawn inside eachother). 
            Asteroids[i].GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.Interpolate;
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
