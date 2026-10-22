Shader "VoxelRacer/Night Headlight Beam"
{
    Properties { _BeamColour("Beam Colour", Color) = (1, .82, .12, .22) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            UNITY_INSTANCING_BUFFER_START(Beam)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BeamColour)
            UNITY_INSTANCING_BUFFER_END(Beam)
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 colour = UNITY_ACCESS_INSTANCED_PROP(Beam, _BeamColour);
                half width = lerp(.025h, 1.h, input.uv.y);
                half edge = 1.h - smoothstep(width * .55h, width, abs(input.uv.x * 2.h - 1.h));
                half lengthFade = smoothstep(0.h, .035h, input.uv.y) * (1.h - smoothstep(.55h, 1.h, input.uv.y));
                half3 rgb = MixFogColor(colour.rgb, half3(0, 0, 0), input.fog);
                return half4(rgb, colour.a * edge * lengthFade);
            }
            ENDHLSL
        }
    }
}
