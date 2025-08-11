using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DriverSystem : MonoBehaviour
{
    float PassiveDistance = 300.0f;
    float AggressiveDistance = 5.0f;

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
        if (ShipSystem.nShips > 0)
        {
            PlayerPosition = ShipSystem.Ships[0].transform.position;
        }
        else Debug.LogError("Couldn't get player position");

        // I'd like a few ships to be aggressive. 
        // So we'll have one passive tick, and one aggressive tick function.
        // If you want this to affect the player, have i start at 0, not 1.

        // Make half aggressive
        int nEnemies = ShipUtility.EnemyCount();
        int nAggressive = 0;
        int MaxAggressive = Mathf.CeilToInt(nEnemies / 2);
        for (int i = 1; i < ShipSystem.nShips; i++)
        {
            var Ship = ShipSystem.Ships[i];

            if (Ship.GetComponent<Team>().team == team.Enemies)
            {
                if (nAggressive < MaxAggressive)
                {
                    Orbit(Ship.GetComponent<Ship>(), PlayerPosition, AggressiveDistance);
                }
                else
                {
                    Orbit(Ship.GetComponent<Ship>(), PlayerPosition, PassiveDistance);
                }
            }
        }
    }

    void Orbit(Ship Ship, Vector3 TargetPos, float DesiredDistance)
    {
        var rb = Ship.GetComponent<Rigidbody>();

        // Go in a circle.
        // Basically we have a desired distance from the center, and we have a desired direction (clockwise or whatever)
        Vector3 ShipPos = Ship.transform.position;
        Vector3 TargetToShip = ShipPos - TargetPos;

        // Don't move in Y axis for now.
        TargetPos.y = 0;
        TargetToShip.y = 0;
        Vector3 DesiredPosition = TargetPos + TargetToShip.normalized * DesiredDistance;// DesiredDistance;
        Vector3 ToOrbit = DesiredPosition - ShipPos; // This is nice to debug, shows a line to the orbit ring.
        Vector3 ToOrbitDir = ToOrbit.normalized;

        // Determine how long it will take us to reach 0 velocity. 
        float DistanceToOrbit = ToOrbit.magnitude;

        bool IsGoingRightWay = Vector3.Dot(rb.velocity.normalized, ToOrbitDir) > 0; 
        if (IsGoingRightWay && DistanceToOrbit < ShipUtility.DistanceToStop(Ship, rb)) ToOrbitDir *= -1; // Reverse thrusters.

        // Let's take velocities into account here.
        //TurretUtility.CalculateLeadShot(ShipPos, DesiredPosition, Vector3.zero, rb.velocity.magnitude, out Vector3 LeadingShotDir);

        //Ship.Controls.DesiredDirection = LeadingShotDir;
        Ship.Controls.DesiredDirection = ToOrbitDir;

        // Debug
        Debug.DrawLine(ShipPos, DesiredPosition);
        var p = ShipPos + rb.velocity.normalized * 10.0f;
        Debug.DrawLine(p, p + rb.velocity, Color.red);
        Debug.DrawLine(p, p + Ship.Controls.DesiredDirection * 10.0f, Color.yellow);
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
