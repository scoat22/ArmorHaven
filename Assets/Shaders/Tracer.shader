Shader "Custom/Tracer"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Size("Size", Float) = 200
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" } 
        //Blend SrcAlpha OneMinusSrcAlpha
        //Blend One One
        Blend SrcAlpha One
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
                float type : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float depth : TEXCOORD0;
                float type : TEXCOORD1;
            };

            float4 _Color;
            float _Size;

            struct TracerType
            {
                float4 color;
                float size;
            };
            StructuredBuffer<TracerType> TracerTypes;

            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.type = v.type; // Pack type into W component.
                o.depth = -viewPos.z; // View space Z is negative in front of camera
                //if(o.type == 1.0) o.vertex.y += _Size;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float FarPlane = _ProjectionParams.z; // Essentially the furthest value possible.
                //float linearDepth = i.depth / FarPlane; // Normalize depth (0 to 1)
                float s = i.depth;
                //return float4(s, s, s, 1.0); // Output grayscale depth
                
                int type = ceil(i.type);
                float size = _Size;
                float4 col = _Color;
                switch(ceil(i.type))
                {
                    case 0:
                        col = float4(1, 0.294, 0, 1);
                        size = 600;
                        break;
                    case 1:
                        //col = float4(0, 1, 0, 1);
                        col = float4(1, 0.694, 0.212, 1);
                        size = 10;
                        break;
                }
                /*TracerType TypeInfo = TracerTypes[type];
                float size = TypeInfo.size;
                float4 col = TypeInfo.color * 3.0;*/
                float Alpha = saturate(size / i.depth);
                col *= 3.0;

                return float4(col.rgb, Alpha);
            }
            ENDCG
        }
    }
}
