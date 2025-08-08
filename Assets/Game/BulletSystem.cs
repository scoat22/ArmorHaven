using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TDLN.CameraControllers;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class BulletSystem : MonoBehaviour
{
    public static BulletSystem Instance;

    public GameObject ImpactPrefab;
    public GameObject SparksPrefab;

    [SerializeField] int _MaxBullets;
    public static int MaxBullets = 150;
    static int nBullets;
    static NativeArray<Vector3> Positions;  // Position per bullet
    static NativeArray<Vector3> Velocities; // Velocity per bullet
    static NativeArray<BulletInfo> BulletData; // Basically denotes size and color.

    public AudioClip[] CannonClips;
    public AudioClip HitNoise;
    AudioSource _AudioSource; // Cache AudioSource.

    Mesh _Mesh; // lines mesh (rendering all bullets)
    public float BulletDamage = 0.5f;
    public bool SpawnVfxParticles = true;

    [Header("Ricochet")]
    public AudioClip[] RicochetNoises;
    public float RicochetDotAngleTolerance = -0.85f;
    public float RicochetEnergyLoss = 0.5f;
    public Transform BoundingBox;

    public List<TracerTypeInfo> TracerTypes = new List<TracerTypeInfo>();
    [System.Serializable, StructLayout(LayoutKind.Sequential)]
    public struct TracerTypeInfo
    {
        public Color Color;
        public float Size;
    }

    public enum TracerType
    {
        Turret,
        MachineGun,
    }

    public struct BulletInfo
    {
        public int AmmoType;
        public bool IsPlayer; // Did the player fire this bullet?
    }

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
        MaxBullets = _MaxBullets;
        _AudioSource = GetComponent<AudioSource>();
        Positions = new NativeArray<Vector3>(MaxBullets, Allocator.Persistent);
        Velocities = new NativeArray<Vector3>(MaxBullets, Allocator.Persistent);
        BulletData = new NativeArray<BulletInfo>(MaxBullets, Allocator.Persistent);
        _Mesh = MeshUtility.CreateLinesMesh(MaxBullets);

        GetComponent<MeshFilter>().mesh = _Mesh;
        var buffer = new ComputeBuffer(TracerTypes.Count, sizeof(float) * 5);
        buffer.SetData(TracerTypes);
        GetComponent<MeshRenderer>().material.SetBuffer("TracerTypes", buffer);
        //AmmoTypesBuffer.Dispose();
    }

    void OnDestroy()
    {
        Positions.Dispose();
        Velocities.Dispose();
        BulletData.Dispose();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        int nNewBullets = 0;
        var NewPositions = new NativeArray<Vector3>(MaxBullets, Allocator.Persistent);
        var NewVelocities = new NativeArray<Vector3>(MaxBullets, Allocator.Persistent);
        var NewBulletData = new NativeArray<BulletInfo>(MaxBullets, Allocator.Persistent);

        for (int i = 0; i < nBullets; i++)
        {
            // If it hits a ship, do damage. (Audio-Visual reward)
            // Multiply raycast length by two to avoid going through walls (kinda janky).
            if (Physics.Raycast(Positions[i], Velocities[i], out RaycastHit hit, Velocities[i].magnitude * dt * 2.0f))
            {
                if (Vector3.Dot(Velocities[i].normalized, hit.normal) < RicochetDotAngleTolerance)
                {
                    OnBulletHit(i, hit);
                    if(SpawnVfxParticles) Instantiate(ImpactPrefab, hit.point, Quaternion.LookRotation(hit.normal));  // Vfx / Sfx (Audio is attached to prefab).
                    continue;
                }
                else
                {
                    Vector3 original = Velocities[i];
                    // Ricochet.
                    Velocities[i] = Vector3.Reflect(Velocities[i], hit.normal) * RicochetEnergyLoss;

                    _AudioSource.PlayOneShot(RicochetNoises[Random.Range(0, RicochetNoises.Length - 1)]); // Sfx
                    //Instantiate(SparksPrefab, hit.point, Quaternion.LookRotation(hit.normal)); // Vfx
                    if (SpawnVfxParticles) Instantiate(SparksPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                }
            }
            // Make a sound
            // Make an explosion

            // For the 10 oldest bullets, destroy them if they aren't destined to hit anything. 
            if (nBullets == MaxBullets && i <= 10)
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
            NewPositions[nNewBullets] = Positions[i];
            NewVelocities[nNewBullets] = Velocities[i];
            NewBulletData[nNewBullets] = BulletData[i];
            nNewBullets++;
        }

        // Swap pointer.
        var TempPositions = Positions;
        var TempVelocities = Velocities;
        var TempBulletData = BulletData;
        Positions = NewPositions;
        Velocities = NewVelocities;
        BulletData = NewBulletData;
        nBullets = nNewBullets;
        // Dispose old
        TempPositions.Dispose();
        TempVelocities.Dispose();
        TempBulletData.Dispose();
    }

    void OnBulletHit(int idx, RaycastHit hit)
    {
        if (ShipUtility.TryGetShip(hit.collider.transform, out Ship Ship))
        {
            // For now hard code damage value.
            if (Ship.Health > 0)
                Ship.Health = Mathf.Max(0, Ship.Health - BulletDamage);
            //Debug.LogFormat("Hit a ship. New health: {0}", Ship.Health);
            if (BulletData[idx].IsPlayer)
            {
                int Points = 10;
                // If it was a killing blow, add more points;
                if (Ship.Health == 0) Points = 120;

                PlayerController.Instance.AddPoints(Points);
            }
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
            Vector3 CameraVelocity = CameraOrbit.Velocity;
            var Flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

            for (int i = 0; i < nBullets; i++)
            {
                Positions[i] += Velocities[i] * dt;
            }

            var VertexPositions = new NativeArray<Vector3>(MaxBullets * 2, Allocator.Temp);
            var Types = new NativeArray<float>(MaxBullets * 2, Allocator.Temp);
            for (int i = 0; i < nBullets; i++)
            {
                VertexPositions[i * 2] = Positions[i];
                VertexPositions[i * 2 + 1] = Positions[i] + (Velocities[i] - CameraVelocity) * dt;
                Types[i * 2] = BulletData[i].AmmoType;
                Types[i * 2 + 1] = BulletData[i].AmmoType;
            }
            for (int i = nBullets * 2; i < MaxBullets * 2; i++)
            {
                VertexPositions[i] = Vector4.zero;
            }

            var descriptors = new VertexAttributeDescriptor[2];
            descriptors[0] = new VertexAttributeDescriptor()
            {
                attribute = VertexAttribute.Position,
                format = VertexAttributeFormat.Float32,
                dimension = 3,
                stream = 0,

            };
            descriptors[1] = new VertexAttributeDescriptor()
            {
                attribute = VertexAttribute.TexCoord0,
                format = VertexAttributeFormat.Float32,
                dimension = 1,
                stream = 1,
            };
            _Mesh.SetVertexBufferParams(VertexPositions.Length, descriptors);
            _Mesh.SetVertexBufferData(VertexPositions, 0, 0, VertexPositions.Length, stream: 0, Flags);
            _Mesh.SetVertexBufferData(Types, 0, 0, Types.Length, stream: 1, Flags);

            // Render all the bullets at their current position
            //Render();
            //RenderGameObjects(dt);

            // Test
            //float Range = SmokeRes;
            //AddSmoke(new Vector3(Random.Range(0, Range), Random.Range(0, Range), Random.Range(0, Range)), 1.0f);
            //RaymarchGeneric.AddSmoke(new Vector3(0, y, 0), 1.0f);
            //y += Time.deltaTime;
        }
    }

    bool OutOfRange(Vector3 Position)
    {
        Vector3 Scale = BoundingBox.localScale;
        return Mathf.Abs(Position.x) > Scale.x || Mathf.Abs(Position.y) > Scale.y || Mathf.Abs(Position.z) > Scale.z;
    }

    // Type: Basically only changes the look of the projectile.
    public static bool TryAddBullet(Vector3 Position, Vector3 Velocity, TracerType Type = TracerType.Turret, bool IsPlayer = false)
    {
        if (nBullets < MaxBullets)
        {
            Positions[nBullets] = Position;
            Velocities[nBullets] = Velocity;
            BulletData[nBullets] = new BulletInfo() { AmmoType = (int)Type, IsPlayer = IsPlayer };
            nBullets++;

            // Play audio
            var Clips = Instance.CannonClips;
            Instance._AudioSource.PlayOneShot(Clips[Random.Range(0, Clips.Length - 1)]);
            // Add smoke
            //RaymarchGeneric.AddSmoke(Position, 1.0f);
            return true;
        }
        else Debug.LogWarningFormat("MaxBullets {0} reached.", MaxBullets);
        return false;
    }

    public void Clear()
    {
        nBullets = 0;
    }
}
