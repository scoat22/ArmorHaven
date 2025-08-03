using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerUI : MonoBehaviour
{
    public HealthBar HealthBar;
    public HealthBar FuelBar;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    private void FixedUpdate()
    {
        var Ship = ShipSystem.Instance.Ships[0].GetComponent<Ship>();

        HealthBar.SetFill(Ship.Health / Ship.MaxHealth);
        FuelBar.SetFill(Ship.Fuel / Ship.MaxFuel);
    }
}
