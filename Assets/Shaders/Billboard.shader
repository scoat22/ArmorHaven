Shader "Custom/Billboard"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }// "IgnoreProjector" = "True" } 
        Blend SrcAlpha OneMinusSrcAlpha
        //Cull front 
        //ZWrite On
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float3 pos : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Scale;
            Buffer<float3> _Positions;
            
            v2f vert(appdata_t i, uint InstanceId: SV_InstanceID, uint VertexId : SV_VertexID) {
                v2f o;
                float4 pos = float4(_Positions[InstanceId], 1.0);
                pos = mul(UNITY_MATRIX_V, pos);
                
                //float size = pos.z * _Scale; // This makes it a fixed size on the screen.
                float size = _Scale;
                float3 right = float3(size, 0, 0);
                float3 up    = float3(0, size, 0);

                switch(VertexId)
                {
                    case (0):
                        pos.xyz += right + up;
                        o.uv = 1;
                        break;
                    case (1):
                        pos.xyz += right - up;
                        o.uv = float2(1, 0);
                        break;
                    case (2):
                        pos.xyz += -right - up;
                        o.uv = 0;
                        break;
                    case (3):
                        pos.xyz += -right + up;
                        o.uv = float2(0, 1);
                        break;
                    default: break;
                }
                pos = mul(UNITY_MATRIX_P, float4(pos.xyz, 1.0));
                o.pos = pos;

                // Bias forward in from of triangles, plus a little more if its selected.
                //o.pos.z += _Offset + _Offset * o.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                //col *= _Color;
                //if(col.a < 0.5) discard;
                return col;
            }
            ENDCG
        }
    }
    //Fallback "Diffuse"
}
