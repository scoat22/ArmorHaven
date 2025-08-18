Shader "Unlit/SmokeTrailOld"
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

                float Magnitude = length(i.normal.xyz);
                float TimeFired = i.normal.w;
                float TimeSinceFired = _Time.y - TimeFired;
                float TimeFactor = saturate(1.0 - TimeSinceFired);

                //float u = TimeSinceFired / Magnitude;
                float2 uv = float2(i.uv * _Scale * Magnitude + _Time.y * 0.77, 0.5) ;
                fixed4 Sample = tex2D(_MainTex, uv);
                //Sample.r *= 1.0 - TimeSinceFired;

                fixed4 col = _Color;
                float Alpha = size / 100.0;
                Alpha = saturate(Alpha / TimeSinceFired / i.depth);
                //col.a *= TimeFactor
                Alpha *= Sample.r ;
                //return fixed4(pow(Sample, 2).rgb, 1);
                //Sample = step(Sample, i.uv / Magnitude * _Scale);

                // WIP
                //Alpha = step(Sample, lerp(0, 0.5, TimeFactor)) / i.depth;
                //return _Scale / Magnitude;

                return fixed4(_Color.rgb, Alpha);

                return col;
            }
            ENDCG
        }
    }
}