using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tampilan dialogue-only (tanpa roda pilihan / resonansi): satu kotak di bawah layar, label nama opsional,
/// dan petunjuk "[E] Lanjut". Pasang SATU kali di scene (GameObject kosong, mis. "DialogueUI"); semua
/// DialogueInteractable memakainya bersama.
///
/// Secara bawaan UI dibuat sendiri lewat kode saat dialogue pertama muncul (seperti ItemNotificationUI dan EndingScreen),
/// jadi tidak perlu menyusun Canvas. Mau desain sendiri? Isi 'Root' + 'Text Label' (dan opsional Speaker Label /
/// Continue Label) dengan panel buatanmu; panel itu harus NONAKTIF di awal.
///
/// Lanjut: E, Spasi, Enter, atau klik kiri. Kursor tidak perlu muncul.
/// </summary>
public class DialogueScreenUI : MonoBehaviour, IDialogueView
{
    [Header("Desain sendiri (opsional; kosong = UI otomatis dari script)")]
    [Tooltip("Akar UI dialogue buatanmu, anak sebuah Canvas, NONAKTIF di awal.")]
    [SerializeField] private GameObject root;
    [Tooltip("Opsional. Kosong = label nama tidak dipakai.")]
    [SerializeField] private TMP_Text speakerLabel;
    [Tooltip("Wajib kalau 'Root' diisi.")]
    [SerializeField] private TMP_Text textLabel;
    [Tooltip("Opsional. Menampilkan petunjuk \"[E] Lanjut\" / \"[E] Tutup\".")]
    [SerializeField] private TMP_Text continueLabel;

    [Header("Petunjuk lanjut")]
    [SerializeField] private string continueText = "[E] Lanjut";
    [SerializeField] private string lastPageText = "[E] Tutup";

    [Header("Crosshair (opsional; disembunyikan selama dialogue)")]
    [SerializeField] private Image crosshairImage;

    [Header("Tampilan UI otomatis")]
    [SerializeField] private Color boxColor = new Color(.05f, .05f, .06f, .88f);
    [SerializeField] private Color speakerColor = new Color(.98f, .80f, .38f, 1f);
    [SerializeField] private Color textColor = new Color(.96f, .94f, .90f, 1f);
    [SerializeField] private Color hintColor = new Color(.78f, .74f, .68f, 1f);
    [SerializeField, Min(8f)] private float minFontSize = 24f;
    [SerializeField, Min(8f)] private float maxFontSize = 34f;

    public event Action Advanced;

    private bool _ready;
    private bool _visible;
    private int _shownFrame;
    private RectTransform _builtTextRect;   // hanya untuk UI otomatis: tinggi area teks menyesuaikan ada/tidaknya nama

    // ---------- IDialogueView ----------

    public void Show(string speakerName)
    {
        if (!EnsureReady()) return;

        bool hasSpeaker = !string.IsNullOrEmpty(speakerName);
        if (speakerLabel != null)
        {
            speakerLabel.text = speakerName;
            speakerLabel.gameObject.SetActive(hasSpeaker);
        }
        if (_builtTextRect != null)
            _builtTextRect.offsetMax = new Vector2(-32f, hasSpeaker ? -72f : -28f);

        root.SetActive(true);
        if (crosshairImage != null) crosshairImage.enabled = false;

        _visible = true;
        _shownFrame = Time.frameCount;   // tombol yang MEMBUKA dialogue tidak boleh langsung dihitung sebagai "lanjut"
    }

    public void SetText(string text, bool isLastPage)
    {
        if (!EnsureReady()) return;

        textLabel.text = text;
        if (continueLabel != null) continueLabel.text = isLastPage ? lastPageText : continueText;
    }

    public void Hide()
    {
        _visible = false;
        if (root != null) root.SetActive(false);
        if (crosshairImage != null) crosshairImage.enabled = true;
    }

    // ---------- Input ----------

    private void Update()
    {
        if (!_visible || Time.frameCount <= _shownFrame) return;

        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;

        bool pressed =
            (keyboard != null && (keyboard.eKey.wasPressedThisFrame ||
                                  keyboard.spaceKey.wasPressedThisFrame ||
                                  keyboard.enterKey.wasPressedThisFrame)) ||
            (mouse != null && mouse.leftButton.wasPressedThisFrame);

        if (pressed) Advanced?.Invoke();
    }

    private void OnDisable() => Hide();

    // ---------- Persiapan ----------

    private bool EnsureReady()
    {
        if (_ready) return true;

        if (root == null)
        {
            Build();
        }
        else if (textLabel == null)
        {
            Debug.LogError($"{name}: 'Root' diisi tapi 'Text Label' kosong.", this);
            return false;
        }

        if (textLabel != null)
        {
            textLabel.enableAutoSizing = true;
            textLabel.fontSizeMin = minFontSize;
            textLabel.fontSizeMax = Mathf.Max(minFontSize, maxFontSize);
        }

        root.SetActive(false);
        _ready = true;
        return true;
    }

    private void Build()
    {
        GameObject canvasObject = new GameObject("DialogueScreenUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;   // di atas UI encounter (10), di bawah layar ending (100)

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        root = canvasObject;

        // Kotak di tengah bawah.
        RectTransform box = NewRect("Box", canvasObject.transform);
        box.anchorMin = new Vector2(.5f, 0f);
        box.anchorMax = new Vector2(.5f, 0f);
        box.pivot = new Vector2(.5f, 0f);
        box.anchoredPosition = new Vector2(0f, 56f);
        box.sizeDelta = new Vector2(1500f, 280f);
        Image boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = boxColor;
        boxImage.raycastTarget = false;

        // Label nama (kiri atas kotak).
        speakerLabel = AddText("Speaker", box, 36f, speakerColor, FontStyles.Bold, TextAlignmentOptions.Left);
        RectTransform speakerRect = speakerLabel.rectTransform;
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot = new Vector2(.5f, 1f);
        speakerRect.offsetMin = new Vector2(32f, -68f);
        speakerRect.offsetMax = new Vector2(-32f, -20f);

        // Isi dialogue.
        textLabel = AddText("Text", box, maxFontSize, textColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _builtTextRect = textLabel.rectTransform;
        _builtTextRect.anchorMin = Vector2.zero;
        _builtTextRect.anchorMax = Vector2.one;
        _builtTextRect.offsetMin = new Vector2(32f, 56f);
        _builtTextRect.offsetMax = new Vector2(-32f, -72f);

        // Petunjuk lanjut (kanan bawah kotak).
        continueLabel = AddText("Continue", box, 24f, hintColor, FontStyles.Italic, TextAlignmentOptions.BottomRight);
        RectTransform continueRect = continueLabel.rectTransform;
        continueRect.anchorMin = new Vector2(1f, 0f);
        continueRect.anchorMax = new Vector2(1f, 0f);
        continueRect.pivot = new Vector2(1f, 0f);
        continueRect.anchoredPosition = new Vector2(-32f, 14f);
        continueRect.sizeDelta = new Vector2(400f, 36f);
        continueLabel.text = continueText;
    }

    private static TMP_Text AddText(string objectName, Transform parent, float size, Color color,
        FontStyles style, TextAlignmentOptions alignment)
    {
        RectTransform rt = NewRect(objectName, parent);
        TextMeshProUGUI label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
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
