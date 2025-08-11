using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;

    // Dependant on surface area of ship and the air density;
    public float AirResistance = 0.3f;
    public float RocketPower = 1.0f;
    public float TurnAcceleration = 5;
    private Vector3 Velocity;
    public float PlayerSpeedMultiplier = 2.0f;
    public int _Points = 0;
    public float ThrusterPower;
    int nThrusters; // Reference for audio.

    public float Pitch = 1.0f;
    public AudioSource Engine;
    public Material mat;
    [Header("UI")]
    public PointsUI _PointsUI;

    private void Start()
    {
        Instance = this;
    }

    private void Update()
    {
        Camera camera = Camera.main;
        
        Vector3 fwd = camera.transform.forward;
        Vector3 right = camera.transform.right;
        Vector3 up = Vector3.up;
        fwd.y = 0;
        right.y = 0;

        Vector3 DesiredDirection = Vector3.zero;
        Vector3 DesiredRotation = Vector3.zero;

        // Add velocity with W key, in the direction of the ship's forward vector.
        if (Input.GetKey(KeyCode.W)) DesiredDirection += fwd;
        if (Input.GetKey(KeyCode.S)) DesiredDirection -= fwd;
        if (Input.GetKey(KeyCode.A)) DesiredDirection -= right;
        if (Input.GetKey(KeyCode.D)) DesiredDirection += right;
        if (Input.GetKey(KeyCode.Space)) DesiredDirection += up;
        if (Input.GetKey(KeyCode.LeftShift)) DesiredDirection -= up;
        //if (Input.GetKey(KeyCode.E)) AngularAccelerate(fwd, 1.0f); //TurnVelocityY -= TurnAcceleration * Time.deltaTime; // Spin left.
        //if (Input.GetKey(KeyCode.Q)) AngularAccelerate(fwd, 1.0f); //TurnVelocityY += TurnAcceleration * Time.deltaTime; // Spin right.

        // Now actually modify the ship (the player's ship).
        var Ship = ShipSystem.Instance.Ships[0];
        if (Ship)
        {
            //if (Input.GetKey(KeyCode.Q)) Ship.GetComponent<Ship>().AngularAccelerate(Ship.transform, camera.transform.forward, 1.0f);
            //if (Input.GetKey(KeyCode.E)) Ship.GetComponent<Ship>().AngularAccelerate(Ship.transform, -camera.transform.forward, 1.0f);

            if (Input.GetKey(KeyCode.Q)) DesiredRotation += camera.transform.up;
            if (Input.GetKey(KeyCode.E)) DesiredRotation += -camera.transform.up;

            Ship.GetComponent<Ship>().Controls.DesiredDirection = DesiredDirection;
            Ship.GetComponent<Ship>().Controls.DesiredRotation = DesiredRotation;

            GetComponent<AudioSource>().volume = GetThrusterVolume(Ship.transform.position, nThrusters);
        }

        // Todo: change this to the thruster system.
        //Engine.volume = Mathf.Clamp(Velocity.magnitude * 10.0f, 0.2f, 1.0f);

        if (Input.GetKeyDown(KeyCode.T)) Time.timeScale = 1.0f - Time.timeScale;
    }

    private void FixedUpdate()
    {
        _PointsUI.SetPoints(_Points);

        var Ship = ShipSystem.Instance.Ships[0];
        if (Ship) nThrusters = Ship.GetComponentsInChildren<Thruster>().Length;
    }

    public void AddPoints(int Points)
    {
        _Points += Points;
        _PointsUI.AddPoints(Points);
    }

    void OnPostRender()
    {
        if (!mat)
        {
            Debug.LogError("Please Assign a material on the inspector");
            return;
        }
        var Ship = ShipSystem.Instance.Ships[0];
        var rb = Ship.GetComponent<Rigidbody>();

        mat.SetPass(0);
        GL.Begin(GL.LINES);
        GL.Color(Color.white);
        GL.Vertex(Ship.transform.position);
        //GL.Vertex(Ship.transform.position + rb.velocity * 15.0f);
        GL.Vertex(Vector3.zero);
        GL.End();
    }

    float GetThrusterVolume(Vector3 PlayerShipPosition, int MaxThrusters)
    {
        float MinDistanceSq = 15 * 15;
        ThrusterPower = 0;
        if (ThrusterSystem.Instance.nThrusters > 0)
        {
            foreach (var thruster in ThrusterSystem.Instance.Thrusters)
            {
                if (Vector3.SqrMagnitude(thruster.Transform.GetPosition() - PlayerShipPosition) < MinDistanceSq)
                {
                    ThrusterPower += thruster.Power;
                }
                //else Debug.LogFormat("Thruster wasn't close enough (Position: {0}, Distance: {1}", thruster.Transform.GetPosition(), Vector3.Distance(thruster.Transform.GetPosition(), PlayerShipPosition));
            }
        }
        //else Debug.Log("No thrusters");

        return ThrusterPower / (float)MaxThrusters;
    }
}
