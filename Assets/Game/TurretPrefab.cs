using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretPrefab : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        if (ShipUtility.TryGetShip(transform, out Ship Ship))
        {
            // Same team as ship.
            //var nTypes = typeof(TurretSystem.TurretType).GetEnumNames().Length;
            const int nTypes = 2;
            //var Type = TurretSystem.TurretType.LightTurret;
            // For now make each turret a random type.
            var Type = (TurretSystem.TurretType)(int)(Random.value * nTypes); 
                                                                              
            TurretSystem.Instance.AddTurret(transform.parent, transform.position, transform.rotation, Type, Ship.GetComponent<Team>().value, Ship.IsPlayer);
            Destroy(gameObject);
        }
        else Debug.LogError("Failed to get parent ship", gameObject);
    }
}
