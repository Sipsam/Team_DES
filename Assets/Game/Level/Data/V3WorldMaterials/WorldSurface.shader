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
        [Toggle(_MOUNTAIN_LAYERS)] _MountainLayers("Blend mountain rock layers", Float) = 0
        _TopMap("Mountain top", 2D) = "white" {}
        [NoScaleOffset] _TopNormal("Mountain top normal", 2D) = "bump" {}
        _TopTint("Mountain top tint", Color) = (1,1,1,1)
        _TopNormalScale("Mountain top normal strength", Float) = 1
        _BottomMap("Mountain bottom", 2D) = "white" {}
        [NoScaleOffset] _BottomNormal("Mountain bottom normal", 2D) = "bump" {}
        _BottomTint("Mountain bottom tint", Color) = (1,1,1,1)
        _BottomNormalScale("Mountain bottom normal strength", Float) = 1
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
            float _MountainLayers;
            float4 _TopMap_ST;
            float4 _BottomMap_ST;
            half4 _TopTint;
            half4 _BottomTint;
            half _TopNormalScale;
            half _BottomNormalScale;
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
            #pragma shader_feature_local_fragment _MOUNTAIN_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            #if defined(_MOUNTAIN_LAYERS)
            TEXTURE2D(_TopMap); SAMPLER(sampler_TopMap);
            TEXTURE2D(_BottomMap); SAMPLER(sampler_BottomMap);
            TEXTURE2D(_TopNormal); TEXTURE2D(_BottomNormal);
            #endif

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

            #if defined(_MOUNTAIN_LAYERS)
            SurfaceSample ProjectMountain(float3 p, float3 tangent, float3 bitangent, half2 coverage)
            {
                float2 uv = float2(dot(p, tangent), dot(p, bitangent));
                float2 dx = ddx(uv), dy = ddy(uv);
                SurfaceSample result = SampleSurface(TRANSFORM_TEX(uv, _BaseMap), tangent, bitangent);
                result.color *= _BaseColor.rgb;
                if (coverage.x > .002)
                {
                    float2 topUV = TRANSFORM_TEX(uv, _TopMap);
                    half3 color = SAMPLE_TEXTURE2D_GRAD(_TopMap, sampler_TopMap, topUV, dx*_TopMap_ST.xy, dy*_TopMap_ST.xy).rgb * _TopTint.rgb;
                    half2 normal = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_TopNormal, sampler_TopMap, topUV,
                        dx*_TopMap_ST.xy, dy*_TopMap_ST.xy), _TopNormalScale).xy;
                    result.color = lerp(result.color, color, coverage.x);
                    result.normalOffset = lerp(result.normalOffset, normal.x * tangent + normal.y * bitangent, coverage.x);
                }
                if (coverage.y > .002)
                {
                    float2 bottomUV = TRANSFORM_TEX(uv, _BottomMap);
                    half3 color = SAMPLE_TEXTURE2D_GRAD(_BottomMap, sampler_BottomMap, bottomUV,
                        dx*_BottomMap_ST.xy, dy*_BottomMap_ST.xy).rgb * _BottomTint.rgb;
                    half2 normal = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_BottomNormal, sampler_BottomMap, bottomUV,
                        dx*_BottomMap_ST.xy, dy*_BottomMap_ST.xy), _BottomNormalScale).xy;
                    result.color = lerp(result.color, color, coverage.y);
                    result.normalOffset = lerp(result.normalOffset, normal.x * tangent + normal.y * bitangent, coverage.y);
                }
                return result;
            }
            #endif

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
            #if defined(_MOUNTAIN_LAYERS)
                // Wind deposits follow the mountain's aspect and broken seams across the exposed rock.
                float seam = sin(dot(p, float3(.042, .037, .017)) + 1.8 * cos(dot(p.xz, float2(.025, -.043))));
                float drift = .62 * seam + .26 * cos(dot(p, float3(-.061, .012, .089))) +
                    .12 * sin(dot(p, float3(.131, -.08, -.115)));
                float exposure = .31 * normal.y + dot(normal.xz, float2(.22, -.19));
                half snow = smoothstep(.17, .94, exposure + drift*.39 + (p.y-141)*.003) * .80;
                half scree = 1 - smoothstep(22, 140, p.y + drift*31 - (1-normal.y)*16);
                half2 layers = half2(snow, scree);
                float3 warped = p + float3(1.6 * drift, .7 * seam, -1.1 * drift);
                SurfaceSample x = ProjectMountain(warped, float3(0, 0, normal.x < 0 ? 1 : -1), float3(0, 1, 0), layers);
                SurfaceSample y = ProjectMountain(warped, float3(1, 0, 0), float3(0, 0, normal.y < 0 ? 1 : -1), layers);
                SurfaceSample z = ProjectMountain(warped, float3(normal.z < 0 ? -1 : 1, 0, 0), float3(0, 1, 0), layers);
            #else
                SurfaceSample x = Project(p, float3(0, 0, normal.x < 0 ? 1 : -1), float3(0, 1, 0));
                SurfaceSample y = Project(p, float3(1, 0, 0), float3(0, 0, normal.y < 0 ? 1 : -1));
                SurfaceSample z = Project(p, float3(normal.z < 0 ? -1 : 1, 0, 0), float3(0, 1, 0));
            #endif
                surface.color = x.color * weights.x + y.color * weights.y + z.color * weights.z;
            #if defined(_MOUNTAIN_LAYERS)
                half sediment = (1-smoothstep(38,143,p.y+drift*26))*smoothstep(.38,.91,normal.y);
                surface.color *= lerp(half3(1.30,1.29,1.26),half3(1.56,1.37,1.15),sediment) * (1.0 + .19 * drift);
            #endif
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
            #if !defined(_MOUNTAIN_LAYERS)
                surface.color *= _BaseColor.rgb;
            #endif
                half4 color = UniversalFragmentPBR(lighting, surface.color,
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
