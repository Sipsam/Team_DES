Shader "Twin Edge/V3 World Surface"
{
    Properties
    {
        _BaseMap("Base map", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _BumpMap("Normal map", 2D) = "bump" {}
        _BumpScale("Normal strength", Range(0,2)) = 1
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Occlusion strength", Range(0,1)) = 1
        _ParallaxMap("Source height (unused)", 2D) = "black" {}
        _EmissionMap("Emission", 2D) = "black" {}
        [HDR] _EmissionColor("Emission tint", Color) = (0,0,0,0)
        _Smoothness("Smoothness", Range(0,1)) = 0.15
        [HideInInspector] _Cull("Cull", Float) = 2
        [Toggle(_TRIPLANAR)] _Triplanar("Blend three projections", Float) = 0
        [Toggle(_OCCLUSIONMAP)] _UseOcclusion("Use occlusion map", Float) = 0
        [Toggle(_EMISSION)] _UseEmission("Use emission map", Float) = 0
        [Toggle(_RIVER_FLOW)] _RiverFlow("Use river ribbon UVs", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _BumpScale;
            half _OcclusionStrength;
            half _Smoothness;
            float _Cull;
            float _Triplanar;
            float _UseOcclusion;
            float _UseEmission;
            float _RiverFlow;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma shader_feature_local_fragment _TRIPLANAR
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local _RIVER_FLOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            #if defined(_RIVER_FLOW)
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 fogAndVertexLight : TEXCOORD2;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD3;
            #endif
            #if defined(_RIVER_FLOW)
                float4 tangentWS : TEXCOORD4;
                float2 uv : TEXCOORD5;
            #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogAndVertexLight = half4(ComputeFogFactor(position.positionCS.z),
                    VertexLighting(position.positionWS, output.normalWS));
            #if defined(_RIVER_FLOW)
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz),
                    input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(position);
            #endif
                return output;
            }

            struct SurfaceSample
            {
                half3 color;
                half3 normalOffset;
                half occlusion;
                half3 emission;
            };

            SurfaceSample SampleSurface(float2 uv, float3 tangent, float3 bitangent)
            {
                SurfaceSample result;
                result.color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                result.normalOffset = normalTS.x * tangent + normalTS.y * bitangent;
                result.occlusion = 1;
                result.emission = 0;
            #if defined(_OCCLUSIONMAP)
                result.occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
            #endif
            #if defined(_EMISSION)
                result.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
            #endif
                return result;
            }

            SurfaceSample Project(float3 p, float3 tangent, float3 bitangent)
            {
                float2 uv = float2(dot(p, tangent), dot(p, bitangent));
                return SampleSurface(TRANSFORM_TEX(uv, _BaseMap), tangent, bitangent);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;
                half3 normal = normalize(input.normalWS);
                SurfaceSample surface;
            #if defined(_RIVER_FLOW)
                // V increases upstream. Increasing the sample offset moves the pattern downstream.
                float3 tangent = normalize(input.tangentWS.xyz);
                float3 bitangent = cross(normal, tangent) * input.tangentWS.w;
                float2 flowUV = input.uv + float2(0, frac(_Time.y * .18));
                surface = SampleSurface(flowUV, tangent, bitangent);
                float2 rippleUV = input.uv * 1.7 + float2(.03 * sin(_Time.y), frac(_Time.y * .27));
                half3 ripple = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, rippleUV), .35);
                surface.normalOffset += ripple.x * tangent + ripple.y * bitangent;
            #elif defined(_TRIPLANAR)
                half3 weights = pow(abs(normal), 4);
                weights /= weights.x + weights.y + weights.z;
                SurfaceSample x = Project(p, float3(0, 0, normal.x < 0 ? 1 : -1), float3(0, 1, 0));
                SurfaceSample y = Project(p, float3(1, 0, 0), float3(0, 0, normal.y < 0 ? 1 : -1));
                SurfaceSample z = Project(p, float3(normal.z < 0 ? -1 : 1, 0, 0), float3(0, 1, 0));
                surface.color = x.color * weights.x + y.color * weights.y + z.color * weights.z;
                surface.normalOffset = x.normalOffset * weights.x + y.normalOffset * weights.y + z.normalOffset * weights.z;
                surface.occlusion = dot(half3(x.occlusion, y.occlusion, z.occlusion), weights);
                surface.emission = 0;
            #else
                // Compute a face-aligned frame even on roofs that share smoothed vertex normals.
                float3 face = normalize(cross(ddy(p), ddx(p)));
                face *= dot(face, normal) < 0 ? -1 : 1;
                float3 across = cross(float3(0, 1, 0), face);
                // Keep nearly horizontal surfaces aligned to the world floor axes.
                if (dot(across, across) < .0001) across = float3(1, 0, 0);
                across = normalize(across);
                surface = Project(p, across, cross(face, across));
            #endif
                half3 offset = surface.normalOffset - normal * dot(normal, surface.normalOffset);
                InputData lighting = (InputData)0;
                lighting.positionWS = p;
                lighting.positionCS = input.positionCS;
                lighting.normalWS = normalize(normal + offset);
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(p);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                lighting.shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                lighting.shadowCoord = TransformWorldToShadowCoord(p);
            #endif
                lighting.bakedGI = SampleSH(lighting.normalWS);
                lighting.vertexLighting = input.fogAndVertexLight.yzw;
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half4 color = UniversalFragmentPBR(lighting, surface.color * _BaseColor.rgb,
                    0, 0, _Smoothness, lerp(1, surface.occlusion, _OcclusionStrength),
                    surface.emission * _EmissionColor.rgb, 1);
                color.rgb = MixFog(color.rgb, input.fogAndVertexLight.x);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZWrite On
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
}
