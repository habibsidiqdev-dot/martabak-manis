using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace TrustSystem
{
    /// <summary>
    /// Pasang di NPC (harus punya Collider). Menghubungkan Interact -> logika (TrustEncounter) -> UI,
    /// mengunci input pemain selama encounter, dan mengelola syarat item, hadiah, dan retry.
    /// UI diakses lewat ITrustEncounterView (implementasinya: TrustWorldEncounterUI).
    ///
    /// Item: kalau TrustEncounterData.RequiredItems terisi, pemain harus punya semuanya untuk bicara dengan NPC ini.
    /// Item DIKONSUMSI sekali, saat pertemuan pertama dimulai. Setelah itu NPC ini "terbuka" selamanya,
    /// jadi kalah lalu mencoba lagi tidak meminta item lagi.
    /// </summary>
    public class TrustNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private TrustEncounterData data;

        [Tooltip("Komponen tampilan encounter. Harus mengimplementasikan ITrustEncounterView " +
                 "(TrustWorldEncounterUI).")]
        [SerializeField] private MonoBehaviour ui;

        [Tooltip("Titik di NPC tempat UI world space berpusat (sekitar dada). " +
                 "Kosong = posisi NPC + 1.2 m ke atas.")]
        [SerializeField] private Transform stageAnchor;

        [SerializeField, Min(0f)] private float resultDelay = 1.2f;

        [Header("Kamera")]
        [Tooltip("Empty GameObject (anak NPC) tempat kamera pemain berpindah selama encounter. " +
                 "Letakkan di depan NPC, rotasinya menghadap NPC (Align With View). " +
                 "Kosong = kamera tidak bergerak (perilaku lama).")]
        [SerializeField] private Transform cameraFocusPoint;

        [Tooltip("Dipanggil saat pemain menutup panel setelah MENANG (buka pintu, lanjut cerita, dll). " +
                 "Hadiah item (kalau ada) sudah masuk inventori sebelum ini dipanggil.")]
        [SerializeField] private UnityEvent onTrustWon;

        private ITrustEncounterView _view;
        private TrustEncounter _encounter;
        private PlayerInputReader _playerInput;
        private PlayerInventory _inventory;
        private PlayerInteractor _player;
        private PlayerCameraFocus _cameraFocus;
        private bool _active;
        private bool _resolving;
        private bool _won;
        private bool _unlocked;

        private Vector3 AnchorPosition =>
            stageAnchor != null ? stageAnchor.position : transform.position + Vector3.up * 1.2f;

        /// <summary>Syarat item yang MASIH harus dipenuhi. Null = tidak ada (tanpa syarat, atau sudah terbuka).</summary>
        private IReadOnlyList<ItemData> PendingRequirement
        {
            get
            {
                if (_unlocked || data == null) return null;
                IReadOnlyList<ItemData> required = data.RequiredItems;
                return required != null && required.Count > 0 ? required : null;
            }
        }

        private void OnValidate()
        {
            if (ui != null && !(ui is ITrustEncounterView))
                Debug.LogWarning($"{name}: 'ui' harus komponen yang mengimplementasikan ITrustEncounterView.", this);
        }

        private void OnDrawGizmosSelected()
        {
            if (cameraFocusPoint == null) return;

            Camera cam = Camera.main;
            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.TRS(cameraFocusPoint.position, cameraFocusPoint.rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, cam != null ? cam.fieldOfView : 60f, 4f, .1f, 16f / 9f);
        }

        // ---------- IInteractable ----------

        public string GetPrompt(GameObject interactor)
        {
            string npcName = data != null ? data.NpcName : "NPC";
            string prompt = $"[E] Bicara dengan {npcName}";

            IReadOnlyList<ItemData> required = PendingRequirement;
            if (required == null) return prompt;

            string names = ItemData.FormatList(required);
            if (names.Length == 0) return prompt;   // semua slot syarat kosong = tanpa syarat

            PlayerInventory inventory = interactor != null ? interactor.GetComponent<PlayerInventory>() : null;
            if (inventory == null) return $"{prompt}  (butuh: {names})";

            List<ItemData> missing = inventory.GetMissing(required);
            return missing.Count > 0
                ? $"{prompt}  (butuh: {ItemData.FormatList(missing)})"
                : $"{prompt}  (memakai: {names})";
        }

        // Tetap true saat terkunci: pemain harus bisa menekan Interact dan melihat alasannya.
        public bool CanInteract => !_active && !_won;

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;

            _view = ui as ITrustEncounterView;
            if (data == null || _view == null)
            {
                Debug.LogError($"{name}: 'data' belum di-assign, atau 'ui' bukan ITrustEncounterView.", this);
                return;
            }

            _playerInput = interactor.GetComponent<PlayerInputReader>();
            _inventory = interactor.GetComponent<PlayerInventory>();   // null = pemain tidak punya item
            _player = interactor.GetComponent<PlayerInteractor>();     // null = tanpa pesan di layar
            _cameraFocus = interactor.GetComponent<PlayerCameraFocus>();   // null = kamera tidak bergerak

            if (!TryUnlock()) return;

            Camera viewer = interactor.GetComponentInChildren<Camera>();
            if (viewer == null) viewer = Camera.main;

            _active = true;

            // Input langsung dikunci. Kalau kamera akan bergerak, kursor dibiarkan tersembunyi dulu
            // dan baru dimunculkan di StartEncounter (saat kamera tiba).
            bool focusCamera = cameraFocusPoint != null && _cameraFocus != null;
            if (_playerInput != null) _playerInput.SetGameplayEnabled(false, updateCursor: !focusCamera);

            _view.ApproachChosen += OnApproachChosen;
            _view.RetryClicked += OnRetry;
            _view.CloseClicked += OnClose;

            // UI baru muncul SETELAH kamera tiba, supaya penempatannya dihitung dari pose akhir kamera
            // (selalu sama, tidak peduli dari mana atau sejauh apa pemain berdiri saat menekan Interact).
            if (focusCamera)
                _cameraFocus.FocusOn(cameraFocusPoint, () => StartEncounter(viewer));
            else
                StartEncounter(viewer);
        }

        private void StartEncounter(Camera viewer)
        {
            if (!_active) return;   // dibatalkan (mis. NPC dinonaktifkan) selagi kamera masih bergerak

            if (_playerInput != null) _playerInput.SetCursorFree(true);   // kamera sudah tiba: kursor muncul

            _view.Show(data.NpcName, AnchorPosition, viewer);
            BeginEncounter();
        }

        // ---------- Syarat item ----------

        /// <summary>
        /// Semua atau tidak sama sekali: item baru dikonsumsi kalau pemain punya SEMUANYA.
        /// Kalau kurang, tidak ada yang hilang dan pemain mendapat pesan penolakan.
        /// </summary>
        private bool TryUnlock()
        {
            IReadOnlyList<ItemData> required = PendingRequirement;
            if (required == null)
            {
                _unlocked = true;
                return true;
            }

            if (_inventory != null && _inventory.TryConsumeAll(required))
            {
                _unlocked = true;
                return true;
            }

            string message = !string.IsNullOrEmpty(data.LockedDialogue)
                ? data.LockedDialogue
                : $"{data.NpcName} belum mau bicara. Butuh: {ItemData.FormatList(required)}";
            if (_player != null) _player.ShowMessage(message);
            return false;
        }

        // Notifikasi "item didapat" muncul sendiri di kanan bawah (ItemNotificationUI, lewat PlayerInventory.ItemAdded).
        private void GiveRewards()
        {
            IReadOnlyList<ItemData> rewards = data.RewardItems;
            if (_inventory == null || rewards == null) return;

            for (int i = 0; i < rewards.Count; i++)
            {
                if (rewards[i] != null) _inventory.Add(rewards[i]);
            }
        }

        // ---------- Alur encounter ----------

        private void BeginEncounter()
        {
            _encounter = new TrustEncounter(data.MaxTurns, data.TargetResonance, data.MoodPool, data.ClueChance);
            _encounter.Begin();

            _view.HideResult();
            _view.SetDialogue(ComposeIntro());   // deskripsi pembuka + dialogue pembuka, satu kotak
            PresentTurn();
        }

        // Narasi (deskripsi pembuka) tampil miring & sedikit redup di atas dialogue NPC, dipisah satu baris kosong.
        // Supaya jalan, gaya dasar DialogueLabel harus Normal. Trim() membuang sisa \r / spasi dari data.
        private const string NarrationColor = "#C7BDAD";

        private string ComposeIntro()
        {
            string narration = (data.IntroDescription ?? string.Empty).Trim();
            string line = (data.IntroDialogue ?? string.Empty).Trim();

            if (narration.Length == 0) return line;

            string styled = $"<i><color={NarrationColor}>{narration}</color></i>";
            return line.Length == 0 ? styled : styled + "\n\n" + line;
        }

        private void PresentTurn()
        {
            // Kotak petunjuk HANYA berisi petunjuk bahasa tubuh (miring).
            // Deskripsi pembuka ada di kotak dialogue (lihat ComposeIntro).
            var hints = new StringBuilder();
            IReadOnlyList<Clue> clues = _encounter.Clues;
            for (int i = 0; i < clues.Count; i++)
            {
                string line = data.GetClueText(clues[i].Approach, clues[i].Mood);
                if (!string.IsNullOrEmpty(line)) hints.AppendLine(line);
            }

            // Giliran ini tanpa petunjuk, atau semua teks petunjuknya kosong: pakai teks "sulit dibaca".
            if (hints.Length == 0) hints.Append(data.NoClueText);

            _view.SetDescription("<i>" + hints.ToString().Trim() + "</i>");
            RefreshStatus();
            _view.SetInputEnabled(true);
        }

        private void OnApproachChosen(Approach approach)
        {
            if (_resolving || _encounter == null || _encounter.State != EncounterState.AwaitingChoice) return;

            ChoiceResult result = _encounter.Choose(approach);
            StartCoroutine(ResolveRoutine(result));
        }

        private IEnumerator ResolveRoutine(ChoiceResult result)
        {
            _resolving = true;
            _view.SetInputEnabled(false);
            RefreshStatus();

            string reaction = data.GetReaction(
                result.Approach,
                result.Mood
            );
            // Kategori reaksi kosong: tampilkan "..." supaya deskripsi/dialogue pembuka tidak tertinggal di kotak.
            _view.SetDialogue(string.IsNullOrEmpty(reaction) ? "..." : reaction);

            string feedback = $"Kamu memilih {result.Approach.Label()}.   Resonansi {result.Delta.ToString("+0;-0;0")}";
            _view.SetDescription("<i>" + feedback + "</i>");   // tetap miring seperti petunjuk

            yield return new WaitForSeconds(resultDelay);

            switch (_encounter.State)
            {
                case EncounterState.Won:
                    _won = true;
                    GiveRewards();
                    _view.SetDialogue(data.WinDialogue);
                    _view.ShowResult(true, $"{data.NpcName} mempercayaimu.");
                    break;

                case EncounterState.Lost:
                    _view.SetDialogue(data.LoseDialogue);
                    _view.ShowResult(false, $"{data.NpcName} belum percaya. Resonansi {_encounter.Resonance} / {_encounter.TargetResonance}.");
                    break;

                default:
                    PresentTurn();   // giliran berikutnya, mood sudah diputar ulang di TrustEncounter
                    break;
            }

            _resolving = false;
        }

        // ---------- Retry / tutup ----------

        private void OnRetry()
        {
            if (_resolving || _encounter == null || _encounter.State != EncounterState.Lost) return;

            // Item sudah dibayar di pertemuan pertama (_unlocked), jadi retry gratis.
            BeginEncounter();
        }

        private void OnClose()
        {
            bool won = _won;
            Cleanup(blendBack: true);   // kamera kembali dulu, baru gameplay aktif lagi

            if (!won) return;

            onTrustWon.Invoke();
        }

        private void OnDisable() => Cleanup();

        private void Cleanup(bool blendBack = false)
        {
            if (!_active) return;

            StopAllCoroutines();
            _resolving = false;

            // 'ui' dicek dengan null-check Unity: saat scene dibongkar komponennya bisa sudah hancur.
            if (ui != null && _view != null)
            {
                _view.ApproachChosen -= OnApproachChosen;
                _view.RetryClicked -= OnRetry;
                _view.CloseClicked -= OnClose;
                _view.Hide();
            }

            _active = false;

            if (blendBack && _cameraFocus != null && cameraFocusPoint != null)
            {
                // Kursor langsung disembunyikan; input pemain baru dibuka setelah kamera mendarat di pose normal.
                PlayerInputReader input = _playerInput;
                if (input != null) input.SetCursorFree(false);
                _cameraFocus.Release(() =>
                {
                    if (input != null) input.SetGameplayEnabled(true);
                });
                return;
            }

            if (_cameraFocus != null) _cameraFocus.ReleaseImmediate();
            if (_playerInput != null) _playerInput.SetGameplayEnabled(true);
        }

        // ---------- Helper UI ----------

        private void RefreshStatus()
        {
            _view.SetStatus(_encounter.Resonance, _encounter.TargetResonance, _encounter.TurnsLeft);
        }
    }
}