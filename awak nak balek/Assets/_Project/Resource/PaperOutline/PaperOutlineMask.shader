// Bagian 1 dari 2 outline kertas untuk MESH (pintu, item, tunggul, dll).
//
// Menggambar ulang bentuk objek yang sama persis, tapi tidak mengeluarkan warna: hanya menulis stencil
// di piksel tempat objek itu benar-benar terlihat (lolos depth test, jadi bagian yang tertutup tembok
// tidak ikut). PaperOutlineHull.shader lalu hanya menggambar di piksel yang stencil-nya BELUM ditandai,
// jadi hasilnya cuma pinggiran di luar siluet, dan tidak menutupi tubuh objek.
//
// Kenapa pakai stencil dan bukan "inverted hull" biasa: inverted hull biasa tidak kelihatan kalau objek
// rata dengan tembok (pintu di dalam dinding). Dengan stencil, hull boleh digeser sedikit ke arah kamera.
//
// Dipakai lewat InteractableOutline.cs. Taruh di folder bernama "Resources" supaya ikut ke build.
Shader "Interact/PaperOutlineMask"
{
    Properties { }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+1"
        }

        Pass
        {
            Name "Mask"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            ColorMask 0
            Offset -1, -1       // tarik sedikit ke kamera supaya tidak z-fighting dengan objek aslinya

            Stencil
            {
                Ref 128
                ReadMask 128
                WriteMask 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
