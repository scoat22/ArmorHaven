using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    public HealthBar HealthBar;
    public HealthBar FuelBar;

    public Text FramerateText;
    float Framerate = 0;

    void FixedUpdate()
    {
        var Ships = ShipSystem.Instance.Ships;
        if (Ships[0] != null)
        {
            var Ship = ShipSystem.Instance.Ships[0].GetComponent<Ship>();
            HealthBar.SetFill(Ship.Health / Ship.MaxHealth);
            FuelBar.SetFill(Ship.Fuel / Ship.MaxFuel);
        }
    }

    /*void Update()
    {
        float Fps = 1.0f / Time.deltaTime;
        FramerateText.text = "FPS: " + (Fps < 60 ? string.Format("<color=red>{0}</color>", Fps.ToString()) : Fps.ToString());
    }*/
}
