Shader "Custom/Tracer"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _ColorAdd ("Color Add", Color) = (1,1,1,1)
        _Size("Size", Float) = 200
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" } 
        //Blend SrcAlpha OneMinusSrcAlpha
        Blend One One
        ZWrite Off
        Cull Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float depth : TEXCOORD0;
            };

            float4 _Color;
            float4 _ColorAdd;
            float _Size;

            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.depth = -viewPos.z; // View space Z is negative in front of camera
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float FarPlane = _ProjectionParams.z; // Essentially the furthest value possible.
                //float linearDepth = i.depth / FarPlane; // Normalize depth (0 to 1)
                float s = i.depth;
                //return float4(s, s, s, 1.0); // Output grayscale depth
                
                
                float Alpha = saturate(_Size / i.depth);
                float4 col = _Color + _ColorAdd * 2.0;

                return float4(col.rgb, Alpha);
            }
            ENDCG
        }
    }
}
