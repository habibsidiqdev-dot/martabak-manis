using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrustSystem
{
    /// <summary>
    /// Membuat seluruh isi Canvas World Space SELALU tergambar di atas objek 3D (tembok, pintu, kusen, dll).
    ///
    /// Penyebab UI "tertutup": material UI bawaan di Canvas World Space memakai ZTest LEqual, jadi UI diperlakukan
    /// seperti objek 3D biasa dan kalah oleh geometri yang lebih dekat ke kamera. Script ini mengganti material
    /// Image ke shader "UI/AlwaysOnTop" dan material TMP ke "TextMeshPro/Distance Field Overlay" (keduanya ZTest Always).
    /// Klik mouse tidak terpengaruh: Raycaster menguji UI, bukan depth buffer.
    ///
    /// Pasang di objek StageCanvas (yang punya komponen Canvas). Aman dipasang pada objek yang di-SetActive(false):
    /// penerapan dilakukan setiap OnEnable, dan idempoten.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class WorldCanvasAlwaysOnTop : MonoBehaviour
    {
        [Tooltip("Shader 'UI/AlwaysOnTop' (UI_AlwaysOnTop.shader). Seret file shader-nya ke sini supaya ikut ter-build.")]
        [SerializeField] private Shader imageShader;

        [Tooltip("Shader 'TextMeshPro/Distance Field Overlay'. Boleh kosong: dicari lewat Shader.Find " +
                 "(untuk build, tambahkan ke Project Settings > Graphics > Always Included Shaders).")]
        [SerializeField] private Shader textShader;

        [Tooltip("Render queue untuk semua material. Lebih tinggi = digambar belakangan (di atas sprite NPC / objek transparan).")]
        [SerializeField, Range(3000, 5000)] private int renderQueue = 3500;

        private Material _imageMaterial;
        private readonly Dictionary<Material, Material> _textMaterials = new Dictionary<Material, Material>();

        private void OnEnable() => Apply();

        /// <summary>Terapkan ke semua Graphic anak (termasuk yang nonaktif). Panggil ulang bila menambah elemen UI saat runtime.</summary>
        public void Apply()
        {
            if (imageShader == null) imageShader = Shader.Find("UI/AlwaysOnTop");
            if (textShader == null) textShader = Shader.Find("TextMeshPro/Distance Field Overlay");

            if (imageShader == null)
                Debug.LogWarning($"{name}: shader 'UI/AlwaysOnTop' tidak ditemukan; Image tidak diubah.", this);
            if (textShader == null)
                Debug.LogWarning($"{name}: shader 'TextMeshPro/Distance Field Overlay' tidak ditemukan; teks tidak diubah.", this);

            if (imageShader != null && _imageMaterial == null)
            {
                _imageMaterial = new Material(imageShader) { name = "UI_AlwaysOnTop (instance)" };
                _imageMaterial.renderQueue = renderQueue;
            }

            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];

                if (graphic is TMP_Text text)
                {
                    ApplyToText(text);
                }
                else if (_imageMaterial != null && graphic.material != _imageMaterial)
                {
                    graphic.material = _imageMaterial;
                }
            }
        }

        private void ApplyToText(TMP_Text text)
        {
            if (textShader == null) return;

            Material source = text.fontSharedMaterial;
            if (source == null || source.shader == textShader) return;

            if (!_textMaterials.TryGetValue(source, out Material overlay))
            {
                // Salinan material font asli (atlas & pengaturan outline/underlay ikut), hanya shader-nya diganti.
                overlay = new Material(source) { name = source.name + " (AlwaysOnTop)" };
                overlay.shader = textShader;
                overlay.renderQueue = renderQueue;
                _textMaterials[source] = overlay;
            }

            text.fontSharedMaterial = overlay;
        }

        private void OnDestroy()
        {
            if (_imageMaterial != null) Destroy(_imageMaterial);
            foreach (Material material in _textMaterials.Values)
            {
                if (material != null) Destroy(material);
            }
            _textMaterials.Clear();
        }
    }
}