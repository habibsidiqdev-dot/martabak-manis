Shader "Custom/GlassURP"
{
    Properties
    {
        _Color ("Glass Tint", Color) = (1, 1, 1, 0.1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _RefractionStrength ("Refraction Strength", Range(0, 0.1)) = 0.02
        _FresnelPower ("Fresnel Power", Range(0.5, 10)) = 3.0
        _ReflectionIntensity ("Reflection Intensity", Range(0, 1)) = 0.5
        _NormalMap ("Normal Map (optional)", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.3
        _Thickness ("Glass Thickness", Range(0, 1)) = 0.1
        _EdgeTint ("Edge Tint (Fresnel Color)", Color) = (0.8, 0.95, 1.0, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Pass 1: Copy screen behind object (opaque texture) ──────────────
        Pass
        {
            Name "GrabPass"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZWrite Off
            ColorMask 0
        }

        // ── Pass 2: Glass surface ───────────────────────────────────────────
        Pass
        {
            Name "GlassSurface"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Smoothness;
                float  _Metallic;
                float  _RefractionStrength;
                float  _FresnelPower;
                float  _ReflectionIntensity;
                float  _NormalStrength;
                float  _Thickness;
                float4 _EdgeTint;
                float4 _NormalMap_ST;
            CBUFFER_END

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 tangentWS   : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float2 uv          : TEXCOORD4;
                float4 screenPos   : TEXCOORD5;
                float  fogFactor   : TEXCOORD6;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = nrmInputs.normalWS;
                OUT.tangentWS   = nrmInputs.tangentWS;
                OUT.bitangentWS = nrmInputs.bitangentWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _NormalMap);
                OUT.screenPos   = ComputeScreenPos(posInputs.positionCS);
                OUT.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ── Normal (tangent space) ──────────────────────────────────
                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv));
                normalTS.xy    *= _NormalStrength;
                float3 N = normalize(
                    normalTS.x * IN.tangentWS +
                    normalTS.y * IN.bitangentWS +
                    normalTS.z * IN.normalWS
                );

                // ── View & Fresnel ──────────────────────────────────────────
                float3 V       = normalize(GetCameraPositionWS() - IN.positionWS);
                float  NdotV   = saturate(dot(N, V));
                float  fresnel = pow(1.0 - NdotV, _FresnelPower);

                // ── Screen UV + refraction distortion ──────────────────────
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float2 distort  = N.xy * _RefractionStrength;
                float2 refractUV = screenUV + distort;
                half3  bgColor   = SampleSceneColor(refractUV);

                // ── Main light specular ─────────────────────────────────────
                Light mainLight  = GetMainLight();
                float3 L         = normalize(mainLight.direction);
                float3 H         = normalize(V + L);
                float  spec      = pow(saturate(dot(N, H)), lerp(16, 512, _Smoothness));
                half3  specColor = mainLight.color * spec * _ReflectionIntensity;

                // ── Compose ─────────────────────────────────────────────────
                // Base: background seen through glass (refracted)
                half3 glassColor = bgColor;

                // Tint the glass slightly
                glassColor = lerp(glassColor, glassColor * _Color.rgb, _Color.a);

                // Add fresnel edge tint
                glassColor = lerp(glassColor, _EdgeTint.rgb, fresnel * 0.6);

                // Add specular highlight on top
                glassColor += specColor;

                // Alpha: more opaque at glancing angles (fresnel)
                float alpha = lerp(_Color.a + 0.05, 0.6, fresnel) + spec * 0.5;
                alpha = saturate(alpha);

                half4 finalColor = half4(glassColor, alpha);
                finalColor.rgb = MixFog(finalColor.rgb, IN.fogFactor);
                return finalColor;
            }
            ENDHLSL
        }

        // ── Shadow caster (glass still casts subtle shadow) ─────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // ShadowCasterPass uses Shadows.hlsl, which calls LerpWhiteTo.
            // Include its definition explicitly for this independently compiled pass.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
