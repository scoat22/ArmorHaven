using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ShipUtility
{
    public static bool TryGetShip(Transform child, out Ship Ship)
    {
        Ship = null;
        Transform current = child.transform;
        for (int i = 0; i < 6; i++)
        {
            if (current.TryGetComponent(out Ship))
            {
                return true;
            }
            else
            {
                if (current.parent != null)
                    current = current.parent;
                else break; // Reached top parent.
            }
        }
        return false;
    }

    public static bool TryGetPlayer(out Ship Ship)
    {
        if (ShipSystem.Instance.nShips > 0)
        {
            Ship = ShipSystem.Instance.Ships[0].GetComponent<Ship>();
            return true;
        }
        Ship = null;
        return false;
    }

    public static int EnemyCount()
    {
        var Ships = ShipSystem.Instance.Ships;
        var nShips = ShipSystem.Instance.nShips;

        // Count enemies.
        int nEnemies = 0;
        for (int i = 0; i < nShips; i++)
            if (Ships[i].GetComponent<Team>().value == team.Enemies)
                nEnemies++;

        return nEnemies;
    }

    public static T TryAddComponent<T>(this GameObject go) where T : Component
    {
        if (go.GetComponent<T>() != null)
            return go.GetComponent<T>();
        else return go.AddComponent<T>();
    }

    public static Vector3 GetIdealDirection(Ship Ship)
    {
        Vector3 Ideal = Vector3.zero;
        var Thrusters = Ship.GetComponentsInChildren<Thruster>();
        if (Thrusters.Length > 0)
        {
            foreach (var Thruster in Thrusters)
            {
                Ideal += Thruster.transform.forward * Thruster.MaxPower;
            }
            if (Ideal == Vector3.zero)
            {
                Ideal = Thrusters[0].transform.forward;
            }
        }
        // Not normalized, so we can determine the factor to which its ideal.
        return Ideal;
    }

    public static float DistanceToStop(Ship Ship, Rigidbody rb)
    {
        float Velocity = rb.velocity.magnitude;
        float Acceleration = 4.0f * Ship.RocketPower * Time.fixedDeltaTime / rb.mass; // On average there's 4 thrusters engaging, so multiply by 4.
        return DistanceToReachVelocity(0, Velocity, Acceleration);
    }

    public static float DistanceToReachVelocity(float DesiredVelocity, float Velocity, float Acceleration)
    {
        return (DesiredVelocity * DesiredVelocity - Velocity * Velocity) / (2.0f * Acceleration);
    }
}
