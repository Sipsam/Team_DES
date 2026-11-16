Shader "GameConcept/URP/FacetWaterLava" {
Properties { _Mode("Water 0 Lava 1 Whirlpool 2",Float)=0 }
SubShader { Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" } Cull Off
Pass { Name "ForwardLit" Tags { "LightMode"="UniversalForward" }
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
CBUFFER_START(UnityPerMaterial)
float _Mode;
CBUFFER_END
struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
struct Varyings {float4 positionCS:SV_POSITION;float3 local:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;};
Varyings vert(Attributes v){Varyings o;o.local=v.positionOS.xyz;float amp=_Mode>1.5?.025:.035;v.positionOS.y+=sin(v.positionOS.x*3+_Time.y)*cos(v.positionOS.z*2-_Time.y*.8)*amp;o.world=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(v.normalOS);return o;}
half4 frag(Varyings i):SV_Target {
float2 p=i.local.xz;float r=length(p);float a=atan2(p.y,p.x);float t=_Time.y;half3 albedo,emission=0;half smoothness,metallic=0;
if(_Mode>.5&&_Mode<1.5){float f=abs(sin(p.x*5+sin(p.y*3+t*.13))*sin(p.y*5+cos(p.x*4-t*.1)));float cracks=1-smoothstep(.03,.12,f);albedo=lerp(float3(.11,.025,.012),float3(.65,.1,.005),cracks);emission=float3(1,.24,.008)*cracks*(.8+.2*sin(t+p.x*2));smoothness=.15;}
else {float flow=sin(r*27+a*3-t*2);float foam=_Mode>1.5?smoothstep(.82,.99,flow):smoothstep(.88,1,sin(p.x*12+t)*cos(p.y*9-t*.5));albedo=lerp(float3(.035,.25,.35),float3(.12,.56,.66),saturate(r*.45));albedo=lerp(albedo,float3(.78,.91,.9),foam*.7);smoothness=.55;metallic=.12;}
InputData d=(InputData)0;d.positionWS=i.world;d.normalWS=normalize(i.normal);d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);d.shadowCoord=TransformWorldToShadowCoord(i.world);d.bakedGI=SampleSH(d.normalWS);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
return UniversalFragmentPBR(d,albedo,metallic,half3(.04,.04,.04),smoothness,1,emission,1);
}
ENDHLSL
} } }
