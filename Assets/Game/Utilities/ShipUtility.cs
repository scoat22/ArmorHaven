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

    public static T TryAddComponent<T>(this GameObject go) where T : Component
    {
        if (go.GetComponent<T>() != null)
            return go.GetComponent<T>();
        else return go.AddComponent<T>();
    }
}
