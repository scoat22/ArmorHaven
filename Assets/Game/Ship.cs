using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ShipControls
{
    public Vector3 DesiredDirection;
    public float ThrusterPower; // What percent [0, 1] to throttle the thrusters
}

/// <summary>
/// Use this to store data
/// </summary>
public class Ship : MonoBehaviour
{
    public team Team;

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
    public float Health = 1.0f;
    public float MaxHealth = 1.0f;

    public float Radius = 10.0f;

    // Runtime controls.
    public ShipControls Controls;

    void Awake()
    {
        RocketPower = 100000.0f;
        Controls = new ShipControls();
        Controls.ThrusterPower = 1.0f;
    }

    public void AngularAccelerate(Transform Ship, Vector3 AngularAcceleration, float Power)
    {
        var Direction = AngularAcceleration.normalized;
        var rb = Ship.GetComponent<Rigidbody>();

        // Search for Thrusters 
        // Note: GetComponentsInChildren won't find disabled GameObjects.
        var Thrusters = Ship.GetComponentsInChildren<Thruster>();
        Power = Mathf.Clamp01(Power) * RocketPower;

        foreach (var Thruster in Thrusters)
        {
            Vector3 ToThruster = Thruster.transform.position - Ship.transform.position;
            Vector3 Cross = Vector3.Normalize(Vector3.Cross(AngularAcceleration, ToThruster));
            bool IsCorrectDirection = Vector3.Dot(Thruster.transform.forward, Cross) < -0.1f;

            // This seems to be enough. Now just need to modulate power. 
            if (IsCorrectDirection)
            {
                Thrust(rb, Thruster, Power);
            }
        }
    }

    public void Accelerate(Transform Ship, Vector3 Acceleration, float Power)
    {
        var Direction = Acceleration.normalized;
        var rb = Ship.GetComponent<Rigidbody>();

        // Search for Thrusters 
        // Note: GetComponentsInChildren won't find disabled GameObjects.
        var Thrusters = Ship.GetComponentsInChildren<Thruster>();
        Power = Mathf.Clamp01(Power) * RocketPower;

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
        Ship.AddForceAtPosition(-Thruster.transform.forward * Power, Thruster.transform.position, ForceMode.Force);
        Thruster.Thrust();
    }

    void OnDrawGizmos()
    {
        //Gizmos.DrawWireSphere(transform.position, SpottingRange);
        Gizmos.DrawLine(transform.position, transform.position + Controls.DesiredDirection);
    }
}
