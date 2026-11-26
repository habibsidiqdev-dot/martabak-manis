using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrustSystem
{
    /// <summary>
    /// Tampilan encounter SCREEN SPACE (pengganti TrustWorldEncounterUI + TrustStageView + TrustHudView).
    /// Mengimplementasikan ITrustEncounterView, jadi TrustNpc tidak perlu diubah: cukup isi field 'ui' dengan komponen ini.
    ///
    /// Susunan: panel setengah layar berisi roda 4 pilihan (Resonansi dan Giliran di tengah), kotak petunjuk,
    /// dan kotak dialogue. Panel otomatis dipasang di sisi layar yang BERLAWANAN dengan posisi NPC di layar
    /// (jadi tidak peduli bagaimana cameraFocusPoint membingkai NPC). Label nama bisa menempel di titik anchor NPC
    /// atau tetap di posisi Hierarchy-nya, mis. pojok kanan atas (lihat 'nameTagFollowsNpc').
    ///
    /// Pasang di objek Canvas (Screen Space - Overlay) yang SELALU aktif; 'encounterRoot' adalah anak yang di-show/hide.
    /// Cara termudah menyusunnya: menu Tools > Trust > Buat UI Screen Space.
    /// </summary>
    public class TrustScreenEncounterUI : MonoBehaviour, ITrustEncounterView
    {
        public enum PanelSide { Auto, Left, Right }

        [Serializable]
        private class ApproachButton
        {
            public Approach approach;
            public Button button;
            public TMP_Text label;
        }

        [Header("Akar (anak Canvas Overlay; yang di-show/hide)")]
        [SerializeField] private GameObject encounterRoot;
        [Tooltip("Panel setengah layar berisi roda, petunjuk, dan dialogue. Anchor-nya diatur script (kiri/kanan).")]
        [SerializeField] private RectTransform panelRoot;
        [Tooltip("Label nama NPC. Harus anak langsung EncounterRoot (bukan anak PanelRoot). " +
                 "Kalau 'Name Tag Follows Npc' aktif, pivot juga harus di tengah.")]
        [SerializeField] private RectTransform nameTag;

        [Header("Teks")]
        [SerializeField] private TMP_Text npcNameLabel;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private TMP_Text dialogueLabel;
        [SerializeField] private TMP_Text resonanceLabel;
        [Tooltip("Opsional: teks angka Resonansi (mis. 3 / 10). Boleh kosong jika progres hanya ditampilkan lewat ring.")]
        [SerializeField] private TMP_Text turnsLabel;
        [Tooltip("Bar Resonansi bulat. Isi dengan komponen ResonanceRing pada Image ring.")]
        [SerializeField] private ResonanceRing resonanceRing;

        [Header("Dialogue (deskripsi pembuka + dialogue bisa panjang)")]
        [Tooltip("Aktif = font dialogue mengecil otomatis (antara Min dan Max) supaya teks panjang muat di kotak.")]
        [SerializeField] private bool dialogueAutoSize = true;
        [SerializeField, Min(8f)] private float dialogueMinFontSize = 22f;
        [SerializeField, Min(8f)] private float dialogueMaxFontSize = 34f;

        [Header("Pilihan (isi 4, urutan = Atas, Kanan, Bawah, Kiri: OBS, KNO, ACT, SIL)")]
        [SerializeField] private ApproachButton[] approachButtons;

        [Header("Hasil (anak EncounterRoot)")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultLabel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button closeButton;

        [Header("Crosshair (opsional; disembunyikan selama encounter)")]
        [SerializeField] private Image crosshairImage;

        [Header("Penempatan")]
        [Tooltip("Auto = panel di sisi layar yang berlawanan dengan NPC.")]
        [SerializeField] private PanelSide side = PanelSide.Auto;
        [SerializeField, Range(.3f, .6f)] private float panelWidthRatio = .5f;
        [Tooltip("Aktif = label nama menempel di titik anchor NPC (perilaku lama). " +
                 "Nonaktif = label nama tetap di posisinya di Hierarchy (mis. pojok kanan atas) dan tidak digeser script.")]
        [SerializeField] private bool nameTagFollowsNpc = true;
        [Tooltip("Hanya dipakai kalau 'Name Tag Follows Npc' aktif. Geser label nama dari titik anchor NPC, dalam satuan canvas (+Y = ke atas).")]
        [SerializeField] private Vector2 nameTagOffset = Vector2.zero;
        [Tooltip("Hanya dipakai kalau 'Name Tag Follows Npc' aktif. Jarak minimum label nama dari tepi layar, dalam piksel.")]
        [SerializeField, Min(0f)] private float nameTagMargin = 24f;

        [Header("Input")]
        [Tooltip("Tombol 1-4 di keyboard memilih pendekatan (selain klik mouse).")]
        [SerializeField] private bool enableNumberShortcuts = true;

        public event Action<Approach> ApproachChosen;
        public event Action RetryClicked;
        public event Action CloseClicked;

        private bool _ready;
        private bool _inputEnabled;
        private Canvas _canvas;

        private void Awake() => TryInitialize();

        private bool TryInitialize()
        {
            if (_ready) return true;

            if (encounterRoot == null || panelRoot == null || npcNameLabel == null || hintLabel == null ||
                dialogueLabel == null || resonanceLabel == null || turnsLabel == null ||
                dialogueLabel == null || turnsLabel == null ||
                resultPanel == null || resultLabel == null || retryButton == null || closeButton == null ||
                approachButtons == null || approachButtons.Length != TrustExtensions.ApproachCount)
                return false;

            foreach (ApproachButton entry in approachButtons)
            {
                if (entry == null || entry.button == null) return false;
            }

            for (int i = 0; i < approachButtons.Length; i++)
            {
                ApproachButton current = approachButtons[i];
                Approach captured = current.approach;
                current.button.onClick.AddListener(() => Choose(captured));
                if (current.label != null)
                    current.label.text = $"[{i + 1}] {captured.Label()}";
            }

            retryButton.onClick.AddListener(() => RetryClicked?.Invoke());
            closeButton.onClick.AddListener(() => CloseClicked?.Invoke());

            Canvas canvas = encounterRoot.GetComponentInParent<Canvas>(true);
            _canvas = canvas != null ? canvas.rootCanvas : null;

            dialogueLabel.enableAutoSizing = dialogueAutoSize;
            if (dialogueAutoSize)
            {
                dialogueLabel.fontSizeMin = dialogueMinFontSize;
                dialogueLabel.fontSizeMax = Mathf.Max(dialogueMinFontSize, dialogueMaxFontSize);
            }

            resultPanel.SetActive(false);
            encounterRoot.SetActive(false);
            _ready = true;
            return true;
        }

        // ---------- ITrustEncounterView ----------

        /// <param name="anchorWorldPosition">Hanya dipakai untuk menempatkan label nama dan memilih sisi panel.</param>
        public void Show(string npcName, Vector3 anchorWorldPosition, Camera viewer)
        {
            if (!TryInitialize())
            {
                Debug.LogError($"{name}: TrustScreenEncounterUI punya referensi Inspector yang kosong atau tidak valid.", this);
                return;
            }

            if (viewer == null) viewer = Camera.main;

            npcNameLabel.text = npcName;
            if (resonanceRing != null) resonanceRing.SnapNext();   // encounter baru: bar tidak "meluncur" dari nilai NPC sebelumnya
            HideResult();
            encounterRoot.SetActive(true);
            if (crosshairImage != null) crosshairImage.enabled = false;

            Layout(anchorWorldPosition, viewer);
        }

        public void Hide()
        {
            _inputEnabled = false;
            if (encounterRoot != null) encounterRoot.SetActive(false);
            if (crosshairImage != null) crosshairImage.enabled = true;
        }

        public void SetDescription(string text)
        {
            if (hintLabel != null) hintLabel.text = text;
        }

        public void SetDialogue(string text)
        {
            if (dialogueLabel == null) return;
            dialogueLabel.text = text;

#if UNITY_EDITOR
            // Bantu penulis: kasih tahu kalau teks yang tergambar lebih tinggi dari kotaknya.
            // Diukur langsung (tinggi teks vs tinggi kotak), bukan lewat isTextOverflowing yang bisa meleset
            // kalau layout kotak belum dihitung saat dipanggil.
            if (dialogueLabel.gameObject.activeInHierarchy)
            {
                Canvas.ForceUpdateCanvases();
                dialogueLabel.ForceMeshUpdate();
                float textHeight = dialogueLabel.textBounds.size.y;
                float boxHeight = dialogueLabel.rectTransform.rect.height;
                if (textHeight > boxHeight + 1f)
                    Debug.LogWarning($"{name}: teks dialogue terlalu panjang untuk kotaknya " +
                                     $"(tinggi teks {textHeight:0} > kotak {boxHeight:0}, font {dialogueLabel.fontSize:0.#}). " +
                                     "Pendekkan deskripsi/dialogue NPC, atau tinggikan kotak DialogueBox.", this);
            }
#endif
        }

        public void SetStatus(int resonance, int target, int turnsLeft)
        {
            if (resonanceRing != null) resonanceRing.SetResonance(resonance, target);
            if (resonanceLabel != null) resonanceLabel.text = $"{resonance}";
            if (turnsLabel != null) turnsLabel.text = $"{turnsLeft}";
        }

        /// <summary>Kunci/buka tombol pilihan (mis. saat animasi hasil berjalan).</summary>
        public void SetInputEnabled(bool isEnabled)
        {
            _inputEnabled = isEnabled;
            if (approachButtons == null) return;

            foreach (ApproachButton entry in approachButtons)
            {
                if (entry != null && entry.button != null) entry.button.interactable = isEnabled;
            }
        }

        public void ShowResult(bool won, string message)
        {
            if (!_ready) return;
            resultPanel.SetActive(true);
            resultLabel.text = message;
            retryButton.gameObject.SetActive(!won);   // coba lagi hanya kalau kalah
        }

        public void HideResult()
        {
            if (resultPanel != null) resultPanel.SetActive(false);
        }

        // ---------- Input ----------

        private void Update()
        {
            if (!_inputEnabled || !enableNumberShortcuts) return;

            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) ChooseByIndex(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) ChooseByIndex(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) ChooseByIndex(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) ChooseByIndex(3);
        }

        private void ChooseByIndex(int index)
        {
            if (approachButtons == null || index < 0 || index >= approachButtons.Length) return;
            Choose(approachButtons[index].approach);
        }

        private void Choose(Approach approach)
        {
            if (!_inputEnabled) return;

            // Lepas seleksi supaya tombol tidak "menempel" dan tidak terpicu ulang oleh Submit.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            ApproachChosen?.Invoke(approach);
        }

        // ---------- Penempatan ----------

        /// <summary>
        /// Pilih sisi panel dan (kalau 'nameTagFollowsNpc' aktif) tempatkan label nama di titik NPC.
        /// Dipanggil sekali saat Show (kamera sudah diam di titik fokus).
        /// </summary>
        private void Layout(Vector3 anchorWorldPosition, Camera viewer)
        {
            Vector3 screen = viewer != null
                ? viewer.WorldToScreenPoint(anchorWorldPosition)
                : new Vector3(Screen.width * .5f, Screen.height * .5f, 1f);
            bool inFront = screen.z > 0f;

            bool panelOnLeft;
            if (side == PanelSide.Left) panelOnLeft = true;
            else if (side == PanelSide.Right) panelOnLeft = false;
            else panelOnLeft = !inFront || screen.x >= Screen.width * .5f;   // NPC di kanan -> panel di kiri

            panelRoot.anchorMin = new Vector2(panelOnLeft ? 0f : 1f - panelWidthRatio, 0f);
            panelRoot.anchorMax = new Vector2(panelOnLeft ? panelWidthRatio : 1f, 1f);
            panelRoot.offsetMin = Vector2.zero;
            panelRoot.offsetMax = Vector2.zero;

            if (nameTag == null || !nameTagFollowsNpc) return;

            // Pada Canvas Overlay, posisi dunia RectTransform = piksel layar, jadi cukup set 'position'.
            LayoutRebuilder.ForceRebuildLayoutImmediate(nameTag);
            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            Vector2 half = nameTag.rect.size * scale * .5f;

            Vector2 p = inFront
                ? new Vector2(screen.x, screen.y)
                : new Vector2(Screen.width * .5f, Screen.height * .5f);
            p += nameTagOffset * scale;
            p.x = Mathf.Clamp(p.x, half.x + nameTagMargin, Screen.width - half.x - nameTagMargin);
            p.y = Mathf.Clamp(p.y, half.y + nameTagMargin, Screen.height - half.y - nameTagMargin);

            nameTag.position = new Vector3(p.x, p.y, 0f);
        }
    }
}