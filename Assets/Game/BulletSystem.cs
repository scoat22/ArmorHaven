using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TDLN.CameraControllers;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using static Unity.Mathematics.math;

public enum BulletType
{
    Large,
    Small,
    SmallTracer,
}

public class BulletSystem : MonoBehaviour
{
    public static BulletSystem Instance;

    public GameObject ImpactPrefab;
    public GameObject SparksPrefab;

    [SerializeField] int _MaxBullets;
    public const int MaxBullets = 1024;
    static int nBullets;
    static NativeArray<ShooterInfo> Shooters;
    static NativeArray<float3> Positions;  // Position per bullet
    static NativeArray<float3> LastPositions;
    static NativeArray<float3> Velocities; // Velocity per bullet
    static NativeArray<BulletInfo> BulletData; // Basically denotes size and color.

    public AudioClip HitNoise;
    AudioSource _AudioSource; // Cache AudioSource.

    Mesh _Mesh; // lines mesh (rendering all bullets)
    Mesh _SmokeTrailMesh;
    MeshRenderer _MeshRenderer;
    MeshRenderer _SmokeTrailMeshRenderer;
    ComputeBuffer CurveBuffer;
    public Material SmokeTrailMaterial;
    public float BulletDamage = 0.5f;
    public bool SpawnVfxParticles = true;
    [Range(0f, 1f)]
    public float SmokeCurveFactor = 0.7f;
    public bool IncludeObserverInSmoke = false;
    int nSegments = 16;
    static float LastFixedTime; // For rendering bullets (interpolate position between physics updates)

    public NativeArray<float3> _Positions;
    public NativeArray<float3> _Normals;
    public NativeArray<float> _Types;
    public NativeArray<float3> _SmokePositions;
    public NativeArray<float4> _SmokeNormals;
    public NativeArray<float> _UVs;
    JobHandle VerticesJob;

    [Header("Ricochet")]
    public AudioClip[] RicochetNoises;
    public float RicochetDotAngleTolerance = -0.85f;
    public float RicochetEnergyLoss = 0.5f;
    public Transform BoundingBox;

    public List<BulletTypeInfo> BulletTypes = new List<BulletTypeInfo>();
    [System.Serializable, StructLayout(LayoutKind.Sequential)]
    public struct BulletTypeInfo
    {
        public Color Color;
        public float Size;
        public float Mass;
    }

    // Used for smoke trail rendering
    public struct ShooterInfo
    {
        public float TimeFired;
        public float3 Position;
        public float3 Velocity;
    }

    public struct BulletInfo
    {
        public int Type;
        public float Size; // Size of the round
        public bool IsPlayer; // Did the player fire this bullet?
    }

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
        nBullets = 0;
        _AudioSource = GetComponent<AudioSource>();
        Shooters = new NativeArray<ShooterInfo>(MaxBullets, Allocator.Persistent);
        Positions = new NativeArray<float3>(MaxBullets, Allocator.Persistent);
        LastPositions = new NativeArray<float3>(MaxBullets, Allocator.Persistent);
        Velocities = new NativeArray<float3>(MaxBullets, Allocator.Persistent);
        BulletData = new NativeArray<BulletInfo>(MaxBullets, Allocator.Persistent);
        _Mesh = MeshUtility.CreateLinesMesh(MaxBullets);
        _SmokeTrailMesh = MeshUtility.CreateLinesMesh(MaxBullets * nSegments);

        GetComponent<MeshFilter>().mesh = _Mesh;
        _MeshRenderer = GetComponent<MeshRenderer>();

        var go = new GameObject("SmokeTrail");
        go.transform.SetParent(transform);
        go.AddComponent<MeshFilter>().mesh = _SmokeTrailMesh;
        _SmokeTrailMeshRenderer = go.AddComponent<MeshRenderer>();
        _SmokeTrailMeshRenderer.material = SmokeTrailMaterial;
        _SmokeTrailMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        /*var buffer = new ComputeBuffer(BulletTypes.Count, sizeof(float) * 5);
        buffer.SetData(BulletTypes);
        GetComponent<MeshRenderer>().material.SetBuffer("TracerTypes", buffer);*/
        //AmmoTypesBuffer.Dispose();

        var options = NativeArrayOptions.ClearMemory;
        var alloc = Allocator.Persistent;
        _Positions = new NativeArray<float3>(MaxBullets * 2, alloc, options);
        _Normals = new NativeArray<float3>(MaxBullets * 2, alloc, options);
        _Types = new NativeArray<float>(MaxBullets * 2, alloc, options);
        _SmokePositions = new NativeArray<float3>(MaxBullets * 2, alloc, options);
        _SmokeNormals = new NativeArray<float4>(MaxBullets * 2, alloc, options);
        _UVs = new NativeArray<float>(MaxBullets * 2, alloc, options);
    }

    void OnDestroy()
    {
        Shooters.Dispose();
        Positions.Dispose();
        Velocities.Dispose();
        BulletData.Dispose();
        CurveBuffer?.Dispose();
        CurveBuffer = null;

        _Positions.Dispose();
        _Normals.Dispose();
        _Types.Dispose();
        _SmokePositions.Dispose();
        _SmokeNormals.Dispose();
        _UVs.Dispose();
    }

    // Type: Changes the look of the projectile.
    public static bool TryAddBullet(Vector3 Position, Vector3 Velocity, Vector3 ShooterVelocity, BulletType Type = BulletType.Large, bool IsPlayer = false)
    {
        if (nBullets < MaxBullets)
        {
            Velocities[nBullets] = ShooterVelocity + Velocity;

            LastPositions[nBullets] = Position;
            Positions[nBullets] = Position;
            //Positions[nBullets] = Position + Velocity * Time.fixedDeltaTime;

            /*float TimeSinceFixedUpdate = abs(Time.time - LastFixedTime);
            //Vector3 Offset = Velocity * TimeSinceFixedUpdate;
            Vector3 Offset = Velocities[nBullets] * TimeSinceFixedUpdate;
            LastPositions[nBullets] = Position + Offset;
            Positions[nBullets] = Position + Offset;*/

            BulletData[nBullets] = new BulletInfo()
            {
                Type = (int)Type,
                IsPlayer = IsPlayer
            };
            Shooters[nBullets] = new ShooterInfo()
            {
                TimeFired = Time.time,
                //Position = Position - ShooterVelocity * TimeSinceFixedUpdate,
                Position = Position,
                Velocity = ShooterVelocity
            };
            nBullets++;

            // Add smoke
            //RaymarchGeneric.AddSmoke(Position, 1.0f);
            return true;
        }
        else Debug.LogWarningFormat("MaxBullets {0} reached.", MaxBullets);
        return false;
    }

    void Update()
    {
        RenderBulletsWithSmokeTrails(Time.time);

        // Test
        //float Range = SmokeRes;
        //AddSmoke(new Vector3(Random.Range(0, Range), Random.Range(0, Range), Random.Range(0, Range)), 1.0f);
        //RaymarchGeneric.AddSmoke(new Vector3(0, y, 0), 1.0f);
        _MeshRenderer.material.SetVector("_ObserverPosition", ShipUtility.TryGetPlayer(out Ship PlayerShip) ? PlayerShip.transform.position : Camera.main.transform.forward);
    }

    private void LateUpdate()
    {
        VerticesJob.Complete();

        var Flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
        var descriptors = new VertexAttributeDescriptor[3];
        descriptors[0] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.Position,
            format = VertexAttributeFormat.Float32,
            dimension = 3,
            stream = 0,

        };
        descriptors[1] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.Normal,
            format = VertexAttributeFormat.Float32,
            dimension = 3,
            stream = 1,

        };
        descriptors[2] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.TexCoord0,
            format = VertexAttributeFormat.Float32,
            dimension = 1,
            stream = 2,
        };

        _Mesh.SetVertexBufferParams(_Positions.Length, descriptors);
        _Mesh.SetVertexBufferData(_Positions, 0, 0, _Positions.Length, stream: 0, Flags);
        _Mesh.SetVertexBufferData(_Normals, 0, 0, _Normals.Length, stream: 1, Flags);
        _Mesh.SetVertexBufferData(_Types, 0, 0, _Types.Length, stream: 2, Flags);

        descriptors = new VertexAttributeDescriptor[4];
        descriptors[0] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.Position,
            format = VertexAttributeFormat.Float32,
            dimension = 3,
            stream = 0,

        };
        descriptors[1] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.Normal,
            format = VertexAttributeFormat.Float32,
            dimension = 4,
            stream = 1,

        };
        descriptors[2] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.TexCoord0,
            format = VertexAttributeFormat.Float32,
            dimension = 1,
            stream = 2,
        };

        descriptors[3] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.TexCoord1,
            format = VertexAttributeFormat.Float32,
            dimension = 1,
            stream = 3,
        };
        _SmokeTrailMesh.SetVertexBufferParams(_SmokePositions.Length, descriptors);
        _SmokeTrailMesh.SetVertexBufferData(_SmokePositions, 0, 0, _SmokePositions.Length, stream: 0, Flags);
        _SmokeTrailMesh.SetVertexBufferData(_SmokeNormals, 0, 0, _SmokeNormals.Length, stream: 1, Flags);
        _SmokeTrailMesh.SetVertexBufferData(_Types, 0, 0, _Types.Length, stream: 2, Flags);
        _SmokeTrailMesh.SetVertexBufferData(_UVs, 0, 0, _UVs.Length, stream: 3, Flags);
    }

    // Raycast
    private void FixedUpdate()
    {
        if (!Input.GetKey(KeyCode.T)) LastFixedTime = Time.time;
        float dt = Time.fixedDeltaTime;
        int nNewBullets = 0;
        var options = NativeArrayOptions.UninitializedMemory;
        var NewShooters = new NativeArray<ShooterInfo>(MaxBullets, Allocator.Persistent, options);
        var NewPositions = new NativeArray<float3>(MaxBullets, Allocator.Persistent, options);
        var NewLastPositions = new NativeArray<float3>(MaxBullets, Allocator.Persistent, options);
        var NewVelocities = new NativeArray<float3>(MaxBullets, Allocator.Persistent, options);
        var NewBulletData = new NativeArray<BulletInfo>(MaxBullets, Allocator.Persistent, options);
        var Hits = new RaycastHit[5];

        if (dt > 0)
        {
            new UpdateBulletsJob()
            {
                LastPositions = LastPositions,
                Positions = Positions,
                Velocities = Velocities,
                Shooters = Shooters,
                dt = dt,
            }
            .Schedule(nBullets, 16)
            .Complete();
        }

        for (int i = 0; i < nBullets; i++)
        {
            var p0 = LastPositions[i];
            var p1 = Positions[i];
            var Ray = new Ray(p0, p1 - p0);
            float Distance = length(p1 - p0);
            if (Physics.Raycast(Ray, out var hit, Distance))
                OnBulletHit(Ray, i, hit);
        }

        for (int i = 0; i < nBullets; i++)
        {
            // If it hits a ship, do damage. (Audio-Visual reward)
            // Multiply raycast length by two to avoid going through walls (kinda janky).
            var p0 = LastPositions[i];
            var p1 = Positions[i];
            var Ray = new Ray(p0, p1 - p0);
            float Distance = length(p1 - p0);
            /*int nHits = Physics.RaycastNonAlloc(Ray, Hits, Distance);
            if (nHits > 0)
            {
                // Sort based on distance
                Array.Sort(Hits, new HitSorter());
                for (int j = 0; j < nHits; j++)
                    OnBulletHit(Ray, i, Hits[j]);
            }*/
            /*if(Physics.Raycast(Ray, out var hit, Distance))
                OnBulletHit(Ray, i, hit);*/

            // If we're at max bullets, then for the 15% oldest bullets, destroy them if: 
            if (nBullets == MaxBullets && i <= MaxBullets / 8)
            {
                // If it stopped (hit something) or isn't destined to hit anything.
                if (IsZero(Velocities[i]) || !Physics.Raycast(Ray, out var hit2, 1000f))
                {
                    continue;
                }
            }

            // If it gets too far away, delete (Skip).
            if (OutOfRange(Positions[i]))
                continue;

            // Copy the old bullet to the new array.
            NewShooters[nNewBullets] = Shooters[i];
            NewPositions[nNewBullets] = Positions[i];
            NewLastPositions[nNewBullets] = LastPositions[i];
            NewVelocities[nNewBullets] = Velocities[i];
            NewBulletData[nNewBullets] = BulletData[i];
            nNewBullets++;
        }

        // Swap pointer.
        var TempStartPositions = Shooters;
        var TempPositions = Positions;
        var TempLastPositions = LastPositions;
        var TempVelocities = Velocities;
        var TempBulletData = BulletData;
        Shooters = NewShooters;
        Positions = NewPositions;
        LastPositions = NewLastPositions;
        Velocities = NewVelocities;
        BulletData = NewBulletData;
        nBullets = nNewBullets;
        // Dispose old
        TempStartPositions.Dispose();
        TempPositions.Dispose();
        TempLastPositions.Dispose();
        TempVelocities.Dispose();
        TempBulletData.Dispose();

        if(Input.GetKey(KeyCode.T)) LastFixedTime = Time.time;
    }

    struct HitSorter : IComparer<RaycastHit>
    {
        public int Compare(RaycastHit x, RaycastHit y)
        {
            // Sort smallest to largest.
            return y.distance.CompareTo(x.distance);
            //return x.distance.CompareTo(y.distance);
        }
    }

    // Returns whether the bullet was stopped or not.
    void OnBulletHit(Ray ray, int idx, RaycastHit hit)
    {
        float TracerLength = length(Positions[idx] - LastPositions[idx]);
        float PercentHit = hit.distance / TracerLength; // How far along the ray did the hit occur?

        if (dot(normalize(Velocities[idx]), hit.normal) < RicochetDotAngleTolerance)
        {
            // Simplified damage model.
            if (ShipUtility.TryGetShip(hit.collider.transform, out Ship Ship))
            {
                var Bullet = BulletData[idx];
                if (Ship.Health > 0)
                {
                    Ship.Health = max(0, Ship.Health - BulletDamage);
                }
                if (Bullet.IsPlayer)
                {
                    int Points = 10;
                    if (Ship.Health == 0) Points = 120; // If it was a killing blow, add more points;
                    PlayerController.Instance.AddPoints(Points);
                    //CameraOrbit.Instance.ShowHitmarker();
                }

                // Stop the bullet in its tracks. 
                Positions[idx] = hit.point;
                Velocities[idx] = Vector3.zero;

                /*if (TracerLength > 1f)
                {
                    // Test: create three new bullets with random direction along negative hit normal
                    int nSplits = 3;
                    float EnergyLoss = 0.75f;
                    for (int i = 0; i < nSplits; i++)
                    {
                        float3 Direction = normalize(Velocities[idx] + float3(TurretUtility.RandomVector(0.5f)));
                        float3 Velocity = TracerLength * EnergyLoss / (float)nSplits;
                        TryAddBullet(hit.point, Velocity, Ship.GetComponent<Rigidbody>().velocity);
                    }
                }*/
            }
            if (false)
            {
                if (hit.collider.TryGetComponent(out Armor Armor))
                {
                    var Bullet = BulletData[idx];

                    if (Armor.Health > 0)
                    {
                        // Do more damage if the bullet is heavier.
                        //float Energy = BulletTypes[Bullet.Type].Mass * Velocities[idx].magnitude;
                        //float Damage = Bullet.Type == 0 ? 0.1f : 0.002f;
                        float Damage = 0.5f;
                        Armor.Health = Mathf.Max(0, Armor.Health - Damage);
                    }

                    //Debug.LogFormat("Hit a ship. New health: {0}", Ship.Health);
                    if (Bullet.IsPlayer)
                    {
                        int Points = 10;
                        // If it was a killing blow, add more points;
                        //if (Armor.Health == 0) Points = 120;

                        PlayerController.Instance.AddPoints(Points);
                    }
                }
            }
            //else Debug.Log("Collider didnt have armor", hit.collider);
            if (SpawnVfxParticles) Instantiate(ImpactPrefab, hit.point, Quaternion.LookRotation(hit.normal));  // Vfx / Sfx (Audio is attached to prefab).
        }
        else
        {
            // Ricochet.
            Velocities[idx] = reflect(Velocities[idx], hit.normal) * RicochetEnergyLoss;
            // For now just visually put the LastPosition at the hit point (later we can do elbow vertexes during one frame).
            LastPositions[idx] = hit.point;
            // Calculate how far along the reflected vector the bullet would have travelled during this frame.
            
            Positions[idx] = float3(hit.point) + normalize(Velocities[idx]) * PercentHit;

            // Let's repair the trail.
            // For now just set the start/time of the trail
            var shooter = Shooters[idx];
            shooter.Position = hit.point;
            shooter.Velocity = reflect(shooter.Velocity, hit.normal) * RicochetEnergyLoss;
            shooter.TimeFired = Time.time;
            Shooters[idx] = shooter;

            // If it has an armor component, play metal sound. Adjust clip/pitch based on thickness of the armor. If no armor component, play a more thud/dirt sound.
            //if(hit.collider.material.bounciness )
            //_AudioSource.PlayOneShot(RicochetNoises[Random.Range(0, RicochetNoises.Length - 1)]); // Sfx
            //SoundSystem.Instance.PlaySound(Sound.Ricochet, hit.point, 1.0f);

            //Instantiate(SparksPrefab, hit.point, Quaternion.LookRotation(hit.normal)); // Vfx
            if (SpawnVfxParticles) Instantiate(SparksPrefab, hit.point, Quaternion.LookRotation(hit.normal));
        }
    }

    float GetRayThicknessThroughPlane(Vector3 rayDirection, Vector3 planeNormal, float planeThickness)
    {
        float angleCos = Mathf.Abs(Vector3.Dot(rayDirection.normalized, planeNormal.normalized));
        return planeThickness / angleCos;
    }

    [BurstCompile]
    struct UpdateBulletsJob : IJobParallelFor
    {
        public NativeArray<float3> LastPositions;
        public NativeArray<float3> Positions;
        public NativeArray<float3> Velocities;
        public NativeArray<ShooterInfo> Shooters;
        public float dt;
        public void Execute(int i)
        {
            // Update bullets.
            LastPositions[i] = Positions[i];
            Positions[i] += Velocities[i] * dt;

            // Update smoke trails.
            var shooter = Shooters[i];
            shooter.Position += shooter.Velocity * dt;
            Shooters[i] = shooter;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CurveData
    {
        public float3 Start;
        public float3 Control1;
        public float3 Control2;
        public float3 End;
        public float TimeFired;
    }

    [BurstCompile]
    struct SetVerticesJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<float3> Positions;
        [NativeDisableParallelForRestriction] public NativeArray<float3> Normals;
        [NativeDisableParallelForRestriction] public NativeArray<float> Types;
        [NativeDisableParallelForRestriction] public NativeArray<float> UVs;
        [NativeDisableParallelForRestriction] public NativeArray<float3> SmokePositions;
        [NativeDisableParallelForRestriction] public NativeArray<float4> SmokeNormals;
        public int nBullets;
        public float3 CameraVelocity;
        public float SegmentLength;
        public float t;
        public float dt;
        public float FixedDt;
        public float time;
        public void Execute(int i)
        {
            int i0 = i * 2;
            int i1 = i * 2 + 1;
            float3 p0 = LastPositions[i];
            float3 p1 = BulletSystem.Positions[i];
            // Don't render if the length of the tracer is 0 (aka it just spawned).
            if (i < nBullets && distance(p0, p1) > 0.01f)
            {
                Positions[i0] = p0 + (p1 - p0) * t;
                Positions[i1] = p0 + (p1 - p0) * (t + SegmentLength) - CameraVelocity * dt;
                //Positions[i1] = p0 + (p1 - p0 - CameraVelocity * dt) * (t + SegmentLength);
                Normals[i0] = Positions[i1] - Positions[i0];
                Normals[i1] = Positions[i1] - Positions[i0];
                Types[i0] = BulletData[i].Type;
                Types[i1] = BulletData[i].Type;

                // Smoke trails
                SmokePositions[i0] = Shooters[i].Position + Shooters[i].Velocity * FixedDt * t - CameraVelocity * FixedDt;
                //SmokePositions[i0] = Shooters[i].Position + (Shooters[i].Velocity - CameraVelocity) * FixedDt * t;
                SmokePositions[i1] = Positions[i0];
                float3 SmokeNormal = SmokePositions[i1] - SmokePositions[i0];
                // Pack time into the W component.
                SmokeNormals[i0] = float4(SmokeNormal.xyz, Shooters[i].TimeFired);
                SmokeNormals[i1] = float4(SmokeNormal.xyz, time); // The final vertex is always "fresh" smoke.
                UVs[i0] = 0;
                UVs[i1] = 1;
            }
            else
            {
                Positions[i0] = float3(0);
                Positions[i1] = float3(0);
                SmokePositions[i0] = float3(0);
                SmokePositions[i1] = float3(0);
            }
        }
    }

    enum DeltaTimeTest { None, DeltaTime, FixedDeltaTime, Custom };
    DeltaTimeTest DeltaTimeType = DeltaTimeTest.Custom;

    void RenderBulletsWithSmokeTrails(float time)
    {
        //float dt = time - LastFixedTime;
        float dt = PlayerController.DeltaTime;
        float TimeSinceFixedUpdate = Time.time - LastFixedTime;
        float TimeToNextFixedUpdate = LastFixedTime + Time.fixedDeltaTime;
        float t = TimeSinceFixedUpdate / Time.fixedDeltaTime;
        //float SegmentLength = Time.deltaTime / Time.fixedDeltaTime; // The percentage length of this frame's tracer length (in between each FixedUpdate).
        float SegmentLength = dt / Time.fixedDeltaTime;
        float3 CameraVelocity = CameraOrbit.Instance.Velocity;

        // Testing.
        if (Input.GetKeyDown(KeyCode.Alpha4)) DeltaTimeType = DeltaTimeTest.None;
        if (Input.GetKeyDown(KeyCode.Alpha5)) DeltaTimeType = DeltaTimeTest.DeltaTime;
        if (Input.GetKeyDown(KeyCode.Alpha6)) DeltaTimeType = DeltaTimeTest.FixedDeltaTime;
        if (Input.GetKeyDown(KeyCode.Alpha7)) DeltaTimeType = DeltaTimeTest.Custom;

        switch (DeltaTimeType)
        {
            case DeltaTimeTest.None: t = 0;  dt = 0; break;
            case DeltaTimeTest.DeltaTime: dt = Time.deltaTime; break;
            case DeltaTimeTest.FixedDeltaTime: dt = Time.fixedDeltaTime; break;
            case DeltaTimeTest.Custom: dt = PlayerController.DeltaTime; break;
        }
        
        VerticesJob = new SetVerticesJob()
        {
            Positions = _Positions,
            Normals = _Normals,
            Types = _Types,
            UVs = _UVs,
            SmokePositions = _SmokePositions,
            SmokeNormals = _SmokeNormals,
            nBullets = nBullets,
            SegmentLength = SegmentLength,
            t = t,
            dt = dt,
            FixedDt = Time.fixedDeltaTime,
            time = Time.time,
            CameraVelocity = CameraVelocity,
        }
        .Schedule(MaxBullets, 8);
    }

    void RenderCurvedSmokeTrails(float dt)
    {
        // We could convert this a job very easily.
        float3 CameraVelocity = CameraOrbit.Instance.Velocity * dt;
        var Flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

        var options = NativeArrayOptions.UninitializedMemory;
        var Positions = new NativeArray<float3>(MaxBullets * 2, Allocator.Temp, options);
        var Normals = new NativeArray<float3>(MaxBullets * 2, Allocator.Temp, options);
        var Types = new NativeArray<float>(MaxBullets * 2, Allocator.Temp, options);
        var Curves = new NativeArray<CurveData>(MaxBullets, Allocator.Temp, options);
        var CurveIds = new NativeArray<float>(MaxBullets * nSegments * 2, Allocator.Temp, options); // The curve index (gets CurvData from buffer) (Mac doesn't support passing integers to shaders)
        var LocalIds = new NativeArray<float>(MaxBullets * nSegments * 2, Allocator.Temp, options); // The index along the local bezier curve (use to calculate our position in the curve).

        // If index is less than nBullets.
        for (int i = 0; i < nBullets; i++)
        {
            float3 Start = BulletSystem.Positions[i];
            float3 End = BulletSystem.Positions[i] + Velocities[i] * dt - CameraVelocity;
            int i0 = i * 2;
            int i1 = i0 + 1;

            // We're going to pass the first and last point, two control points, and the index number of each vertex (the "t" of the curve).
            float t = 0.00001f;
            float step = 1.0f / (float)(nSegments - 1); // We have to subtract one so we don't overshoot.
            int StartIdx = i * nSegments * 2;
            for (int SegmentId = 0; SegmentId < nSegments; SegmentId++)
            {
                int idx = StartIdx + SegmentId * 2;
                CurveIds[idx] = i;
                CurveIds[idx + 1] = i;
                LocalIds[idx] = t; t += step;
                LocalIds[idx + 1] = t;
                //Debug.LogFormat("[{0}] {1} -> {2}", i, LocalIds[idx], LocalIds[idx + 1]);
            }

            // The first control point needs to be opposite the shooter's velocity. And the second one needs to be opposite the bullet's velocity (do that later).
            float Magnitude = Vector3.Magnitude(End - Start);
            float TimePassed = Time.time - Shooters[i].TimeFired;
            var curve = new CurveData()
            {
                Start = Shooters[i].Position,
                End = Start,
                TimeFired = Shooters[i].TimeFired,
            };
            curve.Control1 = Shooters[i].Position - Shooters[i].Velocity * TimePassed * SmokeCurveFactor;
            if (IncludeObserverInSmoke)
            {
                curve.Control2 = Start - (Velocities[i] - CameraVelocity) * TimePassed * SmokeCurveFactor;
            }
            else
            {
                curve.Control2 = Start - Velocities[i] * TimePassed * SmokeCurveFactor;
            }
            //Control1 = Shooters[i].Position - Shooters[i].Velocity.normalized * Magnitude / 3.0f,
            //Control2 = Start - Velocities[i].normalized * Magnitude / 3.0f,
            //Control1 = Shooters[i].Position,  // Line Test
            //Control2 = End,                   // Line Test
            Curves[i] = curve;
        }
        // Set the rest to zero. (if index is higher than nBullets).
        for (int i = nBullets; i < MaxBullets; i++)
        {
            Positions[i * 2] = float3(0);
            Positions[i * 2 + 1] = float3(0);
            CurveIds[i * 2] = 0;
            CurveIds[i * 2 + 1] = 0;
            int StartIdx = i * nSegments * 2;
            for (int j = 0; j < nSegments * 2; j++)
            {
                LocalIds[StartIdx + j] = 0;
            }
        }

        var descriptors = new VertexAttributeDescriptor[2];
        descriptors[0] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.TexCoord0,
            format = VertexAttributeFormat.Float32,
            dimension = 1,
            stream = 0,

        };
        descriptors[1] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.TexCoord1,
            format = VertexAttributeFormat.Float32,
            dimension = 1,
            stream = 1,

        };

        _SmokeTrailMesh.SetVertexBufferParams(CurveIds.Length, descriptors);
        _SmokeTrailMesh.SetVertexBufferData(CurveIds, 0, 0, CurveIds.Length, stream: 0, Flags);
        _SmokeTrailMesh.SetVertexBufferData(LocalIds, 0, 0, LocalIds.Length, stream: 1, Flags);

        CurveBuffer?.Dispose();
        CurveBuffer = new ComputeBuffer(MaxBullets, sizeof(float) * 13);
        CurveBuffer.SetData(Curves);
        _SmokeTrailMeshRenderer.material.SetBuffer("_Curves", CurveBuffer);
        _SmokeTrailMeshRenderer.material.SetFloat("_nBullets", nBullets);

        CurveIds.Dispose();
        LocalIds.Dispose();
    }

    void RenderBullets(float dt)
    {
        // We could convert this a job very easily.
        float3 CameraVelocity = CameraOrbit.Instance.Velocity * dt;
        var Flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

        var options = NativeArrayOptions.UninitializedMemory;
        var Positions = new NativeArray<float3>(MaxBullets * 2, Allocator.Temp, options);
        var Normals = new NativeArray<float3>(MaxBullets * 2, Allocator.Temp, options);
        var Types = new NativeArray<float>(MaxBullets * 2, Allocator.Temp, options);
        var Curves = new NativeArray<CurveData>(MaxBullets, Allocator.Temp, options);
        var CurveIds = new NativeArray<float>(MaxBullets * nSegments * 2, Allocator.Temp, options); // The curve index (gets CurvData from buffer) (Mac doesn't support passing integers to shaders)
        var LocalIds = new NativeArray<float>(MaxBullets * nSegments * 2, Allocator.Temp, options); // The index along the local bezier curve (use to calculate our position in the curve).

        // If index is less than nBullets.
        for (int i = 0; i < nBullets; i++)
        {
            float3 Start = BulletSystem.Positions[i];
            float3 End = BulletSystem.Positions[i] + Velocities[i] * dt - CameraVelocity;
            int i0 = i * 2;
            int i1 = i0 + 1;
            Positions[i0] = Start;
            Positions[i1] = End;
            Normals[i0] = End - Start;
            Normals[i1] = End - Start;
            Types[i0] = BulletData[i].Type;
            Types[i1] = BulletData[i].Type;

            // We're going to pass the first and last point, two control points, and the index number of each vertex (the "t" of the curve).
            float t = 0.00001f;
            float step = 1.0f / (float)(nSegments - 1); // We have to subtract one so we don't overshoot.
            int StartIdx = i * nSegments * 2;
            for (int SegmentId = 0; SegmentId < nSegments; SegmentId++)
            {
                int idx = StartIdx + SegmentId * 2;
                CurveIds[idx] = i;
                CurveIds[idx + 1] = i;
                LocalIds[idx] = t; t += step;
                LocalIds[idx + 1] = t;
                //Debug.LogFormat("[{0}] {1} -> {2}", i, LocalIds[idx], LocalIds[idx + 1]);
            }

            // The first control point needs to be opposite the shooter's velocity. And the second one needs to be opposite the bullet's velocity (do that later).
            float Magnitude = Vector3.Magnitude(End - Start);
            float TimePassed = Time.time - Shooters[i].TimeFired;
            var curve = new CurveData()
            {
                Start = Shooters[i].Position,
                End = Start,
                TimeFired = Shooters[i].TimeFired,
            };
            curve.Control1 = Shooters[i].Position - Shooters[i].Velocity * TimePassed * SmokeCurveFactor;
            if (IncludeObserverInSmoke)
            {
                curve.Control2 = Start - (Velocities[i] - CameraVelocity) * TimePassed * SmokeCurveFactor;
            }
            else
            {
                curve.Control2 = Start - Velocities[i] * TimePassed * SmokeCurveFactor;
            }
            //Control1 = Shooters[i].Position - Shooters[i].Velocity.normalized * Magnitude / 3.0f,
            //Control2 = Start - Velocities[i].normalized * Magnitude / 3.0f,
            //Control1 = Shooters[i].Position,  // Line Test
            //Control2 = End,                   // Line Test
            Curves[i] = curve;
        }
        // Set the rest to zero. (if index is higher than nBullets).
        for (int i = nBullets; i < MaxBullets; i++)
        {
            Positions[i * 2] = float3(0);
            Positions[i * 2 + 1] = float3(0);
            CurveIds[i * 2] = 0;
            CurveIds[i * 2 + 1] = 0;
            int StartIdx = i * nSegments * 2;
            for (int j = 0; j < nSegments * 2; j++)
            {
                LocalIds[StartIdx + j] = 0;
            }
        }

        var descriptors = new VertexAttributeDescriptor[3];
        descriptors[0] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.Position,
            format = VertexAttributeFormat.Float32,
            dimension = 3,
            stream = 0,

        };
        descriptors[1] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.Normal,
            format = VertexAttributeFormat.Float32,
            dimension = 3,
            stream = 1,

        };
        descriptors[2] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.TexCoord0,
            format = VertexAttributeFormat.Float32,
            dimension = 1,
            stream = 2,
        };

        _Mesh.SetVertexBufferParams(Positions.Length, descriptors);
        _Mesh.SetVertexBufferData(Positions, 0, 0, Positions.Length, stream: 0, Flags);
        _Mesh.SetVertexBufferData(Normals, 0, 0, Normals.Length, stream: 1, Flags);
        _Mesh.SetVertexBufferData(Types, 0, 0, Types.Length, stream: 2, Flags);

        descriptors = new VertexAttributeDescriptor[2];
        descriptors[0] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.TexCoord0,
            format = VertexAttributeFormat.Float32,
            dimension = 1,
            stream = 0,

        };
        descriptors[1] = new VertexAttributeDescriptor()
        {
            attribute = VertexAttribute.TexCoord1,
            format = VertexAttributeFormat.Float32,
            dimension = 1,
            stream = 1,

        };

        _SmokeTrailMesh.SetVertexBufferParams(CurveIds.Length, descriptors);
        _SmokeTrailMesh.SetVertexBufferData(CurveIds, 0, 0, CurveIds.Length, stream: 0, Flags);
        _SmokeTrailMesh.SetVertexBufferData(LocalIds, 0, 0, LocalIds.Length, stream: 1, Flags);

        CurveBuffer?.Dispose();
        CurveBuffer = new ComputeBuffer(MaxBullets, sizeof(float) * 13);
        CurveBuffer.SetData(Curves);
        _SmokeTrailMeshRenderer.material.SetBuffer("_Curves", CurveBuffer);
        _SmokeTrailMeshRenderer.material.SetFloat("_nBullets", nBullets);

        Positions.Dispose();
        Normals.Dispose();
        Types.Dispose();
        CurveIds.Dispose();
        LocalIds.Dispose();
    }

    bool OutOfRange(float3 Position)
    {
        float3 Scale = BoundingBox.localScale;
        return abs(Position.x) > Scale.x || abs(Position.y) > Scale.y || abs(Position.z) > Scale.z;
    }

    bool IsZero(float3 v) => v.x == 0 && v.y == 0 && v.z == 0;

    public void Clear()
    {
        nBullets = 0;
    }
}
