// Outline kertas untuk SPRITE (NPC). Dipakai lewat InteractableOutline.cs.
//
// Cara kerja: InteractableOutline menggambar quad sedikit lebih besar dari sprite, tepat di belakangnya.
// Di tiap piksel quad, shader ini mencari apakah ada bagian sprite (alpha) dalam radius tertentu
// ("dilate"). Kalau ada, piksel diwarnai krem. Radius digoyang pakai noise supaya tepinya terlihat
// seperti digunting tangan, dan noise-nya berganti beberapa kali per detik (efek stop-motion).
//
// Semua nilai dikirim per sprite lewat MaterialPropertyBlock oleh InteractableOutline.
// Taruh di folder bernama "Resources" supaya ikut ke build.
Shader "Interact/PaperOutlineSprite"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Warna kertas", Color) = (0.96, 0.92, 0.82, 1)
        _RadiusUV ("Radius outline (UV)", Vector) = (0.01, 0.01, 0, 0)
        _ClipRectUV ("Area sprite di texture (UV: xMin yMin xMax yMax)", Vector) = (0, 0, 1, 1)
        _Wobble ("Goyang tepi", Range(0, 1)) = 0.2
        _BoilFps ("Kecepatan goyang (fps, 0 = diam)", Float) = 6
        _Grain ("Butiran kertas", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-1"     // digambar tepat sebelum sprite aslinya (antrian 3000)
        }

        Pass
        {
            Name "SpriteOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Offset 1, 1                   // sedikit ke belakang: kalau sprite menulis depth, outline tidak menimpanya

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _RadiusUV;
                float4 _ClipRectUV;
                float _Wobble;
                float _BoilFps;
                float _Grain;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Hash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash12(i);
                float b = Hash12(i + float2(1, 0));
                float c = Hash12(i + float2(0, 1));
                float d = Hash12(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Alpha sprite di titik uv; di luar area sprite (sprite sheet tetangga) dianggap transparan.
            float SpriteAlpha(float2 uv)
            {
                float2 inside = step(_ClipRectUV.xy, uv) * step(uv, _ClipRectUV.zw);
                float a = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).a;
                return a * inside.x * inside.y;
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Noise berukuran sebanding dengan tebal outline, berganti tiap "tick" (stop-motion).
                float tick = floor(_Time.y * _BoilFps);
                float2 cell = IN.uv / max(_RadiusUV.xy * 2.5, 0.00001);
                float n = ValueNoise(cell + tick * 7.31);
                float radiusScale = 1.0 + _Wobble * (n * 2.0 - 1.0);

                float cover = SpriteAlpha(IN.uv);

                // 3 cincin x 12 titik: cukup halus untuk outline beberapa piksel, tetap murah.
                [unroll]
                for (int ring = 1; ring <= 3; ring++)
                {
                    float r = radiusScale * ring / 3.0;

                    [unroll]
                    for (int k = 0; k < 12; k++)
                    {
                        float angle = (k + 0.5 * (ring & 1)) * (6.2831853 / 12.0);
                        float2 offset = float2(cos(angle), sin(angle)) * _RadiusUV.xy * r;
                        cover = max(cover, SpriteAlpha(IN.uv + offset));
                    }
                }

                float alpha = smoothstep(0.15, 0.55, cover);
                clip(alpha - 0.01);

                float grain = Hash13(float3(floor(IN.positionCS.xy * 0.5), 1.0));
                half3 color = _Color.rgb * (1.0 - _Grain * grain);
                return half4(color, alpha * _Color.a);
            }
            ENDHLSL
        }
    }
}
