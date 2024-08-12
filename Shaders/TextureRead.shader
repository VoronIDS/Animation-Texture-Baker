Shader "Unlit/TextureRead"
{
    Properties
    {
        _Min("Min Vector", Vector) = (0, 0, 0, 0)
        _Max("Max Vector", Vector) = (0, 0, 0, 0)
        _MainTex ("Texture", 2D) = "white" {}
        _PosTex("Position Texture", 2D) = "black" {}
        _NmlTex("Normal Texture", 2D) = "white" {}
        _Scale("Scale", Range(0, 5)) = 1
        _AnimTime("Animation Time", Range(0,1)) = 0

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        LOD 100
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            #define ts _PosTex_TexelSize

            struct appdata
            {
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex, _PosTex, _NmlTex;
            float4 _PosTex_TexelSize;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimTime)
                UNITY_DEFINE_INSTANCED_PROP(float, _Scale)
                UNITY_DEFINE_INSTANCED_PROP(Vector, _Min)
                UNITY_DEFINE_INSTANCED_PROP(Vector, _Max)
            UNITY_INSTANCING_BUFFER_END(Props)

            float3 Denormalize(float3 normalizedValue, float3 min, float3 max)
            {
                return normalizedValue * (max - min) + min;
            }

            v2f vert(appdata v, uint vid : SV_VertexID)
            {
               UNITY_SETUP_INSTANCE_ID(v);

                float animTime = UNITY_ACCESS_INSTANCED_PROP(Props, _AnimTime);

                float x = (vid + 0.5) * _PosTex_TexelSize.x;

                float4 uv = float4(x, animTime,0,0);

                float4 position = tex2Dlod(_PosTex, uv);

                float3 normalTemp = tex2Dlod(_NmlTex, uv);

                float3 normal = float3(normalTemp.x, normalTemp.y, position.a) * 2 - 1;

                float3 pos = Denormalize(position.xyz, 
                    UNITY_ACCESS_INSTANCED_PROP(Props, _Min).xyz,  
                    UNITY_ACCESS_INSTANCED_PROP(Props, _Max)) 
                    * UNITY_ACCESS_INSTANCED_PROP(Props, _Scale);

                v2f o;
                o.vertex = UnityObjectToClipPos(float4(pos, 1.0));
                o.normal = UnityObjectToWorldNormal(normal);
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half diff = dot(i.normal, float3(0, 1, 0)) * 0.5 + 0.5;

                half4 col = tex2D(_MainTex, i.uv);

                return diff * col;
            }
            ENDCG
        }
    }
}
