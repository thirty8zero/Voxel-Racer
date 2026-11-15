Shader "VoxelRacer/Carrier Bay Darkness"
{
    Properties
    {
        _Tint("Tint (Alpha = Darkness)", Color) = (0, 0, 0, .985)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }

        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half opacity : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // Each slice contributes an equal fraction of the full bay's optical depth.
                output.opacity = 1.0 - pow(1.0 - saturate(_Tint.a), input.uv.x);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(_Tint.rgb, input.opacity);
            }
            ENDHLSL
        }
    }
}
