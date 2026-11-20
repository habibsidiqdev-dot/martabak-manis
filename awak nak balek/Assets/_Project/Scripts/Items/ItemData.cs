using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data satu jenis item. Item dipakai sebagai SYARAT interaksi dengan NPC
/// (lihat TrustEncounterData.RequiredItems) dan HILANG dari inventori saat dipakai.
/// Buat lewat Create > Items > Item.
/// </summary>
[CreateAssetMenu(menuName = "Items/Item", fileName = "Item_New")]
public class ItemData : ScriptableObject
{
    [Tooltip("ID unik & stabil (untuk save nanti). Kosong = pakai nama asset. Jangan diubah setelah dipakai.")]
    [SerializeField] private string id;

    [SerializeField] private string displayName = "Item";
    [SerializeField, TextArea(1, 3)] private string description;
    [SerializeField] private Sprite icon;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;

    /// <summary>"Surat Jalan, Kunci Emas x2". Slot kosong (null) dilewati; item kembar digabung.</summary>
    public static string FormatList(IReadOnlyList<ItemData> items)
    {
        if (items == null) return string.Empty;

        var kinds = new List<ItemData>();
        var counts = new List<int>();
        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item == null) continue;

            int index = kinds.IndexOf(item);
            if (index < 0)
            {
                kinds.Add(item);
                counts.Add(1);
            }
            else
            {
                counts[index]++;
            }
        }

        var parts = new List<string>(kinds.Count);
        for (int i = 0; i < kinds.Count; i++)
            parts.Add(counts[i] > 1 ? $"{kinds[i].DisplayName} x{counts[i]}" : kinds[i].DisplayName);

        return string.Join(", ", parts);
    }
}
