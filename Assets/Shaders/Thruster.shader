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
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _ColorEnd;
            float _Cutoff;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // sample the texture
                float2 uv = i.uv;
                uv.x *= 3;
                uv.y -= _Time.y * 8;
                float4 Sample1 = tex2D(_MainTex, uv);
                uv.y += _Time.y * 4;
                float4 Sample2 = tex2D(_MainTex, uv / 2);
                uv.y -= _Time.y;
                float4 Sample3 = tex2D(_MainTex, uv / 8);
                //return Sample3;

                // Make gradient from bottom to top.
                float4 col = lerp(_Color, _ColorEnd, i.uv.y);
                // Layered noise.
                Sample3 += Sample1;
                Sample3 += Sample2;

                float a = lerp(1.0, Sample3, saturate(i.uv.y + 0.25));
                a = lerp(a, 0, saturate(i.uv.y + 0.2));

                //col += step(0.5, Sample1) * 0.1;
                //float noise = lerp(1.0, Sample3, i.uv.y) * 0.5 + Sample1 * 0.3 + Sample2 * 0.2;

                //col.rgba *= noise + 0.5;
                //col.a = saturate(lerp(0, noise, 1.0 - i.uv.y * 3));
                //float4 col = Sample * i.uv.y;

                //clip(Sample.r - _Cutoff - i.uv.x);

                //col *= _Color;
                //col.a = 1.0 - col.a;
                //col.a = 1;
                return float4(col.rgb, a) * 2;
                return col * 4;
            }
            ENDCG
        }
    }
}
