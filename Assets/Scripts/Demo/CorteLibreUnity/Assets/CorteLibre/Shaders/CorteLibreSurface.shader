Shader "CorteLibre/UnlitSurface"
{
    Properties { _Color ("Color", Color) = (0.78,0.38,0.43,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Off
            ZWrite On
            CGPROGRAM
            #pragma vertex VertexMain
            #pragma fragment FragmentMain
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct InputVertex
            {
                float4 position : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct PixelInput
            {
                float4 position : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            fixed4 _Color;
            PixelInput VertexMain(InputVertex v)
            {
                PixelInput o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position=UnityObjectToClipPos(v.position);
                return o;
            }
            fixed4 FragmentMain(PixelInput i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return _Color;
            }
            ENDCG
        }
    }
    FallBack Off
}
