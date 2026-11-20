using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inventori sederhana. Satu entri = satu item (kalau punya 2 Kunci, ada 2 entri).
/// Item dipakai sebagai syarat interaksi dan DIKONSUMSI lewat TryConsumeAll.
/// Pasang di root Player.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private List<ItemData> startingItems = new List<ItemData>();

    private readonly List<ItemData> _items = new List<ItemData>();
    private bool _initialized;

    public IReadOnlyList<ItemData> Items
    {
        get
        {
            EnsureInitialized();
            return _items;
        }
    }

    public event Action Changed;

    private void EnsureInitialized()
    {
        // The demo builder adds this component before assigning its serialized list.
        // In editor sessions with reload disabled, Awake can run before that assignment.
        if (_initialized || !Application.isPlaying) return;
        _items.Clear();
        if (startingItems != null)
        {
            for (int i = 0; i < startingItems.Count; i++)
                if (startingItems[i] != null) _items.Add(startingItems[i]);
        }
        _initialized = true;
    }

    public void Add(ItemData item)
    {
        EnsureInitialized();
        if (item == null) return;
        _items.Add(item);
        Changed?.Invoke();
    }

    public bool Remove(ItemData item)
    {
        EnsureInitialized();
        bool removed = _items.Remove(item);
        if (removed) Changed?.Invoke();
        return removed;
    }

    public int Count(ItemData item)
    {
        EnsureInitialized();
        int count = 0;
        for (int i = 0; i < _items.Count; i++)
            if (_items[i] == item) count++;
        return count;
    }

    public bool Has(ItemData item) => Count(item) > 0;

    /// <summary>
    /// Item dari 'required' yang BELUM dimiliki. Item kembar dihitung per buah:
    /// required [Kunci, Kunci] dengan 1 Kunci di tas -> missing [Kunci]. Slot null dilewati.
    /// </summary>
    public List<ItemData> GetMissing(IReadOnlyList<ItemData> required)
    {
        EnsureInitialized();
        var missing = new List<ItemData>();
        if (required == null) return missing;

        var pool = new List<ItemData>(_items);
        for (int i = 0; i < required.Count; i++)
        {
            ItemData item = required[i];
            if (item == null) continue;
            if (!pool.Remove(item)) missing.Add(item);
        }
        return missing;
    }

    public bool HasAll(IReadOnlyList<ItemData> required) => GetMissing(required).Count == 0;

    /// <summary>
    /// Semua atau tidak sama sekali: kalau ada yang kurang, tidak ada item yang dihapus dan hasilnya false.
    /// Kalau lengkap, semua item di 'required' dihapus dari inventori (hilang) dan hasilnya true.
    /// </summary>
    public bool TryConsumeAll(IReadOnlyList<ItemData> required)
    {
        if (!HasAll(required)) return false;
        if (required == null) return true;

        for (int i = 0; i < required.Count; i++)
        {
            if (required[i] != null) _items.Remove(required[i]);
        }

        Changed?.Invoke();
        return true;
    }
}
