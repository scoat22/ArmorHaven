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
        var nTypes = typeof(TurretSystem.TurretType).GetEnumNames().Length;
        var RandomType = (TurretType)(int)(Random.value * nTypes); // For now make each turret a random type.
        TurretSystem.Instance.AddTurret(transform.parent, transform.position, transform.rotation, RandomType, Ship.GetComponent<Team>().value, Ship.IsPlayer);
        Destroy(gameObject);
    }

    GameObject GetShip(GameObject Turret) => Turret.transform.parent.parent.parent.gameObject;
}
