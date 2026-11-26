using System;

/// <summary>
/// Kontrak antara DialogueInteractable dan tampilan dialogue. DialogueInteractable hanya tahu interface ini,
/// jadi tampilannya bisa diganti tanpa menyentuh logika (implementasi sekarang: DialogueScreenUI).
/// Sengaja terpisah dari ITrustEncounterView, yang bentuknya khusus trust (pilihan, resonansi, retry).
/// </summary>
public interface IDialogueView
{
    /// <summary>Pemain menekan "lanjut" (E / Spasi / Enter / klik kiri).</summary>
    event Action Advanced;

    /// <param name="speakerName">Kosong = tanpa label nama.</param>
    void Show(string speakerName);

    /// <param name="isLastPage">True = halaman terakhir (petunjuk berubah dari "Lanjut" jadi "Tutup").</param>
    void SetText(string text, bool isLastPage);

    void Hide();
}
