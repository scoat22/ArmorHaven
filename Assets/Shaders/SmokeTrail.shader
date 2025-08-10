Shader "Unlit/SmokeTrail"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Scale ("Scale", Float) = 1
        _Color ("Color", Color) = (0.5, 0.5, 0.5, 0.5)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent"  "RenderType" = "Transparent" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 position : POSITION;
                float4 normal : NORMAL;
                float uv : TEXCOORD1;
                float type : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float4 normal : NORMAL;
                float type : TEXCOORD0;
                float uv : TEXCOORD1;
                float depth : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Scale;

            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.position).xyz;
                float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz;

                o.position = UnityObjectToClipPos(v.position);
                o.normal = v.normal;
                o.uv = v.uv;
                o.depth = -viewPos.z; // View space Z is negative in front of camera
                o.type = v.type;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float size = 20;
                switch(ceil(i.type))
                {
                    case 0:
                        size = 300;//88;
                        break;
                    case 1:
                        size = 20;
                        break;
                }

                float magnitude = length(i.normal.xyz);
                float TimeFired = i.normal.w;
                float TimeSinceFired = _Time.y - TimeFired;

                //float u = TimeSinceFired / magnitude * 1024;
                float2 uv = float2(i.uv * magnitude, 0);
                // UV scale
                uv *= _Scale;
                fixed4 col = _Color;
                fixed4 Sample = tex2D(_MainTex, uv);
                col.a = size / 100.0;
                col.a = saturate(col.a / TimeSinceFired / i.depth);
                //col.a *= saturate(1.0 - TimeSinceFired);
                col.a *= Sample.r;

                return col;
            }
            ENDCG
        }
    }
}
