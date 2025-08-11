using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TDLN.CameraControllers;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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
    public static int MaxBullets = 150;
    static int nBullets;
    static NativeArray<ShooterInfo> Shooters;
    static NativeArray<Vector3> Positions;  // Position per bullet
    static NativeArray<Vector3> Velocities; // Velocity per bullet
    static NativeArray<BulletInfo> BulletData; // Basically denotes size and color.

    public AudioClip HitNoise;
    AudioSource _AudioSource; // Cache AudioSource.

    Mesh _Mesh; // lines mesh (rendering all bullets)
    Mesh _SmokeTrailMesh;
    MeshRenderer _MeshRenderer;
    MeshRenderer _SmokeTrailMeshRenderer;
    public Material SmokeTrailMaterial;
    public float BulletDamage = 0.5f;
    public bool SpawnVfxParticles = true;
    public bool RenderSmokeTrails = true;

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
        public Vector3 Position;
        public Vector3 Velocity;
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
        MaxBullets = _MaxBullets;
        _AudioSource = GetComponent<AudioSource>();
        Shooters = new NativeArray<ShooterInfo>(MaxBullets, Allocator.Persistent);
        Positions = new NativeArray<Vector3>(MaxBullets, Allocator.Persistent);
        Velocities = new NativeArray<Vector3>(MaxBullets, Allocator.Persistent);
        BulletData = new NativeArray<BulletInfo>(MaxBullets, Allocator.Persistent);
        _Mesh = MeshUtility.CreateLinesMesh(MaxBullets);
        _SmokeTrailMesh = MeshUtility.CreateLinesMesh(MaxBullets);

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
    }

    void OnDestroy()
    {
        Shooters.Dispose();
        Positions.Dispose();
        Velocities.Dispose();
        BulletData.Dispose();
    }

    // Type: Basically only changes the look of the projectile.
    public static bool TryAddBullet(Vector3 Position, Vector3 Velocity, Vector3 ShooterVelocity, BulletType Type = BulletType.Large, bool IsPlayer = false)
    {
        if (nBullets < MaxBullets)
        {
            Shooters[nBullets] = new ShooterInfo()
            {
                TimeFired = Time.time,
                Position = Position,
                Velocity = ShooterVelocity
            };
            Positions[nBullets] = Position;
            Velocities[nBullets] = ShooterVelocity + Velocity;
            BulletData[nBullets] = new BulletInfo() { Type = (int)Type, IsPlayer = IsPlayer };
            nBullets++;

            // Add smoke
            //RaymarchGeneric.AddSmoke(Position, 1.0f);
            return true;
        }
        else Debug.LogWarningFormat("MaxBullets {0} reached.", MaxBullets);
        return false;
    }

    // Raycast
    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        int nNewBullets = 0;
        var options = NativeArrayOptions.UninitializedMemory;
        var NewShooters = new NativeArray<ShooterInfo>(MaxBullets, Allocator.Persistent, options);
        var NewPositions = new NativeArray<Vector3>(MaxBullets, Allocator.Persistent, options);
        var NewVelocities = new NativeArray<Vector3>(MaxBullets, Allocator.Persistent, options);
        var NewBulletData = new NativeArray<BulletInfo>(MaxBullets, Allocator.Persistent, options);

        for (int i = 0; i < nBullets; i++)
        {
            // If it hits a ship, do damage. (Audio-Visual reward)
            // Multiply raycast length by two to avoid going through walls (kinda janky).
            if (Physics.Raycast(Positions[i], Velocities[i], out RaycastHit hit, Velocities[i].magnitude * dt * 2.0f))
            {
                OnBulletHit(i, hit);
            }
            // Make a sound
            // Make an explosion

            // If we're at max bullets, then for the 15% oldest bullets, destroy them if they aren't destined to hit anything. 
            if (nBullets == MaxBullets && i <= MaxBullets / 8)
            {
                if (!Physics.Raycast(Positions[i], Velocities[i], out var hit2, 1000f))
                {
                    continue;
                }
            }

            // If it gets too far away, die (just don't copy to Next)
            if (OutOfRange(Positions[i]))
            {
                //Debug.Log("Bullet left the bounds and was destroyed");
                continue;
            }

            // Copy the old bullet to the new array.
            NewShooters[nNewBullets] = Shooters[i];
            NewPositions[nNewBullets] = Positions[i];
            NewVelocities[nNewBullets] = Velocities[i];
            NewBulletData[nNewBullets] = BulletData[i];
            nNewBullets++;
        }

        // Swap pointer.
        var TempStartPositions = Shooters;
        var TempPositions = Positions;
        var TempVelocities = Velocities;
        var TempBulletData = BulletData;
        Shooters = NewShooters;
        Positions = NewPositions;
        Velocities = NewVelocities;
        BulletData = NewBulletData;
        nBullets = nNewBullets;
        // Dispose old
        TempStartPositions.Dispose();
        TempPositions.Dispose();
        TempVelocities.Dispose();
        TempBulletData.Dispose();
    }

    void OnBulletHit(int idx, RaycastHit hit)
    {
        if (Vector3.Dot(Velocities[idx].normalized, hit.normal) < RicochetDotAngleTolerance)
        {
            // Simplified damage model.
            if (ShipUtility.TryGetShip(hit.collider.transform, out Ship Ship))
            {
                var Bullet = BulletData[idx];
                if (Ship.Health > 0)
                {
                    Ship.Health = Mathf.Max(0, Ship.Health - BulletDamage);
                }
                if (Bullet.IsPlayer)
                {
                    int Points = 10;
                    if (Ship.Health == 0) Points = 120; // If it was a killing blow, add more points;
                    PlayerController.Instance.AddPoints(Points);
                    //CameraOrbit.Instance.ShowHitmarker();
                }
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
            Vector3 original = Velocities[idx];
            // Ricochet.
            Positions[idx] = hit.point;
            Velocities[idx] = Vector3.Reflect(Velocities[idx], hit.normal) * RicochetEnergyLoss;

            // Let's repair the trail.
            // For now just set the start/time of the trail
            var shooter = Shooters[idx];
            shooter.Position = hit.point;
            shooter.Velocity = Vector3.Reflect(shooter.Velocity, hit.normal) * RicochetEnergyLoss;
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

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;
        if (Time.timeScale > 0)
        {
            RenderBullets(dt);
            UpdateSmokeTrails(dt);
            UpdateBullets(dt);

            // Test
            //float Range = SmokeRes;
            //AddSmoke(new Vector3(Random.Range(0, Range), Random.Range(0, Range), Random.Range(0, Range)), 1.0f);
            //RaymarchGeneric.AddSmoke(new Vector3(0, y, 0), 1.0f);

            if (ShipSystem.Instance.nShips > 0)
                _MeshRenderer.material.SetVector("_ObserverPosition", ShipSystem.Instance.Ships[0].transform.position);
            else
                _MeshRenderer.material.SetVector("_ObserverPosition", Camera.main.transform.forward);
        }
    }

    void UpdateBullets(float dt)
    {
        // Push bullets forward
        for (int i = 0; i < nBullets; i++)
        {
            Positions[i] += Velocities[i] * dt;
        }
    }

    void UpdateSmokeTrails(float dt)
    {
        for (int i = 0; i < nBullets; i++)
        {
            var shooter = Shooters[i];
            shooter.Position += shooter.Velocity * dt;
            Shooters[i] = shooter;
        }
    }

    void RenderBullets(float dt)
    {
        // We could convert this a job very easily.
        Vector3 CameraVelocity = CameraOrbit.Instance.Velocity * dt;
        var Flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

        var options = NativeArrayOptions.UninitializedMemory;
        int nVertex = 10;
        var Positions = new NativeArray<Vector3>(MaxBullets * nVertex, Allocator.Temp, options);
        var Normals = new NativeArray<Vector3>(MaxBullets * nVertex, Allocator.Temp, options);
        var Types = new NativeArray<float>(MaxBullets * nVertex, Allocator.Temp, options);
        var SmokePositions = new NativeArray<Vector3>(MaxBullets * nVertex, Allocator.Temp, options);
        var SmokeNormals = new NativeArray<Vector4>(MaxBullets * nVertex, Allocator.Temp, options);
        var UVs = new NativeArray<float>(MaxBullets * nVertex, Allocator.Temp, options);

        // If index is less than nBullets.
        for (int i = 0; i < nBullets; i++)
        {
            Vector3 p0 = BulletSystem.Positions[i];
            Vector3 p1 = BulletSystem.Positions[i] + Velocities[i] * dt - CameraVelocity;
            int i0 = i * 2;
            int i1 = i * 2 + 1;

            Positions[i0] = p0;
            Positions[i1] = p1;
            Normals[i0] = p1 - p0;
            Normals[i1] = p1 - p0;
            Types[i0] = BulletData[i].Type;
            Types[i1] = BulletData[i].Type;

            SmokePositions[i0] = Shooters[i].Position;
            SmokePositions[i1] = p0;
            Vector3 SmokeNormal = p0 - Shooters[i].Position;
            // Pack time into the W component.
            SmokeNormals[i0] = new Vector4(SmokeNormal.x, SmokeNormal.y, SmokeNormal.z, Shooters[i].TimeFired);
            SmokeNormals[i1] = new Vector4(SmokeNormal.x, SmokeNormal.y, SmokeNormal.z, Time.time); // The final vertex is always "fresh" smoke.
            UVs[i0] = 0;
            UVs[i1] = 1;
        }
        // Set the rest to zero. (if index is higher than nBullets).
        for (int i = nBullets * 2; i < MaxBullets * 2; i++)
        {
            Positions[i] = Vector4.zero;
            SmokePositions[i] = Vector3.zero;
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
        _SmokeTrailMesh.SetVertexBufferParams(SmokePositions.Length, descriptors);
        _SmokeTrailMesh.SetVertexBufferData(SmokePositions, 0, 0, SmokePositions.Length, stream: 0, Flags);
        _SmokeTrailMesh.SetVertexBufferData(SmokeNormals, 0, 0, SmokeNormals.Length, stream: 1, Flags);
        _SmokeTrailMesh.SetVertexBufferData(Types, 0, 0, Types.Length, stream: 2, Flags);
        _SmokeTrailMesh.SetVertexBufferData(UVs, 0, 0, UVs.Length, stream: 3, Flags);

        Positions.Dispose();
        Normals.Dispose();
        Types.Dispose();
        SmokePositions.Dispose();
        SmokeNormals.Dispose();
        UVs.Dispose();
    }

    bool OutOfRange(Vector3 Position)
    {
        Vector3 Scale = BoundingBox.localScale;
        return Mathf.Abs(Position.x) > Scale.x || Mathf.Abs(Position.y) > Scale.y || Mathf.Abs(Position.z) > Scale.z;
    }

    public void Clear()
    {
        nBullets = 0;
    }
}
