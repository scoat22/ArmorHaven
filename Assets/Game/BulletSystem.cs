using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

public class BulletSystem : MonoBehaviour
{
    public static BulletSystem Instance;

    public GameObject ImpactPrefab;
    public GameObject SparksPrefab;

    public static int MaxBullets = 100;
    static int nBullets;
    // Position per bullet
    static NativeArray<Vector3> Positions;
    // Velocity per bullet
    static NativeArray<Vector3> Velocities;

    public AudioClip[] CannonClips;
    public AudioClip HitNoise;
    AudioSource _AudioSource; // Cache AudioSource.

    [Header("Ricochet")]
    public AudioClip RicochetNoise;
    public float RicochetDotAngleTolerance = -0.85f;
    public float RicochetEnergyLoss = 0.5f;
    public Transform BoundingBox;

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
        _AudioSource = GetComponent<AudioSource>();
        Positions = new NativeArray<Vector3>(MaxBullets, Allocator.Persistent);
        Velocities = new NativeArray<Vector3>(MaxBullets, Allocator.Persistent);
        _Mesh = MeshUtility.CreateBillboardQuad(1, 1);
        _Material = CreateMaterial();
        CreateBulletPrefabs();
    }

    void OnDestroy()
    {
        ArgsBuffer?.Dispose();
        ArgsBuffer = null;
        PositionsBuffer?.Dispose();
        PositionsBuffer = null;
        Positions.Dispose();
        Velocities.Dispose();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        int nNewBullets = 0;
        var NewPositions = new NativeArray<Vector3>(MaxBullets, Allocator.Persistent);
        var NewVelocities = new NativeArray<Vector3>(MaxBullets, Allocator.Persistent);

        for (int i = 0; i < nBullets; i++)
        {
            // If it hits a ship, do damage. (Audio-Visual reward)
            if (Physics.Raycast(Positions[i], Velocities[i], out var hit, Velocities[i].magnitude * dt))
            {
                if (Vector3.Dot(Velocities[i].normalized, hit.normal) < RicochetDotAngleTolerance)
                {
                    // Hit.
                    //Debug.LogFormat("Hit {0}", hit.collider.name);

                    // Play a hit noise at the location
                    _AudioSource.PlayOneShot(HitNoise); // Sfx
                    Instantiate(ImpactPrefab, hit.point, Quaternion.LookRotation(hit.normal));  // Vfx
                    continue;
                }
                else
                {
                    // Ricochet.
                    Velocities[i] = Vector3.Reflect(Velocities[i], hit.normal) * RicochetEnergyLoss;

                    _AudioSource.PlayOneShot(RicochetNoise); // Sfx
                    Instantiate(SparksPrefab, hit.point, Quaternion.LookRotation(hit.normal)); // Vfx
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
                Debug.Log("Bullet left the bounds and was destroyed");
                continue;
            }

            // Copy the old bullet to the new array.
            NewPositions[nNewBullets] = Positions[i];
            NewVelocities[nNewBullets] = Velocities[i];
            nNewBullets++;
        }

        // Swap pointer.
        var TempPositions = Positions;
        var TempVelocities = Velocities;
        Positions = NewPositions;
        Velocities = NewVelocities;
        nBullets = nNewBullets;
        // Dispose old
        TempPositions.Dispose();
        TempVelocities.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < nBullets; i++)
        {
            Positions[i] += Velocities[i] * dt;
        }

        // Render all the bullets at their current position
        //Render();
        RenderGameObjects(dt);

        // Test
        //float Range = SmokeRes;
        //AddSmoke(new Vector3(Random.Range(0, Range), Random.Range(0, Range), Random.Range(0, Range)), 1.0f);
        //RaymarchGeneric.AddSmoke(new Vector3(0, y, 0), 1.0f);
        //y += Time.deltaTime;
    }

    bool OutOfRange(Vector3 Position)
    {
        Vector3 Scale = BoundingBox.localScale;
        return Mathf.Abs(Position.x) > Scale.x || Mathf.Abs(Position.y) > Scale.y || Mathf.Abs(Position.z) > Scale.z;
    }

    public static bool TryAddBullet(Vector3 Position, Vector3 Velocity)
    {
        if (nBullets < MaxBullets)
        {
            Positions[nBullets] = Position;
            Velocities[nBullets] = Velocity;
            nBullets++;

            // Play audio
            var Clips = Instance.CannonClips;
            Instance._AudioSource.PlayOneShot(Clips[Random.Range(0, Clips.Length - 1)]);
            // Add smoke
            RaymarchGeneric.AddSmoke(Position, 1.0f);
            return true;
        }
        else Debug.LogErrorFormat("MaxBullets {0} reached.", MaxBullets);
        return false;
    }
    
    [SerializeField] Texture2D _VertexTexture;
    [SerializeField] float _BulletSize = 0.02f;
    [SerializeField] Color _Color = Color.yellow;
    [SerializeField] GameObject _BulletPrefab;
    static GameObject[] _Bullets;
    static Bounds Bounds = new Bounds(Vector3.zero, new Vector3(100, 100, 100));
    static Material _Material;
    static Mesh _Mesh;
    static ComputeBuffer ArgsBuffer;
    static ComputeBuffer PositionsBuffer;

    Material CreateMaterial()
    {
        var material = new Material(Shader.Find("Custom/Billboard"));
        material.SetTexture("_MainTex", _VertexTexture);
        material.SetFloat("_Scale", _BulletSize);
        material.SetVector("_Color", _Color);
        return material;
    }

    void CreateBulletPrefabs()
    {
        _Bullets = new GameObject[MaxBullets];
        for (int i = 0; i < MaxBullets; i++)
        {
            var go = Instantiate(_BulletPrefab);
            go.transform.localScale = Vector3.one * _BulletSize;
            go.SetActive(false);
            _Bullets[i] = go;
        }
    }

    void RenderGameObjects(float dt)
    {
        // We actually need to add the camera's relative velocity for this line rendering "hack" to work.
        //Vector3 CameraVelocity = ShipSystem.Instance.Velocities[0
        Vector3 CameraVelocity = ShipManager.Instance.Ships[0].GetComponent<Rigidbody>().velocity;

        // Use the pool of game objects instead. 
        for (int i = 0; i < nBullets; i++)
        {
            _Bullets[i].SetActive(true);
            //_Bullets[i].transform.position = Positions[i]; // Old.

            // Set the line renderer properties.
            var lr = _Bullets[i].GetComponent<LineRenderer>();
            lr.SetPosition(0, Positions[i]);
            lr.SetPosition(1, Positions[i] + (Velocities[i] - CameraVelocity) * dt);
        }
        // Disable the rest
        for (int i = nBullets; i < MaxBullets; i++)
        {
            _Bullets[i].SetActive(false);
        }
    }

    void Render()
    {
        if (Positions != null)
        {
            // Prevent error from Unity API
            if (nBullets > 0)
            {
                // Argument buffer used by DrawMeshInstancedIndirect.
                var args = new NativeArray<uint>(5, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                args[0] = (uint)_Mesh.GetIndexCount(0);
                args[1] = (uint)nBullets;
                args[2] = (uint)_Mesh.GetIndexStart(0);
                args[3] = (uint)_Mesh.GetBaseVertex(0);
                args[4] = 0;

                ArgsBuffer?.Dispose();
                ArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                ArgsBuffer.SetData(args);


                PositionsBuffer?.Dispose();
                PositionsBuffer = new ComputeBuffer(nBullets, sizeof(float) * 3);
                PositionsBuffer.SetData(Positions);

                _Material.SetBuffer("_Positions", PositionsBuffer);
                _Material.SetInt("_PositionStride", 3);

                args.Dispose();

                Graphics.DrawMeshInstancedIndirect(_Mesh, 0, _Material, Bounds, ArgsBuffer);
                //buffer.DrawMeshInstancedIndirect(_Mesh, 0, _Material, 0, ArgsBuffer, 0);
            }
        }
    }

}
