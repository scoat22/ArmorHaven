using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretPrefab : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var Ship = GetShip(gameObject).GetComponent<Ship>();
        // Same team as ship.
        TurretSystem.Instance.AddTurret(transform.parent, transform.position, transform.rotation, Ship.Team);
        Destroy(gameObject);
    }

    GameObject GetShip(GameObject Turret) => Turret.transform.parent.parent.parent.gameObject;
}
