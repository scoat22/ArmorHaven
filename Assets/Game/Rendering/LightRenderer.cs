using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class LightRenderer : MonoBehaviour
{
    public static LightRenderer Instance;
    public GameObject LightPrefab;
    GameObject[] Lights;
    public int MaxLights = 100;
    public int nLights;

    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
        Lights = new GameObject[MaxLights];
        for (int i = 0; i < MaxLights; i++)
        {
            Lights[i] = Instantiate(LightPrefab);
            Lights[i].GetComponent<Light>().enabled = false;
        }
    }

    public void AddLight(Vector3 Position, float size, float intensity, Color color)
    {
        if (nLights < MaxLights)
        {
            var Light = Instance.Lights[nLights];
            Light.GetComponent<Light>().enabled = true;
            Light.GetComponent<Light>().range = size;
            Light.GetComponent<Light>().intensity = intensity;
            Light.GetComponent<Light>().color = color; 
            Light.transform.position = Position;
            nLights++;
        }
        else Debug.Log("Reached max lights.");
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.timeScale > 0)
        {
            // Render all lights for a single frame.
            if (nLights > 0)
            {
                for (int i = 0; i < nLights; i++)
                {
                    Instance.Lights[i].GetComponent<Light>().enabled = false;
                }
            }
            nLights = 0;
        }
    }
}
