using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Cloud : MonoBehaviour
{
    [Header("Raymarch Settings")]
    public bool raymarchByStepCount;
    [Range(10, 48)]
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
    public Shader shader;
    public GameObject boundingBox;
    public Material EffectMaterial;

    // New:
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



    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (EffectMaterial == null)
            EffectMaterial = new Material(shader);

        // New:
        EffectMaterial.SetMatrix("_CameraInvViewMatrix", CurrentCamera.cameraToWorldMatrix);
        EffectMaterial.SetVector("_CameraWS", CurrentCamera.transform.position);
        EffectMaterial.SetMatrix("_FrustumCornersES", GetFrustumCorners(CurrentCamera));
        /*CustomGraphicsBlit(source, destination, EffectMaterial, 0); // Replace Graphics.Blit with CustomGraphicsBlit
        return;*/

        EffectMaterial.SetTexture("_MainTex", source);

        Transform transform = boundingBox.transform;
        EffectMaterial.SetVector("boundsMin", transform.position - transform.localScale / 2);
        EffectMaterial.SetVector("boundsMax", transform.position + transform.localScale / 2);
        EffectMaterial.SetInt("raymarchByCount", raymarchByStepCount ? 1 : 0);
        EffectMaterial.SetInt("raymarchStepCount", stepCount);
        EffectMaterial.SetFloat("raymarchStepSize", stepSize);
        EffectMaterial.SetTexture("BlueNoise", blueNoise);

        NoiseGenerator noiseGenerator = FindObjectOfType<NoiseGenerator>();
        if (noiseGenerator.shouldUpdateNoise) noiseGenerator.updateNoise();

        WeatherMapGenerator WMGenerator = FindObjectOfType<WeatherMapGenerator>();
        if (WMGenerator.shouldUpdateNoise) WMGenerator.updateNoise();



        // values related to shaping the cloud
        EffectMaterial.SetFloat("time", Time.time);

        EffectMaterial.SetTexture("BaseNoise", noiseGenerator.baseRenderTexture);
        EffectMaterial.SetVector("baseNoiseOffset", baseNoiseOffset);
        EffectMaterial.SetFloat("baseNoiseScale", baseNoiseScale);

        EffectMaterial.SetTexture("DetailNoise", noiseGenerator.detailRenderTexture);
        EffectMaterial.SetVector("detailNoiseOffset", detailNoiseOffset);
        EffectMaterial.SetFloat("detailNoiseScale", detailNoiseScale);
        // material.SetFloat("densityThreshold", densityThreshold);
        EffectMaterial.SetFloat("densityMultiplier", densityMultiplier);
        EffectMaterial.SetFloat("globalCoverage", globalCoverageMultiplier);
        // material.SetFloat("anvilBias", anvilBias);

        EffectMaterial.SetTexture("WeatherMap", WMGenerator.WMRenderTexture);

        // values related to lighting the cloud
        EffectMaterial.SetFloat("darknessThreshold", darknessThreshold);
        EffectMaterial.SetFloat("lightAbsorptionThroughCloud", lightAbsorptionThroughCloud);
        EffectMaterial.SetFloat("lightAbsorptionTowardSun", lightAbsorptionTowardSun);
        EffectMaterial.SetVector("phaseParams", new Vector4(forwardScattering, backScattering, baseBrightness, phaseFactor));

        Graphics.Blit(source, destination, EffectMaterial);
        //CustomGraphicsBlit(source, destination, EffectMaterial, 0); // Replace Graphics.Blit with CustomGraphicsBlit
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
}

