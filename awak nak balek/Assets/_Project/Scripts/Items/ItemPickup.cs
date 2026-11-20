using UnityEngine;

/// <summary>
/// Item di dunia yang diambil lewat tombol Interact. Pasang di objek yang punya Collider
/// DAN berada di layer yang termasuk 'Interact Mask' PlayerInteractor.
/// Setelah diambil objeknya dimatikan (belum ada save: akan muncul lagi kalau scene dimuat ulang).
/// </summary>
public class ItemPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private ItemData item;

    private bool _taken;

    public string GetPrompt(GameObject interactor) =>
        item != null ? $"[E] Ambil {item.DisplayName}" : "[E] Ambil";

    public bool CanInteract => !_taken && item != null;

    public void Interact(GameObject interactor)
    {
        if (!CanInteract) return;

        if (!interactor.TryGetComponent(out PlayerInventory inventory))
        {
            Debug.LogError($"{name}: pemain tidak punya PlayerInventory.", this);
            return;
        }

        inventory.Add(item);
        _taken = true;

        if (interactor.TryGetComponent(out PlayerInteractor messages))
            messages.ShowMessage($"Mendapat: {item.DisplayName}");

        gameObject.SetActive(false);
    }
}
