using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipAcceleration : MonoBehaviour
{
    // Dependant on surface area of ship and the air density;
    public float AirResistance = 0.3f;
    public float RocketPower = 1.0f;
    public float TurnAcceleration = 5;
    private Vector3 Velocity;
    public float PlayerSpeedMultiplier = 2.0f;

    public float Pitch = 1.0f;
    public AudioSource Engine;
    public Material mat;

    /*private void Start()
    {
        var Ship = ShipSystem.Instance.Ships[0].GetComponent<Ship>();
        Ship.RocketPower *= PlayerSpeedMultiplier;
    }*/

    private void Update()
    {
        Camera camera = Camera.main;
        
        Vector3 fwd = camera.transform.forward;
        Vector3 right = camera.transform.right;
        Vector3 up = Vector3.up;
        fwd.y = 0;
        right.y = 0;

        Vector3 DesiredDirection = Vector3.zero;
        Vector3 DesiredAngularRotation = Vector3.zero;

        // Add velocity with W key, in the direction of the ship's forward vector.
        if (Input.GetKey(KeyCode.W)) DesiredDirection += fwd;
        if (Input.GetKey(KeyCode.S)) DesiredDirection -= fwd;
        if (Input.GetKey(KeyCode.A)) DesiredDirection -= right;
        if (Input.GetKey(KeyCode.D)) DesiredDirection += right;
        if (Input.GetKey(KeyCode.Space)) DesiredDirection += up;
        if (Input.GetKey(KeyCode.LeftShift)) DesiredDirection -= up;
        //if (Input.GetKey(KeyCode.E)) AngularAccelerate(-up, 1.0f); //TurnVelocityY -= TurnAcceleration * Time.deltaTime; // Spin left.
        //if (Input.GetKey(KeyCode.Q)) AngularAccelerate(up, 1.0f); //TurnVelocityY += TurnAcceleration * Time.deltaTime; // Spin right.

        // Now actually modify the ship (the player's ship).
        var Ship = ShipSystem.Instance.Ships[0];
        if(Ship) Ship.GetComponent<Ship>().Controls.DesiredDirection = DesiredDirection;

        // Todo: change this to the thruster system.
        //Engine.volume = Mathf.Clamp(Velocity.magnitude * 10.0f, 0.2f, 1.0f);


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
}
