using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ShipControls
{
    public Vector3 DesiredDirection;
    public Vector3 DesiredRotation;
    public float DirectionPower; // What percent [0, 1] to throttle the thrusters
    //public float RotationPower;
}

/// <summary>
/// Use this to store data
/// </summary>
public class Ship : MonoBehaviour
{
    /// <summary>
    /// Updated every half second.
    /// </summary>
    //public List<SpottedEnemy> SpottedEnemies = new List<SpottedEnemy>();

    // Ship properties
    public float SpottingRange = 8000.0f;
    public float RocketPower = 10000000.0f;

    // There's a centralized fuel source in the ship.
    public float MaxFuel = 1.0f;
    public float Fuel = 1.0f;

    // very temp (assume max is 1.0)
    // The ship will despawn after 5 seconds if Health is 0.
    public float Health = 1.0f;
    public float MaxHealth = 1.0f;

    public float Radius = 10.0f;

    // Runtime controls.
    public ShipControls Controls;

    public bool IsPlayer;

    // (LOCAL SPACE) The direction that we'll get the most thrust out of. 
    public Vector3 IdealDirection;
    public Vector3 Torque; // Caching this from RigidBody (since it resets internally every frame).

    public Rigidbody rb; // Cache

    public debug_values DebugValues;
    public struct debug_values
    {
        public float ReadAcceleration;
        public float DistanceToTargetVelocity;
        public float VelocityDrift;
        public bool IsBreaking;
    }

    void Awake()
    {
        //RocketPower = 100000.0f;
        Controls = new ShipControls();
        Controls.DirectionPower = 1.0f;
        rb = GetComponent<Rigidbody>();
    }

    public void AngularAccelerate(Transform Ship, Vector3 AngularAcceleration)
    {
        var Direction = AngularAcceleration.normalized;

        // Search for Thrusters 
        // Note: GetComponentsInChildren won't find disabled GameObjects.
        var Thrusters = Ship.GetComponentsInChildren<Thruster>();
        float Amount = Mathf.Clamp01(AngularAcceleration.magnitude) * RocketPower;

        foreach (var Thruster in Thrusters)
        {
            Vector3 ToThruster = Thruster.transform.position - Ship.transform.position;
            Vector3 Cross = Vector3.Normalize(Vector3.Cross(AngularAcceleration, ToThruster));
            bool IsCorrectDirection = Vector3.Dot(Thruster.transform.forward, Cross) < -0.1f;
            if (IsCorrectDirection)
            {
                Thrust(rb, Thruster, Amount);
            }
        }
    }

    public void Accelerate(Transform Ship, Vector3 Acceleration, float Amount)
    {
        var Direction = Acceleration.normalized;

        // Search for Thrusters 
        // Note: GetComponentsInChildren won't find disabled GameObjects.
        var Thrusters = Ship.GetComponentsInChildren<Thruster>();
        Amount = Mathf.Clamp01(Amount) * RocketPower;
        foreach (var Thruster in Thrusters)
        {
            // Is the thruster in the direction that we want?
            if (Vector3.Dot(Direction, Thruster.transform.forward) < -0.5f)
            {
                //Debug.LogFormat("Thrusting by {0:N0}", Amount);
                Thrust(rb, Thruster, Amount);
            }
        }
    }

    void Thrust(Rigidbody Ship, Thruster Thruster, float Amount)
    {
        Thruster.Thrust();
        Vector3 Force = -Thruster.transform.forward * Amount; // Thruster.MaxPower
        Ship.AddForceAtPosition(Force, Thruster.transform.position, ForceMode.Force);
        //Debug.LogFormat("Adding force: {0:N0} (Thruster.MaxPower: {1}, Amount: {2})", Force.magnitude, Thruster.MaxPower, Amount);
    }

    void OnDrawGizmos()
    {
        //Gizmos.DrawWireSphere(transform.position, SpottingRange);
        Gizmos.DrawLine(transform.position, transform.position + Controls.DesiredDirection);
    }
}
