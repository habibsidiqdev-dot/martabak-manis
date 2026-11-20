using System.Collections;
using UnityEngine;

public class PaperBackflip : MonoBehaviour
{
    [Header("Pengaturan Lompatan (Backflip)")]
    [SerializeField] private float jumpHeight = 2.5f;       // Tinggi lompatan backflip
    [SerializeField] private float flipDuration = 0.65f;    // Lama animasi dari lompat sampai mendarat
    [SerializeField] private int spinCount = 1;             // Jumlah putaran (1 = 360 derajat, 2 = 720 derajat)
    [SerializeField] private bool flipBackward = true;      // Centang untuk membalik arah putaran X

    [Header("Opsi Otomatis (Untuk Testing)")]
    [SerializeField] private bool autoTrigger = false;      // Centang jika ingin otomatis lompat berkala
    [SerializeField] private float autoInterval = 3f;       // Jeda waktu jika autoTrigger aktif

    private bool isFlipping = false;

    private void Start()
    {
        if (autoTrigger)
        {
            StartCoroutine(AutoFlipRoutine());
        }
    }

    /// <summary>
    /// Panggil fungsi ini dari script lain saat karakter melompat
    /// </summary>
    public void TriggerBackflip()
    {
        if (!isFlipping)
        {
            StartCoroutine(BackflipRoutine());
        }
    }

    private IEnumerator AutoFlipRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(autoInterval);
            TriggerBackflip();
        }
    }

    private IEnumerator BackflipRoutine()
    {
        isFlipping = true;

        // Simpan posisi dan rotasi awal sebelum melompat
        Vector3 startPos = transform.localPosition;
        Quaternion startRot = transform.localRotation;

        // Tentukan arah putaran sumbu X (ubah centang Flip Backward jika arah salto terbalik)
        float direction = flipBackward ? -1f : 1f;
        float totalRotation = 360f * spinCount * direction;

        float elapsed = 0f;

        while (elapsed < flipDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flipDuration);

            // 1. Gerakan Melompat (Kurva Parabola mulus: naik lalu turun)
            float heightOffset = Mathf.Sin(t * Mathf.PI) * jumpHeight;
            transform.localPosition = new Vector3(
                startPos.x,
                startPos.y + heightOffset,
                startPos.z
            );

            // 2. Gerakan Berputar pada Sumbu X menggunakan Quaternion agar bebas Gimbal Lock
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float currentAngleX = totalRotation * smoothT;
            transform.localRotation = startRot * Quaternion.Euler(currentAngleX, 0f, 0f);

            yield return null;
        }

        // Pastikan posisi dan rotasi kembali presisi ke titik awal saat mendarat
        transform.localPosition = startPos;
        transform.localRotation = startRot;

        isFlipping = false;
    }
}
