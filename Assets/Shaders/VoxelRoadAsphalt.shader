Shader "Voxel Racer/Road Asphalt"
{
    Properties
    {
        _BaseColor ("Asphalt Colour", Color) = (.29,.29,.32,1)
        _Variation ("Patch Contrast", Range(0,.35)) = .14
        _PatchSize ("Patch Width / Length (m)", Vector) = (.65,7,0,0)
        _RoadCoordinates ("Road Width / Length / Distance", Vector) = (16,30,0,0)
        [HideInInspector] _UseRoadUV ("Use sampled road coordinates", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Asphalt"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 road : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
            };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Variation;
                float4 _PatchSize;
                float4 _RoadCoordinates;
                float _UseRoadUV;
            CBUFFER_END
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34,456.21));
                p += dot(p,p + 45.32);
                return frac(p.x * p.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.road = lerp(input.positionOS.xz * _RoadCoordinates.xy + float2(0,_RoadCoordinates.z), input.uv, _UseRoadUV);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.fog = ComputeFogFactor(pos.positionCS.z);
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 patch = input.road / max(_PatchSize.xy,float2(.1,.5));
                // Stagger strip endings so patches never form a repeated cross-road seam.
                patch.y += Hash(float2(floor(patch.x),13)) * 8;
                float shade = (Hash(floor(patch)) * 2 - 1) * _Variation;
                shade += (Hash(floor(input.road / float2(.12,.3))) - .5) * .025;
                half3 normal = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 illumination = SampleSH(normal) + light.color * saturate(dot(normal,light.direction)) * light.shadowAttenuation;
                half3 colour = _BaseColor.rgb * (1 + shade) * illumination;
                return half4(MixFog(colour,input.fog),_BaseColor.a);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
