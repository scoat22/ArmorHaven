using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewCameraController : MonoBehaviour
{
    public static NewCameraController Instance; 

    public float xSpeed = 250.0f;
    public float ySpeed = 120.0f;
    public Vector3 Angles;

    public float yMinLimit = -20;
    public float yMaxLimit = 80;

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
    }

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;
        if (Input.GetMouseButton(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Angles.x += Input.GetAxis("Mouse X") * xSpeed * dt;
            Angles.y -= Input.GetAxis("Mouse Y") * ySpeed * dt;
            Angles.y = ClampAngle(Angles.y, yMinLimit, yMaxLimit);
            transform.localRotation = Quaternion.Euler(Angles.y, Angles.x, 0);
            
        }
        else Cursor.lockState = CursorLockMode.None;
    }

    static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360)
            angle += 360;
        if (angle > 360)
            angle -= 360;
        return Mathf.Clamp(angle, min, max);
    }
}
