//using System;
using System.Collections;
using System.Collections.Generic;
using TDLN.CameraControllers;
using Unity.Collections;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

public class TurretSystem : MonoBehaviour
{
    public static TurretSystem Instance;

    [Header("Main")]
    public GameObject TurretPrefab;
    public int MaxTurrets = 200;

    [Header("Details")]
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

    public AudioClip[] TurretShotClips;

    int nTurrets;
    NativeArray<Turret> TurretData;

    // Every frame each turret gets their turn spotting
    public int CurrentTurretSpotting;
    public LayerMask Layer;

    struct Turret
    {
        // Physical properties
        public float YawVelocity;
        public float Yaw;
        public float PitchVelocity;
        public float Pitch;
        public float ReloadTimeRemaining;

        // AI stuff
        public team Team;
        public bool HasTarget;
        public Vector3 AimDirection;
        public SpottedEnemy SpottedEnemy;
    }

    // Turret transforms
    GameObject[] Turrets;


    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
        TurretData = new NativeArray<Turret>(MaxTurrets, Allocator.Persistent);
        CreateTurrets();

        /*var ExistingTurrets = FindObjectsByType<TurretPrefab>(FindObjectsSortMode.None);
        Debug.LogFormat("Replacing {0} existing turrets", ExistingTurrets.Length);
        foreach (var prefab in ExistingTurrets)
        {
            if (prefab.gameObject.activeSelf)
            {
                AddTurret(prefab.transform.parent, prefab.transform.position, prefab.transform.rotation);
                Destroy(prefab.gameObject);
            }
        }*/
    }

    private void OnDestroy()
    {
        TurretData.Dispose();
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

    public void AddTurret(Transform Parent, Vector3 Position, Quaternion Rotation, team Team)
    {
        if (nTurrets < MaxTurrets)
        {
            TurretData[nTurrets] = new Turret()
            {
                Team = Team,
            };
            Turrets[nTurrets].transform.parent = Parent;
            Turrets[nTurrets].transform.position = Position;
            Turrets[nTurrets].transform.rotation = Rotation;
            nTurrets++;
        }
        else Debug.LogError("Reached max turrets");
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        var TeamSystem = Teams.Instance;
        int nNoTargets = 0;
        //if (Input.GetMouseButton(1)) return;

        //Debug.LogFormat("Iterating {0} turrets.", nTurrets);
        for (int i = 0; i < nTurrets; i++)
        {
            var Turret = TurretData[i];
            var TurretGo = Turrets[i].transform;
            var Barrel = Turrets[i].transform.GetChild(0);
            var Enemies = TeamSystem.DataPerTeam[(int)Turret.Team];
            var Ship = GetShip(Turrets[i]);
            var CanParse = int.TryParse(Ship.name, out int ShipIdx);
            if (!CanParse) Debug.LogErrorFormat("Can't parse ship name: {0}", Ship.name);
            var IsPlayer = ShipIdx == 0;

            if (IsPlayer)
            {
                Turret.HasTarget = true;
                Turret.AimDirection = (CameraOrbit.MouseAimPosition - TurretGo.position).normalized;
            }
            else
            {
                // Aim so that the bullet will hit the ship if they continue at a linear trajectory. (lead the shot). 
                /*Turret.HasTarget = false;
                foreach (var Enemy in Enemies.SpottedEnemies.Values)
                {
                    //if (i > 24) Debug.Log("Enemy position: " + enemy.Position);
                    Turret.HasTarget = true;
                    var Velocity = Enemy.Velocity;

                    // Subtract ship's velocity.
                    Velocity -= ShipSystem.Instance.Ships[ShipIdx].GetComponent<Rigidbody>().velocity;

                    //else Debug.LogErrorFormat("Couldn't parse ship's name: {0}", Ship.name);

                    // Set Turret.AimDirection:
                    CalculateLeadShot(TurretGo.position, Enemy.Position, Velocity, BulletSpeed, out Turret.AimDirection);
                    break;
                }*/
                if(Turret.HasTarget)
                {
                    var Enemy = Turret.SpottedEnemy;

                    //else Debug.LogErrorFormat("Couldn't parse ship's name: {0}", Ship.name);
                    var ExtrapolatedPosition = Enemy.Position + Enemy.Velocity * (Enemy.Time - Time.time);

                    // Set Turret.AimDirection:
                    CalculateLeadShot(TurretGo.position, ExtrapolatedPosition, Enemy.Velocity, BulletSpeed, out Turret.AimDirection);
                }
            }

            if (Turret.HasTarget)
            {
                // Turret
                {
                    // We basically just need the direction for now
                    // Let's get the angle difference for the turret
                    Quaternion TargetRot = Quaternion.LookRotation(Turret.AimDirection, TurretGo.up);
                    TargetRot = Quaternion.Inverse(TurretGo.parent.rotation) * TargetRot;
                    float TargetAngle = TargetRot.eulerAngles.y;
                    Turret.Yaw = TargetAngle;

                    // The pitch has a limit.
                    if (LimitRotation(Turret.Yaw, MinYaw, MaxYaw, out float NewYaw))
                    {
                        Turret.Yaw = NewYaw;
                        Turret.YawVelocity = 0; // Reset velocity.
                    }
                    TurretGo.localEulerAngles = new Vector3(0, Turret.Yaw, 0);
                }

                // Barrel
                {
                    Quaternion TargetRot = Quaternion.LookRotation(Turret.AimDirection, Barrel.right);
                    TargetRot = Quaternion.Inverse(TurretGo.parent.rotation) * TargetRot;
                    float TargetAngle = TargetRot.eulerAngles.x;
                    Turret.Pitch = TargetAngle;

                    // The pitch has a limit.
                    if (LimitRotation(Turret.Pitch, MinPitch, MaxPitch, out float NewPitch))
                    {
                        Turret.Pitch = NewPitch;
                        Turret.PitchVelocity = 0; // Reset velocity.
                    }

                    Barrel.localEulerAngles = new Vector3(Turret.Pitch, 0, 0);
                }
            }
            else nNoTargets++;

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
            Turret.Pitch += Turret.PitchVelocity;
            TurretData[i] = Turret; // Set data back.

            // Play audio
            //TurretGo.GetComponent<AudioSource>().volume = Mathf.Clamp(Turret.YawVelocity * 0.5f, 0.0f, 1.0f);
        }
        if (nTurrets > 0)
        {
            //DoTurretSpotting(1, dt);
            DoTurretSpotting(CurrentTurretSpotting, dt);
            CurrentTurretSpotting = (CurrentTurretSpotting + 1) % nTurrets;
        }
        else Debug.LogError("No turrets");

        //if(nNoTargets > 0) Debug.LogFormat("{0} turrets had no target.", nNoTargets);

        RenderGameObjects();
    }

    Vector3 LineStart;
    Vector3 LineEnd;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(LineStart, LineEnd);
    }

    void DoTurretSpotting(int idx, float dt)
    {
        Debug.Log("Checking turret " + idx);
        var go = Turrets[idx];
        var Turret = TurretData[idx];
        Turret.HasTarget = false;
        var SpottingRange = 800.0f;
        var Colliders = new Collider[5]; // Can be cached.

        // Do a sphere cast
        // Spot enemies.
        int nSpotted = Physics.OverlapSphereNonAlloc(go.transform.position, SpottingRange, Colliders, Layer);
        if (nSpotted > 0)
        {
            //Debug.LogFormat("Spotted {0} ships", nSpotted);
            for (int i = 0; i < nSpotted; i++)
            {
                Collider SpottedEnemy = Colliders[i];

                if (SpottedEnemy.TryGetComponent(out Ship SpottedShip))
                {
                    // Make sure its not our team (this will also avoid targetting self).
                    var Team = SpottedShip.Team;
                    if (Team != Turret.Team && Team != team.Neutral)
                    {
                        // For now just target the first ship found.

                        // Check to see if we can actually see the ship
                        Vector3 Direction = (SpottedShip.transform.position - go.transform.position).normalized;
                        var Ray = new Ray(go.transform.position + Direction * 1.7f, Direction);
                        LineStart = Ray.origin;
                        LineEnd = SpottedShip.transform.position;
                        if (Physics.Raycast(Ray, out RaycastHit hit))
                        {
                            var HitTeam = hit.collider.GetComponent<Team>();
                            // If the blocking object is on our team or doesn't have a team.
                            if (HitTeam == null || HitTeam.value == Turret.Team)
                            {
                                // The collider that was hit wasn't the enemy, therefore something was blocking it.
                                Debug.Log("No ship found. Object hit: " + hit.collider.name, hit.collider);
                            }
                            else
                            {
                                Debug.Log("<color=white>Ship found!</color>");
                                // We have a clear line of sight to the enemy.
                                var Spotting = new SpottedEnemy()
                                {
                                    Time = Time.time,
                                    Position = SpottedEnemy.transform.position,
                                    // Make the velocity relative by subtracting the ship's velocity
                                    //Velocity -= ShipSystem.Instance.Ships[ShipIdx].GetComponent<Rigidbody>().velocity;
                                    Velocity = SpottedEnemy.GetComponent<Rigidbody>().velocity
                                };
                                Turret.SpottedEnemy = Spotting;
                                Turret.HasTarget = true;
                                break;
                            }
                        }
                    }
                }
                else Debug.LogError("Spotted enemy didn't have Ship component", SpottedEnemy);
            }
        }
        else Debug.Log("All ships were out of range.");

        TurretData[idx] = Turret; // Set back
    }

    // Written by AI so I have no idea if its right.
    public static bool CalculateLeadShot(
        Vector3 shooterPos,
        Vector3 targetPos,
        Vector3 targetVel,
        float projectileSpeed,
        out Vector3 leadDirection
    )
    {
        Vector3 toTarget = targetPos - shooterPos;

        // Quadratic equation coefficients a*t^2 + b*t + c = 0
        float a = Vector3.Dot(targetVel, targetVel) - projectileSpeed * projectileSpeed;
        float b = 2.0f * Vector3.Dot(toTarget, targetVel);
        float c = Vector3.Dot(toTarget, toTarget);

        // Discriminant
        float discriminant = b * b - 4 * a * c;

        // No valid solution
        if (discriminant < 0)
        {
            leadDirection = toTarget.normalized; // fallback: shoot directly
            return false;
        }

        // Choose the smallest positive time
        float sqrtDisc = Mathf.Sqrt(discriminant);
        float t1 = (-b + sqrtDisc) / (2 * a);
        float t2 = (-b - sqrtDisc) / (2 * a);
        float t = Mathf.Min(t1, t2);
        if (t < 0)
            t = Mathf.Max(t1, t2);
        if (t < 0)
        {
            leadDirection = toTarget.normalized;
            return false;
        }

        Vector3 interceptPoint = targetPos + targetVel * t;
        leadDirection = (interceptPoint - shooterPos).normalized;
        return true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < nTurrets; i++)
        {
            var Turret = TurretData[i];
            var Ship = GetShip(Turrets[i]);
            if (int.TryParse(Ship.name, out int idx))
            {
                // If its the player's turrets, don't fire until the player wants them to.
                if (idx == 0 && !Input.GetMouseButton(0)) continue;

                // If we can fire
                if (Turret.HasTarget && Turret.ReloadTimeRemaining == 0)
                {
                    var Barrel = Turrets[i].transform.GetChild(0);

                    // Determine if we're close enough to actually shoot and have a chance of hitting the target.
                    if (Vector3.Dot(Turret.AimDirection, Barrel.forward) >= 1.0f - AngleDotTolerance)
                    {
                        Vector3 Velocity = Barrel.forward * BulletSpeed;

                        // Add ship's velocity.
                        Velocity += ShipSystem.Instance.Ships[idx].GetComponent<Rigidbody>().velocity;

                        if (BulletSystem.TryAddBullet(Barrel.position + Barrel.forward * 2.0f, Velocity))
                        {
                            // Create Muzzle Flash
                            var go = Instantiate(MuzzleFlashPrefab, Barrel);
                            // Sfx
                            Turrets[i].GetComponent<AudioSource>().PlayOneShot(TurretShotClips[Random.Range(0, TurretShotClips.Length - 1)]);
                            Turrets[i].GetComponent<AudioSource>().pitch = Random.Range(0.8f, 1.1f);
                        }
                        // Add some randomness to simulate not all machinery/soldiers working at the same exact speed.
                        Turret.ReloadTimeRemaining = ReloadSpeed * Random.Range(1.24f, 1.0f);
                    }
                }
                TurretData[i] = Turret;
            }
            else Debug.LogErrorFormat("Couldn't parse ship's name: {0}", Ship.name);
        }

        // Reload
        for (int i = 0; i < nTurrets; i++)
        {
            var Turret = TurretData[i];
            if (Turret.ReloadTimeRemaining > 0)
            {
                Turret.ReloadTimeRemaining = Mathf.Max(0, Turret.ReloadTimeRemaining - dt);
            }
            TurretData[i] = Turret;
        }
    }

    GameObject GetShip(GameObject Turret) => Turret.transform.parent.parent.parent.gameObject;

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
