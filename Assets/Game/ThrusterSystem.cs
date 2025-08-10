using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class ThrusterSystem : MonoBehaviour
{
    public static ThrusterSystem Instance;
    public struct ThrusterData
    {
        public Matrix4x4 Transform;
        public float Power;
        public float TimeEnd;
    }
    public List<ThrusterData> Thrusters;
    public int nThrusters = 0;
    public ComputeBuffer ThrusterBuffer;
    public ComputeBuffer ArgsBuffer;
    public Mesh _Mesh;
    public Material _Material;
    Bounds Bounds;

    // Start is called before the first frame update
    void Start()
    {
        if (Instance != null) Debug.LogError("There was already a ThrusterSystem!", this);
        Instance = this;
        Bounds = new Bounds(Vector3.zero, Vector3.one * float.MaxValue / 4);
        Thrusters = new List<ThrusterData>();
    }

    private void OnDestroy()
    {
        ThrusterBuffer?.Dispose();
        ThrusterBuffer = null;
        ArgsBuffer?.Dispose();
        ArgsBuffer = null;
    }

    public void AddThruster(ThrusterData ThrusterData)
    {
        Thrusters.Add(ThrusterData);
        nThrusters++;
    }

    // Update is called once per frame
    void Update()
    {
        // Render thrusters based on power

        // Prevent error from Unity API
        if (Thrusters.Count > 0)
        {
            //if (Camera.current == Camera.main)
            {
                // Argument buffer used by DrawMeshInstancedIndirect.
                var args = new NativeArray<uint>(5, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                args[0] = (uint)_Mesh.GetIndexCount(0);
                args[1] = (uint)nThrusters;
                args[2] = (uint)_Mesh.GetIndexStart(0);
                args[3] = (uint)_Mesh.GetBaseVertex(0);
                args[4] = 0;

                ArgsBuffer?.Dispose();
                ArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                ArgsBuffer.SetData(args);


                ThrusterBuffer?.Dispose();
                ThrusterBuffer = new ComputeBuffer(nThrusters, sizeof(float) * 18);
                ThrusterBuffer.SetData(Thrusters);

                _Material.SetBuffer("Thrusters", ThrusterBuffer);

                Debug.LogFormat("Rendering {0} thrusters", args[1]);

                Graphics.DrawMeshInstancedIndirect(_Mesh, 0, _Material, Bounds, ArgsBuffer, castShadows: UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows: false);
                args.Dispose();
            }
            //else Debug.LogError("Camera wasn't main camera");
        }

        // Clear every frame.
        Thrusters.Clear();
        nThrusters = 0;
    }
}
