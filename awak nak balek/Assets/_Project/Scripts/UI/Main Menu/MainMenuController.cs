using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu di atas scene gameplay (Prototype). Saat scene dimuat: input pemain dikunci, kursor bebas,
/// menu tampil. Start Game memudarkan menu lalu memainkan IntroCutscene; setelah cutscene selesai
/// gameplay dimulai (input dibuka oleh IntroCutscene).
///
/// UI-nya kamu susun sendiri. Seret panel dan tombolnya ke field di bawah; tombol yang diisi dihubungkan
/// otomatis oleh script ini, jadi JANGAN menambah On Click manual untuk tombol yang sama (akan terpanggil dua kali).
/// Method public (StartGame, OpenSettings, OpenCredits, BackToMenu, QuitGame) tetap bisa dipakai lewat On Click
/// kalau kamu lebih suka menghubungkan sendiri; dalam hal itu kosongkan field tombolnya di sini.
///
/// Settings (volume, sensitivitas mouse, layar penuh) disimpan di PlayerPrefs dan diterapkan sejak scene dimuat.
/// Pasang di GameObject kosong (mis. "MainMenu"). Canvas menu harus punya Sort Order lebih tinggi dari HUD lain.
/// </summary>
[DefaultExecutionOrder(100)]   // jalan SETELAH PlayerInputReader.Start(), yang memaksa input menyala
public class MainMenuController : MonoBehaviour
{
    private const string VolumeKey = "opt_volume";
    private const string SensitivityKey = "opt_sensitivity";
    private const string FullscreenKey = "opt_fullscreen";

    [Header("Panel (buatan sendiri)")]
    [Tooltip("Panel menu utama. Wajib punya CanvasGroup (otomatis ditambahkan kalau belum ada) untuk fade.")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject logoPanel;

    [Header("Tombol (opsional: diisi = dihubungkan otomatis)")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Button creditsBackButton;

    [Header("Settings")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Toggle fullscreenToggle;
    [Tooltip("Rentang slider sensitivitas (nilai asli PlayerLook.sensitivity; bawaannya 0.1).")]
    [SerializeField] private Vector2 sensitivityRange = new Vector2(0.02f, 0.4f);

    [Header("Cutscene")]
    [SerializeField] private IntroCutscene cutscene;
    [SerializeField, Min(0f)] private float menuFadeDuration = 0.6f;

    [Header("Sembunyikan sampai gameplay mulai (opsional)")]
    [Tooltip("HUD/crosshair/dll yang tidak boleh terlihat di belakang menu dan selama cutscene.")]
    [SerializeField] private GameObject[] hideUntilGameplay;

    [Header("Debug")]
    [Tooltip("Centang untuk langsung masuk gameplay tanpa menu & cutscene (buat tes cepat). Matikan sebelum build.")]
    [SerializeField] private bool skipIntro;

    private PlayerInputReader _input;
    private PlayerLook _look;
    private CanvasGroup _menuGroup;
    private bool _started;

    // ---------- Siklus hidup ----------

    private void Awake()
    {
        if (menuPanel != null && !menuPanel.TryGetComponent(out _menuGroup))
            _menuGroup = menuPanel.AddComponent<CanvasGroup>();

        Wire(startButton, StartGame);
        Wire(settingsButton, OpenSettings);
        Wire(creditsButton, OpenCredits);
        Wire(quitButton, QuitGame);
        Wire(settingsBackButton, BackToMenu);
        Wire(creditsBackButton, BackToMenu);

        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
    }

    private void Start()
    {
        _input = FindFirstObjectByType<PlayerInputReader>();
        _look = FindFirstObjectByType<PlayerLook>();
        if (_input == null) Debug.LogError($"{name}: PlayerInputReader tidak ditemukan di scene.", this);

        LoadSettings();

        if (skipIntro)
        {
            HideAllPanels();
            return;   // PlayerInputReader sudah menyalakan input sendiri
        }

        SetHudVisible(false);
        if (_input != null) _input.SetGameplayEnabled(false);   // input terkunci, kursor bebas

        HideAllPanels();
        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
            _menuGroup.alpha = 1f;
            _menuGroup.interactable = true;
            _menuGroup.blocksRaycasts = true;
        }
        
        if (cutscene != null) cutscene.BeginIdleShake();
    }

    private void OnDestroy()
    {
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
    }

    // ---------- Aksi menu ----------

    public void StartGame()
    {
        if (_started) return;
        _started = true;
        StartCoroutine(StartRoutine());
    }

    public void OpenSettings()
    {
        if (_started) return;
        if (menuPanel != null) menuPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (logoPanel != null) logoPanel.SetActive(true);
    }

    public void OpenCredits()
    {
        if (_started) return;
        if (menuPanel != null) menuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(true);
        if (logoPanel != null) logoPanel.SetActive(true);
    }

    public void BackToMenu()
    {
        if (_started) return;
        PlayerPrefs.Save();   // simpan setelan setiap keluar dari panel Settings
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
    }

    public void QuitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator StartRoutine()
    {
        // Cegah klik ganda selama fade
        _menuGroup.interactable = false;
        _menuGroup.blocksRaycasts = false;

        for (float t = 0f; t < menuFadeDuration; t += Time.unscaledDeltaTime)
        {
            _menuGroup.alpha = 1f - t / menuFadeDuration;
            yield return null;
        }

        HideAllPanels();

        if (cutscene != null)
        {
            bool finished = false;
            cutscene.Play(() => finished = true);
            while (!finished) yield return null;
        }
        else
        {
            Debug.LogWarning($"{name}: 'Cutscene' kosong, langsung masuk gameplay.", this);
            if (_input != null) _input.SetGameplayEnabled(true);
        }

        SetHudVisible(true);
    }

    // ---------- Settings ----------

    private void LoadSettings()
    {
        float volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        float defaultSensitivity = _look != null ? _look.Sensitivity : 0.1f;
        float sensitivity = PlayerPrefs.GetFloat(SensitivityKey, defaultSensitivity);
        bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.SetValueWithoutNotify(volume);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = sensitivityRange.x;
            sensitivitySlider.maxValue = sensitivityRange.y;
            sensitivitySlider.SetValueWithoutNotify(sensitivity);
        }

        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(fullscreen);

        // Terapkan tanpa menyimpan ulang (nilai ini baru saja dibaca).
        AudioListener.volume = Mathf.Clamp01(volume);
        if (_look != null) _look.Sensitivity = Mathf.Clamp(sensitivity, sensitivityRange.x, sensitivityRange.y);
        ApplyFullscreen(fullscreen);
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumeKey, value);
    }

    private void OnSensitivityChanged(float value)
    {
        if (_look != null) _look.Sensitivity = value;
        PlayerPrefs.SetFloat(SensitivityKey, value);
    }

    private void OnFullscreenChanged(bool value)
    {
        ApplyFullscreen(value);
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
    }

    private static void ApplyFullscreen(bool fullscreen)
    {
        Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    // ---------- Helper ----------

    private void HideAllPanels()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (logoPanel != null) logoPanel.SetActive(false);
    }

    private void SetHudVisible(bool visible)
    {
        if (hideUntilGameplay == null) return;
        for (int i = 0; i < hideUntilGameplay.Length; i++)
            if (hideUntilGameplay[i] != null) hideUntilGameplay[i].SetActive(visible);
    }

    private static void Wire(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }
}
