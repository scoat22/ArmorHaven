using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Thruster : MonoBehaviour
{
    public float MaxPower = 1.0f;
    float TimeStart;
    float Power;

    void Update()
    {
        //ThrusterSystem.AddThruster(new ThrusterSystem.ThrusterData()
        ThrusterSystem.Instance.AddThruster(new ThrusterSystem.ThrusterData()
        {
            Transform = transform.localToWorldMatrix,
            Power = Power / MaxPower,
            TimeEnd = TimeStart,
        });
        Power = Mathf.Max(0, Power - Time.deltaTime * 4.0f); // Fade speed.
    }

    public void Thrust(float Amount = 1.0f)
    {
        Power = MaxPower * Amount;
        TimeStart = Time.time;
    }
}
