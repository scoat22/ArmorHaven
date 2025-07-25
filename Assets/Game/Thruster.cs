using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Thruster : MonoBehaviour
{
    public float TimeStart = 0;

    void Update()
    {
        ThrusterSystem.Thrusters.Add(new ThrusterSystem.ThrusterData()
        {
            Transform = transform.localToWorldMatrix,
            Power = 1.0f,
            TimeEnd = TimeStart,
        });
    }

    public void Thrust()
    {
        TimeStart = Time.time;
    }
}
