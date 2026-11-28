// Sprite UI dengan tepi yang memudar lembut (alpha fade) dan, kalau mau, sedikit "sobek" bergerak
// seperti lingkaran di referensi. Dibuat untuk 4 segmen roda (Button_OBS / KNO / ACT / SIL).
//
// Cara kerja: alpha di sekitar tepi bentuk dirata-rata dari 12 titik sampel dalam cakram kecil
// (hasilnya gradasi 0..1 melintasi tepi), lalu dipertajam lagi dengan smoothstep. Sampel di luar
// gambar dianggap transparan, jadi tepi yang menempel ke batas gambar pun ikut halus.
//
// Pakai di Image UI biasa (Source Image = sprite segmen). Tint dari Image.color dan ColorBlock Button
// (hover / pressed / disabled) tetap jalan. Kompatibel dengan Mask / RectMask2D / CanvasGroup alpha.
// Jangan masukkan sprite ini ke Sprite Atlas (UV harus 0..1 per sprite).
Shader "UI/SoftEdgeSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Tepi lembut)]
        _Feather ("Lebar fade (fraksi sisi terpendek sprite)", Range(0, 0.2)) = 0.04
        _EdgeLow ("Batas bawah (alpha mulai muncul)", Range(0, 0.5)) = 0.1
        _EdgeHigh ("Batas atas (alpha penuh)", Range(0.5, 1)) = 0.9

        [Header(Tepi sobek)]
        _Ragged ("Kekuatan sobek (0 = rata)", Range(0, 1)) = 0.35
        _NoiseScale ("Kerapatan sobek", Range(1, 30)) = 8
        _NoiseSpeed ("Kecepatan gerak (0 = diam)", Range(0, 1)) = 0.12

        // --- standar UI (biar Mask / RectMask2D jalan) ---
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #define TAPS 12

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 uv            : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;      // (1/w, 1/h, w, h)
            fixed4 _Color;
            float4 _ClipRect;

            float _Feather, _EdgeLow, _EdgeHigh;
            float _Ragged, _NoiseScale, _NoiseSpeed;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.uv = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // ---------- noise ----------
            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm2(float2 p)
            {
                float v = 0.5 * vnoise(p);
                p = p * 2.03 + float2(17.1, 9.2);
                v += 0.25 * vnoise(p);
                return v / 0.75;             // 0..1
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);

                // 1 satuan = sisi terpendek sprite. Offset diubah ke UV supaya cakram blur bulat di layar
                // (mengandaikan rasio rect Image = rasio gambar, yaitu Preserve Aspect / ukuran yang sama).
                float2 texSize = max(_MainTex_TexelSize.zw, float2(1, 1));
                float2 toUV = min(texSize.x, texSize.y) / texSize;
                float2 radius = _Feather * toUV;

                float4 center = tex2Dgrad(_MainTex, uv, dx, dy);
                float aSum = center.a;
                float3 rgbSum = center.rgb * center.a;

                [unroll]
                for (int i = 0; i < TAPS; i++)
                {
                    float ang = (float)i * 2.39996323;                  // sudut emas
                    float rad = sqrt(((float)i + 0.5) / (float)TAPS);   // sebaran merata di cakram
                    float2 tuv = uv + float2(cos(ang), sin(ang)) * rad * radius;

                    float4 s = tex2Dgrad(_MainTex, tuv, dx, dy);
                    float inside = step(0.0, tuv.x) * step(tuv.x, 1.0) * step(0.0, tuv.y) * step(tuv.y, 1.0);
                    s *= inside;                                        // di luar gambar = transparan

                    aSum += s.a;
                    rgbSum += s.rgb * s.a;
                }

                float coverage = aSum / (float)(TAPS + 1);              // 0 di luar, 1 di dalam, gradasi di tepi

                // Tepi sobek: noise hanya menggeser area gradasi (tidak melubangi bagian dalam).
                float2 np = uv * (texSize / min(texSize.x, texSize.y)) * _NoiseScale;
                float t = _Time.y * _NoiseSpeed;
                float n = fbm2(np + float2(t, -t * 0.7));
                float edgeness = 4.0 * coverage * (1.0 - coverage);     // 1 di tengah tepi, 0 di dalam/luar
                coverage = saturate(coverage + (n - 0.5) * 2.0 * _Ragged * edgeness);

                float a = smoothstep(_EdgeLow, max(_EdgeHigh, _EdgeLow + 0.001), coverage);

                // Warna: asli (tajam) di dalam bentuk, warna tetangga di zona fade supaya tidak jadi hitam.
                float3 blurRGB = rgbSum / max(aSum, 1e-4);
                float3 rgb = lerp(blurRGB, center.rgb, saturate(center.a * 2.0));

                fixed4 col = fixed4(rgb, a) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
        ENDCG
        }
    }
}
