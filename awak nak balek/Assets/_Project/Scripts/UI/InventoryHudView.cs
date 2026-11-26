using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inventory HUD: deretan ikon item yang selalu tampil di layar (tanpa teks), seperti di Baldi's Basics.
/// Hanya TAMPILAN. Item dikonsumsi otomatis lewat PlayerInventory.TryConsumeAll, jadi tidak ada slot terpilih atau tombol pakai.
///
/// - Isi slot mengikuti PlayerInventory.Items dan di-refresh hanya saat PlayerInventory.Changed (bukan tiap frame).
/// - Satu entri inventori = satu slot (dua Kunci = dua ikon).
/// - Memudar saat gameplay dikunci (dialog, encounter, ending) lewat PlayerInputReader.GameplayEnabledChanged.
/// - Item yang masuk saat HUD tersembunyi (hadiah NPC, hadiah dialog) baru "pop" begitu HUD muncul lagi.
/// - Item tanpa ikon tampil sebagai kotak oranye (placeholder), jadi bisa dites sebelum seni ikonnya jadi.
///
/// Pasang di GameObject kosong anak HUDCanvas. Atur RectTransform-nya: anchor di satu titik (JANGAN stretch),
/// pivot sama dengan titik anchor (mis. kiri bawah: Anchor 0,0 / Pivot 0,0). Ukuran dihitung otomatis oleh script
/// dan tumbuh dari titik pivot. Layout Group ditambahkan otomatis kalau belum ada.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class InventoryHudView : MonoBehaviour
{
    [Header("Sumber")]
    [Tooltip("Kosong = dicari otomatis di scene.")]
    [SerializeField] private PlayerInventory inventory;

    [Tooltip("Dipakai untuk menyembunyikan HUD saat gameplay dikunci. Kosong = dicari otomatis di scene.")]
    [SerializeField] private PlayerInputReader input;

    [Header("Susunan")]
    [Tooltip("false = berjajar ke kanan, true = ke bawah.")]
    [SerializeField] private bool vertical;

    [Tooltip("Ukuran satu slot, dalam satuan canvas (referensi 1920x1080).")]
    [SerializeField] private Vector2 slotSize = new Vector2(96f, 96f);

    [SerializeField, Min(0f)] private float spacing = 12f;

    [Tooltip("true = bingkai kosong tetap tampil sebanyak Min Slots. false = slot hanya muncul kalau ada item.")]
    [SerializeField] private bool showEmptySlots = true;

    [Tooltip("Jumlah bingkai yang selalu tampil (kalau Show Empty Slots aktif). Item lebih banyak dari ini menambah slot otomatis.")]
    [SerializeField, Min(0)] private int minSlots = 4;

    [Header("Tampilan")]
    [Tooltip("Sprite bingkai slot. Kosong = kotak polos berwarna. Kalau sprite punya 9-slice border, otomatis dipakai.")]
    [SerializeField] private Sprite frameSprite;

    [SerializeField] private Color frameColor = new Color(0.08f, 0.08f, 0.10f, 0.85f);
    [SerializeField] private Color emptyFrameColor = new Color(0.08f, 0.08f, 0.10f, 0.35f);

    [Tooltip("Jarak ikon dari tepi bingkai.")]
    [SerializeField, Min(0f)] private float iconPadding = 10f;

    [Tooltip("Dipakai kalau ItemData belum punya ikon. Kosong = kotak berwarna 'Missing Icon Color'.")]
    [SerializeField] private Sprite missingIconSprite;

    [SerializeField] private Color missingIconColor = new Color(1f, 0.65f, 0.2f, 1f);

    [Header("Animasi")]
    [SerializeField] private bool popOnAdd = true;
    [SerializeField, Min(0.05f)] private float popDuration = 0.35f;
    [SerializeField, Min(1f)] private float popScale = 1.35f;

    [Tooltip("Sembunyikan HUD saat dialog, encounter, atau ending berjalan.")]
    [SerializeField] private bool hideWhenGameplayLocked = true;

    [SerializeField, Min(0.01f)] private float fadeTime = 0.2f;

    private sealed class Slot
    {
        public RectTransform root;
        public UnityEngine.UI.Image frame;
        public UnityEngine.UI.Image icon;
        public Coroutine pop;
    }

    private readonly List<Slot> _slots = new List<Slot>();
    private readonly List<int> _pendingPops = new List<int>();   // pop yang tertunda karena HUD sedang tersembunyi

    private RectTransform _rect;
    private CanvasGroup _group;
    private float _targetAlpha = 1f;
    private bool _inventorySubscribed;
    private bool _inputSubscribed;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _group = GetComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;   // HUD tidak boleh menghalangi klik

        SetupLayout();
        Refresh();   // sebelum inventori ketemu: tampilkan bingkai kosong saja
    }

    private void OnEnable() => TrySubscribe();

    // Pemain bisa saja baru ada setelah OnEnable, jadi dicoba lagi di Start.
    private void Start() => TrySubscribe();

    private void OnDisable()
    {
        if (_inventorySubscribed && inventory != null)
        {
            inventory.Changed -= Refresh;
            inventory.ItemAdded -= OnItemAdded;
        }
        _inventorySubscribed = false;

        if (_inputSubscribed && input != null) input.GameplayEnabledChanged -= OnGameplayEnabledChanged;
        _inputSubscribed = false;

        // Coroutine ikut berhenti saat dimatikan, jadi skala ikon dikembalikan supaya tidak macet di tengah animasi.
        for (int i = 0; i < _slots.Count; i++) StopPop(_slots[i]);
        _pendingPops.Clear();
    }

    private void OnValidate()
    {
        slotSize = Vector2.Max(slotSize, Vector2.one);
    }

    private void Update()
    {
        if (Mathf.Approximately(_group.alpha, _targetAlpha)) return;
        _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Time.unscaledDeltaTime / fadeTime);
    }

    // ---------- Sumber event ----------

    private void TrySubscribe()
    {
        if (inventory == null) inventory = FindFirstObjectByType<PlayerInventory>();
        if (!_inventorySubscribed && inventory != null)
        {
            inventory.Changed += Refresh;
            inventory.ItemAdded += OnItemAdded;
            _inventorySubscribed = true;
            Refresh();
        }

        if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
        if (!_inputSubscribed && input != null)
        {
            input.GameplayEnabledChanged += OnGameplayEnabledChanged;
            _inputSubscribed = true;

            // Langsung ikut keadaan sekarang, tanpa fade.
            _targetAlpha = TargetAlphaFor(input.GameplayEnabled);
            _group.alpha = _targetAlpha;
        }
    }

    private void OnGameplayEnabledChanged(bool gameplayEnabled)
    {
        _targetAlpha = TargetAlphaFor(gameplayEnabled);
        if (gameplayEnabled) FlushPendingPops();
    }

    private float TargetAlphaFor(bool gameplayEnabled) =>
        (!hideWhenGameplayLocked || gameplayEnabled) ? 1f : 0f;

    // ---------- Isi slot ----------

    private void Refresh()
    {
        IReadOnlyList<ItemData> items = inventory != null ? inventory.Items : null;
        int itemCount = items != null ? items.Count : 0;
        int shown = Mathf.Max(itemCount, showEmptySlots ? minSlots : 0);

        while (_slots.Count < shown)
            _slots.Add(CreateSlot(_slots.Count));

        for (int i = 0; i < _slots.Count; i++)
        {
            Slot slot = _slots[i];
            bool visible = i < shown;
            bool filled = i < itemCount;

            slot.root.gameObject.SetActive(visible);
            if (!visible)
            {
                StopPop(slot);
                continue;
            }

            slot.frame.color = filled ? frameColor : emptyFrameColor;
            slot.icon.enabled = filled;

            if (filled) ApplyIcon(slot.icon, items[i]);
            else StopPop(slot);
        }

        ApplySize(shown);
    }

    private void ApplyIcon(UnityEngine.UI.Image image, ItemData item)
    {
        bool hasIcon = item != null && item.Icon != null;

        image.sprite = hasIcon ? item.Icon : missingIconSprite;

        // Tanpa ikon dan tanpa sprite pengganti: Image tanpa sprite digambar sebagai kotak berwarna.
        image.color = (hasIcon || missingIconSprite != null) ? Color.white : missingIconColor;
    }

    // ---------- Susunan ----------

    private void SetupLayout()
    {
        var layout = GetComponent<UnityEngine.UI.HorizontalOrVerticalLayoutGroup>();
        if (layout == null)
        {
            layout = vertical
                ? (UnityEngine.UI.HorizontalOrVerticalLayoutGroup)gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>()
                : gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        }

        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    // Ukuran kontainer pas dengan slot yang tampil, jadi arah tumbuhnya ditentukan pivot RectTransform.
    private void ApplySize(int visibleCount)
    {
        if (visibleCount <= 0)
        {
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
            return;
        }

        float gaps = (visibleCount - 1) * spacing;
        float width = vertical ? slotSize.x : visibleCount * slotSize.x + gaps;
        float height = vertical ? visibleCount * slotSize.y + gaps : slotSize.y;

        _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private Slot CreateSlot(int index)
    {
        RectTransform root = NewRect("Slot_" + (index + 1), transform);

        var frame = root.gameObject.AddComponent<UnityEngine.UI.Image>();
        frame.sprite = frameSprite;
        frame.type = (frameSprite != null && frameSprite.border.sqrMagnitude > 0f)
            ? UnityEngine.UI.Image.Type.Sliced
            : UnityEngine.UI.Image.Type.Simple;
        frame.raycastTarget = false;

        var element = root.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        element.preferredWidth = slotSize.x;
        element.preferredHeight = slotSize.y;

        RectTransform iconRect = NewRect("Icon", root);
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(iconPadding, iconPadding);
        iconRect.offsetMax = new Vector2(-iconPadding, -iconPadding);

        var icon = iconRect.gameObject.AddComponent<UnityEngine.UI.Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;

        return new Slot { root = root, frame = frame, icon = icon };
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

    // ---------- Animasi pop ----------

    private void OnItemAdded(ItemData item)
    {
        if (!popOnAdd || item == null || inventory == null) return;

        // Item baru selalu ditaruh di akhir daftar, tapi dicari dari belakang supaya aman untuk item kembar.
        IReadOnlyList<ItemData> items = inventory.Items;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] != item) continue;

            // HUD sedang tersembunyi (dialog/encounter): pop-nya ditunda sampai HUD muncul lagi.
            if (_targetAlpha < 0.99f) _pendingPops.Add(i);
            else if (i < _slots.Count) StartPop(_slots[i]);
            return;
        }
    }

    private void FlushPendingPops()
    {
        int count = inventory != null ? inventory.Items.Count : 0;

        for (int i = 0; i < _pendingPops.Count; i++)
        {
            int index = _pendingPops[i];
            if (index < count && index < _slots.Count) StartPop(_slots[index]);
        }

        _pendingPops.Clear();
    }

    private void StartPop(Slot slot)
    {
        if (slot == null || !isActiveAndEnabled || !slot.root.gameObject.activeInHierarchy) return;

        StopPop(slot);
        slot.pop = StartCoroutine(Pop(slot));
    }

    private void StopPop(Slot slot)
    {
        if (slot == null) return;

        if (slot.pop != null)
        {
            StopCoroutine(slot.pop);
            slot.pop = null;
        }

        // Saat scene dibongkar, ikon bisa sudah dihancurkan lebih dulu daripada OnDisable ini.
        if (slot.icon != null) slot.icon.rectTransform.localScale = Vector3.one;
    }

    private IEnumerator Pop(Slot slot)
    {
        RectTransform target = slot.icon.rectTransform;

        // 0 -> popScale (cepat) -> 1 (turun pelan)
        const float riseShare = 0.4f;

        for (float t = 0f; t < popDuration; t += Time.unscaledDeltaTime)
        {
            float k = t / popDuration;
            float scale = k < riseShare
                ? Mathf.Lerp(0f, popScale, EaseOut(k / riseShare))
                : Mathf.Lerp(popScale, 1f, EaseOut((k - riseShare) / (1f - riseShare)));

            target.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        target.localScale = Vector3.one;
        slot.pop = null;
    }

    private static float EaseOut(float k)
    {
        k = Mathf.Clamp01(k);
        float inv = 1f - k;
        return 1f - inv * inv * inv;
    }
}
