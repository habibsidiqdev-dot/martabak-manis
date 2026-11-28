using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SusSystem
{
    /// <summary>
    /// Tampilan sus system. Dua mode:
    ///
    /// 1) DESAIN SENDIRI (disarankan): susun UI-mu di Canvas, lalu isi 'Filled Objects' dengan satu objek per bar
    ///    (urutan = bar 1, 2, 3, 4). Objek ke-i DIAKTIFKAN saat bar ke-i terisi, dan dinonaktifkan kalau belum.
    ///    Letakkan dan jangkarkan di mana saja (mis. kiri atas) lewat Canvas-mu sendiri.
    ///    Opsional: 'Empty Objects' (tampil saat bar kosong), 'Counter Label' (teks "2/4"),
    ///    dan event 'On Changed' / 'On Warning' untuk animasi lewat Animator, suara, dsb.
    ///
    /// 2) UI BAWAAN: kalau 'Filled Objects' kosong, script membuat UI sederhana sendiri di kiri atas layar.
    ///
    /// Pasang komponen ini di objek mana pun (mis. sama dengan SusManager atau di Canvas-mu).
    /// Canvas-mu sebaiknya punya Sort Order di atas UI encounter (10) kalau sus mau tetap terlihat saat dialog,
    /// dan di bawah layar ending (100).
    /// </summary>
    public class SusHud : MonoBehaviour
    {
        [Header("Desain sendiri (isi ini kalau mau UI buatanmu)")]
        [Tooltip("Satu objek per bar, urut dari bar 1. Diaktifkan saat bar itu terisi, dinonaktifkan saat belum. " +
                 "Jumlahnya sebaiknya sama dengan Max Bars di SusManager. Kosong = pakai UI bawaan.")]
        [SerializeField] private GameObject[] filledObjects;

        [Tooltip("Opsional. Objek per bar yang tampil saat bar itu KOSONG (mis. kotak redup). Urutannya sama.")]
        [SerializeField] private GameObject[] emptyObjects;

        [Tooltip("Opsional. Teks penghitung, mis. \"2/4\".")]
        [SerializeField] private TMP_Text counterLabel;

        [Tooltip("Format teks penghitung. {current} = terisi, {max} = total.")]
        [SerializeField] private string counterFormat = "{current}/{max}";

        [Tooltip("Objek yang baru terisi 'membal' sebentar (skala).")]
        [SerializeField] private bool popOnFill = true;

        [Tooltip("Dipanggil saat awal dan tiap sus berubah: (terisi, total). " +
                 "Cocok untuk Animator, suara, atau getar layar.")]
        [SerializeField] private UnityEvent<int, int> onChanged;

        [Tooltip("True saat tinggal 1 bar menuju penuh (mis. 3/4), false di luar itu. " +
                 "Sambungkan ke Animator.SetBool untuk efek berdenyut buatanmu.")]
        [SerializeField] private UnityEvent<bool> onWarning;

        [Header("UI bawaan (dipakai hanya kalau 'Filled Objects' kosong)")]
        [Tooltip("Jarak dari tepi kiri dan atas layar, dalam piksel referensi 1920x1080.")]
        [SerializeField] private Vector2 margin = new Vector2(48f, 40f);
        [SerializeField] private string label = "SUS";
        [SerializeField, Min(8f)] private float pipSize = 34f;
        [SerializeField, Min(0f)] private float spacing = 10f;
        [Tooltip("Di atas UI encounter (10), di bawah layar ending (100).")]
        [SerializeField] private int sortingOrder = 20;
        [SerializeField] private Color labelColor = new Color(.96f, .94f, .90f, 1f);
        [SerializeField] private Color borderColor = new Color(.96f, .94f, .90f, .85f);
        [SerializeField] private Color emptyColor = new Color(.05f, .05f, .06f, .70f);
        [SerializeField] private Color filledColor = new Color(.86f, .20f, .16f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, .55f, .45f, 1f);

        private SusManager _manager;
        private bool _custom;
        private Image[] _fills;   // hanya untuk UI bawaan
        private int _current;
        private int _max;
        private bool _warning;

        private int PipCount => _custom ? filledObjects.Length : _fills.Length;

        // Start (bukan Awake) supaya SusManager.Instance pasti sudah terisi.
        private void Start()
        {
            _manager = SusManager.Instance;
            if (_manager == null)
            {
                Debug.LogWarning($"{name}: SusHud tidak menemukan SusManager di scene.", this);
                enabled = false;
                return;
            }

            _max = _manager.Max;
            _custom = HasAny(filledObjects);

            if (_custom)
            {
                if (filledObjects.Length != _max)
                    Debug.LogWarning($"{name}: 'Filled Objects' berisi {filledObjects.Length} objek, " +
                                     $"tapi SusManager punya {_max} bar. Samakan jumlahnya.", this);
            }
            else
            {
                Build(_max);
            }

            Apply(_manager.Current, animate: false);
            _manager.Changed += OnChanged;
        }

        private void OnDestroy()
        {
            if (_manager != null) _manager.Changed -= OnChanged;
        }

        private void OnChanged(int current, int max) => Apply(current, animate: true);

        private void Update()
        {
            // Denyut hanya untuk UI bawaan. UI buatan sendiri memakai event 'On Warning'.
            if (_custom || !_warning || _fills == null) return;

            float t = Mathf.PingPong(Time.unscaledTime * 2f, 1f);
            Color pulse = Color.Lerp(filledColor, warningColor, t);
            for (int i = 0; i < _current && i < _fills.Length; i++)
                _fills[i].color = pulse;
        }

        // ---------- Menerapkan nilai ----------

        private void Apply(int current, bool animate)
        {
            int count = PipCount;
            for (int i = 0; i < count; i++)
            {
                bool filled = i < current;
                bool newlyFilled = animate && filled && i >= _current;

                if (_custom) ApplyCustom(i, filled, newlyFilled);
                else ApplyBuiltIn(i, filled, newlyFilled);
            }

            _current = current;

            if (counterLabel != null)
                counterLabel.text = counterFormat.Replace("{current}", current.ToString()).Replace("{max}", _max.ToString());

            bool warning = current > 0 && current == _max - 1;
            if (warning != _warning)
            {
                _warning = warning;
                onWarning?.Invoke(warning);
            }

            onChanged?.Invoke(current, _max);
        }

        private void ApplyCustom(int index, bool filled, bool newlyFilled)
        {
            GameObject filledObject = filledObjects[index];
            if (filledObject != null)
            {
                filledObject.SetActive(filled);
                if (newlyFilled && popOnFill && isActiveAndEnabled)
                    StartCoroutine(Pop(filledObject.transform));
            }

            if (emptyObjects != null && index < emptyObjects.Length && emptyObjects[index] != null)
                emptyObjects[index].SetActive(!filled);
        }

        private void ApplyBuiltIn(int index, bool filled, bool newlyFilled)
        {
            _fills[index].color = filled ? filledColor : emptyColor;

            if (newlyFilled && popOnFill)
                StartCoroutine(Pop(_fills[index].transform.parent));
        }

        private static bool HasAny(GameObject[] objects)
        {
            if (objects == null) return false;
            for (int i = 0; i < objects.Length; i++)
                if (objects[i] != null) return true;
            return false;
        }

        // Skala dasar diambil dari objek itu sendiri, jadi skala buatan desainermu tidak ditimpa.
        private static IEnumerator Pop(Transform target)
        {
            const float duration = .28f;
            Vector3 baseScale = target.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (target == null) yield break;

                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                target.localScale = baseScale * (1f + .5f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }

            if (target != null) target.localScale = baseScale;
        }

        // ---------- UI bawaan ----------

        private void Build(int count)
        {
            GameObject root = new GameObject("SusHudUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.layer = LayerMask.NameToLayer("UI");
            root.transform.SetParent(transform, false);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            // Baris: [SUS] [] [] [] []  , jangkar kiri atas, ukuran menyesuaikan isi.
            RectTransform row = NewRect("Row", root.transform);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(margin.x, -margin.y);

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (!string.IsNullOrEmpty(label))
            {
                RectTransform labelRect = NewRect("Label", row);
                TextMeshProUGUI text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
                text.text = label;
                text.fontSize = pipSize * .85f;
                text.fontStyle = FontStyles.Bold;
                text.color = labelColor;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.raycastTarget = false;
            }

            _fills = new Image[count];
            for (int i = 0; i < count; i++)
            {
                // Kotak = bingkai (Image) + isi (Image anak). Warna isi yang berganti kosong/terisi.
                RectTransform pip = NewRect($"Pip{i + 1}", row);
                Image border = pip.gameObject.AddComponent<Image>();
                border.color = borderColor;
                border.raycastTarget = false;

                LayoutElement size = pip.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = pipSize;
                size.preferredHeight = pipSize;

                RectTransform fill = NewRect("Fill", pip);
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = Vector2.one;
                fill.offsetMin = new Vector2(3f, 3f);
                fill.offsetMax = new Vector2(-3f, -3f);

                Image fillImage = fill.gameObject.AddComponent<Image>();
                fillImage.color = emptyColor;
                fillImage.raycastTarget = false;
                _fills[i] = fillImage;
            }
        }

        private static RectTransform NewRect(string objectName, Transform parent)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }
    }
}
