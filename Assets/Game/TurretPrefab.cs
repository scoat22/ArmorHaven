using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static TurretSystem;

public class TurretPrefab : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var Ship = GetShip(gameObject).GetComponent<Ship>();
        // Same team as ship.
        //var nTypes = typeof(TurretSystem.TurretType).GetEnumNames().Length;
        const int nTypes = 2;
        var Type = (TurretType)(int)(Random.value * nTypes); // For now make each turret a random type.
        //var Type = TurretType.LightTurret;
        TurretSystem.Instance.AddTurret(transform.parent, transform.position, transform.rotation, Type, Ship.GetComponent<Team>().value, Ship.IsPlayer);
        Destroy(gameObject);
    }

    GameObject GetShip(GameObject Turret) => Turret.transform.parent.parent.parent.gameObject;
}
