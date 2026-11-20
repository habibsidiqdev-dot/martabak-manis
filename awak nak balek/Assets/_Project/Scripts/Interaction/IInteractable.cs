using UnityEngine;

/// <summary>Apa pun yang bisa diajak interaksi lewat tombol Interact (NPC, pintu, item, dll).</summary>
public interface IInteractable
{
    /// <summary>
    /// Teks prompt untuk pemain yang sedang mengarahkan kamera ke objek ini.
    /// Menerima interactor karena teksnya bisa bergantung pada inventori
    /// (mis. "butuh Surat Jalan"). Dipanggil saat fokus berubah, bukan tiap frame.
    /// </summary>
    string GetPrompt(GameObject interactor);

    /// <summary>
    /// False = objek tidak bisa ditarget sama sekali (prompt tidak muncul), mis. sudah selesai.
    /// Objek yang TERKUNCI (butuh item) tetap true: pemain harus bisa menekan Interact dan melihat alasannya.
    /// </summary>
    bool CanInteract { get; }

    void Interact(GameObject interactor);
}
