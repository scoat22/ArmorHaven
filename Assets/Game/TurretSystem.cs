//using System;
using System.Collections;
using System.Collections.Generic;
using TDLN.CameraControllers;
using Unity.Collections;
using UnityEngine;

public class TurretSystem : MonoBehaviour
{
    public GameObject TurretPrefab;
    public GameObject MuzzleFlashPrefab;
    public float TurnAcceleration = 5.0f;
    public float MaxRotationSpeed = 45f;
    public float BulletSpeed = 30.0f;
    public float ReloadSpeed = 3.0f;
    public float MinPitch = 270.0f;
    public float MaxPitch = 360.0f;
    public float MinYaw = -90.0f;
    public float MaxYaw = 90.0f;
    public float AngleDotTolerance = 0.03f;

    public int MaxTurrets = 100;
    int nTurrets;
    // TurnVelocity per turret
    NativeArray<float> YawVelocities;
    NativeArray<float> Yaw;
    NativeArray<float> PitchVelocities;
    NativeArray<float> Pitch;
    NativeArray<float> ReloadTimeRemaining;
    // Turret transforms
    GameObject[] Turrets;


    // Start is called before the first frame update
    void Start()
    {
        YawVelocities = new NativeArray<float>(MaxTurrets, Allocator.Persistent);
        Yaw = new NativeArray<float>(MaxTurrets, Allocator.Persistent);
        PitchVelocities = new NativeArray<float>(MaxTurrets, Allocator.Persistent);
        Pitch = new NativeArray<float>(MaxTurrets, Allocator.Persistent);
        ReloadTimeRemaining = new NativeArray<float>(MaxTurrets, Allocator.Persistent);
        CreateTurrets();

        var ExistingTurrets = FindObjectsByType<TurretPrefab>(FindObjectsSortMode.None);
        Debug.LogFormat("Replacing {0} existing turrets", ExistingTurrets.Length);
        foreach (var prefab in ExistingTurrets)
        {
            if (prefab.gameObject.activeSelf)
            {
                AddTurret(prefab.transform.parent, prefab.transform.position, prefab.transform.rotation);
                Destroy(prefab.gameObject);
            }
        }
    }

    private void OnDestroy()
    {
        YawVelocities.Dispose();
        Yaw.Dispose();
        PitchVelocities.Dispose();
        Pitch.Dispose();
        ReloadTimeRemaining.Dispose();
    }

    void CreateTurrets()
    {
        Turrets = new GameObject[MaxTurrets];
        for (int i = 0; i < MaxTurrets; i++)
        {
            var go = Instantiate(TurretPrefab);
            Turrets[i] = go;
            go.SetActive(false);
        }
    }

    public void AddTurret(Transform Parent, Vector3 Position, Quaternion Rotation)
    {
        if (nTurrets < MaxTurrets)
        {
            YawVelocities[nTurrets] = 0;
            Yaw[nTurrets] = 0;
            PitchVelocities[nTurrets] = 0;
            Pitch[nTurrets] = 0;
            ReloadTimeRemaining[nTurrets] = 0;
            Turrets[nTurrets].transform.parent = Parent;
            Turrets[nTurrets].transform.position = Position;
            Turrets[nTurrets].transform.rotation = Rotation;
            nTurrets++;
        }
        else Debug.LogError("Reached max turrets");
    }

    void FixedUpdate()
    {
        if (Input.GetMouseButton(1)) return;

        Vector3 AimPosition = CameraOrbit.MouseAimPosition;

        float dt = Time.fixedDeltaTime;
        for (int i = 0; i < nTurrets; i++)
        {
            var Turret = Turrets[i].transform;
            var Barrel = Turrets[i].transform.GetChild(0);

            // For now (skip if not the player).
            if (Turret.parent.parent.name != "0") continue;

            // Make them turn towards the aim position
            Vector3 TargetDir = (AimPosition - Turret.position);//.normalized;

            // Turret
            {
                // We basically just need the direction for now
                // Let's get the angle difference for the turret
                Quaternion TargetRot = Quaternion.LookRotation(TargetDir, Turret.up);
                TargetRot = Quaternion.Inverse(Turret.parent.rotation) * TargetRot;
                float TargetAngle = TargetRot.eulerAngles.y; // For some reason its 90 degrees off.
                Yaw[i] = TargetAngle;

                // The pitch has a limit.
                if (LimitRotation(Yaw[i], MinYaw, MaxYaw, out float NewYaw))
                {
                    Yaw[i] = NewYaw;
                    YawVelocities[i] = 0; // Reset velocity.
                }

                Turret.localEulerAngles = new Vector3(0, Yaw[i], 0);
            }

            // Barrel
            {
                Quaternion TargetRot = Quaternion.LookRotation(TargetDir, Barrel.right);
                TargetRot = Quaternion.Inverse(Turret.parent.rotation) * TargetRot;
                float TargetAngle = TargetRot.eulerAngles.x;
                Pitch[i] = TargetAngle;

                // The pitch has a limit.
                if (LimitRotation(Pitch[i], MinPitch, MaxPitch, out float NewPitch))
                {
                    Pitch[i] = NewPitch;
                    PitchVelocities[i] = 0; // Reset velocity.
                }

                Barrel.localEulerAngles = new Vector3(Pitch[i], 0, 0);
            }

            //Vector3 LocalTargetDir = Turret.InverseTransformDirection(TargetDir);
            /*Vector3 LocalTargetDir = Turret.worldToLocalMatrix.MultiplyVector(TargetDir);
            LocalTargetDir.y = 0;
            float TargetAngle = Mathf.Atan2(LocalTargetDir.x, LocalTargetDir.z) * Mathf.Rad2Deg;
            float AngleDelta = TargetAngle - Turret.localEulerAngles.y;
            float Sign = Mathf.Sign(AngleDelta);*/
            //Debug.LogFormat("Desigred Angle: {0}, Angle Delta: {1}", TargetAngle, AngleDelta);

            // Accelerate
            // Test: just set the angle to see if it works:

            //TurnVelocities[i] += Mathf.Min(MaxRotationSpeed, TurnAcceleration * dt) * Sign;
            //PitchVelocities[i] += Mathf.Min(MaxRotationSpeed, TurnAcceleration * dt);

            // Limit Yaw
            //if(Yaw)
            // Apply yaw.
            //Turret.eulerAngles = new Vector3(0, Yaw[i], 0);

            // Apply velocity. 
            //Turret.Rotate(Turret.up, TurnVelocities[i]);
            Pitch[i] += PitchVelocities[i];

            // Play audio
            Turret.GetComponent<AudioSource>().volume = Mathf.Clamp(YawVelocities[i] * 0.5f, 0.0f, 1.0f);
        }

        RenderGameObjects();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector3 AimPosition = CameraOrbit.MouseAimPosition;
        // This should prob go somewhere else, but for now its here:
        if (Input.GetMouseButton(0))
        {
            for (int i = 0; i < nTurrets; i++)
            {
                // If we can fire
                if (ReloadTimeRemaining[i] == 0)
                {
                    var Barrel = Turrets[i].transform.GetChild(0);

                    // Determine if we're close enough to actually shoot and have a chance of hitting the target.
                    Vector3 TargetDir = Vector3.Normalize(AimPosition - Barrel.position);
                    if (Vector3.Dot(TargetDir, Barrel.forward) >= 1.0f - AngleDotTolerance)
                    {
                        Vector3 Velocity = Barrel.forward * BulletSpeed;

                        // Add ship's velocity.
                        Transform Ship = Turrets[i].transform.parent.parent;

                        if (int.TryParse(Ship.name, out int idx))
                        {
                            //Vector3 ShipVelocity = ShipSystem.Instance.Velocities[idx];
                            Vector3 ShipVelocity = ShipManager.Instance.Ships[idx].GetComponent<Rigidbody>().velocity;
                            Velocity += ShipVelocity;
                            //Debug.LogFormat("Spawning bullet with velocity: {0} (Added {1})", Velocity, ShipVelocity);

                            // Make it spawn a little higher just so we can see it better.
                            if (BulletSystem.TryAddBullet(Barrel.position + Barrel.forward * 2.0f, Velocity))
                            {
                                // Create Muzzle Flash
                                var go = Instantiate(MuzzleFlashPrefab, Barrel);
                            }
                            // Add some randomness to simulate not all machinery/soldiers working at the same exact speed.
                            ReloadTimeRemaining[i] = ReloadSpeed * Random.Range(1.24f, 1.0f);
                        }
                        else Debug.LogErrorFormat("Couldn't parse ship's name: {0}", Ship.name);
                    }
                }
            }
        }
        // Reload
        for (int i = 0; i < nTurrets; i++)
        {
            if (ReloadTimeRemaining[i] > 0)
            {
                ReloadTimeRemaining[i] = Mathf.Max(0, ReloadTimeRemaining[i] - dt);
            }
        }
    }

    void RenderGameObjects()
    {
        // Use the pool of game objects instead. 
        for (int i = 0; i < nTurrets; i++)
        {
            Turrets[i].SetActive(true);
        }
        // Disable the rest
        for (int i = nTurrets; i < MaxTurrets - nTurrets; i++)
        {
            Turrets[i].SetActive(false);
        }
    }

    static bool LimitRotation(float angle, float minAngle, float maxAngle, out float NewAngle)
    {
        // Normalize all angles to [0, 360)
        angle = Mathf.Repeat(angle, 360f);
        minAngle = Mathf.Repeat(minAngle, 360f);
        maxAngle = Mathf.Repeat(maxAngle, 360f);

        // Check if angle is inside the min-max range
        bool inside = false;

        if (minAngle <= maxAngle)
        {
            inside = angle >= minAngle && angle <= maxAngle;
        }
        else // wraparound case (e.g., min=330, max=30)
        {
            inside = angle >= minAngle || angle <= maxAngle;
        }

        if (inside)
        {
            NewAngle = angle;
            return false;
        }

        // Compute distance to both bounds and return the closest
        float distToMin = Mathf.DeltaAngle(angle, minAngle);
        float distToMax = Mathf.DeltaAngle(angle, maxAngle);

        NewAngle = Mathf.Abs(distToMin) < Mathf.Abs(distToMax) ? minAngle : maxAngle;
        return true;
    }
}
