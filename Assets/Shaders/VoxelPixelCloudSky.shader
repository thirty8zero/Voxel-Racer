Shader "Voxel Racer/Pixel Cloud Sky"
{
    Properties
    {
        _PixelColumns ("Pixel Columns Around Sky", Range(256,2048)) = 768
        _CloudCoverage ("Cloud Coverage", Range(0,1)) = .62
        _CloudDetail ("Cloud Detail", Range(0,1)) = .75
        _Rotation ("Sky Rotation", Range(0,360)) = 0
        _Exposure ("Brightness", Range(.2,2)) = 1
        _Zenith ("Upper Sky", Color) = (.035,.09,.28,1)
        _Blue ("Blue Sky", Color) = (.025,.26,.50,1)
        _Horizon ("Sunset Horizon", Color) = (1,.55,.22,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; };
            struct v2f { float4 pos:SV_POSITION; float3 ray:TEXCOORD0; };
            float _PixelColumns,_CloudCoverage,_CloudDetail,_Rotation,_Exposure;
            float4 _Zenith,_Blue,_Horizon;
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.ray=v.vertex.xyz; return o; }
            float hash(float3 p) { p=frac(p*.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }
            float noise(float3 p)
            {
                float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);
            }
            float fbm(float3 p) { return noise(p)*.54+noise(p*2.07+17)*.27+noise(p*4.13+43)*.13+noise(p*8.31+7)*.06; }
            fixed4 frag(v2f i):SV_Target
            {
                float3 ray=normalize(i.ray);
                float2 uv=float2(atan2(ray.z,ray.x)/6.2831853+.5+_Rotation/360,asin(clamp(ray.y,-1,1))/3.14159265+.5);
                float2 grid=float2(_PixelColumns,_PixelColumns*.5);
                uv=(floor(uv*grid)+.5)/grid;
                float longitude=uv.x*6.2831853, latitude=(uv.y-.5)*3.14159265;
                float h=sin(latitude);
                float3 d=float3(cos(longitude)*cos(latitude),h,sin(longitude)*cos(latitude));
                float level=floor(saturate(h)*32)/32;
                float3 sky=lerp(_Horizon.rgb,float3(.58,.24,.43),saturate(level*7));
                sky=lerp(sky,_Blue.rgb,saturate((level-.12)*3.6));
                sky=lerp(sky,_Zenith.rgb,saturate((level-.42)*1.7));
                // World-direction noise wraps all the way around without a panorama seam.
                float3 p=d*float3(7,12,7);
                float cloud=fbm(p+float3(4,1,9));
                cloud+= (noise(p*5.3)-.5)*_CloudDetail*.18;
                float banks=sin(h*21+noise(d*4)*3)*.04;
                float threshold=lerp(.69,.38,_CloudCoverage)+saturate((h-.7)*2)*.2;
                float mass=saturate((cloud+banks-threshold)*7);
                mass=floor(mass*5)/5;
                float3 shadow=lerp(float3(.57,.23,.40),float3(.13,.35,.57),saturate(h*3));
                float3 light=lerp(float3(1,.69,.32),float3(.20,.67,.76),saturate((h-.07)*3.5));
                float edge=floor(saturate((cloud-threshold)*3+.22+noise(p+float3(0,1.8,0))*.3)*4)/4;
                float3 cloudColour=lerp(shadow,light,edge);
                sky=lerp(sky,cloudColour,mass*.95);
                float star=step(.9987,hash(float3(floor(uv.x*grid.x)%grid.x,floor(uv.y*grid.y),91)))*step(.38,h)*(1-step(.2,mass));
                sky=lerp(sky,float3(.70,.88,.91),star*.85);
                if(h<0) sky=lerp(_Horizon.rgb,float3(.19,.11,.20),saturate(-h*6));
                return float4(sky*_Exposure,1);
            }
            ENDCG
        }
    }
}
