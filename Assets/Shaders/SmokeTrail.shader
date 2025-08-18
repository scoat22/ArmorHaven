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
                float CurveId : TEXCOORD0;
                float LocalId : TEXCOORD1;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float CurveId : TEXCOORD0;
                float LocalId : TEXCOORD1;
                float depth : TEXCOORD2;
            };

            struct Curve
            {
                float3 Start;
                float3 Control1;
                float3 Control2;
                float3 End;
                float TimeFired;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Scale;
            float _nBullets;
            StructuredBuffer<Curve> _Curves;

            float3 BezierCurve(float3 p1, float3 p2, float3 p3, float3 p4, float t)
            {
		        float3 ap1 = lerp(p1, p2, t);
		        float3 ap2 = lerp(p2, p3, t);
		        float3 ap3 = lerp(p3, p4, t);
		
		        float3 bp1 = lerp(ap1, ap2, t);
		        float3 bp2 = lerp(ap2, ap3, t);
		
		        return lerp(bp1, bp2, t);
            }

            float3 LineTest(float3 p1, float3 p2, float3 p3, float3 p4, float t)
            {
                return lerp(p1, p4, t);
             }

            v2f vert (appdata v)
            {
                v2f o;
                Curve curve = _Curves[(int)v.CurveId];
                // Test
                //float s = 20; curve.Start = 0; curve.Control1 = float3(0, s, 0); curve.Control2 = float3(s, s, 0); curve.End = float3(0, 0, s);
                float3 CurvePos = BezierCurve(curve.Start, curve.Control1, curve.Control2, curve.End, v.LocalId);
                //float3 CurvePos = LineTest(curve.Start.xyz, curve.Control1.xyz, curve.Control2.xyz, curve.End.xyz, v.LocalId);
                float4 position = float4(CurvePos, 1);
                float3 worldPos = mul(unity_ObjectToWorld, position).xyz;
                float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz;

                o.position = UnityObjectToClipPos(position);
                o.depth = -viewPos.z; // View space Z is negative in front of camera
                o.CurveId = v.CurveId;
                o.LocalId = v.LocalId;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                //if(i.CurveId >= _nBullets) discard;

                Curve curve = _Curves[(int)i.CurveId];
                float3 normal = curve.End - curve.Start;
                float size = 20;
                /*switch(ceil(i.type))
                {
                    case 0:
                        size = 300;//88;
                        break;
                    case 1:
                        size = 20;
                        break;
                }*/

                float Magnitude = length(normal);
                // The point nearest to the bullet is always "fresh" smoke.
                float TimeSinceFired = lerp(_Time.y - curve.TimeFired, 0.0001, i.LocalId);

                //float u = TimeSinceFired / Magnitude * 1024;
                float2 uv = float2(i.LocalId * Magnitude, 1024.0 / i.CurveId);
                // UV scale
                uv *= _Scale;
                fixed4 col = _Color;
                fixed4 Sample = tex2D(_MainTex, uv);
                col.a = size / 100.0;
                //col.a = saturate(col.a / TimeSinceFired / i.depth);
                col.a = saturate(col.a / TimeSinceFired);
                //col.a *= saturate(1.0 - TimeSinceFired);
                col.a *= Sample.r;
                //col.a = i.LocalId * i.LocalId;
                return col;
            }
            ENDCG
        }
    }
}
