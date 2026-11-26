using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Interaksi yang HANYA memunculkan dialogue (tanpa mekanik trust), dan bisa memberi item.
/// Cocok untuk benda yang diperiksa (laci, surat, lemari) atau NPC yang cuma bicara.
/// Pasang di objek yang punya Collider DAN berada di layer yang termasuk 'Interact Mask' PlayerInteractor.
///
/// Alur: Interact -> input dikunci (kursor tetap tersembunyi) -> kamera pindah ke 'Camera Focus Point' (opsional)
/// -> halaman dialogue satu per satu (E / Spasi / Enter / klik kiri untuk lanjut) -> kamera kembali, input aktif lagi.
///
/// Item: masuk inventori SEKALI saja, tepat saat halaman terakhir ditampilkan (notifikasi "item didapat" muncul
/// berbarengan dengan teksnya). Setelah dialogue selesai, interaksi berikutnya memutar 'Repeat Pages';
/// kalau kosong, objek tidak bisa diinteraksi lagi (prompt hilang).
/// </summary>
public class DialogueInteractable : MonoBehaviour, IInteractable
{
    [Header("Prompt & tampilan")]
    [SerializeField] private string prompt = "[E] Periksa";
    [Tooltip("Label nama di atas dialogue. Kosong = tanpa label nama.")]
    [SerializeField] private string speakerName;
    [Tooltip("Komponen DialogueScreenUI. Kosong = dicari otomatis di scene.")]
    [SerializeField] private MonoBehaviour ui;

    [Header("Dialogue")]
    [Tooltip("Satu elemen = satu halaman. Kosong = tanpa dialogue (item langsung masuk, seperti ItemPickup).")]
    [SerializeField, TextArea(2, 5)] private string[] pages;
    [Tooltip("Dipakai kalau pemain berinteraksi lagi SETELAH selesai (mis. \"Lacinya sudah kosong.\"). " +
             "Kosong = tidak bisa diinteraksi lagi.")]
    [SerializeField, TextArea(2, 5)] private string[] repeatPages;

    [Header("Item")]
    [SerializeField] private ItemData[] rewardItems;

    [Header("Kamera")]
    [Tooltip("Empty GameObject tempat kamera pemain berpindah selama dialogue, rotasinya menghadap objek. " +
             "Kosong = kamera tidak bergerak.")]
    [SerializeField] private Transform cameraFocusPoint;

    [Tooltip("Dipanggil setelah dialogue PERTAMA selesai ditutup (item sudah masuk). Mis. membuka pintu, mengaktifkan objek.")]
    [SerializeField] private UnityEvent onFinished;

    private IDialogueView _view;
    private PlayerInputReader _playerInput;
    private PlayerInventory _inventory;
    private PlayerCameraFocus _cameraFocus;
    private string[] _script;
    private int _page;
    private bool _active;
    private bool _done;       // dialogue pertama sudah selesai
    private bool _rewarded;   // item sudah diberikan (sekali seumur hidup objek ini)

    private bool HasRepeat => repeatPages != null && repeatPages.Length > 0;

    private void OnValidate()
    {
        if (ui != null && !(ui is IDialogueView))
            Debug.LogWarning($"{name}: 'ui' harus komponen yang mengimplementasikan IDialogueView (DialogueScreenUI).", this);
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

    public string GetPrompt(GameObject interactor) => prompt;

    public bool CanInteract => !_active && (!_done || HasRepeat);

    public void Interact(GameObject interactor)
    {
        if (!CanInteract) return;

        _inventory = interactor.GetComponent<PlayerInventory>();   // null = tidak bisa memberi item

        string[] script = _done ? repeatPages : pages;
        if (script == null || script.Length == 0)
        {
            // Tanpa dialogue: langsung beri item dan selesai (tidak perlu UI atau mengunci input).
            GiveRewards();
            _done = true;
            onFinished.Invoke();
            return;
        }

        _view = ui as IDialogueView;
        if (_view == null && ui == null)
            _view = FindFirstObjectByType<DialogueScreenUI>(FindObjectsInactive.Include);
        if (_view == null)
        {
            Debug.LogError($"{name}: tidak ada DialogueScreenUI di scene (atau 'ui' bukan IDialogueView).", this);
            return;
        }

        _playerInput = interactor.GetComponent<PlayerInputReader>();
        _cameraFocus = interactor.GetComponent<PlayerCameraFocus>();   // null = kamera tidak bergerak

        _script = script;
        _page = 0;
        _active = true;

        // Input dikunci, tapi kursor TIDAK dimunculkan: dialogue tidak butuh kursor (lanjut lewat E / klik kiri).
        if (_playerInput != null) _playerInput.SetGameplayEnabled(false, updateCursor: false);

        if (cameraFocusPoint != null && _cameraFocus != null)
            _cameraFocus.FocusOn(cameraFocusPoint, BeginDialogue);
        else
            BeginDialogue();
    }

    // ---------- Alur dialogue ----------

    private void BeginDialogue()
    {
        if (!_active) return;   // dibatalkan (mis. objek dinonaktifkan) selagi kamera masih bergerak

        _view.Advanced += OnAdvanced;
        _view.Show(speakerName);
        ShowPage();
    }

    private void ShowPage()
    {
        bool isLast = _page >= _script.Length - 1;
        _view.SetText(_script[_page], isLast);

        if (isLast) GiveRewards();   // sekali saja (dijaga _rewarded)
    }

    private void OnAdvanced()
    {
        if (!_active) return;

        _page++;
        if (_page < _script.Length) ShowPage();
        else Finish();
    }

    private void Finish()
    {
        bool firstTime = !_done;
        _done = true;

        Cleanup(blendBack: true);   // kamera kembali dulu, baru gameplay aktif lagi

        if (firstTime) onFinished.Invoke();
    }

    private void GiveRewards()
    {
        if (_rewarded || rewardItems == null || rewardItems.Length == 0) return;

        if (_inventory == null)
        {
            Debug.LogWarning($"{name}: pemain tidak punya PlayerInventory, item tidak bisa diberikan.", this);
            return;
        }

        _rewarded = true;
        for (int i = 0; i < rewardItems.Length; i++)
        {
            // Notifikasi "item didapat" muncul sendiri (ItemNotificationUI, lewat PlayerInventory.ItemAdded).
            if (rewardItems[i] != null) _inventory.Add(rewardItems[i]);
        }
    }

    // ---------- Tutup ----------

    private static bool IsAlive(IDialogueView view) =>
        view != null && !(view is UnityEngine.Object unityObject && unityObject == null);

    private void OnDisable() => Cleanup();

    private void Cleanup(bool blendBack = false)
    {
        if (!_active) return;
        _active = false;

        // _view bisa hasil FindFirstObjectByType (field 'ui' kosong), jadi cek langsung: saat scene dibongkar
        // komponennya bisa sudah hancur.
        if (IsAlive(_view))
        {
            _view.Advanced -= OnAdvanced;
            _view.Hide();
        }

        PlayerInputReader input = _playerInput;

        if (blendBack && _cameraFocus != null && cameraFocusPoint != null)
        {
            // Input pemain baru dibuka setelah kamera mendarat di pose normal.
            _cameraFocus.Release(() => { if (input != null) input.SetGameplayEnabled(true); });
            return;
        }

        if (_cameraFocus != null) _cameraFocus.ReleaseImmediate();
        if (input != null) input.SetGameplayEnabled(true);
    }
}
