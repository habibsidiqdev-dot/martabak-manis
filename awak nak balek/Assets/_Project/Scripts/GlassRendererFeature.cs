// ─────────────────────────────────────────────────────────────────────────────
// GlassRendererFeature.cs
// Tambahkan script ini ke URP Renderer Asset agar glass bisa baca Opaque Texture
// ─────────────────────────────────────────────────────────────────────────────
// CARA PAKAI:
//   Tidak perlu attach script ini ke object manapun.
//   Cukup di Project Settings → Graphics → URP Asset,
//   centang "Opaque Texture" dan "Depth Texture". Selesai!
//
//   Script ini hanya referensi / dokumentasi pengingat.
// ─────────────────────────────────────────────────────────────────────────────

/*
   CHECKLIST SETUP URP GLASS:

   1. Project Settings → Graphics
      ✅ Pastikan URP Asset kamu aktif

   2. Buka URP Asset kamu (di folder Settings atau Assets)
      ✅ Centang: Opaque Texture  ← WAJIB untuk refraksi
      ✅ Centang: Depth Texture   ← optional tapi direkomendasikan

   3. Import shader GlassURP.shader ke folder Shaders/ di project kamu

   4. Buat Material baru:
      - Klik kanan di Project → Create → Material
      - Di Inspector, klik dropdown Shader → pilih Custom/GlassURP

   5. Setting material yang direkomendasikan:
      - Glass Tint      : warna putih, alpha rendah (0.05–0.15)
      - Smoothness      : 0.95
      - Refraction      : 0.02 (naikan untuk efek lebih dramatis)
      - Fresnel Power   : 3–5
      - Edge Tint       : biru muda / cyan pucat
      - Normal Map      : bisa kosong, atau pakai subtle noise texture

   6. Apply material ke object (Cube, Sphere, jendela, dll.)

   7. Jika glass tidak tembus pandang → pastikan Opaque Texture sudah ON
*/
