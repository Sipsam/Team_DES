Shader "GameConcept/FacetWaterLava" {
Properties{_Mode("Water 0 Lava 1 Whirlpool 2",Float)=0}
SubShader{Tags{"RenderType"="Opaque"} Cull Off
CGPROGRAM
#pragma surface surf Standard vertex:vert
#pragma target 3.0
float _Mode;
struct Input{float3 local;};
void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.local=v.vertex.xyz;float amp=_Mode>1.5?.025:.035;v.vertex.y+=sin(v.vertex.x*3+_Time.y)*cos(v.vertex.z*2-_Time.y*.8)*amp;}
void surf(Input i,inout SurfaceOutputStandard o){float2 p=i.local.xz;float r=length(p);float a=atan2(p.y,p.x);float t=_Time.y;
if(_Mode>.5&&_Mode<1.5){float f=abs(sin(p.x*5+sin(p.y*3+t*.13))*sin(p.y*5+cos(p.x*4-t*.1)));float cracks=1-smoothstep(.03,.12,f);o.Albedo=lerp(float3(.11,.025,.012),float3(.65,.1,.005),cracks);o.Emission=float3(1,.24,.008)*cracks*(.8+.2*sin(t+p.x*2));o.Smoothness=.15;}
else{float flow=sin(r*27+a*3-t*2);float foam=_Mode>1.5?smoothstep(.82,.99,flow):smoothstep(.88,1,sin(p.x*12+t)*cos(p.y*9-t*.5));o.Albedo=lerp(float3(.035,.25,.35),float3(.12,.56,.66),saturate(r*.45));o.Albedo=lerp(o.Albedo,float3(.78,.91,.9),foam*.7);o.Smoothness=.55;o.Metallic=.12;}}
ENDCG
}Fallback "Standard"}
