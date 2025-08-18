Shader "Custom/Tracer"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Size("Size", Float) = 200
        _ObserverPosition("Observer Position", Vector) = (0,0,0,0)
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
                float4 position : POSITION;
                float3 normal : NORMAL;
                float type : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 normal : NORMAL;
                float depth : TEXCOORD0;
                float type : TEXCOORD1;
                float3 WorldPos : TEXCOORD2;
            };

            float4 _Color;
            float _Size;
            float3 _ObserverPosition;

            struct TracerType
            {
                float4 color;
                float size;
            };
            StructuredBuffer<TracerType> TracerTypes;

            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.position).xyz;
                float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz;

                o.position = UnityObjectToClipPos(v.position);
                o.normal = v.normal;
                o.type = v.type;
                o.depth = -viewPos.z; // View space Z is negative in front of camera
                o.WorldPos = worldPos;
                //if(o.type == 1.0) o.vertex.y += _Size;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                if(i.position.x == 0 && i.position.y == 0 && i.position.z == 0) discard;
                float FarPlane = _ProjectionParams.z; // Essentially the furthest value possible.
                //float linearDepth = i.depth / FarPlane; // Normalize depth (0 to 1)
                float s = i.depth;
                //return float4(s, s, s, 1.0); // Output grayscale depth
                
                int type = ceil(i.type);
                float size = _Size;
                float4 col = _Color;
                float Brightness = 5.0;
                switch(ceil(i.type))
                {
                    case 0: // Large bullet
                        col = float4(1, 0.294, 0, 1);
                        size = 88;
                        Brightness = 10;
                        break;
                    case 1: // Small bullet
                        //col = float4(0, 1, 0, 1);
                        col = 0;
                        size = 0;
                        break;
                    case 2: // Small tracer.
                        col = float4(1, 0.694, 0.212, 1);
                        size = 20;
                        break;
                }
                /*TracerType TypeInfo = TracerTypes[type];
                float size = TypeInfo.size;
                float4 col = TypeInfo.color * 3.0;*/

                float magnitude = length(i.normal);
                //_ObserverPosition = UNITY_MATRIX_IT_MV[2].xyz; // Camera forward.
                float3 S = -normalize(i.WorldPos - _ObserverPosition);
                float NdotS = dot(S, i.normal / magnitude); // Bullet to Ship.
                float NdotV = dot( UNITY_MATRIX_IT_MV[2].xyz, i.normal / magnitude);
                float RedShift = clamp(1 - (NdotS + 1) * 0.5, 0, 0.8);
                col = lerp(col, float4(1, 0, 0, 1), RedShift);
                
                
                //Brightness *= RedShift;

                float Alpha = saturate(size / i.depth);
                // Make more transparent the faster the bullet is going (linear relationship).
                //Alpha *= 10.0 / magnitude;

                //Alpha -= (1.0 - abs(NdotV)) * 0.5;

                // Make bullets easier to see as they're coming towards you.
                Brightness += abs(NdotV) * 5.0;

                // Brighten values
                col *= Brightness;

                return float4(col.rgb, saturate(Alpha));
            }
            ENDCG
        }
    }
}
