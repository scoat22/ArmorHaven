using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DriverSystem : MonoBehaviour
{
    float PassiveDistance = 300.0f;
    float AggressiveDistance = 20.0f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        var ShipSystem = global::ShipSystem.Instance;
        var TeamSystem = Teams.Instance;
        Vector3 PlayerPosition = Vector3.zero;
        if (ShipSystem.nShips > 0) PlayerPosition = ShipSystem.Ships[0].transform.position;

        // I'd like a few ships to be aggressive. 
        // So we'll have one passive tick, and one aggressive tick function.
        // If you want this to affect the player, have i start at 0, not 1.

        // Make one ship aggressive
        int nEnemies = ShipUtility.EnemyCount();
        for (int i = 1 + nEnemies / 2; i < ShipSystem.nShips; i++)
        {
            var Ship = ShipSystem.Ships[i];
            //if(Ship.GetComponent<Team>().team == team.Enemies)
            Orbit(Ship.GetComponent<Ship>(), PlayerPosition, AggressiveDistance);
        }
        // The rest are passive
        for (int i = 2; i < ShipSystem.nShips; i++)
        {
            var Ship = ShipSystem.Ships[i];
            Orbit(Ship.GetComponent<Ship>(), PlayerPosition, PassiveDistance);
        }
    }

    void Passive(Ship Ship)
    {
        Orbit(Ship, Vector3.zero, PassiveDistance);
    }

    void Aggressive(Ship Ship)
    {
        Orbit(Ship, Vector3.zero, AggressiveDistance);
    }

    void Orbit(Ship Ship, Vector3 TargetPos, float DesiredDistance)
    {
        // Go in a circle.
        // Basically we have a desired distance from the center, and we have a desired direction (clockwise or whatever)
        Vector3 ShipPos = Ship.transform.position;
        Vector3 ToTarget = TargetPos - ShipPos;
        /*Vector3 ToNextRingSpot = TargetPos + Vector3.Cross(ToTarget, Vector3.up).normalized * DesiredDistance;
        float Distance = ToTarget.magnitude;
        ToTarget = Vector3.Lerp(ToTarget, ToNextRingSpot, Distance / DesiredDistance);
        */

        ToTarget.y = 0;
        Vector3 DesiredPosition = -ToTarget.normalized * DesiredDistance;
        Vector3 ToOrbit = DesiredPosition - ShipPos; // This is nice to debug, shows a line to the orbit ring.
        Vector3 ToOrbitDir = ToOrbit.normalized;

        // Determine how long it will take us to reach 0 velocity. 
        float DistanceToOrbit = ToOrbit.magnitude;

        var rb = Ship.GetComponent<Rigidbody>();
        bool IsGoingRightWay = Vector3.Dot(rb.velocity.normalized, ToOrbitDir) > 0; 
        if (IsGoingRightWay && DistanceToOrbit < DistanceToStop(Ship)) ToOrbitDir *= -1; // Reverse thrusters.

        Ship.Controls.DesiredDirection = ToOrbitDir * 10.0f; // Make longer so we can see it.
        //Ship.Controls.ThrusterPower = 
    }

    public static float DistanceToStop(Ship Ship)
    {
        var rb = Ship.GetComponent<Rigidbody>();
        float Velocity = rb.velocity.magnitude;
        // On average there's 4 thrusters engaging, so multiply by 4.
        float Acceleration = 4.0f * Ship.RocketPower * Time.fixedDeltaTime / rb.mass;
        return DistanceToReachVelocity(0, Velocity, Acceleration);
    }

    static float DistanceToReachVelocity(float DesiredVelocity, float Velocity, float Acceleration)
    {
        return (Velocity * Velocity - DesiredVelocity * DesiredVelocity) / 2 * Acceleration;
    }

    /*private void OnGUI()
    {
        var ShipPos = ShipSystem.Instance.Ships[0];
        var ScreenPos = Camera.main.WorldToScreenPoint(ShipPos.transform.position);
        var w = 300;
        var h = 20;
        GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), "DistanceToMotionless: " + DistanceToMotionless);
        ScreenPos.y += h;
        GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), "DistanceToOrbit: " + DistanceToOrbit);
    }*/

    /*private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1.0f, 0.0f, 1.0f);
        Gizmos.DrawWireSphere(Vector3.zero, PassiveDistance);
    }*/
}
