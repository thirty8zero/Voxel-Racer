Shader "Voxel Racer/Ground Pixel Noise"
{
    Properties
    {
        _BaseColor ("Base Colour", Color) = (0.31, 0.18, 0.07, 1)
        _PixelSize ("Pixel Size", Float) = 0.75
        _NoiseDensity ("Noise Density", Range(0, 1)) = 0.6
        _ColourVariation ("Colour Variation", Range(0, 0.5)) = 0.1
        _NoiseSeed ("Noise Seed", Float) = 317
        [HideInInspector] _CreekReveal ("Creek Reveal", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _PixelSize;
                float _NoiseDensity;
                float _ColourVariation;
                float _NoiseSeed;
                float _CreekReveal;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32 + _NoiseSeed * 0.013);
                return frac(value.x * value.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_CreekReveal > 0.0)
                    clip(InterleavedGradientNoise(input.positionCS.xy, 0) - _CreekReveal);
                float pixelSize = max(0.05, _PixelSize);
                float2 cell = floor(input.positionWS.xz / pixelSize);
                float selection = Hash21(cell + _NoiseSeed);
                float shadeHash = Hash21(cell.yx + _NoiseSeed * 2.37);
                float selected = step(1.0 - saturate(_NoiseDensity), selection);
                float shade = 1.0 + (shadeHash * 2.0 - 1.0) * _ColourVariation * selected;
                half3 colour = _BaseColor.rgb * shade;
                // Streaming changes the ground's triangulation. Fog must depend on the
                // pixel's depth so identical terrain renders the same across mesh seams.
                real fogFactor = InitializeInputDataFog(float4(input.positionWS, 1.0), 0.0);
                colour = MixFog(colour, fogFactor);
                return half4(colour, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
