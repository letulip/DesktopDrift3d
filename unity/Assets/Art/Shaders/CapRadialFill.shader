Shader "DesktopDrift/CapRadialFill"
{
    Properties
    {
        _BaseColor ("Ring", Color) = (1, 0.6, 0.6, 0.6)
        _FillColor ("Fill", Color) = (1, 0.231, 0.231, 0.95)
        _Sweep ("Sweep", Range(0, 1)) = 0
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
                half4 _FillColor;
                float _Sweep;
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
                float2 p = i.uv - 0.5;
                if (length(p) > 0.5) discard;
                // Clockwise from "north" as seen from above (uv y is up on the table); 2D: arc(0, 0, r, -PI/2, -PI/2 + sweep).
                float t = frac((1.57079632679 - atan2(p.y, p.x)) / 6.28318530718);
                return t < _Sweep ? _FillColor : _BaseColor;
            }
            ENDHLSL
        }
    }
}
