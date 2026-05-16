Shader "Unlit/GrassBladeIndirect"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _TextureStrength("Texture Strength", Range(0,1)) = 0
        _PrimaryCol ("Primary Color", Color) = (1, 1, 1, 1)
        _SecondaryCol ("Secondary Color", Color) = (1, 0, 1, 1)
        _AOColor ("AO Color", Color) = (1, 0, 1, 1)
        _TipColor ("Tip Color", Color) = (0, 0, 1, 1)
        _Scale ("Scale", Range(0.0, 2.0)) = 0.0
        _Cutoff ("Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            StructuredBuffer<float4x4> Matrices;

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _PrimaryCol;
                half4 _SecondaryCol;
                half4 _AOColor;
                half4 _TipColor;
                half _Scale;
                half _Cutoff;
                half _TextureStrength;
            CBUFFER_END

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

                float4x4 instanceMatrix = Matrices[instanceID];
                float3 positionWS = mul(instanceMatrix, float4(IN.positionOS, 1.0)).xyz;
                float3 normalWS = normalize(mul((float3x3)instanceMatrix, IN.normalOS));

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionWS);

                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.positionCS = positionInputs.positionCS;
                OUT.positionWS = positionWS;
                OUT.normalWS = normalWS;
                OUT.shadowCoord = GetShadowCoord(positionInputs);
                OUT.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(albedo.a - _Cutoff);

                half4 baseCol = lerp(_PrimaryCol, _SecondaryCol, IN.uv.y);
                half4 ao = lerp(_AOColor, half4(1, 1, 1, 1), IN.uv.y);
                half4 tip = lerp(half4(0, 0, 0, 0), _TipColor, IN.uv.y * IN.uv.y * (1.0h + _Scale));
                half3 surfaceColor = lerp((baseCol + tip).rgb, albedo.rgb, _TextureStrength) * ao.rgb;

                half3 normalWS = normalize(IN.normalWS);
                Light mainLight = GetMainLight(IN.shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 diffuse = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation * ndotl);

                half3 color = surfaceColor * (ambient + diffuse);
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
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            StructuredBuffer<float4x4> Matrices;

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

                float4x4 instanceMatrix = Matrices[instanceID];
                float3 positionWS = mul(instanceMatrix, float4(IN.positionOS, 1.0)).xyz;

                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.positionCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(albedo.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
