using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menampilkan kartu notifikasi "item didapat" setiap kali item masuk ke PlayerInventory
/// (ambil di dunia lewat ItemPickup, atau hadiah NPC lewat TrustNpc.GiveRewards).
///
/// Desain kartu = PREFAB buatanmu (lihat ItemToastView). Script ini hanya memunculkan, menggeser masuk,
/// dan memudarkan kartunya. Kartu yang masuk bersamaan bertumpuk, yang terbaru di paling bawah.
///
/// Pasang di GameObject kosong mana saja. Field Container boleh kosong: kartu muncul di pojok kanan bawah.
/// </summary>
public class ItemNotificationUI : MonoBehaviour
{
    [Header("Sumber")]
    [Tooltip("Kosong = dicari otomatis di scene.")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Prefab")]
    [Tooltip("Prefab kartu. Root-nya harus punya komponen ItemToastView. Atur Width & Height di Rect Transform-nya (anchor jangan stretch).")]
    [SerializeField] private ItemToastView toastPrefab;

    [Tooltip("Tempat kartu ditaruh. Kosong = dibuat otomatis di pojok kanan bawah layar. " +
             "Kalau diisi, pasang Vertical Layout Group di objek ini (Control Child Size: Width + Height) supaya kartunya tersusun.")]
    [SerializeField] private RectTransform container;

    [Header("Isi")]
    [Tooltip("Teks kecil di kartu (dikirim ke Caption Text di prefab).")]
    [SerializeField] private string caption = "ITEM DIDAPAT";

    [Header("Perilaku")]
    [SerializeField, Min(0.5f)] private float showDuration = 3f;
    [Tooltip("Jumlah kartu maksimum di layar. Yang paling lama dibuang kalau kebanyakan.")]
    [SerializeField, Range(1, 6)] private int maxVisible = 4;

    [Header("Animasi")]
    [Tooltip("Jarak geser awal. Positif = masuk dari kanan, negatif = dari kiri, 0 = tanpa geser.")]
    [SerializeField] private float slideOffset = 80f;
    [SerializeField, Min(0.01f)] private float fadeInTime = 0.25f;
    [SerializeField, Min(0.01f)] private float fadeOutTime = 0.35f;

    [Header("Posisi otomatis (hanya kalau Container kosong)")]
    [Tooltip("Jarak dari tepi kanan (x) dan bawah (y) layar, dalam satuan referensi 1920x1080.")]
    [SerializeField] private Vector2 margin = new Vector2(40f, 40f);
    [SerializeField, Min(0f)] private float spacing = 10f;

    private static readonly Vector2 FallbackCardSize = new Vector2(380f, 84f);
    private const int StackCapacity = 6;   // tinggi kontainer otomatis cukup untuk batas atas maxVisible

    private sealed class Toast
    {
        public RectTransform root;   // dipegang layout group
        public RectTransform body;   // instance prefab, bebas digeser untuk animasi
        public CanvasGroup group;
        public Coroutine routine;
    }

    private readonly List<Toast> _active = new List<Toast>();
    private bool _subscribed;

    private void Awake()
    {
        if (toastPrefab == null)
        {
            Debug.LogError("ItemNotificationUI: Toast Prefab belum diisi, notifikasi item tidak akan muncul.", this);
            return;
        }

        if (container == null) container = BuildAutoContainer();
    }

    private void OnEnable() => TrySubscribe();

    // Pemain bisa saja baru ada setelah OnEnable, jadi dicoba lagi di Start.
    private void Start() => TrySubscribe();

    private void OnDisable()
    {
        Unsubscribe();

        // Coroutine ikut berhenti saat dimatikan, jadi kartu yang tersisa dibuang supaya tidak macet di layar.
        while (_active.Count > 0) Remove(_active[0]);
    }

    // ---------- Sumber event ----------

    private void TrySubscribe()
    {
        if (_subscribed) return;

        if (inventory == null) inventory = FindInventory();
        if (inventory == null) return;

        inventory.ItemAdded += Show;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (_subscribed && inventory != null) inventory.ItemAdded -= Show;
        _subscribed = false;
    }

    private static PlayerInventory FindInventory()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<PlayerInventory>();
#else
        return FindObjectOfType<PlayerInventory>();
#endif
    }

    // ---------- Tampil ----------

    /// <summary>Tampilkan kartu untuk satu item. Dipanggil otomatis lewat PlayerInventory.ItemAdded.</summary>
    public void Show(ItemData item)
    {
        if (item == null || toastPrefab == null || container == null || !isActiveAndEnabled) return;

        // Terlalu banyak: buang yang paling lama.
        while (_active.Count >= maxVisible)
            Remove(_active[0]);

        Toast toast = CreateToast(item);
        _active.Add(toast);
        toast.routine = StartCoroutine(Animate(toast));
    }

    private Toast CreateToast(ItemData item)
    {
        Vector2 size = PrefabSize();

        // Pembungkus yang diatur layout group. Prefab ditaruh di dalamnya supaya bisa digeser bebas.
        var rootGo = new GameObject("Toast_" + item.DisplayName, typeof(RectTransform), typeof(LayoutElement), typeof(CanvasGroup));
        rootGo.layer = container.gameObject.layer;

        var root = (RectTransform)rootGo.transform;
        root.SetParent(container, false);

        LayoutElement element = rootGo.GetComponent<LayoutElement>();
        element.preferredWidth = size.x;
        element.preferredHeight = size.y;

        CanvasGroup group = rootGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;   // jangan pernah menghalangi klik (tombol encounter, dll)

        ItemToastView view = Instantiate(toastPrefab, root);
        var body = (RectTransform)view.transform;
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = Vector2.zero;
        body.offsetMax = Vector2.zero;
        body.localScale = Vector3.one;

        view.Setup(item, caption);

        return new Toast { root = root, body = body, group = group };
    }

    // Ukuran kartu diambil dari Width/Height di Rect Transform prefab.
    private Vector2 PrefabSize()
    {
        Vector2 size = ((RectTransform)toastPrefab.transform).sizeDelta;
        if (size.x <= 0f || size.y <= 0f) size = FallbackCardSize;
        return size;
    }

    private IEnumerator Animate(Toast toast)
    {
        // Geser masuk sambil fade in
        for (float t = 0f; t < fadeInTime; t += Time.unscaledDeltaTime)
        {
            float k = EaseOut(t / fadeInTime);
            toast.group.alpha = k;
            toast.body.anchoredPosition = new Vector2(Mathf.Lerp(slideOffset, 0f, k), 0f);
            yield return null;
        }

        toast.group.alpha = 1f;
        toast.body.anchoredPosition = Vector2.zero;

        yield return new WaitForSecondsRealtime(showDuration);

        for (float t = 0f; t < fadeOutTime; t += Time.unscaledDeltaTime)
        {
            toast.group.alpha = 1f - t / fadeOutTime;
            yield return null;
        }

        toast.routine = null;
        Remove(toast);
    }

    private void Remove(Toast toast)
    {
        if (toast == null) return;

        if (toast.routine != null) StopCoroutine(toast.routine);
        _active.Remove(toast);
        if (toast.root != null) Destroy(toast.root.gameObject);
    }

    private static float EaseOut(float k)
    {
        k = Mathf.Clamp01(k);
        float inv = 1f - k;
        return 1f - inv * inv * inv;
    }

    // ---------- Kontainer otomatis ----------

    private RectTransform BuildAutoContainer()
    {
        RectTransform root;

        if (GetComponentInParent<Canvas>() != null)
        {
            // Sudah ada di dalam Canvas: ikut Canvas itu.
            root = NewRect("ItemToasts", transform);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
        }
        else
        {
            // Canvas sendiri (Screen Space Overlay, skala sama dengan HUDCanvas: 1920x1080, match 0.5).
            root = NewRect("ItemToastCanvas", transform);

            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        Vector2 size = PrefabSize();

        // Kontainer di pojok kanan bawah. Kartunya disusun dari bawah ke atas oleh layout group.
        RectTransform stack = NewRect("Stack", root);
        stack.anchorMin = new Vector2(1f, 0f);
        stack.anchorMax = new Vector2(1f, 0f);
        stack.pivot = new Vector2(1f, 0f);
        stack.sizeDelta = new Vector2(size.x, (size.y + spacing) * StackCapacity);
        stack.anchoredPosition = new Vector2(-margin.x, margin.y);

        VerticalLayoutGroup layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.LowerRight;
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        return stack;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) go.layer = uiLayer;

        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }
}