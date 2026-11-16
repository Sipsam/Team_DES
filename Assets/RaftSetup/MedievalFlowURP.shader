Shader "GameConcept/URP/MedievalFlow" {
Properties {_ColorA("Deep Color",Color)=(.1,.4,.6,1) _ColorB("Light Color",Color)=(.3,.8,.9,1)}
SubShader {Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
Pass {Tags {"LightMode"="SRPDefaultUnlit"}
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _ColorA,_ColorB;
CBUFFER_END
struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
half4 frag(Varyings i):SV_Target {float wave=sin(i.uv.x*47+sin(i.uv.y*31)+_Time.y*.8)*sin(i.uv.y*23-_Time.y*.6);return lerp(_ColorA,_ColorB,smoothstep(.1,.8,wave));}
ENDHLSL
} } }
