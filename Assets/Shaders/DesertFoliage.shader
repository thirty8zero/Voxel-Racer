Shader "Voxel Racer/Desert Foliage"
{
    Properties
    {
        [MainTexture] _BaseMap("Foliage Atlas", 2D) = "white" {}
        [MainColor] _Tint("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = .45
        _FadeStart("Fade Start", Float) = 110
        _DrawDistance("Draw Distance", Float) = 150
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Cutoff, _FadeStart, _DrawDistance;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 colour : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 light : TEXCOORD1; half fog : TEXCOORD2; float distance : TEXCOORD3; half4 colour : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world); output.uv = input.uv;
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                Light sun = GetMainLight();
                output.light = half3(.42,.42,.42) + sun.color * (.35 + .25 * abs(dot(normal, sun.direction)));
                output.fog = ComputeFogFactor(output.positionCS.z); output.distance = distance(world, _WorldSpaceCameraPos);
                output.colour = input.colour;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 plant = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(plant.a - _Cutoff);
                float fade = saturate((_DrawDistance - input.distance) / max(1, _DrawDistance - _FadeStart));
                float dither = frac(52.9829189 * frac(dot(floor(input.positionCS.xy), float2(.06711056,.00583715))));
                clip(fade - dither);
                half3 colour = plant.rgb * _Tint.rgb * input.colour.rgb * input.light;
                return half4(MixFog(colour, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
