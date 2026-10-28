Shader "DesktopDrift/ZoneRing"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 0.5)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Soft ring: peak at 0.42 of the quad half-size, fading to nothing towards the centre and the edge.
                float d = length(i.uv - 0.5) * 2.0;
                float k = saturate(1.0 - abs(d - 0.84) / 0.16);
                half4 c = _BaseColor;
                c.a *= k * k * (3.0 - 2.0 * k);
                return c;
            }
            ENDHLSL
        }
    }
}
