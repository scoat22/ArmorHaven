using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Thruster : MonoBehaviour
{
    float TimeStart;
    float Power;

    void Update()
    {
        ThrusterSystem.Thrusters.Add(new ThrusterSystem.ThrusterData()
        {
            Transform = transform.localToWorldMatrix,
            Power = Power,
            TimeEnd = TimeStart,
        });
        Power = Mathf.Max(0, Power - Time.deltaTime * 4.0f); // Fade speed.
    }

    public void Thrust(float power = 1.0f)
    {
        Power = power;
        TimeStart = Time.time;
    }
}
