using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TurretUtility
{
    public static bool LimitRotation(float angle, float minAngle, float maxAngle, out float NewAngle)
    {
        // Normalize all angles to [0, 360)
        angle = Mathf.Repeat(angle, 360f);
        minAngle = Mathf.Repeat(minAngle, 360f);
        maxAngle = Mathf.Repeat(maxAngle, 360f);

        // Check if angle is inside the min-max range
        bool inside = false;

        if (minAngle <= maxAngle)
        {
            inside = angle >= minAngle && angle <= maxAngle;
        }
        else // wraparound case (e.g., min=330, max=30)
        {
            inside = angle >= minAngle || angle <= maxAngle;
        }

        if (inside)
        {
            NewAngle = angle;
            return false;
        }

        // Compute distance to both bounds and return the closest
        float distToMin = Mathf.DeltaAngle(angle, minAngle);
        float distToMax = Mathf.DeltaAngle(angle, maxAngle);

        NewAngle = Mathf.Abs(distToMin) < Mathf.Abs(distToMax) ? minAngle : maxAngle;
        return true;
    }

    // Written by AI so I have no idea if its right.
    public static bool CalculateLeadShot(Vector3 shooterPos, Vector3 targetPos, Vector3 targetVel, float projectileSpeed, out Vector3 leadDirection)
    {
        Vector3 toTarget = targetPos - shooterPos;

        // Quadratic equation coefficients a*t^2 + b*t + c = 0
        float a = Vector3.Dot(targetVel, targetVel) - projectileSpeed * projectileSpeed;
        float b = 2.0f * Vector3.Dot(toTarget, targetVel);
        float c = Vector3.Dot(toTarget, toTarget);

        // Discriminant
        float discriminant = b * b - 4 * a * c;

        // No valid solution
        if (discriminant < 0)
        {
            leadDirection = toTarget.normalized; // fallback: shoot directly
            return false;
        }

        // Choose the smallest positive time
        float sqrtDisc = Mathf.Sqrt(discriminant);
        float t1 = (-b + sqrtDisc) / (2 * a);
        float t2 = (-b - sqrtDisc) / (2 * a);
        float t = Mathf.Min(t1, t2);
        if (t < 0)
            t = Mathf.Max(t1, t2);
        if (t < 0)
        {
            leadDirection = toTarget.normalized;
            return false;
        }

        Vector3 interceptPoint = targetPos + targetVel * t;
        leadDirection = (interceptPoint - shooterPos).normalized;
        return true;
    }
}
