using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    public HealthBar HealthBar;
    public HealthBar FuelBar;

    public Text FramerateText;
    float Framerate = 0;
    float LastFrameRateReset = 0;

    CameraPoint[] CameraPoints = new CameraPoint[0];

    public GUIContent ButtonContent;
    // Don't show the camera point we just travelled to.
    int LastCameraPointIdx = -1;

    void FixedUpdate()
    {
        var Ships = ShipSystem.Instance.Ships;
        if (Ships[0] != null)
        {
            var Ship = ShipSystem.Instance.Ships[0].GetComponent<Ship>();
            HealthBar.SetFill(Ship.Health / Ship.MaxHealth);
            FuelBar.SetFill(Ship.Fuel / Ship.MaxFuel);

            //CameraPoints = Ship.GetComponentsInChildren<CameraPoint>();
        }
    }

    void Update()
    {
        int Fps = Mathf.RoundToInt(1.0f / Time.deltaTime);
        Framerate = Mathf.Max(Fps, Framerate);
        
        if(Time.time > LastFrameRateReset)
        {
            FramerateText.text = "FPS: " + (Framerate < 60 ? string.Format("<color=red>{0}</color>", Framerate.ToString()) : Framerate.ToString());
            Framerate = Fps;
            LastFrameRateReset = Time.time + 1;
        }
    }

    private void OnGUI()
    {
        /*var CameraController = NewCameraController.Instance;
        var camera = Camera.main;
        const float ButtonWidth = 30.0f;

        if(CameraController.transform.parent == null && CameraPoints.Length > 0 && CameraPoints[0] != null)
        {
            SetCameraToPoint(CameraController.transform, CameraPoints[0].transform);
        }

        //GUIContent
        // Render a button for each point
        for (int i = 0; i < CameraPoints.Length; i++)
        {
            if (LastCameraPointIdx == i) continue;

            var Position = camera.WorldToScreenPoint(CameraPoints[i].transform.position);
            if (Position.x < 0 || Position.x > Screen.width) continue;
            if (Position.y < 0 || Position.y > Screen.height) continue;
            var rect = new Rect(Position.x, Screen.height - Position.y, ButtonWidth, ButtonWidth);

            if(GUI.Button(rect, ButtonContent))
            {
                Debug.Log("Teleporting to camera point: ", CameraPoints[i]);
                SetCameraToPoint(CameraController.transform, CameraPoints[i].transform);
                LastCameraPointIdx = i;
            }
        }*/
    }

    static void SetCameraToPoint(Transform CameraTransform, Transform transform)
    {
        CameraTransform.SetParent(transform);
        CameraTransform.localPosition = Vector3.zero;
        CameraTransform.localRotation = Quaternion.identity;
        NewCameraController.Instance.Angles = Vector3.zero;
    }
}
