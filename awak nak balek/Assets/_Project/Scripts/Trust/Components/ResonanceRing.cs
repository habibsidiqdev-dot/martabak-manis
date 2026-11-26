using UnityEngine;
using UnityEngine.UI;

namespace TrustSystem
{
    /// <summary>
    /// Mengisi bar Resonansi bulat (shader "UI/ResonanceRing") dengan animasi halus.
    /// Pasang di Image yang memakai material ResonanceRing (Source Image KOSONG, ukuran persegi).
    /// Dipanggil oleh TrustScreenEncounterUI.SetStatus; tidak ada logika permainan di sini.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ResonanceRing : MonoBehaviour
    {
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int SegmentsId = Shader.PropertyToID("_Segments");

        [Tooltip("Lama kira-kira isi bar mengejar nilai baru (detik). 0 = langsung.")]
        [SerializeField, Min(0f)] private float fillSmoothTime = 0.35f;

        private Image _image;
        private Material _instance;   // salinan per-objek, supaya material asli tidak ikut berubah
        private float _target;        // 0..1
        private float _display;       // 0..1, yang tampil di layar
        private float _velocity;
        private bool _snapNext = true;

        private void Awake() => EnsureMaterial();

        private void OnDestroy()
        {
            if (_instance != null) Destroy(_instance);
        }

        private bool EnsureMaterial()
        {
            if (_instance != null) return true;

            if (_image == null) _image = GetComponent<Image>();
            if (_image == null || _image.material == null || !_image.material.HasProperty(FillId))
            {
                Debug.LogError($"{name}: Image belum memakai material dengan shader UI/ResonanceRing.", this);
                return false;
            }

            _instance = new Material(_image.material) { name = _image.material.name + " (Instance)" };
            _image.material = _instance;
            _instance.SetFloat(FillId, _display);
            return true;
        }

        /// <summary>Panggilan berikutnya ke <see cref="SetResonance"/> langsung loncat tanpa animasi (dipakai saat encounter baru dibuka).</summary>
        public void SnapNext() => _snapNext = true;

        public void SetResonance(int resonance, int target)
        {
            if (!EnsureMaterial()) return;

            int segments = Mathf.Max(1, target);
            _instance.SetFloat(SegmentsId, segments);
            _target = Mathf.Clamp01(resonance / (float)segments);

            if (_snapNext || fillSmoothTime <= 0f)
            {
                _snapNext = false;
                _display = _target;
                _velocity = 0f;
                _instance.SetFloat(FillId, _display);
            }
        }

        private void Update()
        {
            if (_instance == null || Mathf.Approximately(_display, _target)) return;

            // unscaled: tetap jalan walau Time.timeScale = 0 saat encounter
            _display = Mathf.SmoothDamp(_display, _target, ref _velocity, fillSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(_display - _target) < 0.0005f)
            {
                _display = _target;
                _velocity = 0f;
            }
            _instance.SetFloat(FillId, _display);
        }
    }
}
