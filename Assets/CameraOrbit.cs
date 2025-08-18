//  A simple Unity C# script for orbital movement around a target gameobject
//  Author: Ashkan Ashtiani
//  Gist on Github: https://gist.github.com/3dln/c16d000b174f7ccf6df9a1cb0cef7f80

using System;
using UnityEngine;
using UnityEngine.UI;
using static Unity.Mathematics.math;

namespace TDLN.CameraControllers
{
    public class CameraOrbit : MonoBehaviour
    {
        public static CameraOrbit Instance;
        //public GameObject target;
        public float distance = 10.0f;
        public float MinDistance = 2.0f;

        public float xSpeed = 250.0f;
        public float ySpeed = 120.0f;

        public float yMinLimit = -20;
        public float yMaxLimit = 80;
        public float _ScrollSpeed = 50;
        Vector3 Angles;

        public Vector3 CameraOffset;
        public float _ThirdPersonDistance = 40.1f;
        public float _FirstPersonDistance = -10.1f;
        public bool _LockFirstPersonCameraIfTimePaused = true;

        public static Vector3 MouseAimPosition;
        Vector3 HoveredObjectVelocity;

        Vector3 Position;
        public Vector3 Velocity;
        Vector3 LastPosition;
        public Image Reticle;

        public enum CameraBehaviour { ThirdPerson, FirstPerson }
        public CameraBehaviour _CameraBehaviour = CameraBehaviour.ThirdPerson;

        void Start()
        {
            Instance = this;
            Angles = transform.eulerAngles;
        }

        float prevDistance;

        private void FixedUpdate()
        {
            //Velocity = (transform.position - LastPosition) / Time.fixedDeltaTime;
            LastPosition = transform.position;

            if (TurretSystem.Instance.PlayerManualAim)
            {
                // Set aim position of all turrets
                var Turrets = TurretSystem.Instance.Turrets;
                var nTurrets = TurretSystem.Instance.nTurrets;
                for (int i = 0; i < nTurrets; i++)
                {
                    if (Turrets[i].IsPlayer)
                    {
                        var Turret = Turrets[i];
                        Turret.HasTarget = true;
                        Turret.SpottedEnemy = new SpottedEnemy()
                        {
                            Position = MouseAimPosition,
                            Velocity = HoveredObjectVelocity,
                        };
                        Turrets[i] = Turret;
                    }
                }
            }
        }

        void LateUpdate()
        {
            // Target ship 0 (the player).
            var target = ShipSystem.Instance.Ships[0];

            if (target)
            {
                Position = target.transform.position;
                Velocity = target.GetComponent<Rigidbody>().velocity;
                //Velocity = GetComponent<Rigidbody>().velocity;
            }

            if(Input.GetKeyDown(KeyCode.Mouse2) || Input.GetKeyDown(KeyCode.C))
            {
                // Toggle first/third person
                if (_CameraBehaviour == CameraBehaviour.ThirdPerson) _CameraBehaviour = CameraBehaviour.FirstPerson;
                else _CameraBehaviour = CameraBehaviour.ThirdPerson;
            }

            bool PlayerDead = target.GetComponent<Ship>().Health <= 0;

            // Go into third person when the player dies.
            if (_CameraBehaviour == CameraBehaviour.ThirdPerson || PlayerDead)
            {
                if (_ThirdPersonDistance < MinDistance) _ThirdPersonDistance = MinDistance;
                _ThirdPersonDistance -= Input.GetAxis("Mouse ScrollWheel") * _ScrollSpeed * Mathf.Log(_ThirdPersonDistance);

                // Lock player into a sort of "view only" mode when they die.
                if (Input.GetMouseButton(1) || PlayerDead)
                {
                    Angles.x += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                    Angles.y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
                    Angles.y = ClampAngle(Angles.y, yMinLimit, yMaxLimit);

                    // What was the point of these lines?
                    /*var pos = Input.mousePosition;
                    var dpiScale = 1f;
                    if (Screen.dpi < 1) dpiScale = 1;
                    if (Screen.dpi < 200) dpiScale = 1;
                    else dpiScale = Screen.dpi / 200f;

                    if (pos.x < 380 * dpiScale && Screen.height - pos.y < 250 * dpiScale) return;*/

                    // comment out these two lines if you don't want to hide mouse curser or you have a UI button 
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    Reticle.gameObject.SetActive(false);
                }
                else
                {
                    // comment out these two lines if you don't want to hide mouse curser or you have a UI button 
                    //Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    Reticle.gameObject.SetActive(true);
                }
                distance = _ThirdPersonDistance;
            }
            // Leave first person view if time is frozen.
            else if(_LockFirstPersonCameraIfTimePaused && Time.timeScale > 0)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Angles.x += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                Angles.y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
                Angles.y = ClampAngle(Angles.y, yMinLimit, yMaxLimit);
                distance = _FirstPersonDistance;
                //if(Input.GetMouseButton(1)) { Debug.Log("Pausing"); Debug.Break(); }
            }

            var rotation = Quaternion.Euler(Angles.y, Angles.x, 0);
            var position = rotation * new Vector3(0.0f, 0.0f, -distance) + Position;
            transform.rotation = rotation;
            transform.position = position + transform.right * CameraOffset.x + transform.up * CameraOffset.y;

            Cursor.visible = false;

            var Camera = UnityEngine.Camera.main;
            var MouseRay = Camera.ScreenPointToRay(Input.mousePosition);
            float Dist = Camera.farClipPlane;
            MouseAimPosition = MouseRay.origin + MouseRay.direction * Dist;
            HoveredObjectVelocity = target ? target.GetComponent<Rigidbody>().velocity : Vector3.zero; // By default use our velocity.
            Vector3 ScreenPos = Input.mousePosition;
            if (Physics.Raycast(MouseRay, out RaycastHit hit))
            {
                //MouseAimPosition = hit.point + hit.normal * 0.5f;
                MouseAimPosition = hit.point;
                if (hit.collider.TryGetComponent(out Rigidbody rb)) HoveredObjectVelocity = rb.velocity;
                Dist = hit.distance;
            }
            // Raycast a camera-facing plane against all enemy ships (if they're in front of the camera)
            var Ships = ShipSystem.Instance.Ships;
            var nShips = ShipSystem.Instance.nShips;
            var PlayerShip = Ships[0];
            float MaxDot = -1f;
            for (int i = 1; i < nShips; i++)
            {
                if (Ships[i].GetComponent<Team>().team == team.Enemies)
                {
                    var plane = new Plane(PlayerShip.transform.position - Ships[i].transform.position, Ships[i].transform.position);
                    if (plane.Raycast(MouseRay, out float HitDistance))
                    {
                        if (Dist > HitDistance)
                        {
                            Dist = HitDistance;
                            MouseAimPosition = MouseRay.origin + MouseRay.direction * HitDistance;
                        }
                    }
                }
            }
            if (Dist < Camera.farClipPlane) ScreenPos = Camera.WorldToScreenPoint(MouseAimPosition);
            ScreenPos.z = 0;
            Reticle.transform.position = ScreenPos;
            /*const float MaxDistance = 10.0f; //800.0f;
            float MaxWidth = 30.0f;
            float MinDist = 10.0f;
            float RayLength = max(0, Dist - MinDist);
            float Percent = RayLength / MaxDistance;
            Percent = Mathf.Clamp(1.0f - Percent * 0.5f, 0.5f, 1.0f); // Convert to [0.5, 1.0]
            Reticle.GetComponent<RectTransform>().sizeDelta = Vector2.one * MaxWidth * Percent;*/

            //transform.rotation = Quaternion.LookRotation((MouseAimPosition - transform.position).normalized, Vector3.up);
        }

        /*private void FixedUpdate()
        {
            Reticle.transform.GetChild(0).GetComponent<Image>().color = new Color(1, 1, 1, 0);
        }*/

        public void ShowHitmarker()
        {
            Reticle.transform.GetChild(0).GetComponent<Image>().color = new Color(1, 1, 1, 0.4f);
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
}