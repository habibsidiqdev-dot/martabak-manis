// Bagian 2 dari 2 outline kertas untuk MESH. Pasangannya: PaperOutlineMask.shader.
//
// Menggambar salinan mesh yang digemukkan sepanjang "smooth normal" (diisi InteractableOutline.cs ke
// UV3), digeser sedikit ke arah kamera, dan hanya di piksel yang BUKAN tubuh objek (stencil NotEqual).
// Hasilnya pinggiran kertas berwarna krem rata dengan sedikit butiran kertas.
//
// Semua nilai di bawah dikirim per objek lewat MaterialPropertyBlock oleh InteractableOutline.
// Dipakai lewat InteractableOutline.cs. Taruh di folder bernama "Resources" supaya ikut ke build.
Shader "Interact/PaperOutlineHull"
{
    Properties
    {
        _Color ("Warna kertas", Color) = (0.96, 0.92, 0.82, 1)
        _Thickness ("Tebal (meter)", Float) = 0.03
        _DepthBias ("Geser ke kamera (meter)", Float) = 0.1
        _Wobble ("Goyang tepi", Range(0, 1)) = 0.2
        _BoilFps ("Kecepatan goyang (fps, 0 = diam)", Float) = 6
        _Grain ("Butiran kertas", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+2"
        }

        Pass
        {
            Name "Hull"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite On
            ZTest LEqual

            Stencil
            {
                Ref 128
                ReadMask 128
                WriteMask 0
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Thickness;
                float _DepthBias;
                float _Wobble;
                float _BoilFps;
                float _Grain;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 smoothNormalOS : TEXCOORD3;   // diisi InteractableOutline; kosong = pakai normal asli
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float Hash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                // Smooth normal supaya sudut kotak (pintu) tidak retak. Kalau mesh tidak bisa dibaca
                // (Read/Write mati), UV3 kosong dan kita jatuh ke normal asli.
                float3 dirOS = IN.smoothNormalOS;
                if (dot(dirOS, dirOS) < 0.25) dirOS = IN.normalOS;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 dirWS = TransformObjectToWorldNormal(dirOS);

                // Goyang ala stop-motion: tebal tiap titik berubah acak, berganti sebanyak _BoilFps kali per detik.
                float tick = floor(_Time.y * _BoilFps);
                float n = Hash13(IN.positionOS.xyz * 53.0 + tick * 17.0);
                float thickness = _Thickness * (1.0 + _Wobble * (n * 2.0 - 1.0));

                positionWS += dirWS * thickness;

                // Geser ke arah kamera supaya pinggiran tetap kelihatan walau objek rata dengan tembok.
                // Tubuh objek tidak tertutup karena dijaga stencil.
                float3 toCamera = _WorldSpaceCameraPos - positionWS;
                positionWS += normalize(toCamera) * _DepthBias;

                OUT.positionCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Butiran kertas per 2x2 piksel layar.
                float grain = Hash13(float3(floor(IN.positionCS.xy * 0.5), 1.0));
                half3 color = _Color.rgb * (1.0 - _Grain * grain);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
