//using System;
using System.Collections;
using System.Collections.Generic;
using TDLN.CameraControllers;
using Unity.Collections;
using UnityEngine;

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
    public float MinPitch = 270.0f;
    public float MaxPitch = 360.0f;
    public float MinYaw = -90.0f;
    public float MaxYaw = 90.0f;
    public float AngleDotTolerance = 0.03f;
    public float TurretBlockRange = 50.0f;
    public float BarrelLength = 0.5f;
    public bool PlayerAutoAim = false;

    [Header("Sfx")]
    public AudioClip HeavyTurretShot;

    [System.Serializable]
    public struct TurretModel
    {
        public float BulletSpeed;
        public float ReloadSpeed;
        public Sound Sound;
    }

    public enum TurretType
    {
        HeavyTurret,
        LightTurret,
    }

    public TurretModel[] TurretModels;

    public int nTurrets;
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
        public TurretType Type;

        // AI stuff
        public team Team;
        public bool HasTarget;
        public Vector3 AimDirection;
        public SpottedEnemy SpottedEnemy;

        // Show X if view is blocked?
        public bool IsPlayer;
    }

    // Turret transforms
    GameObject[] TurretGos;


    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
        TurretData = new NativeArray<Turret>(MaxTurrets, Allocator.Persistent);
        TurretGos = new GameObject[MaxTurrets];

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

    public void AddTurret(Transform Parent, Vector3 Position, Quaternion Rotation, TurretType type, team Team, bool IsPlayer)
    {
        if (nTurrets < MaxTurrets)
        {
            TurretGos[nTurrets] = Instantiate(TurretPrefab);
            TurretData[nTurrets] = new Turret()
            {
                Type = type,
                Team = Team,
                IsPlayer = IsPlayer,
            };
            TurretGos[nTurrets].transform.parent = Parent;
            TurretGos[nTurrets].transform.position = Position;
            TurretGos[nTurrets].transform.rotation = Rotation;
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
        var NewTurretData = new NativeArray<Turret>(MaxTurrets, Allocator.Persistent);
        var NewTurretGos = new GameObject[MaxTurrets];
        int nNewTurrets = 0;
        var PlayerVelocity = Vector3.zero;
        if (ShipSystem.Instance.nShips > 0) PlayerVelocity = ShipSystem.Instance.Ships[0].GetComponent<Rigidbody>().velocity;

        if (nTurrets > 0)
        {
            //DoTurretSpotting(1, dt);
            DoTurretSpotting(CurrentTurretSpotting, dt);
            CurrentTurretSpotting = (CurrentTurretSpotting + 1) % nTurrets;
        }

        //Debug.LogFormat("Iterating {0} turrets.", nTurrets);
        for (int i = 0; i < nTurrets; i++)
        {
            var Turret = TurretData[i];
            var TurretGo = TurretGos[i];

            // If we destroyed it from some other system, skip (I know, a little messy. But for now we're just writing looser code).
            if (TurretGo == null)
                continue;

            var Barrel = TurretGos[i].transform.GetChild(0);
            var Enemies = TeamSystem.DataPerTeam[(int)Turret.Team];
            bool IsPlayer = Turret.IsPlayer;

            // If the turret is dead, skip.
            if (TurretGos[i].GetComponent<TurretComponent>().Skip)
            {
                Destroy(TurretGos[i]);
                continue;
            }

            if (!PlayerAutoAim && IsPlayer)
            {
                Turret.HasTarget = true;
                //Turret.AimDirection = (CameraOrbit.MouseAimPosition - TurretGo.transform.position).normalized;
                Turret.AimDirection = (CameraOrbit.MouseAimPosition - TurretGo.transform.position - PlayerVelocity).normalized; 
            }
            else
            if (Turret.HasTarget)
            {
                var Enemy = Turret.SpottedEnemy;
                var ExtrapolatedPosition = Enemy.Position - Enemy.Velocity * (Time.time - Enemy.Time);
                Vector3 TurretVelocity = Vector3.zero;
                if (ShipUtility.TryGetShip(TurretGo.transform, out Ship Ship))
                {
                    TurretVelocity = Ship.GetComponent<Rigidbody>().velocity;
                }
                // Set Turret.AimDirection:
                TurretUtility.CalculateLeadShot(
                    TurretGo.transform.position, ExtrapolatedPosition, Enemy.Velocity - TurretVelocity, TurretModels[(int)Turret.Type].BulletSpeed, out Turret.AimDirection);
            }

            if (Turret.HasTarget)
            {
                // Turret
                {
                    // We basically just need the direction for now
                    // Let's get the angle difference for the turret
                    Quaternion TargetRot = Quaternion.LookRotation(Turret.AimDirection, TurretGo.transform.up);
                    TargetRot = Quaternion.Inverse(TurretGo.transform.parent.rotation) * TargetRot;
                    float TargetAngle = TargetRot.eulerAngles.y;
                    Turret.Yaw = TargetAngle;

                    // The pitch has a limit.
                    if (TurretUtility.LimitRotation(Turret.Yaw, MinYaw, MaxYaw, out float NewYaw))
                    {
                        Turret.Yaw = NewYaw;
                        Turret.YawVelocity = 0; // Reset velocity.
                    }
                    TurretGo.transform.localEulerAngles = new Vector3(0, Turret.Yaw, 0);
                }

                // Barrel
                {
                    Quaternion TargetRot = Quaternion.LookRotation(Turret.AimDirection, Barrel.right);
                    TargetRot = Quaternion.Inverse(TurretGo.transform.parent.rotation) * TargetRot;
                    float TargetAngle = TargetRot.eulerAngles.x;
                    Turret.Pitch = TargetAngle;

                    // The pitch has a limit.
                    if (TurretUtility.LimitRotation(Turret.Pitch, MinPitch, MaxPitch, out float NewPitch))
                    {
                        Turret.Pitch = NewPitch;
                        Turret.PitchVelocity = 0; // Reset velocity.
                    }

                    Barrel.localEulerAngles = new Vector3(Turret.Pitch, 0, 0);
                }
            }
            else nNoTargets++;

            // Find out if the view is blocked
            if (IsPlayer)
            {
                if (Physics.Raycast(TurretGo.transform.position, TurretGo.transform.forward, out RaycastHit Hit, TurretBlockRange))
                {
                    if (Hit.collider.GetComponent<Team>() == null)
                    {
                        BillboardRenderer.Instance.AddSprite(Hit.point + Hit.normal * 0.5f, BillboardRenderer.SpriteId.WhiteX);
                    }
                }
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
            Turret.Pitch += Turret.PitchVelocity;
            TurretData[i] = Turret; // Set data back.

            // Add new turret
            NewTurretData[nNewTurrets] = TurretData[i];
            NewTurretGos[nNewTurrets] = TurretGos[i];
            nNewTurrets++;
        }

        var TempTurretData = TurretData;
        var TempTurretGos = TurretGos;
        TurretData = NewTurretData;
        TurretGos = NewTurretGos;
        nTurrets = nNewTurrets;
        // Dispose old.
        TempTurretData.Dispose();

        if (nTurrets < nNewTurrets) Debug.LogFormat("Destroyed {0} turrets", nTurrets - nNewTurrets);

        //if (nNoTargets > 0) Debug.Log("Turrets with no target: " + nNoTargets);

        //RenderTurrets();
        // Todo: get an average of the player's turret's velocity, and set the sound to that (basically only play the sound for the player's ship, for efficiency reasons).
        // Play audio
        //TurretGo.GetComponent<AudioSource>().volume = Mathf.Clamp(Turret.YawVelocity * 0.5f, 0.0f, 1.0f);
    }

    Vector3 LineStart;
    Vector3 LineEnd;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(LineStart, LineEnd);
    }

    private void Update()
    {
        float dt = Time.deltaTime;    
        for (int i = 0; i < nTurrets; i++)
        {
            var Turret = TurretData[i];
            if (TurretGos[i] != null && ShipUtility.TryGetShip(TurretGos[i].transform, out Ship ParentShip))
            {
                if (int.TryParse(ParentShip.name, out int idx))
                {
                    // If its the player's turrets, don't fire until the player wants them to.
                    if (!PlayerAutoAim && Turret.IsPlayer && !Input.GetMouseButton(0)) continue;

                    // If we can fire
                    if (Turret.HasTarget && Turret.ReloadTimeRemaining == 0)
                    //if (Turret.ReloadTimeRemaining == 0)
                    {
                        var Barrel = TurretGos[i].transform.GetChild(0);

                        // Determine if we're close enough to actually shoot and have a chance of hitting the target.
                        if (Vector3.Dot(Turret.AimDirection, Barrel.forward) >= 1.0f - AngleDotTolerance)
                        {
                            TurretType type = Turret.Type;
                            TurretModel Model = TurretModels[(int)type];

                            Vector3 Velocity = Barrel.forward * Model.BulletSpeed;

                            // Add ship's velocity.
                            Velocity += ShipSystem.Instance.Ships[idx].GetComponent<Rigidbody>().velocity;

                            Vector3 SpawnPosition = Barrel.position + Barrel.forward * BarrelLength;
                            if (BulletSystem.TryAddBullet(SpawnPosition, Velocity, (BulletSystem.BulletType)type, Turret.IsPlayer))
                            {
                                // Create Muzzle Flash (Set parent to parent of turret so that it doesn't inherit rotation, looks a little better but obviously not 100% accurate).
                                if(type == TurretType.HeavyTurret) Instantiate(MuzzleFlashPrefab, TurretGos[i].transform.parent);
                                // Sfx


                                // Test
                                if (type == TurretType.LightTurret)
                                {
                                    SoundSystem.Instance.PlaySustained(1.0f, TurretGos[i].transform.position);
                                }
                                else
                                {
                                    //SoundSystem.Instance.PlaySound(Model.Sound, TurretGos[i].transform.position, 1.0f);
                                    TurretGos[i].GetComponent<AudioSource>().clip = HeavyTurretShot;
                                    //TurretGos[i].GetComponent<AudioSource>().PlayOneShot(HeavyTurretShot); //TurretShotClips[Random.Range(0, TurretShotClips.Length - 1)]);
                                    TurretGos[i].GetComponent<AudioSource>().pitch = Random.Range(0.8f, 1.1f);
                                    TurretGos[i].GetComponent<AudioSource>().Play();
                                }

                            }

                            // Add some randomness to simulate not all machinery/soldiers working at the same exact speed.
                            Turret.ReloadTimeRemaining = TurretModels[(int)type].ReloadSpeed; //* Random.Range(1.1f, 1.0f);
                        }
                    }
                    TurretData[i] = Turret;
                }
                //else Debug.LogErrorFormat("Couldn't parse ship's name: {0}", Ship.name);
            }
            //else Debug.LogError("Couldn't get ship");
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

    bool DoTurretSpotting(int idx, float dt)
    {
        var TurretGo = TurretGos[idx];
        var Turret = TurretData[idx];
        Turret.HasTarget = false;
        var SpottingRange = 1000.0f;
        var Colliders = new Collider[5]; // Can be cached.
        bool IsNPC = false; // ShipUtility.TryGetShip(TurretGo.transform, out Ship ParentShip) && ParentShip.name != "0";

        if (TurretGo == null || TurretGo.GetComponent<Armor>().Health <= 0)
        {
            //Debug.LogFormat("Turret {0} was null", idx);
            return false;
        }

        // Do a sphere cast
        // Spot enemies.
        int nSpotted = Physics.OverlapSphereNonAlloc(TurretGo.transform.position, SpottingRange, Colliders, Layer);
        if (nSpotted > 0)
        {
            //Debug.LogFormat("Spotted {0} ships", nSpotted);
            for (int i = 0; i < nSpotted; i++)
            {
                Collider SpottedShip = Colliders[i];

                if (SpottedShip.TryGetComponent(out Team Team))
                {
                    // Make sure its not our team (this will also avoid targetting self).
                    if (Team.value != Turret.Team && Team.value != team.Neutral)
                    {
                        //Debug.Log("Found ship on the enemy team: " + SpottedEnemy.name, SpottedEnemy);
                        // For now just target the first ship found.

                        // Check to see if we can actually see the ship
                        Vector3 Direction = (SpottedShip.transform.position - TurretGo.transform.position).normalized;
                        var Ray = new Ray(TurretGo.transform.position + Direction * 1.7f, Direction);
                        LineStart = Ray.origin;
                        LineEnd = SpottedShip.transform.position;
                        if (Physics.Raycast(Ray, out RaycastHit hit))
                        {
                            var HitTeam = hit.collider.GetComponent<Team>();
                            // If the blocking object is on our team or doesn't have a team.
                            if (HitTeam == null || HitTeam.value == Turret.Team)
                            {
                                // The collider that was hit wasn't the enemy, therefore something was blocking it.
                                if (IsNPC) Debug.Log("Raycast towards enemy didn't hit enemy (No line of sight). Instead it hit: " + hit.collider.name, hit.collider);
                            }
                            else
                            {
                                if (IsNPC) Debug.Log("Target acquired: " + SpottedShip.name, SpottedShip);
                                // We have a clear line of sight to the enemy.
                                var Spotting = new SpottedEnemy()
                                {
                                    Time = Time.time,
                                    Position = SpottedShip.transform.position,
                                    // Make the velocity relative by subtracting the ship's velocity
                                    //Velocity -= ShipSystem.Instance.Ships[ShipIdx].GetComponent<Rigidbody>().velocity;
                                    Velocity = SpottedShip.GetComponent<Rigidbody>().velocity
                                };
                                Turret.SpottedEnemy = Spotting;
                                Turret.HasTarget = true;
                                break;
                            }
                        }
                        else if (IsNPC) Debug.LogError("Raycast towards enemy didn't hit anything.");
                    }
                    else if (IsNPC) Debug.Log("Ship was on team: " + Team.value, TurretGo);
                }
                else if (IsNPC) Debug.Log("Spotted enemy has no team component");
            }
        }
        else if (IsNPC) Debug.Log("All ships were out of range.");

        TurretData[idx] = Turret; // Set back
        return Turret.HasTarget;
    }
}
