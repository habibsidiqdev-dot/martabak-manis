using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dipasang di root PREFAB kartu notifikasi item. Tugasnya cuma mengisi tampilan dari ItemData;
/// desain (sprite, font, ukuran, layout) sepenuhnya diatur di prefab.
/// Semua referensi bersifat opsional: yang tidak kamu punya, kosongkan saja.
/// </summary>
public class ItemToastView : MonoBehaviour
{
    [Tooltip("Image untuk ikon item (diisi dari ItemData.Icon).")]
    [SerializeField] private Image iconImage;

    [Tooltip("Teks nama item.")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("Teks kecil, mis. 'ITEM DIDAPAT'. Isinya diambil dari field Caption di ItemNotificationUI.")]
    [SerializeField] private TMP_Text captionText;

    [Tooltip("Teks deskripsi item. Disembunyikan kalau deskripsi item kosong.")]
    [SerializeField] private TMP_Text descriptionText;

    [Tooltip("Objek pengganti yang tampil kalau item belum punya ikon (mis. gambar tanda tanya).")]
    [SerializeField] private GameObject noIconObject;

    public void Setup(ItemData item, string caption)
    {
        if (item == null) return;

        if (nameText != null) nameText.text = item.DisplayName;
        if (captionText != null) captionText.text = caption;

        bool hasDescription = !string.IsNullOrWhiteSpace(item.Description);
        if (descriptionText != null)
        {
            descriptionText.text = hasDescription ? item.Description : string.Empty;
            SetShown(descriptionText, hasDescription);
        }

        bool hasIcon = item.Icon != null;
        if (iconImage != null)
        {
            iconImage.sprite = item.Icon;
            iconImage.preserveAspect = true;
            SetShown(iconImage, hasIcon);
        }

        if (noIconObject != null) noIconObject.SetActive(!hasIcon);
    }

    // Kalau komponennya menempel di root prefab, jangan matikan GameObject-nya (itu mematikan seluruh kartu).
    private void SetShown(Graphic graphic, bool shown)
    {
        if (graphic.gameObject == gameObject) graphic.enabled = shown;
        else graphic.gameObject.SetActive(shown);
    }
}