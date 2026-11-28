using TMPro;
using UnityEngine;

/// <summary>
/// Raycast dari kamera untuk mencari IInteractable di depan pemain.
/// Tombol Interact sudah ada di PlayerInputReader (OnInteractEvent); script ini yang memakainya.
/// Pasang di root Player (satu GameObject dengan PlayerInputReader).
/// Label prompt juga dipakai untuk pesan sementara lewat ShowMessage (mis. "Mendapat: Surat Jalan").
/// </summary>
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerInteractor : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Transform viewOrigin;           // isi dengan Main Camera
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactMask = ~0;    // Everything: tembok tetap menghalangi

    [Header("Prompt UI (opsional)")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptLabel;

    private PlayerInputReader _input;
    private IInteractable _focused;
    private bool _subscribed;
    private float _messageTimeLeft;

    private void Awake()
    {
        _input = GetComponent<PlayerInputReader>();
    }

    private void OnEnable() => EnsureSubscribed();

    private void Start() => EnsureSubscribed();

    private void EnsureSubscribed()
    {
        if (_input == null) _input = GetComponent<PlayerInputReader>();
        if (_input != null && !_subscribed)
        {
            _input.OnInteractEvent += HandleInteract;
            _subscribed = true;
        }
    }

    private void OnDisable()
    {
        if (_input != null && _subscribed) _input.OnInteractEvent -= HandleInteract;
        _subscribed = false;

        _messageTimeLeft = 0f;
        SetOutline(_focused, false);
        _focused = null;
        RefreshPrompt();
    }

    private void Update()
    {
        EnsureSubscribed();

        // Saat dialog/encounter aktif, jangan cari target & sembunyikan prompt
        SetFocus(_input.GameplayEnabled ? FindTarget() : null);
        TickMessage();
    }

    private IInteractable FindTarget()
    {
        if (!Physics.Raycast(viewOrigin.position, viewOrigin.forward, out RaycastHit hit,
                interactDistance, interactMask, QueryTriggerInteraction.Ignore))
            return null;

        IInteractable target = hit.collider.GetComponentInParent<IInteractable>();
        return target != null && target.CanInteract ? target : null;
    }

    private void HandleInteract()
    {
        if (_focused == null || !_focused.CanInteract) return;
        _focused.Interact(gameObject);
    }

    /// <summary>
    /// Tampilkan pesan singkat di label prompt. Selama pesan tampil, prompt biasa ditahan,
    /// lalu kembali mengikuti objek yang sedang dilihat.
    /// </summary>
    public void ShowMessage(string text, float duration = 2.5f)
    {
        if (string.IsNullOrEmpty(text)) return;

        _messageTimeLeft = Mathf.Max(0.1f, duration);
        if (promptLabel != null) promptLabel.text = text;
        if (promptRoot != null) promptRoot.SetActive(true);
    }

    private void TickMessage()
    {
        if (_messageTimeLeft <= 0f) return;

        _messageTimeLeft -= Time.unscaledDeltaTime;
        if (_messageTimeLeft <= 0f)
        {
            _messageTimeLeft = 0f;
            RefreshPrompt();
        }
    }

    private void SetFocus(IInteractable target)
    {
        if (ReferenceEquals(_focused, target)) return;

        SetOutline(_focused, false);
        _focused = target;
        SetOutline(_focused, true);

        RefreshPrompt();
    }

    /// <summary>
    /// Outline kertas di objek yang sedang disorot. InteractableOutline ditambahkan otomatis kalau belum ada,
    /// jadi semua interactable (pintu, item, NPC) langsung dapat outline tanpa setup per objek.
    /// </summary>
    private static void SetOutline(IInteractable target, bool on)
    {
        Component component = target as Component;
        if (component == null) return;   // bukan komponen, atau objeknya sudah dihancurkan

        InteractableOutline outline = component.GetComponent<InteractableOutline>();
        if (outline == null)
        {
            if (!on) return;
            outline = component.gameObject.AddComponent<InteractableOutline>();
        }

        outline.SetHighlighted(on);
    }

    private void RefreshPrompt()
    {
        if (_messageTimeLeft > 0f) return;   // pesan sementara menang atas prompt

        bool show = _focused != null;
        if (promptRoot != null) promptRoot.SetActive(show);
        if (show && promptLabel != null) promptLabel.text = _focused.GetPrompt(gameObject);
    }
}
