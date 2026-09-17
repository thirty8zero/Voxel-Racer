Shader "Voxel Racer/Mountain Card"
{
 Properties { _BaseMap("Mountain Artwork",2D)="white" {} _Cutoff("Alpha Cutoff",Range(0,1))=.5 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
  Cull Off ZWrite On
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   float _Cutoff;
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
   Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
   half4 frag(Varyings i):SV_Target
   {
    // Alternate mirrored strips so both joins meet the identical edge of the artwork.
    float u=1-abs(frac(i.uv.x)*2-1);
    half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,float2(u,i.uv.y));
    clip(c.a-_Cutoff);
    // Reject bright pure-red matte remnants without recolouring the red rock artwork.
    if(c.r>.65 && c.g<.035 && c.b<.035) discard;
    return half4(c.rgb,1);
   }
   ENDHLSL
  }
 }
}
