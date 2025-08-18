using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ShipUtility;
using static UnityEngine.Mathf;

public class DriverSystem : MonoBehaviour
{
    float PassiveDistance = 300.0f;
    float AggressiveDistance = 30.0f;

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
        Vector3 PlayerVelocity = Vector3.zero;
        if (TryGetPlayer(out Ship PlayerShip))
        {
            PlayerPosition = PlayerShip.transform.position;
            PlayerVelocity = PlayerShip.GetComponent<Rigidbody>().velocity;
        }
        else Debug.LogError("Couldn't get player position");

        // I'd like a few ships to be aggressive. 
        // So we'll have one passive tick, and one aggressive tick function.
        // If you want this to affect the player, have i start at 0, not 1.

        // Make half aggressive
        int nEnemies = EnemyCount();
        int nAggressive = 0;
        int MaxAggressive = CeilToInt(nEnemies / 2);
        for (int i = 1; i < ShipSystem.nShips; i++)
        {
            var Ship = ShipSystem.Ships[i];

            if (Ship.GetComponent<Team>().team == team.Enemies)
            {
                if (nAggressive < MaxAggressive)
                {
                    nAggressive++;
                    Orbit(Ship.GetComponent<Ship>(), PlayerPosition, PlayerVelocity, AggressiveDistance);
                }
                else
                {
                    Orbit(Ship.GetComponent<Ship>(), PlayerPosition, PlayerVelocity, PassiveDistance);
                }
            }
        }
    }

    void Orbit(Ship Ship, Vector3 TargetPos, Vector3 TargetVelocity, float DesiredDistance)
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
        Vector3 ToTargetPos = DesiredPosition - ShipPos; // This is nice to debug, shows a line to the orbit ring.

        // We want to cancel out our current velocity a little bit, if its not helpful. 
        Vector3 Acceleration = rb.GetAccumulatedForce(Time.fixedDeltaTime) / rb.mass; // Testing to see if min of 1 helps.
        Vector3 Velocity = rb.velocity;
        Vector3 RelativeVelocity = Velocity - TargetVelocity; // (We don't care about the difference if the target is also moving the same way as us)
        RelativeVelocity.y = 0; // Ignore y axis.

        float VelocityDrift = 0.0f;

        // How different is our current velocity compared to where we want to go?
        //VelocityDrift = 1.0f - Mathf.Abs(Vector3.Dot(RelativeVelocity.normalized, ToTargetPos.normalized));

        Ship.DebugValues.VelocityDrift = VelocityDrift;
        //Vector3 DesiredDirection = Vector3.Lerp(ToTargetPos.normalized, -RelativeVelocity, VelocityDrift).normalized; // Works pretty well.
        Vector3 DesiredDirection = ToTargetPos.normalized;


        // Determine how long it will take us to reach 0 velocity. 
        float DistanceToOrbit = ToTargetPos.magnitude;
        /*bool IsGoingRightWay = Vector3.Dot(rb.velocity.normalized, DesiredDirection) > 0;
        float DistanceToReachTargetVelocity = -DistanceToReachVelocity(TargetVelocity.magnitude, rb.velocity.magnitude, Acceleration);
        bool Break = IsGoingRightWay && DistanceToOrbit < DistanceToReachTargetVelocity;
        if (Break) DesiredDirection *= -1; // Reverse thrusters.
        Ship.DebugValues.IsBreaking = Break;
        Ship.DebugValues.ReadAcceleration = Acceleration;
        Ship.DebugValues.DistanceToTargetVelocity = DistanceToReachTargetVelocity;*/

        var Axes = new Vector3[3] { Vector3.right, Vector3.up, Vector3.forward };
        for (int i = 0; i < 3; i++)
        {
            //Acceleration[i] = Max(1.0f, Abs(Acceleration[i]));
            // Distance to reach target velocity.
            float BreakingDistance = Abs(DistanceToReachVelocity(TargetVelocity[i], rb.velocity[i], Acceleration[i]));
            bool ShouldBreak = BreakingDistance > Abs(ToTargetPos[i]);
            bool IsGoingRightWay = Sign(rb.velocity[i]) == Sign(DesiredDirection[i]);

            //if (Input.GetKey(KeyCode.Alpha6)) ShouldBreak = ShouldBreak && RelativeVelocity != Vector3.zero;

            // Test.
            if (!Input.GetKey(KeyCode.Alpha5))
                if (IsGoingRightWay && ShouldBreak) DesiredDirection[i] *= -1;

            var p = ShipPos;
            if (ShouldBreak)
            {
                Debug.DrawLine(p, p + Axes[i] * Mathf.Sign(rb.velocity[i]) * BreakingDistance, Color.red); p += Vector3.one;
            }
            else
                Debug.DrawLine(p, p + Axes[i] * ToTargetPos[i], Color.green); p += Vector3.one;
            //Debug.DrawLine(p, p + Axes[i] * Mathf.Sign(ToTargetPos[i]) * BreakingDistance,  ShouldBreak ? Color.red : Color.green); p += Vector3.one;
            Debug.DrawLine(p, p + Axes[i] * rb.velocity[i], Color.white); p += Vector3.one;
            Debug.DrawLine(p, p + Axes[i] * DesiredDirection[i] * 10.0f,                    IsGoingRightWay ? Color.yellow : Color.yellow * 0.5f);
        }
        // Let's take velocities into account here.
        //TurretUtility.CalculateLeadShot(ShipPos, DesiredPosition, Vector3.zero, rb.velocity.magnitude, out Vector3 LeadingShotDir);

        //Ship.Controls.DesiredDirection = LeadingShotDir;
        Ship.Controls.DesiredDirection = DesiredDirection;

        // Debug
        //var p = ShipPos + rb.velocity.normalized * 10.0f;
        //var p = ShipPos;
        //Debug.DrawLine(p, p + ToTargetPos);
        //Debug.DrawLine(p, p + rb.velocity, Color.red);
        /*Debug.DrawLine(p, p + Ship.Controls.DesiredDirection * 30.0f, Color.yellow);
         Debug.DrawLine(p, p + DebugDist, Color.blue);*/
    }

    private void OnGUI()
    {
        for (int i = 0; i < ShipSystem.Instance.nShips; i++)
        {
            var Ship = ShipSystem.Instance.Ships[i].GetComponent<Ship>();
            var ShipPos = Ship.transform.position;
            var ScreenPos = Camera.main.WorldToScreenPoint(Ship.transform.position);
            ScreenPos.y = Screen.height - ScreenPos.y;
            var w = 300;
            var h = 20;
            //GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), string.Format("Drift: <color=yellow>{0}%</color>", Mathf.RoundToInt(Ship.DebugValues.VelocityDrift * 100))); ScreenPos.y += h;
            if(Ship.DebugValues.IsBreaking) GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), string.Format("<color=yellow>IsBreaking</color>")); ScreenPos.y += h;
            //GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), string.Format("Accel: <color=yellow>{0:N0}</color>", Ship.DebugValues.ReadAcceleration)); ScreenPos.y += h;
            //GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), string.Format("Dist: <color=yellow>{0}</color>", Ship.DebugValues.DistanceToTargetVelocity)); ScreenPos.y += h;
        }
    }

    /*private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1.0f, 0.0f, 1.0f);
        Gizmos.DrawWireSphere(Vector3.zero, PassiveDistance);
    }*/
}
