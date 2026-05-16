Shader "Custom/Triplanar"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _SliceRange ("Slices", Range(0,16)) = 6
        _MainTextures ("Albedo Textures", 2DArray) = "" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _MapScale ("Tiling Scale", Float) = 1
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        [PerRendererData]_DirectionMask ("Direction mask", Int) = 0
        [PerRendererData]_WorldPos ("WorldPos", Vector) = (0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma require 2darray
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 altPositionOS : TANGENT;
                int nearMask : COLOR;
                float textureIndex : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float textureIndex : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Glossiness;
                half _Metallic;
                half _MapScale;
                half _BumpScale;
                float _SliceRange;
                int _DirectionMask;
                float4 _WorldPos;
            CBUFFER_END

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D_ARRAY(_MainTextures);
            SAMPLER(sampler_MainTextures);

            float3 SelectPositionOS(float3 positionOS, float3 altPositionOS, int nearMask)
            {
                return ((nearMask & _DirectionMask) == nearMask) ? altPositionOS : positionOS;
            }

            float3 GetBlendWeights(float3 normalWS)
            {
                float3 weights = abs(normalWS);
                return weights / max(dot(weights, 1.0), 1e-5);
            }

            half3 SampleTriplanarAlbedo(float3 positionWS, float3 normalWS, float textureIndex)
            {
                float3 blend = GetBlendWeights(normalWS);
                float slice = textureIndex * _SliceRange;

                float2 uvX = positionWS.yz * _MapScale;
                float2 uvY = positionWS.zx * _MapScale;
                float2 uvZ = positionWS.xy * _MapScale;

                half3 x = SAMPLE_TEXTURE2D_ARRAY(_MainTextures, sampler_MainTextures, uvX, slice).rgb;
                half3 y = SAMPLE_TEXTURE2D_ARRAY(_MainTextures, sampler_MainTextures, uvY, slice).rgb;
                half3 z = SAMPLE_TEXTURE2D_ARRAY(_MainTextures, sampler_MainTextures, uvZ, slice).rgb;

                return (x * blend.x + y * blend.y + z * blend.z) * _Color.rgb;
            }

            half3 SampleTriplanarNormalWS(float3 positionWS, float3 normalWS)
            {
                float3 blend = GetBlendWeights(normalWS);
                float3 normalSign = sign(normalWS);

                float2 uvX = positionWS.yz * _MapScale;
                float2 uvY = positionWS.zx * _MapScale;
                float2 uvZ = positionWS.xy * _MapScale;

                half3 normalX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX), _BumpScale);
                half3 normalY = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY), _BumpScale);
                half3 normalZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ), _BumpScale);

                half3 worldX = half3(normalSign.x * normalX.z, normalX.y, normalX.x);
                half3 worldY = half3(normalY.x, normalSign.y * normalY.z, normalY.y);
                half3 worldZ = half3(normalZ.x, normalZ.y, normalSign.z * normalZ.z);

                return normalize(worldX * blend.x + worldY * blend.y + worldZ * blend.z);
            }

            half3 EvaluateLight(Light light, half3 albedo, half3 normalWS, half3 viewDirectionWS)
            {
                half attenuation = light.distanceAttenuation * light.shadowAttenuation;
                half3 diffuse = LightingLambert(light.color * attenuation, light.direction, normalWS) * albedo;
                half3 specular = LightingSpecular(
                    light.color * attenuation,
                    light.direction,
                    normalWS,
                    viewDirectionWS,
                    half4(_Metallic.xxx, 1.0h),
                    saturate(_Glossiness)
                );

                return diffuse + specular;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 selectedPositionOS = SelectPositionOS(IN.positionOS, IN.altPositionOS, IN.nearMask);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(selectedPositionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionWS = positionInputs.positionWS;
                OUT.positionCS = positionInputs.positionCS;
                OUT.normalWS = normalize(normalInputs.normalWS);
                OUT.textureIndex = IN.textureIndex;
                OUT.shadowCoord = GetShadowCoord(positionInputs);
                OUT.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = SampleTriplanarNormalWS(IN.positionWS, normalize(IN.normalWS));
                half3 albedo = SampleTriplanarAlbedo(IN.positionWS, normalWS, IN.textureIndex);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));

                Light mainLight = GetMainLight(IN.shadowCoord);
                half3 color = SampleSH(normalWS) * albedo;
                color += EvaluateLight(mainLight, albedo, normalWS, viewDirectionWS);

                #if defined(_ADDITIONAL_LIGHTS)
                uint additionalLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < additionalLightCount; ++lightIndex)
                {
                    Light additionalLight = GetAdditionalLight(lightIndex, IN.positionWS);
                    color += EvaluateLight(additionalLight, albedo, normalWS, viewDirectionWS);
                }
                #endif

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 altPositionOS : TANGENT;
                int nearMask : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                int _DirectionMask;
            CBUFFER_END

            float3 SelectPositionOS(float3 positionOS, float3 altPositionOS, int nearMask)
            {
                return ((nearMask & _DirectionMask) == nearMask) ? altPositionOS : positionOS;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 selectedPositionOS = SelectPositionOS(IN.positionOS, IN.altPositionOS, IN.nearMask);
                OUT.positionCS = TransformObjectToHClip(selectedPositionOS);
                return OUT;
            }

            half4 frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Back
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 altPositionOS : TANGENT;
                int nearMask : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                int _DirectionMask;
            CBUFFER_END

            float3 SelectPositionOS(float3 positionOS, float3 altPositionOS, int nearMask)
            {
                return ((nearMask & _DirectionMask) == nearMask) ? altPositionOS : positionOS;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 selectedPositionOS = SelectPositionOS(IN.positionOS, IN.altPositionOS, IN.nearMask);
                OUT.positionCS = TransformObjectToHClip(selectedPositionOS);
                return OUT;
            }

            half4 frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
