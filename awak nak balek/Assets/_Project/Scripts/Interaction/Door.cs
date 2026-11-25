using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pintu/gerbang yang dibuka lewat tombol Interact dan BERPUTAR REAL-TIME lewat script
/// (tidak memakai Animator/Animation). Bisa terkunci: butuh item tertentu di inventori.
///
/// Pasang di objek yang punya Collider DAN berada di layer yang termasuk 'Interact Mask' PlayerInteractor
/// (di scene ini: layer yang sama dengan NPC dan ItemPickup).
/// Jangan biarkan ada Animator yang aktif di objek yang sama: Animator akan menimpa rotasi dari script ini.
///
/// - Required Items kosong  = pintu biasa, tidak terkunci.
/// - Required Items terisi  = terkunci sampai pemain memegang semua item itu. Kunci hanya dipakai SEKALI:
///   begitu pintu terbuka, ia tidak terkunci lagi.
/// - Can Close dicentang    = setelah terbuka, pintu bisa ditutup lagi lewat Interact (buka-tutup bebas).
///   Tidak dicentang        = pintu hanya membuka sekali (cocok untuk gerbang alur cerita).
/// Belum ada save: pintu kembali tertutup dan terkunci kalau scene dimuat ulang.
/// Open(), Close(), dan Unlock() bersifat public supaya bisa dipanggil dari UnityEvent
/// (mis. onTrustWon milik NPC); Open/Close dari luar mengabaikan kunci dan Can Close.
/// </summary>
public class Door : MonoBehaviour, IInteractable
{
    [SerializeField] private string doorName = "Pintu";

    [Header("Kunci")]
    [Tooltip("Item yang dibutuhkan untuk membuka. Kosong = pintu tidak terkunci.")]
    [SerializeField] private List<ItemData> requiredItems = new List<ItemData>();

    [Tooltip("Centang = item hilang dari inventori saat pintu pertama kali dibuka.")]
    [SerializeField] private bool consumeItems = true;

    [Tooltip("Pesan saat pintu terkunci. {item} diganti daftar item yang masih kurang.")]
    [SerializeField] private string lockedMessage = "Terkunci. Butuh: {item}";

    [Header("Membuka (berputar real-time)")]
    [Tooltip("Titik engsel tempat pintu berputar. Kosong = berputar di pivot objek ini. " +
             "Kalau pivot pintu ada di tengah, buat objek kosong di tepi engsel dan tarik ke sini " +
             "(pilih objek Door untuk melihat titiknya sebagai bola kuning di Scene view).")]
    [SerializeField] private Transform hinge;

    [Tooltip("Sumbu putar dalam koordinat dunia. Pintu biasa yang berengsel tegak: (0, 1, 0).")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [Tooltip("Sudut buka dalam derajat. Negatif = berputar ke arah sebaliknya.")]
    [SerializeField] private float openAngle = 90f;

    [Tooltip("Lama membuka maupun menutup.")]
    [SerializeField, Min(0.05f)] private float openDuration = 1f;

    [Tooltip("Kurva gerak: sumbu X = waktu (0-1), sumbu Y = seberapa jauh pintu bergerak (0-1).")]
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Menutup (opsional)")]
    [Tooltip("Centang = pemain bisa menutup pintu lagi setelah terbuka. Kosongkan untuk gerbang yang hanya membuka sekali.")]
    [SerializeField] private bool canClose;

    [Tooltip("Pesan kalau pemain berdiri di jalur pintu saat mau menutup.")]
    [SerializeField] private string blockedMessage = "Ada yang menghalangi.";

    [Tooltip("Jarak ekstra (meter) di sekeliling posisi pintu tertutup yang dianggap menghalangi penutupan.")]
    [SerializeField, Min(0f)] private float blockPadding = 0.5f;

    [Header("Event (opsional)")]
    [Tooltip("Dipanggil saat pintu MULAI terbuka. Cocok untuk suara deritan.")]
    [SerializeField] private UnityEvent onOpened;
    [Tooltip("Dipanggil saat pintu MULAI menutup.")]
    [SerializeField] private UnityEvent onClosed;
    [Tooltip("Dipanggil tiap pemain mencoba membuka pintu yang masih terkunci. Cocok untuk suara 'klik'.")]
    [SerializeField] private UnityEvent onLockedAttempt;

    private bool _isOpen;      // keadaan tujuan: true = terbuka atau sedang membuka
    private bool _unlocked;
    private bool _moving;
    private Coroutine _routine;
    private float _angle;      // sudut sekarang, 0 = tertutup

    // Pose tertutup, diambil sekali
    private bool _poseReady;
    private Vector3 _pivot;
    private Vector3 _axis;
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private Bounds _closedBounds;
    private bool _hasBounds;

    public bool IsOpen => _isOpen;
    public bool IsLocked => !_unlocked && HasRequirement();

    private void Awake() => EnsurePose();

    private void OnDisable()
    {
        // Coroutine ikut berhenti saat dimatikan; selesaikan geraknya supaya pintu tidak macet di tengah jalan.
        if (_moving)
        {
            _angle = _isOpen ? openAngle : 0f;
            ApplyAngle();
            _moving = false;
            _routine = null;
        }
    }

    // ---------- IInteractable ----------

    // Terkunci tetap true: pemain harus bisa menekan Interact dan membaca alasannya.
    // Saat pintu sedang bergerak, tidak bisa ditarget.
    public bool CanInteract => !_moving && (!_isOpen || canClose);

    public string GetPrompt(GameObject interactor)
    {
        if (_isOpen) return $"[E] Tutup {doorName}";
        if (!IsLocked) return $"[E] Buka {doorName}";

        bool hasKeys = interactor != null
            && interactor.TryGetComponent(out PlayerInventory inventory)
            && inventory.HasAll(requiredItems);

        return hasKeys ? $"[E] Buka {doorName}" : $"[E] {doorName} (terkunci)";
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract) return;

        // Pintu terbuka (hanya bisa sampai sini kalau Can Close dicentang): tutup
        if (_isOpen)
        {
            if (IsBlocked(interactor))
            {
                ShowMessage(interactor, blockedMessage);
                return;
            }

            Close();
            return;
        }

        if (IsLocked)
        {
            interactor.TryGetComponent(out PlayerInventory inventory);

            if (inventory == null || !inventory.HasAll(requiredItems))
            {
                ShowMessage(interactor, BuildLockedMessage(inventory));
                onLockedAttempt?.Invoke();
                return;
            }

            if (consumeItems) inventory.TryConsumeAll(requiredItems);
            _unlocked = true;
        }

        Open();
    }

    // ---------- Bisa dipanggil dari luar (UnityEvent, script lain) ----------

    /// <summary>Buka pintu sekarang, mengabaikan kunci. Aman dipanggil berkali-kali.</summary>
    public void Open()
    {
        if (_isOpen) return;

        EnsurePose();
        _isOpen = true;
        _unlocked = true;

        onOpened?.Invoke();
        MoveTo(openAngle);
    }

    /// <summary>Tutup pintu sekarang, mengabaikan Can Close. Pintu tetap tidak terkunci setelahnya.</summary>
    public void Close()
    {
        if (!_isOpen) return;

        EnsurePose();
        _isOpen = false;

        onClosed?.Invoke();
        MoveTo(0f);
    }

    /// <summary>Lepas kunci tanpa membukanya: pintu bisa dibuka lewat Interact tanpa item.</summary>
    public void Unlock() => _unlocked = true;

    // ---------- Putaran ----------

    private void MoveTo(float targetAngle)
    {
        if (_routine != null) StopCoroutine(_routine);

        if (isActiveAndEnabled)
        {
            _routine = StartCoroutine(MoveRoutine(targetAngle));
        }
        else
        {
            // Objek sedang nonaktif: langsung ke posisi tujuan.
            _angle = targetAngle;
            ApplyAngle();
            _moving = false;
            _routine = null;
        }
    }

    // Bergerak dari sudut sekarang ke tujuan, jadi bisa berbalik arah di tengah jalan (mis. dipanggil dari event).
    private IEnumerator MoveRoutine(float targetAngle)
    {
        float from = _angle;
        _moving = true;

        for (float t = 0f; t < openDuration; t += Time.deltaTime)
        {
            _angle = Mathf.Lerp(from, targetAngle, openCurve.Evaluate(t / openDuration));
            ApplyAngle();
            yield return null;
        }

        _angle = targetAngle;
        ApplyAngle();

        _moving = false;
        _routine = null;
    }

    private void EnsurePose()
    {
        if (_poseReady) return;
        _poseReady = true;

        _pivot = hinge != null ? hinge.position : transform.position;
        _axis = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.up;
        _startPosition = transform.position;
        _startRotation = transform.rotation;

        // Area pintu saat tertutup, untuk mencegah pintu menutup di atas pemain.
        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (!colliders[i].enabled || colliders[i].isTrigger) continue;

            if (!_hasBounds)
            {
                _closedBounds = colliders[i].bounds;
                _hasBounds = true;
            }
            else
            {
                _closedBounds.Encapsulate(colliders[i].bounds);
            }
        }
    }

    // Pose dihitung dari pose tertutup (bukan ditambah sedikit-sedikit), jadi hasil akhirnya selalu tepat.
    private void ApplyAngle()
    {
        Quaternion delta = Quaternion.AngleAxis(_angle, _axis);
        transform.SetPositionAndRotation(_pivot + delta * (_startPosition - _pivot), delta * _startRotation);
    }

    // Pemain berdiri di jalur pintu tertutup? Dicek secara horizontal saja.
    private bool IsBlocked(GameObject interactor)
    {
        if (!_hasBounds || interactor == null) return false;

        Bounds area = _closedBounds;
        area.Expand(blockPadding * 2f);

        Vector3 position = interactor.transform.position;
        position.y = area.center.y;
        return area.Contains(position);
    }

    // ---------- Helper ----------

    private bool HasRequirement()
    {
        if (requiredItems == null) return false;

        for (int i = 0; i < requiredItems.Count; i++)
            if (requiredItems[i] != null) return true;

        return false;
    }

    private string BuildLockedMessage(PlayerInventory inventory)
    {
        if (string.IsNullOrEmpty(lockedMessage)) return null;

        List<ItemData> missing;
        if (inventory != null)
        {
            missing = inventory.GetMissing(requiredItems);
        }
        else
        {
            missing = new List<ItemData>();
            for (int i = 0; i < requiredItems.Count; i++)
                if (requiredItems[i] != null) missing.Add(requiredItems[i]);
        }

        return lockedMessage.Replace("{item}", ItemData.FormatList(missing));
    }

    private static void ShowMessage(GameObject interactor, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (interactor != null && interactor.TryGetComponent(out PlayerInteractor messenger))
            messenger.ShowMessage(text);
    }

    // Tampilkan titik engsel (bola kuning) dan sumbu putarnya saat objek dipilih di Scene view.
    private void OnDrawGizmosSelected()
    {
        Vector3 point = hinge != null ? hinge.position : transform.position;
        Vector3 axis = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.up;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(point, 0.08f);
        Gizmos.DrawLine(point - axis, point + axis);
    }
}