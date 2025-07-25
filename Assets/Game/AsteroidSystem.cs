using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class AsteroidSystem : MonoBehaviour
{
    //NativeArray<Vector3> Positions;
    //NativeArray<Quaternion> Rotations;
    NativeArray<Matrix4x4> Transforms;
    int nAsteroids;
    int MaxAsteroids = 100;
    public Mesh _Mesh;
    public Material _Material;
    public float MaxRotationSpeed = 45.0f;
    float Range = 500;
    float VerticalRange = 20;

    static ComputeBuffer ArgsBuffer;
    static ComputeBuffer PositionsBuffer;
    Bounds Bounds;

    // Start is called before the first frame update
    void Start()
    {
        nAsteroids = 0;
        Transforms = new NativeArray<Matrix4x4>(MaxAsteroids, Allocator.Persistent);
        Bounds = new Bounds(Vector3.zero, new Vector3(Range, VerticalRange, Range));

        Range /= 2;
        VerticalRange /= 2;
        for (int i = 0; i < MaxAsteroids; i++)
        {
            AddAsteroid(new Vector3(
                Random.Range(-Range, Range),
                Random.Range(-VerticalRange, VerticalRange),
                Random.Range(-Range, Range)),
                Random.rotation);
        }
    }

    private void OnDestroy()
    {
        Transforms.Dispose();
        PositionsBuffer.Dispose();
    }

    void AddAsteroid(Vector3 Position, Quaternion Rotation)
    {
        if (nAsteroids < MaxAsteroids)
        {
            Transforms[nAsteroids] = Matrix4x4.TRS(Position, Rotation, Vector3.one);
            nAsteroids++;
        }
        else Debug.LogError("Reached max aseroids");
    }

    // Update is called once per frame
    void Update()
    {
        RenderAsteroids();
    }

    void RenderAsteroids()
    {
        // Prevent error from Unity API
        if (nAsteroids > 0)
        {
            //Debug.LogFormat("Drawing {0} asteroids", nAsteroids);
            // Argument buffer used by DrawMeshInstancedIndirect.
            var args = new NativeArray<uint>(5, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            args[0] = (uint)_Mesh.GetIndexCount(0);
            args[1] = (uint)nAsteroids;
            args[2] = (uint)_Mesh.GetIndexStart(0);
            args[3] = (uint)_Mesh.GetBaseVertex(0);
            args[4] = 0;

            ArgsBuffer?.Dispose();
            ArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            ArgsBuffer.SetData(args);

            for (int i = 0; i < nAsteroids; i++)
            {
                Random.InitState(i);
                var Rotation = Random.rotation;
                var Speed = Random.value * MaxRotationSpeed;
                Vector3 p = Transforms[i].GetPosition();
                Vector3 s = Vector3.one * Random.Range(0.8f, 4.0f);
                Transforms[i] = Matrix4x4.TRS(p, Rotation * Quaternion.AngleAxis(Time.time * Speed, Vector3.up), Vector3.one);
            }

            PositionsBuffer?.Dispose();
            PositionsBuffer = new ComputeBuffer(nAsteroids, sizeof(float) * 16);
            PositionsBuffer.SetData(Transforms);

            _Material.SetBuffer("Transforms", PositionsBuffer);

            args.Dispose();

            Graphics.DrawMeshInstancedIndirect(_Mesh, 0, _Material, Bounds, ArgsBuffer);

            // Hmm maybe shadows will work better with this shader:
            //Graphics.DrawMeshInstanced()

            /*RenderParams rp = new RenderParams(_Material);
            Graphics.RenderMeshInstanced(rp, _Mesh, 0, Transforms);*/
        }
    }
}
