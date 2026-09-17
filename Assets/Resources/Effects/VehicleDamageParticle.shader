Shader "Voxel Racer/Vehicle Damage Particle"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.color=v.color;o.uv=v.uv;return o; }
            half4 frag(Varyings i):SV_Target
            {
                // Stepped puff silhouette retains the game's voxel style.
                float2 p=abs((floor(i.uv*8)+.5)/8-.5)*2;
                clip(.95-dot(p,p));
                return i.color;
            }
            ENDHLSL
        }
    }
}
