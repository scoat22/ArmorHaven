using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class ThrusterSystem : MonoBehaviour
{
    public struct ThrusterData
    {
        public Matrix4x4 Transform;
        public float Power;
        public float TimeEnd;
    }
    public static List<ThrusterData> Thrusters = new List<ThrusterData>();
    public ComputeBuffer ThrusterBuffer;
    public ComputeBuffer ArgsBuffer;
    public Mesh _Mesh;
    public Material _Material;
    Bounds Bounds;

    // Start is called before the first frame update
    void Start()
    {
        Bounds = new Bounds(Vector3.zero, Vector3.one * float.MaxValue / 4);
    }

    private void OnDestroy()
    {
        ThrusterBuffer?.Dispose();
        ThrusterBuffer = null;
    }

    // Update is called once per frame
    void Update()
    {
        // Render thrusters based on power

        // Prevent error from Unity API
        if (Thrusters.Count > 0)
        {
            // Argument buffer used by DrawMeshInstancedIndirect.
            var args = new NativeArray<uint>(5, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            args[0] = (uint)_Mesh.GetIndexCount(0);
            args[1] = (uint)Thrusters.Count;
            args[2] = (uint)_Mesh.GetIndexStart(0);
            args[3] = (uint)_Mesh.GetBaseVertex(0);
            args[4] = 0;

            ArgsBuffer?.Dispose();
            ArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            ArgsBuffer.SetData(args);


            ThrusterBuffer?.Dispose();
            ThrusterBuffer = new ComputeBuffer(Thrusters.Count, sizeof(float) * 18);
            ThrusterBuffer.SetData(Thrusters);

            _Material.SetBuffer("Thrusters", ThrusterBuffer);

            args.Dispose();

            Graphics.DrawMeshInstancedIndirect(_Mesh, 0, _Material, Bounds, ArgsBuffer);
            //buffer.DrawMeshInstancedIndirect(_Mesh, 0, _Material, 0, ArgsBuffer, 0);
        }

        // Clear every frame.
        Thrusters.Clear();
    }

    /*void FixedUpdate()
    {
        // Clear every frame.
        Thrusters.Clear();
    }*/
}
