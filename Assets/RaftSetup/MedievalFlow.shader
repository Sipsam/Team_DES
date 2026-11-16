Shader "GameConcept/MedievalFlow"
{
    Properties
    {
        _ColorA("Deep Color",Color)=(.1,.4,.6,1)
        _ColorB("Light Color",Color)=(.3,.8,.9,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct Output {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
            fixed4 _ColorA,_ColorB;
            Output vert(Input v){Output o;o.position=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(Output i):SV_Target
            {
                float wave=sin(i.uv.x*47+sin(i.uv.y*31)+_Time.y*.8)*sin(i.uv.y*23-_Time.y*.6);
                return lerp(_ColorA,_ColorB,smoothstep(.1,.8,wave));
            }
            ENDCG
        }
    }
}
