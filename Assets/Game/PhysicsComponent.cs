using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Testing (in the future I don't want to use 
public class PhysicsComponent : MonoBehaviour
{
    public Vector3 AngularVelocity;
    public float Mass; // Later use mass.

    void Start()
    {
        // Test
        AngularVelocity = Random.insideUnitSphere.normalized;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // Apply angular velocity to rotation
        float magnitude = AngularVelocity.magnitude * dt;
        transform.rotation *= Quaternion.AngleAxis(magnitude, AngularVelocity / magnitude);
    }
}
