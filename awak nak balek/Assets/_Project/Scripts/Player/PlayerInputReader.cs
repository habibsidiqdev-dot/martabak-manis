using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pembaca input pemain. Nama class HARUS sama dengan nama file (PlayerInputReader),
/// dan sengaja tidak memakai nama "PlayerInput" karena bentrok dengan komponen bawaan Input System.
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    public event Action OnInteractEvent;

    /// <summary>Dipanggil saat GameplayEnabled BERUBAH (true = gameplay jalan, false = dialog/encounter/ending). Dipakai InventoryHudView.</summary>
    public event Action<bool> GameplayEnabledChanged;

    /// <summary>False saat dialog/encounter: gerak, look, dan interact dikunci, kursor bebas.</summary>
    public bool GameplayEnabled { get; private set; } = true;

    // Di-polling langsung (bukan disimpan dari callback), jadi selalu fresh
    // dan otomatis nol saat input dikunci.
    public Vector2 moveInput => GameplayEnabled ? GetControls().Player.Move.ReadValue<Vector2>() : Vector2.zero;
    public Vector2 lookInput => GameplayEnabled ? GetControls().Player.Look.ReadValue<Vector2>() : Vector2.zero;

    private PlayerControls _controls;

    private void Awake()
    {
        EnsureControls();
    }

    private void OnEnable() => EnsureControls().Enable();
    private void OnDisable() => _controls?.Disable();

    private void Start() => SetGameplayEnabled(true);

    private void OnDestroy()
    {
        if (_controls == null) return;
        _controls.Player.Interact.performed -= OnInteractPerformed;
        _controls.Dispose();
        _controls = null;
    }

    private PlayerControls GetControls() => _controls ?? EnsureControls();

    private PlayerControls EnsureControls()
    {
        if (_controls != null) return _controls;
        _controls = new PlayerControls();
        _controls.Player.Interact.performed += OnInteractPerformed;
        return _controls;
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (GameplayEnabled) OnInteractEvent?.Invoke();
    }

    /// <param name="updateCursor">
    /// False = kunci/buka input saja, kursor tidak diubah (dipakai selama transisi kamera,
    /// lalu kursor diatur terpisah lewat <see cref="SetCursorFree"/>).
    /// </param>
    public void SetGameplayEnabled(bool isEnabled, bool updateCursor = true)
    {
        bool changed = GameplayEnabled != isEnabled;
        GameplayEnabled = isEnabled;
        if (updateCursor) SetCursorFree(!isEnabled);
        if (changed) GameplayEnabledChanged?.Invoke(isEnabled);
    }

    /// <summary>true = kursor bebas dan terlihat (UI), false = terkunci di tengah dan tersembunyi (gameplay).</summary>
    public void SetCursorFree(bool free)
    {
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }
}