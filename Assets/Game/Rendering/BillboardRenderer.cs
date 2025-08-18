using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class BillboardRenderer : MonoBehaviour
{
    public static BillboardRenderer Instance;

    NativeArray<Vector3> Positions;
    int nSprites;
    const int MaxSprites = 100;
    public Texture2D _SpriteTexture;
    public float _Size = 0.02f;
    static Bounds Bounds;
    public Material _Material;
    static Mesh _Mesh;
    static ComputeBuffer ArgsBuffer;
    static ComputeBuffer PositionsBuffer;

    private void Start()
    {
        Instance = this;
        _Mesh = MeshUtility.CreateBillboardQuad();
        _Material.SetTexture("_MainTex", _SpriteTexture);
        _Material.SetFloat("_Scale", _Size);
        Positions = new NativeArray<Vector3>(MaxSprites, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    }

    private void OnDestroy()
    {
        Positions.Dispose();
        ArgsBuffer?.Dispose();
        ArgsBuffer = null;
        PositionsBuffer?.Dispose();
        PositionsBuffer = null;
    }

    private void Update()
    {
        Render();
        if (Time.timeScale > 0) 
            nSprites = 0;
    }

    public void AddSprite(Vector3 Position, SpriteId Sprite = SpriteId.WhiteX)
    {
        if (nSprites < MaxSprites)
        {
            Positions[nSprites] = Position;
            nSprites++;
        }
        else Debug.LogError("Reached max sprites");
    }

    void Render()
    {
        // Prevent error from Unity API
        if (nSprites > 0)
        {
            Bounds = new Bounds(Camera.main.transform.position, Vector3.one * 10000f);

            //Debug.LogFormat("Rendering {0} sprites", nSprites);
            // Argument buffer used by DrawMeshInstancedIndirect.
            var args = new NativeArray<uint>(5, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            args[0] = (uint)_Mesh.GetIndexCount(0);
            args[1] = (uint)nSprites;
            args[2] = (uint)_Mesh.GetIndexStart(0);
            args[3] = (uint)_Mesh.GetBaseVertex(0);
            args[4] = 0;

            ArgsBuffer?.Dispose();
            ArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            ArgsBuffer.SetData(args);

            PositionsBuffer?.Dispose();
            PositionsBuffer = new ComputeBuffer(MaxSprites, sizeof(float) * 3); // nSprites
            PositionsBuffer.SetData(Positions);

            _Material.SetBuffer("_Positions", PositionsBuffer);

            args.Dispose();

            Graphics.DrawMeshInstancedIndirect(_Mesh, 0, _Material, Bounds, ArgsBuffer);
            //buffer.DrawMeshInstancedIndirect(_Mesh, 0, _Material, 0, ArgsBuffer, 0);
        }
    }

    public enum SpriteId
    {
        WhiteX,
        MuzzleFlash
    }
}
