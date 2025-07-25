Shader "Instanced/InstancedSurfaceShader" 
{
    Properties 
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _BumpMap ("Bumpmap", 2D) = "bump" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader 
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model
        #pragma surface surf Standard addshadow fullforwardshadows
        #pragma multi_compile_instancing
        #pragma instancing_options procedural:setup

        struct Input 
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
        };

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
        //StructuredBuffer<float3> positionBuffer;
        StructuredBuffer<float4x4> Transforms;
#endif

        void setup()
        {
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            //unity_ObjectToWorld = mul(_LocalToWorld, Transforms[unity_InstanceID]);
            unity_ObjectToWorld = Transforms[unity_InstanceID];
            //unity_WorldToObject = InvTransforms[unity_InstanceID];
#endif
            //unity_WorldToObject = LinAlg_Invert(unity_ObjectToWorld);
            
        }

        sampler2D _MainTex;
        sampler2D _BumpMap;

        half _Glossiness;
        half _Metallic;

        void surf (Input IN, inout SurfaceOutputStandard o) 
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex);
            o.Albedo = c.rgb;
            o.Normal = UnpackNormal (tex2D (_BumpMap, IN.uv_BumpMap));
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
