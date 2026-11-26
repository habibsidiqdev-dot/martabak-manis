// Bar Resonansi berbentuk cincin bulat, dibuat penuh di shader (tanpa sprite).
//
// Lapisan dari paling bawah:
//   1. Core   : lingkaran gelap tipis di dalam cincin (supaya teks "0 / 9" tetap terbaca)
//   2. Track  : dasar cincin yang kosong, transparan
//   3. Aura   : cahaya yang mengikuti bagian yang terisi (bergetar, berdenyut)
//   4. Fill   : isi hitam dengan asap/uap yang berputar pelan
//   5. Tip    : titik cahaya di ujung isi
//   6. Garis  : satu garis pemisah per resonansi (jumlahnya = _Segments)
//
// Pakai di Image UI (Source Image KOSONG, Image harus persegi). Isi _Fill dan _Segments lewat ResonanceRing.cs.
// Kompatibel dengan Mask / RectMask2D / CanvasGroup alpha.
Shader "UI/ResonanceRing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture (tidak dipakai)", 2D) = "white" {}
        _Color ("Tint (alpha = kecerahan total)", Color) = (1,1,1,1)

        [Header(Isi)]
        _Fill ("Fill 0..1 (diisi script)", Range(0, 1)) = 0.35
        _Segments ("Jumlah garis (diisi script)", Float) = 5

        [Header(Bentuk cincin, relatif terhadap setengah lebar Image)]
        _OuterRadius ("Radius luar", Range(0.2, 1)) = 0.78
        _InnerRadius ("Radius dalam (0 = lingkaran penuh)", Range(0, 0.98)) = 0.62

        [Header(Warna)]
        _CoreColor ("Core (di dalam cincin)", Color) = (0.04, 0.04, 0.06, 0.55)
        _TrackColor ("Track (dasar kosong)", Color) = (1, 1, 1, 0.10)
        _TrackRimColor ("Garis tepi track", Color) = (1, 1, 1, 0.22)
        _FillColor ("Fill (hitam)", Color) = (0.012, 0.012, 0.02, 1)
        _WispColor ("Asap di dalam fill", Color) = (0.55, 0.32, 0.85, 1)
        _AuraColor ("Aura", Color) = (1, 0.74, 0.30, 1)

        [Header(Aura dan gerak)]
        _AuraSize ("Jangkauan aura", Range(0.01, 0.3)) = 0.09
        _AuraStrength ("Kuat aura", Range(0, 3)) = 1.3
        _Wobble ("Getar tepi aura", Range(0, 0.15)) = 0.05
        _Pulse ("Denyut aura", Range(0, 1)) = 0.25
        _FlowSpeed ("Kecepatan putaran asap", Range(0, 2)) = 0.35
        _NoiseScale ("Kerapatan asap", Range(0.5, 8)) = 3
        _TipGlow ("Cahaya ujung fill", Range(0, 3)) = 1.2

        [Header(Garis pemisah)]
        _DividerColor ("Warna garis", Color) = (0.96, 0.94, 0.90, 0.8)
        _DividerWidth ("Tebal garis", Range(0, 0.05)) = 0.012
        _DividerOverhang ("Garis melewati tepi cincin", Range(0, 0.08)) = 0.025

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
        Blend One OneMinusSrcAlpha        // output premultiplied: aura bisa "menambah" cahaya
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

            #define TWO_PI 6.28318530718

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

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _Fill, _Segments, _OuterRadius, _InnerRadius;
            float4 _CoreColor, _TrackColor, _TrackRimColor, _FillColor, _WispColor, _AuraColor, _DividerColor;
            float _AuraSize, _AuraStrength, _Wobble, _Pulse, _FlowSpeed, _NoiseScale, _TipGlow;
            float _DividerWidth, _DividerOverhang;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.uv = v.texcoord;                 // sengaja tanpa TRANSFORM_TEX: UV selalu 0..1 di seluruh Image
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

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    v += amp * vnoise(p);
                    p = p * 2.03 + float2(17.1, 9.2);
                    amp *= 0.5;
                }
                return v / 0.875;                    // 0..1
            }

            float2 rot(float2 p, float a)
            {
                float s, c;
                sincos(a, s, c);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            // dst dan src sama-sama premultiplied
            float4 over(float4 dst, float4 src) { return src + dst * (1.0 - src.a); }

            float4 frag(v2f IN) : SV_Target
            {
                float time = _Time.y;

                // koordinat: pusat Image = (0,0), tepi = 1
                float2 p = (IN.uv - 0.5) * 2.0;
                float r = length(p);
                float aa = max(fwidth(p.x), 1e-4);   // lebar 1 piksel

                // ---------- bentuk cincin ----------
                float outer = _OuterRadius;
                float inner = min(_InnerRadius, outer - 0.01);
                float mid = (outer + inner) * 0.5;
                float halfW = (outer - inner) * 0.5;

                float dRing = abs(r - mid) - halfW;                   // < 0 = di dalam pita cincin
                float ringMask = saturate(0.5 - dRing / aa);
                float coreMask = saturate(0.5 - (r - inner) / aa);

                // ---------- sudut: 0 di atas, searah jarum jam ----------
                float t = frac(atan2(p.x, p.y + 1e-5) / TWO_PI);

                float fill = saturate(_Fill);
                bool hasFill = fill > 0.001;
                bool isFull = fill >= 0.999;

                float arcPx = aa / max(r, 0.02) / TWO_PI;             // 1 piksel busur, dalam satuan t
                float endCov = isFull ? 1.0 : saturate((fill - t) / arcPx + 0.5);
                float startCov = isFull ? 1.0 : saturate(t / arcPx + 0.5);
                float bodyMask = ringMask * startCov * endCov * (hasFill ? 1.0 : 0.0);

                // ---------- gerak asap (domain diputar mengelilingi pusat => tanpa sambungan) ----------
                float2 q1 = rot(p, time * _FlowSpeed) * _NoiseScale;
                float2 q2 = rot(p, -time * _FlowSpeed * 0.6 + 1.7) * (_NoiseScale * 1.7) + float2(0.0, time * 0.15);
                float warp = vnoise(q1);
                float n1 = fbm(q1 + warp * 1.5 + float2(0.0, time * 0.2));
                float n2 = vnoise(q2 + n1 * 2.0);
                float wisps = smoothstep(0.52, 0.92, n1 * 0.6 + n2 * 0.5);

                // ---------- aura ----------
                float angSoft = isFull ? 1.0 : smoothstep(0.0, 0.03, t) * smoothstep(0.0, 0.03, fill - t);
                angSoft *= hasFill ? 1.0 : 0.0;

                float dWob = dRing - (n1 - 0.5) * _Wobble * 2.0;       // tepi aura bergetar
                float reach = max(dWob, 0.0);
                float sideW = (r < mid) ? 0.55 : 1.0;                  // sisi dalam lebih redup (ada teks)
                float pulse = 1.0 + _Pulse * sin(time * 2.2);
                float glow = exp(-reach / _AuraSize) * 0.7 + exp(-reach / (_AuraSize * 0.25)) * 0.6;
                glow *= _AuraStrength * pulse * angSoft * sideW;
                glow *= 1.0 - smoothstep(0.82, 1.0, r);                // jangan terpotong tepi Image

                // ---------- ujung fill ----------
                float dArc = abs(fill - t) * TWO_PI * r;
                float tipRadial = exp(-pow((r - mid) / max(halfW * 1.6, 1e-3), 2.0));
                float tip = exp(-(dArc * dArc) / (2.0 * 0.07 * 0.07)) * tipRadial;
                tip *= _TipGlow * (0.85 + 0.15 * sin(time * 6.0)) * (hasFill && !isFull ? 1.0 : 0.0);

                // ---------- garis pemisah ----------
                float N = max(1.0, floor(_Segments + 0.5));
                float f = frac(t * N + 0.5) - 0.5;
                float dDiv = abs(f) / N * TWO_PI * r;                  // jarak busur ke garis terdekat
                float divAng = saturate(0.5 - (dDiv - _DividerWidth * 0.5) / aa);
                float divRad = saturate(0.5 - (abs(r - mid) - (halfW + _DividerOverhang)) / aa);
                float divMask = divAng * divRad * step(0.0001, _DividerWidth);

                // ---------- susun lapisan (premultiplied) ----------
                float4 col = float4(0, 0, 0, 0);

                // 1. core
                col = over(col, float4(_CoreColor.rgb * _CoreColor.a * coreMask, _CoreColor.a * coreMask));

                // 2. track + garis tepi
                col = over(col, float4(_TrackColor.rgb * _TrackColor.a * ringMask, _TrackColor.a * ringMask));
                float edgeLine = saturate(1.0 - abs(dRing) / (aa * 1.5));
                col = over(col, float4(_TrackRimColor.rgb * _TrackRimColor.a * edgeLine, _TrackRimColor.a * edgeLine));

                // 3. aura (rgb > alpha => sebagian bersifat additive)
                col = over(col, float4(_AuraColor.rgb * glow, saturate(glow) * 0.55));

                // 4. fill hitam + asap + sedikit cahaya di tepi dalamnya
                float rim = pow(saturate(1.0 + dRing / max(halfW * 0.6, 1e-3)), 2.0);
                float3 bodyRGB = _FillColor.rgb
                               + _WispColor.rgb * wisps * 0.55
                               + _AuraColor.rgb * rim * (0.20 + 0.25 * n1);
                float bodyA = _FillColor.a * bodyMask;
                col = over(col, float4(bodyRGB * bodyA, bodyA));

                // 5. ujung fill
                float3 tipRGB = lerp(_AuraColor.rgb, float3(1, 1, 1), 0.5) * tip;
                col = over(col, float4(tipRGB, saturate(tip) * 0.35));

                // 6. garis pemisah
                float dA = _DividerColor.a * divMask;
                col = over(col, float4(_DividerColor.rgb * dA, dA));

                // alpha global (CanvasGroup / Image color)
                col *= IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                col *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
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
