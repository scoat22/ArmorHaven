using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipAcceleration : MonoBehaviour
{
    // Dependant on surface area of ship and the air density;
    public float AirResistance = 0.3f;
    public float RocketPower = 1.0f;
    public float TurnAcceleration = 5;
    private Vector3 Velocity;
    public bool UseStabilizers = true;
    public float StabilizerTheshold = 0.01f;

    public float Pitch = 1.0f;
    public AudioSource Engine;

    private void Update()
    {
        Camera camera = Camera.main;
        float dt = Time.deltaTime;
        Vector3 fwd = camera.transform.forward;
        Vector3 right = camera.transform.right;
        Vector3 up = Vector3.up;
        fwd.y = 0;
        right.y = 0;

        // Add velocity with W key, in the direction of the ship's forward vector.
        if (Input.GetKey(KeyCode.W))
        {
            Accelerate(fwd);

            // Turn (turning should in theory reduce speed, right?)
            // Turn toward camera forward
            Vector3 TargetDir = camera.transform.forward;
            //Vector3 TurnAxis = Vector3.Cross(TargetDir, transorm)
            //Quaternion TargetDir = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
            //transform.rotation = Quaternion.RotateTowards(transform.rotation, TargetDir, TurnSpeed * Time.fixedDeltaTime);
        }
        if (Input.GetKey(KeyCode.S)) Accelerate(-fwd);
        if (Input.GetKey(KeyCode.A)) Accelerate(-right);
        if (Input.GetKey(KeyCode.D)) Accelerate(right);
        if (Input.GetKey(KeyCode.Space)) Accelerate(up);
        if (Input.GetKey(KeyCode.LeftShift)) Accelerate(-up);
        if (Input.GetKey(KeyCode.E)) AngularAccelerate(-up); //TurnVelocityY -= TurnAcceleration * Time.deltaTime; // Spin left.
        if (Input.GetKey(KeyCode.Q)) AngularAccelerate(up); //TurnVelocityY += TurnAcceleration * Time.deltaTime; // Spin right.

        // Auto stabilize
        if (UseStabilizers)
        {
            var Ship = ShipManager.Instance.Ships[0];
            var rb = Ship.GetComponent<Rigidbody>();
            if (rb.angularVelocity.magnitude > StabilizerTheshold)
            {
                Debug.Log("Stabilizing...");
                AngularAccelerate(-rb.angularVelocity);
            }
        }

        Engine.volume = Mathf.Clamp(Velocity.magnitude * 10.0f, 0.2f, 1.0f);
    }

    // Update is called once per frame
    /*void FixedUpdate()
    {
        transform.position += Velocity;
        transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y + TurnVelocityY, transform.localEulerAngles.z);

        // Slow down ship due to air resistance;
        Velocity -= Velocity * AirResistance * Time.fixedDeltaTime;
        TurnVelocityY -= TurnVelocityY * AirResistance * Time.fixedDeltaTime;
    }*/

    void AngularAccelerate(Vector3 AngularAcceleration)
    {
        var Direction = AngularAcceleration.normalized;
        var Ship = ShipManager.Instance.Ships[0];
        var rb = Ship.GetComponent<Rigidbody>();

        // Search for Thrusters 
        // Note: GetComponentsInChildren won't find disabled GameObjects.
        var Thrusters = Ship.GetComponentsInChildren<Thruster>();
        float Power = RocketPower / (float)Thrusters.Length;

        foreach (var Thruster in Thrusters)
        {
            //bool IsPerpendicular = Mathf.Abs(Vector3.Dot(Thruster.transform.forward, Direction)) < 0.5f;

            // For now just assume the acceleration is around the Y axis.
            Vector3 ToThruster = Thruster.transform.position - Ship.transform.position;
            Vector3 Cross = Vector3.Normalize(Vector3.Cross(AngularAcceleration, ToThruster));
            bool IsCorrectDirection = Vector3.Dot(Thruster.transform.forward, Cross) < -0.1f;

            //if (IsPerpendicular && IsCorrectDirection
            // This seems to be enough. Now just need to modulate power. 
            if (IsCorrectDirection)
            {
                Thrust(rb, Thruster, Power);
            }
        }
    }

    void Accelerate(Vector3 Acceleration)
    {
        //ShipSystem.Instance.Velocities[0] += Acceleration * RocketPower;
        //ShipManager.Instance.Ships[0].GetComponent<Rigidbody>().velocity += Acceleration * RocketPower;
        var Direction = Acceleration.normalized;
        var ship = ShipManager.Instance.Ships[0];
        var rb = ship.GetComponent<Rigidbody>();

        // Search for Thrusters 
        // Note: GetComponentsInChildren won't find disabled GameObjects.
        var Thrusters = ship.GetComponentsInChildren<Thruster>();
        float Power = RocketPower / (float)Thrusters.Length;

        foreach (var Thruster in Thrusters)
        {
            // Is the thruster in the direction that we want?
            if (Vector3.Dot(Direction, Thruster.transform.forward) < -0.5f)
            {
                Thrust(rb, Thruster, Power);
            }
        }
    }

    void Thrust(Rigidbody Ship, Thruster Thruster, float Power)
    {
        // We want to add the force AT the location and in the direction of the thruster, if possible. 
        Ship.AddForceAtPosition(-Thruster.transform.forward * Power, Thruster.transform.position);

        // Vfx
        //Thruster.transform.GetChild(0).gameObject.SetActive(true);
        Thruster.Thrust();
    }
}
