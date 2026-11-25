using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Layar ending sederhana: layar menggelap, muncul judul + pesan + tombol "Main lagi" / "Keluar".
/// UI dibuat sendiri lewat kode saat Show() dipanggil (seperti ItemNotificationUI), jadi tidak perlu
/// menyusun Canvas atau mengisi referensi apa pun di Inspector.
///
/// Cara pasang:
///   1. Buat GameObject kosong (mis. "Ending") dan pasang script ini. Isi Title dan Message.
///   2. Pasang komponen Door di gerbang akhir, isi Required Items dengan Item_KunciGerbang.
///   3. Di Door > On Opened, tambahkan objek "Ending" lalu pilih EndingScreen > Show.
///
/// Show() langsung mengunci input pemain; layar baru muncul setelah 'delay' (supaya gerbang sempat terlihat terbuka).
/// Aman dipanggil berkali-kali: hanya yang pertama yang jalan.
/// </summary>
public class EndingScreen : MonoBehaviour
{
    [Header("Teks (isi bawaan = placeholder, ganti dengan teks ceritamu)")]
    [SerializeField] private string title = "Kamu berhasil keluar.";
    [SerializeField, TextArea(2, 6)] private string message = "Gerbang terbuka, dan malam yang panjang itu akhirnya berlalu.";
    [SerializeField] private string restartLabel = "Main lagi";
    [SerializeField] private string quitLabel = "Keluar";
    [SerializeField] private bool showQuitButton = true;

    [Header("Waktu")]
    [Tooltip("Jeda sebelum layar mulai muncul, supaya pemain sempat melihat gerbang terbuka.")]
    [SerializeField, Min(0f)] private float delay = 1.5f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 1.2f;

    [Header("Tampilan")]
    [SerializeField] private Color backgroundColor = new Color(.03f, .03f, .04f, .94f);
    [SerializeField] private Color titleColor = new Color(.98f, .80f, .38f, 1f);
    [SerializeField] private Color messageColor = new Color(.96f, .94f, .90f, 1f);
    [SerializeField] private Color buttonColor = new Color(.10f, .10f, .12f, .90f);
    [SerializeField] private Color buttonHoverColor = new Color(.45f, .32f, .10f, .95f);
    [SerializeField] private Color buttonPressedColor = new Color(.95f, .72f, .25f, 1f);

    private bool _shown;
    private CanvasGroup _group;

    /// <summary>Tampilkan ending. Cocok dipanggil dari UnityEvent (mis. Door.onOpened).</summary>
    public void Show()
    {
        if (_shown) return;
        _shown = true;

        // Kunci gerak/interact sekarang juga, supaya pemain tidak sempat memulai interaksi lain selama jeda.
        // Kursor baru dimunculkan saat layar tampil.
        PlayerInputReader input = FindFirstObjectByType<PlayerInputReader>();
        if (input != null) input.SetGameplayEnabled(false, updateCursor: false);

        StartCoroutine(ShowRoutine(input));
    }

    [ContextMenu("Tes: tampilkan ending (saat Play)")]
    private void TestShow()
    {
        if (Application.isPlaying) Show();
        else Debug.Log("Tes ending hanya jalan saat Play Mode.", this);
    }

    private IEnumerator ShowRoutine(PlayerInputReader input)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        if (input != null)
        {
            input.SetCursorFree(true);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Build();

        _group.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        _group.alpha = 1f;
    }

    // ---------- Tombol ----------

    private void Restart()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();

#if UNITY_EDITOR
        if (scene.buildIndex < 0)
        {
            // Scene belum ada di Build Settings: di Editor tetap bisa dimuat ulang lewat path-nya.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        SceneManager.LoadScene(scene.buildIndex);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Membangun UI ----------

    private void Build()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            Debug.LogWarning("Scene tidak punya EventSystem: tombol layar ending tidak akan bisa diklik.", this);

        GameObject root = new GameObject("EndingScreenUI", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        root.layer = LayerMask.NameToLayer("UI");
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // di atas UI encounter (sortingOrder 10)

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        _group = root.GetComponent<CanvasGroup>();

        // Latar penuh layar (raycastTarget aktif, jadi klik tidak tembus ke bawah).
        RectTransform background = NewRect("Background", root.transform);
        Stretch(background);
        background.gameObject.AddComponent<Image>().color = backgroundColor;

        // Kolom tengah: tinggi menyesuaikan isi.
        RectTransform column = NewRect("Column", background);
        column.anchorMin = new Vector2(.5f, .5f);
        column.anchorMax = new Vector2(.5f, .5f);
        column.pivot = new Vector2(.5f, .5f);
        column.sizeDelta = new Vector2(1100f, 0f);

        VerticalLayoutGroup stack = column.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.childAlignment = TextAnchor.MiddleCenter;
        stack.spacing = 36f;
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;

        ContentSizeFitter fitter = column.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (!string.IsNullOrEmpty(title))
            AddText("Title", column, title, 76f, titleColor, FontStyles.Bold);
        if (!string.IsNullOrEmpty(message))
            AddText("Message", column, message, 38f, messageColor, FontStyles.Normal);

        RectTransform row = NewRect("Buttons", column);
        HorizontalLayoutGroup buttons = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttons.padding = new RectOffset(0, 0, 24, 0);
        buttons.spacing = 40f;
        buttons.childAlignment = TextAnchor.MiddleCenter;
        buttons.childControlWidth = true;
        buttons.childControlHeight = true;
        buttons.childForceExpandWidth = false;
        buttons.childForceExpandHeight = false;

        AddButton("RestartButton", row, restartLabel, Restart);
        if (showQuitButton) AddButton("QuitButton", row, quitLabel, Quit);
    }

    private void AddButton(string objectName, Transform parent, string label, UnityAction onClick)
    {
        RectTransform rt = NewRect(objectName, parent);

        Image image = rt.gameObject.AddComponent<Image>();
        image.color = Color.white;   // warna akhir = white x ColorBlock

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonHoverColor;
        colors.pressedColor = buttonPressedColor;
        colors.selectedColor = buttonColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = .08f;
        button.colors = colors;

        LayoutElement size = rt.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = 360f;
        size.preferredHeight = 84f;

        TextMeshProUGUI text = AddText("Label", rt, label, 36f, messageColor, FontStyles.Normal);
        Stretch(text.rectTransform);

        button.onClick.AddListener(onClick);
    }

    private static TextMeshProUGUI AddText(string objectName, Transform parent, string content, float size,
        Color color, FontStyles style)
    {
        RectTransform rt = NewRect(objectName, parent);
        TextMeshProUGUI label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = content;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
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

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}