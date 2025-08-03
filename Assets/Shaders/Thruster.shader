Shader "Custom/Thruster"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _ColorEnd ("Color End", Color) = (1,1,1,1)
        _Cutoff ("Cutoff", Float) = 0.5
    }
    SubShader
    {
        // Cutout:
        //Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }

        // Transparent:
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        //Cull front 

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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float TimeEnd : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _ColorEnd;
            float _Cutoff;

            struct ThrusterData
            {
                float4x4 Transform;
                float Power;
                float TimeEnd;
            };
            StructuredBuffer<ThrusterData> Thrusters;

            v2f vert (appdata v, uint InstanceID : SV_InstanceID)
            {
                v2f o;
                ThrusterData data = Thrusters[InstanceID];
                o.vertex = mul(data.Transform, float4(v.vertex.xyz, 1.0));
                o.TimeEnd = data.TimeEnd;

                o.vertex = UnityObjectToClipPos(o.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // 1 second since thrusters last engaged.
                float FadeTime = 4.0;
                float TimeFactor = saturate(1.0 - abs(i.TimeEnd - _Time.y) * FadeTime);
                //i.uv.y += 1 - TimeFactor;

                // sample the texture
                float2 uv = i.uv;
               
                uv.x *= 3;
                uv.y -= _Time.y * 8;
                float4 Sample1 = tex2D(_MainTex, uv);
                uv.y += _Time.y * 4;
                float4 Sample2 = tex2D(_MainTex, uv / 2);
                uv.y -= _Time.y;
                float4 Sample3 = tex2D(_MainTex, uv / 8);
 
                // Layered noise.
                float Noise = Sample3 + Sample2 + Sample1;

                // Alpha channel.
                float a = lerp(1.0, Noise, saturate(i.uv.y + 0.25));
                a = lerp(a, 0, saturate(i.uv.y + 0.2));
                //a = lerp(a, 0, saturate(Sample3));

                // Color
                // Make gradient from bottom to top.
                float4 col = lerp(_Color, _ColorEnd, i.uv.y);

                a *= TimeFactor;

                return float4(col.rgb, a) * 2;
                return col * 4;
            }
            ENDCG
        }
    }
}
