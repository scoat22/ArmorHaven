using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

struct blips
{
    public GameObject[] Blips;
    public int nBlips;
}

// Iterate through ships and render them on the X/Y plane as... circles idk. 
public class Radar : MonoBehaviour
{
    public GameObject BlipPrefab;
    blips Result;
    blips Result2;
    public int MaxBlips = 100;
    public float TickRate = 0.7f;
    public bool RotateRader = true;
    public bool SimulateRadarSweep = true;
    public float Scale = 100.0f;
    public float PixelRadius;
    public float PixelRadiusSq;
    private float InvPixelRadius;
    private float Timer = 0;
    public float CurrentSweepAngle = 0.0f;
    enum Type { Enemy, Asteroid }

    // Start is called before the first frame update
    void Start()
    {
        InitBlips(ref Result);
        InitBlips(ref Result2);
        PixelRadius = GetComponent<RectTransform>().sizeDelta.x / 2;
        InvPixelRadius = 1.0f / PixelRadius;
        PixelRadiusSq = PixelRadius * PixelRadius;
    }

    void InitBlips(ref blips Blips)
    {
        Blips.Blips = new GameObject[MaxBlips];
        for (int i = 0; i < MaxBlips; i++)
        {
            Blips.Blips[i] = Instantiate(BlipPrefab, transform);
            Blips.Blips[i].SetActive(false);
        }
    }

    private void Update()
    {
        if (RotateRader) transform.localEulerAngles = new Vector3(0, 0, Camera.main.transform.eulerAngles.y);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        // Re-render.
        if (Timer <= 0)
        {
            Swap(ref Result, ref Result2);
            RenderBlips(ref Result);
            Timer = TickRate;
        }
        Timer -= dt;

        if (SimulateRadarSweep)
        {
            Fade(ref Result, dt);
            Fade(ref Result2, dt);
            // Debug:
            //transform.GetChild(0).localEulerAngles = new Vector3(0, 0, CurrentSweepAngle * Mathf.Rad2Deg);

            // Get angles
            float AngleStep = Mathf.PI * 2 / (TickRate / dt);
            for (int i = 0; i < Result.Blips.Length; i++)
            {
                if (Result.Blips[i].activeSelf)
                {
                    Vector3 Position = Result.Blips[i].transform.localPosition;
                    float Angle = Mathf.Atan2(-Position.y, Position.x);
                    Angle = Mathf.Repeat(Angle + Mathf.PI * 2, Mathf.PI * 2);
                    float Distance = AngleDistance(CurrentSweepAngle, Angle);

                    // If the blip is close enough to the sweep (within one update's arc).
                    if (Distance < AngleStep)
                    {
                        var Image = Result.Blips[i].GetComponent<Image>();
                        Image.color = new Color(Image.color.r, Image.color.g, Image.color.b, 1.0f); // Reset alpha;
                    }
                }
            }

            CurrentSweepAngle = Mathf.Repeat(CurrentSweepAngle + AngleStep, Mathf.PI * 2);
        }
    }

    void Fade(ref blips Result, float dt)
    {
        // Fade them all slowly
        float StepAmount = 1.0f / (TickRate / dt);
        for (int i = 0; i < Result.Blips.Length; i++)
        {
            var Image = Result.Blips[i].GetComponent<Image>();
            Image.color = new Color(Image.color.r, Image.color.g, Image.color.b, Image.color.a - StepAmount);
        }
    }

    void Swap<T>(ref T a, ref T b)
    {
        var temp = a;
        a = b;
        b = temp;
    }

    public static float AngleDistance(float a, float b)
    {
        float diff = Mathf.DeltaAngle(a, b);
        return Mathf.Abs(diff);
    }

    // Update is called once per frame
    void RenderBlips(ref blips e)
    {
        e.nBlips = 0;
        var Ships = ShipSystem.Instance.Ships;
        var Asteroids = BigAsteroidSystem.Instance.Asteroids;
        var MainShipPosition = Ships[0].transform.position;

        // Render enemies
        for (int i = 1; i < Ships.Count; i++)
        {
            if (e.nBlips == e.Blips.Length) break;
            AddBlip(ref e, MainShipPosition, Ships[i], Type.Enemy);
        }

        // Render asteroids.
        for (int i = 0; i < Asteroids.Length; i++)
        {
            if (e.nBlips == e.Blips.Length) break;
            AddBlip(ref e, MainShipPosition, Asteroids[i], Type.Asteroid);
        }

        for (int i = e.nBlips; i < e.Blips.Length; i++)
        {
            e.Blips[i].SetActive(false);
        }
    }

    void AddBlip(ref blips e, Vector3 ObserverPosition, GameObject Object, Type type = Type.Asteroid)
    {
        // Do a raycast to see if its visible.
        /*if(type == Type.Enemy)
        {
            if(!Physics.Raycast(ObserverPosition, Object.transform.position - ObserverPosition))
            {
                // Object wasn't visible.
                return;
            }
        }*/

        Vector3 p = Object.transform.position - ObserverPosition;   // Make relative to main ship's position
        p *= InvPixelRadius * Scale;                                // Scale by some factor.



        // If distance is greater than pixel scale, don't render it. (Cull asteroids outside the radius).
        if (type == Type.Enemy && Vector3.SqrMagnitude(p) >= PixelRadiusSq)
        {
            // Clamp it to the edge of the radar
            p = p.normalized * PixelRadius;
        }

        if (Vector3.SqrMagnitude(p) < PixelRadiusSq)
        {
            e.Blips[e.nBlips].SetActive(true);
            e.Blips[e.nBlips].transform.localPosition = new Vector3(p.x, p.z, 0.0f);
            // Color
            switch (type)
            {
                case Type.Enemy: e.Blips[e.nBlips].GetComponent<Image>().color = new Color(1, 0, 0, 0); break;
                case Type.Asteroid: e.Blips[e.nBlips].GetComponent<Image>().color = new Color(0.7f, 0.7f, 0.7f, 0); break;
            }
            e.nBlips++;
        }
    }

    // Debug:
    /*private void OnGUI()
    {
        for (int i = 0; i < Blips.Length; i++)
        {
            if (Blips[i].activeSelf)
            {
                Vector3 WorldPosition = Blips[i].transform.position;
                var Rect = new Rect(WorldPosition.x, Screen.height - WorldPosition.y, 100, 30);

                Vector3 LocalPosition = Blips[i].transform.localPosition;

                GUI.Label(Rect, LocalPosition.sqrMagnitude.ToString());
            }
        }
    }*/
}
