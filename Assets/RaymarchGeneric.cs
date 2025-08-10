using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Effects/Raymarch (Generic Complete)")]
public class RaymarchGeneric : SceneViewFilter
{
    public static RaymarchGeneric Instance;
    public Transform SunLight;

    [SerializeField]
    private Shader _EffectShader;
    [SerializeField]
    private Texture2D _MaterialColorRamp;
    [SerializeField]
    private Texture2D _PerfColorRamp;
    [SerializeField]
    private float _RaymarchDrawDistance = 40;
    [SerializeField]
    private bool _DebugPerformance = false;

    public Material _Material
    {
        get
        {
            if (!_EffectMaterial && _EffectShader)
            {
                _EffectMaterial = new Material(_EffectShader);
                _EffectMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            return _EffectMaterial;
        }
    }
    private Material _EffectMaterial;

    public Camera CurrentCamera
    {
        get
        {
            if (!_CurrentCamera)
                _CurrentCamera = GetComponent<Camera>();
            return _CurrentCamera;
        }
    }
    private Camera _CurrentCamera;

    [Header("Raymarch Settings")]
    public bool raymarchByStepCount;
    [Range(10, 64)]
    public int stepCount = 30;

    [Range(1, 10)]
    public float stepSize = 10;

    [Header("Cloud Settings")]
    public Texture2D blueNoise; // used to randomly off set the ray origin to reduce layered artifact

    [Header("Base Noise")]
    public Vector3 baseNoiseOffset;
    public float baseNoiseScale = 1;

    [Header("Detail Noise")]
    public Vector3 detailNoiseOffset;
    public float detailNoiseScale = 1;

    [Header("Density Modifiers")]
    // [Range(0, 1)]
    // public float densityThreshold = 1;
    public float densityMultiplier = 1;
    [Range(0, 1)]
    public float globalCoverageMultiplier;
    // public float anvilBias = 1;



    [Header("Lighting")]

    [Range(0, 1)]
    public float darknessThreshold = 0;
    public float lightAbsorptionThroughCloud = 1;
    public float lightAbsorptionTowardSun = 1;
    [Range(0, 1)]
    public float forwardScattering = .83f;
    [Range(0, 1)]
    public float backScattering = .3f;
    [Range(0, 1)]
    public float baseBrightness = 0.5f;
    [Range(0, 1)]
    public float phaseFactor = .15f;

    [Header("Other")]
    public GameObject boundingBox;
    public static GameObject BoundingBox;

    public ComputeShader SmokeShader;
    public float DissipationRate = 1.0f;

    //public Texture3D SmokeMap;
    GraphicsBuffer SmokeMap;
    NativeArray<float> SmokeCPU;
    int SmokeRes = 256;

    public bool DissipateSmoke = true;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        
        float Res = SmokeRes;
        int Length = (int)(Res * Res * Res);
        SmokeMap = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Length, sizeof(float));
        //SmokeTexture

        // Test
        SmokeCPU = new NativeArray<float>(Length, Allocator.Persistent);
        SmokeMap.SetData(SmokeCPU);// Empty.
        /*for (int x = 0; x < Res; x++)
        {
            for (int y = 0; y < Res; y++)
            {
                for (int z = 0; z < Res; z++)
                {
                    var Position = new Vector3(x, y, z);

                    //float value = (float)x / Res; // (float)(x * y * z) / Length;// 
                    //float value = (float)(x * y * z) / Length;
                    //float value = Mathf.Clamp01(Res - (Vector3.Magnitude(Position - Vector3.one * Res)));
                    float value = y == 0 ? 1 : 0;
                    int i = Index3Dto1D(Position, (int)Res);
                    SmokeCPU[i] = value;
                    //Debug.LogFormat("Setting {0} (1D: {1}) to {2}", Position, i, value);
                }
            }
        }
        SmokeMap.SetData(SmokeCPU);*/
    }

    private void OnDestroy()
    {
        SmokeMap?.Dispose();
        SmokeMap = null;
        if(SmokeCPU.IsCreated) SmokeCPU.Dispose();
    }

    static int Index3Dto1D(Vector3 Position, int Res)
    {
        //return (int)(Position.x + Res * (Position.y + Res * Position.z));
        return (int)(((int)Position.z * Res * Res) + ((int)Position.y * Res) + (int)Position.x);
    }

    public static void AddSmoke(Vector3 Position, float value)
    {
        if (Instance.SmokeMap != null)
        {
            var Res = Instance.SmokeRes;

            // Range check
            if (Position.x < Res && Position.x >= 0 &&
                Position.y < Res && Position.y >= 0 &&
                Position.z < Res && Position.z >= 0)
            {
                //Debug.LogFormat("Adding smoke to {0}", Position);

                int index = Index3Dto1D(Position, Res);

                float[] array = new float[1];
                array[0] = value;

                Instance.SmokeMap.SetData(array, 0, (int)index, 1);
            }
            else Debug.Log("Position was outside of map bounds");
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        Matrix4x4 corners = GetFrustumCorners(CurrentCamera);
        Vector3 pos = CurrentCamera.transform.position;

        for (int x = 0; x < 4; x++) {
            corners.SetRow(x, CurrentCamera.cameraToWorldMatrix * corners.GetRow(x));
            Gizmos.DrawLine(pos, pos + (Vector3)(corners.GetRow(x)));
        }

        /*
        // UNCOMMENT TO DEBUG RAY DIRECTIONS
        Gizmos.color = Color.red;
        int n = 10; // # of intervals
        for (int x = 1; x < n; x++) {
            float i_x = (float)x / (float)n;

            var w_top = Vector3.Lerp(corners.GetRow(0), corners.GetRow(1), i_x);
            var w_bot = Vector3.Lerp(corners.GetRow(3), corners.GetRow(2), i_x);
            for (int y = 1; y < n; y++) {
                float i_y = (float)y / (float)n;
                
                var w = Vector3.Lerp(w_top, w_bot, i_y).normalized;
                Gizmos.DrawLine(pos + (Vector3)w, pos + (Vector3)w * 1.2f);
            }
        }
        */
    }

    //[ImageEffectOpaque]
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!_Material)
        {
            Graphics.Blit(source, destination); // do nothing
            return;
        }
        BoundingBox = boundingBox;
        // Set any custom shader variables here.  For example, you could do:
        // EffectMaterial.SetFloat("_MyVariable", 13.37f);
        // This would set the shader uniform _MyVariable to value 13.37

        _Material.SetVector("_LightDir", SunLight ? SunLight.forward : Vector3.down);

        // Construct a Model Matrix for the Torus
        Matrix4x4 MatTorus = Matrix4x4.TRS(
            Vector3.right * Mathf.Sin(Time.time) * 5, 
            Quaternion.identity,
            Vector3.one);
        MatTorus *= Matrix4x4.TRS(
            Vector3.zero, 
            Quaternion.Euler(new Vector3(0, 0, (Time.time * 200) % 360)), 
            Vector3.one);
        // Send the torus matrix to our shader
        _Material.SetMatrix("_MatTorus_InvModel", MatTorus.inverse);

        _Material.SetTexture("_ColorRamp_Material", _MaterialColorRamp);
        _Material.SetTexture("_ColorRamp_PerfMap", _PerfColorRamp);

        _Material.SetFloat("_DrawDistance", _RaymarchDrawDistance);

        if(_Material.IsKeywordEnabled("DEBUG_PERFORMANCE") != _DebugPerformance) {
            if(_DebugPerformance)
                _Material.EnableKeyword("DEBUG_PERFORMANCE");
            else
                _Material.DisableKeyword("DEBUG_PERFORMANCE");
        }

        _Material.SetMatrix("_FrustumCornersES", GetFrustumCorners(CurrentCamera));
        _Material.SetMatrix("_CameraInvViewMatrix", CurrentCamera.cameraToWorldMatrix);
        _Material.SetVector("_CameraWS", CurrentCamera.transform.position);

        // Set cloud variables
        {
            Transform transform = boundingBox.transform;
            _Material.SetVector("boundsMin", transform.position - transform.localScale / 2);
            _Material.SetVector("boundsMax", transform.position + transform.localScale / 2);
            _Material.SetInt("raymarchByCount", raymarchByStepCount ? 1 : 0);
            _Material.SetInt("raymarchStepCount", stepCount);
            // The step cound should be based on the bounds
            //stepSize = Mathf.Max(Mathf.Max(transform.localScale.x, transform.localScale.y), transform.localScale.z) / stepCount;
            _Material.SetFloat("raymarchStepSize", stepSize);
            _Material.SetTexture("BlueNoise", blueNoise);

            NoiseGenerator noiseGenerator = FindObjectOfType<NoiseGenerator>();
            if (noiseGenerator.shouldUpdateNoise) noiseGenerator.updateNoise();

            WeatherMapGenerator WMGenerator = FindObjectOfType<WeatherMapGenerator>();
            if (WMGenerator.shouldUpdateNoise) WMGenerator.updateNoise();

            // values related to shaping the cloud
            _Material.SetFloat("time", Time.time);

            _Material.SetTexture("BaseNoise", noiseGenerator.baseRenderTexture);
            _Material.SetVector("baseNoiseOffset", baseNoiseOffset);
            _Material.SetFloat("baseNoiseScale", baseNoiseScale);

            _Material.SetTexture("DetailNoise", noiseGenerator.detailRenderTexture);
            _Material.SetVector("detailNoiseOffset", detailNoiseOffset);
            _Material.SetFloat("detailNoiseScale", detailNoiseScale);
            // material.SetFloat("densityThreshold", densityThreshold);
            _Material.SetFloat("densityMultiplier", densityMultiplier);
            _Material.SetFloat("globalCoverage", globalCoverageMultiplier);
            // material.SetFloat("anvilBias", anvilBias);

            _Material.SetTexture("WeatherMap", WMGenerator.WMRenderTexture);

            // values related to lighting the cloud
            _Material.SetFloat("darknessThreshold", darknessThreshold);
            _Material.SetFloat("lightAbsorptionThroughCloud", lightAbsorptionThroughCloud);
            _Material.SetFloat("lightAbsorptionTowardSun", lightAbsorptionTowardSun);
            _Material.SetVector("phaseParams", new Vector4(forwardScattering, backScattering, baseBrightness, phaseFactor));

            // Smoke map
            _Material.SetBuffer("SmokeMap", SmokeMap);
            _Material.SetFloat("SmokeRes", SmokeRes);
        }
        // Set time variable

        CustomGraphicsBlit(source, destination, _Material, 0);
    }

    private void FixedUpdate()
    {
        if (DissipateSmoke)
        {
            float dt = Time.fixedDeltaTime;
            SmokeShader.SetBuffer(0, "SmokeMap", SmokeMap);
            SmokeShader.SetInt("Res", SmokeRes);
            SmokeShader.SetFloat("DeltaTime", dt);
            SmokeShader.SetFloat("DissipationRate", DissipationRate);
            SmokeShader.Dispatch(0, SmokeRes / 4, SmokeRes / 4, SmokeRes / 4);
        }
    }

    /// \brief Stores the normalized rays representing the camera frustum in a 4x4 matrix.  Each row is a vector.
    /// 
    /// The following rays are stored in each row (in eyespace, not worldspace):
    /// Top Left corner:     row=0
    /// Top Right corner:    row=1
    /// Bottom Right corner: row=2
    /// Bottom Left corner:  row=3
    private Matrix4x4 GetFrustumCorners(Camera cam)
    {
        float camFov = cam.fieldOfView;
        float camAspect = cam.aspect;

        Matrix4x4 frustumCorners = Matrix4x4.identity;

        float fovWHalf = camFov * 0.5f;

        float tan_fov = Mathf.Tan(fovWHalf * Mathf.Deg2Rad);

        Vector3 toRight = Vector3.right * tan_fov * camAspect;
        Vector3 toTop = Vector3.up * tan_fov;

        Vector3 topLeft = (-Vector3.forward - toRight + toTop);
        Vector3 topRight = (-Vector3.forward + toRight + toTop);
        Vector3 bottomRight = (-Vector3.forward + toRight - toTop);
        Vector3 bottomLeft = (-Vector3.forward - toRight - toTop);

        frustumCorners.SetRow(0, topLeft);
        frustumCorners.SetRow(1, topRight);
        frustumCorners.SetRow(2, bottomRight);
        frustumCorners.SetRow(3, bottomLeft);

        return frustumCorners;
    }

    /// \brief Custom version of Graphics.Blit that encodes frustum corner indices into the input vertices.
    /// 
    /// In a shader you can expect the following frustum cornder index information to get passed to the z coordinate:
    /// Top Left vertex:     z=0, u=0, v=0
    /// Top Right vertex:    z=1, u=1, v=0
    /// Bottom Right vertex: z=2, u=1, v=1
    /// Bottom Left vertex:  z=3, u=1, v=0
    /// 
    /// \warning You may need to account for flipped UVs on DirectX machines due to differing UV semantics
    ///          between OpenGL and DirectX.  Use the shader define UNITY_UV_STARTS_AT_TOP to account for this.
    static void CustomGraphicsBlit(RenderTexture source, RenderTexture dest, Material fxMaterial, int passNr)
    {
        RenderTexture.active = dest;

        fxMaterial.SetTexture("_MainTex", source);

        GL.PushMatrix();
        GL.LoadOrtho(); // Note: z value of vertices don't make a difference because we are using ortho projection

        fxMaterial.SetPass(passNr);

        GL.Begin(GL.QUADS);

        // Here, GL.MultitexCoord2(0, x, y) assigns the value (x, y) to the TEXCOORD0 slot in the shader.
        // GL.Vertex3(x,y,z) queues up a vertex at position (x, y, z) to be drawn.  Note that we are storing
        // our own custom frustum information in the z coordinate.
        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(0.0f, 0.0f, 3.0f); // BL

        GL.MultiTexCoord2(0, 1.0f, 0.0f);
        GL.Vertex3(1.0f, 0.0f, 2.0f); // BR

        GL.MultiTexCoord2(0, 1.0f, 1.0f);
        GL.Vertex3(1.0f, 1.0f, 1.0f); // TR

        GL.MultiTexCoord2(0, 0.0f, 1.0f);
        GL.Vertex3(0.0f, 1.0f, 0.0f); // TL
        
        GL.End();
        GL.PopMatrix();
    }

    /*static void CustomGraphicsBlit(CommandBuffer buffer, RenderTexture source, RenderTexture dest, Material fxMaterial, int passNr)
    {
        buffer.SetRenderTarget(dest);

        fxMaterial.SetTexture("_MainTex", source);

        GL.PushMatrix();
        GL.LoadOrtho(); // Note: z value of vertices don't make a difference because we are using ortho projection

        fxMaterial.SetPass(passNr);

        GL.Begin(GL.QUADS);

        // Here, GL.MultitexCoord2(0, x, y) assigns the value (x, y) to the TEXCOORD0 slot in the shader.
        // GL.Vertex3(x,y,z) queues up a vertex at position (x, y, z) to be drawn.  Note that we are storing
        // our own custom frustum information in the z coordinate.
        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(0.0f, 0.0f, 3.0f); // BL

        GL.MultiTexCoord2(0, 1.0f, 0.0f);
        GL.Vertex3(1.0f, 0.0f, 2.0f); // BR

        GL.MultiTexCoord2(0, 1.0f, 1.0f);
        GL.Vertex3(1.0f, 1.0f, 1.0f); // TR

        GL.MultiTexCoord2(0, 0.0f, 1.0f);
        GL.Vertex3(0.0f, 1.0f, 0.0f); // TL

        GL.End();
        GL.PopMatrix();
    }*/
}
