Shader "Voxel Racer/Creek Surfaces"
{
    Properties
    {
        [HideInInspector] _CreekReveal ("Creek Reveal", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CreekReveal;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_CreekReveal < 1.0)
                    clip(_CreekReveal <= 0.0 ? -1.0 : _CreekReveal - InterleavedGradientNoise(input.positionCS.xy, 0));
                float3 cell = floor(input.positionWS * 5.0);
                half variation = frac(sin(dot(cell, float3(12.9898, 31.416, 78.233))) * 43758.5453);
                float ripple = sin(cell.x * .68 - _Time.y * 1.4 + sin(cell.z * .88) * .85);
                half highlight = smoothstep(.83, .99, ripple) * .045h * input.color.a;
                half3 color = input.color.rgb * lerp(.92h, 1.08h, variation) +
                    half3(1, .83h, .62h) * highlight;
                real fogFactor = InitializeInputDataFog(float4(input.positionWS, 1.0), 0.0);
                return half4(MixFog(color, fogFactor), 1);
            }
            ENDHLSL
        }
    }
}
