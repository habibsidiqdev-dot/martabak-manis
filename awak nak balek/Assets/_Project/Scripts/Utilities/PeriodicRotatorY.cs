using System.Collections;
using UnityEngine;

public class PeriodicRotatorY : MonoBehaviour
{
    [Header("Pengaturan Rotasi (Flip)")]
    [SerializeField] private float targetAngle = -180f;    // Sudut tujuan pertama
    [SerializeField] private bool smoothRotation = false;    // Centang jika ingin putaran mulus
    [SerializeField] private float smoothDuration = 0.3f;    // Lama animasi jika smooth aktif

    [Header("Pengaturan Waktu Flip")]
    [SerializeField] private bool useRandomInterval = true;  // Centang agar waktu jeda acak
    [SerializeField] private float minInterval = 1.5f;       // Batas waktu tercepat (jika acak) / Waktu tetap
    [SerializeField] private float maxInterval = 4.0f;       // Batas waktu terlama (jika acak)

    [Header("Pengaturan Goyang (Idle Sway)")]
    [SerializeField] private bool enableSway = true;         // Centang untuk mengaktifkan goyangan
    [SerializeField] private float swayAngle = 5f;           // Seberapa jauh kemiringan goyang (derajat)
    [SerializeField] private float swaySpeed = 4f;           // Kecepatan goyangan

    private bool isAtTarget = false;
    private bool isFlipping = false;
    private Vector3 initialEulerAngles;
    private float currentBaseY = 0f;

    private void Start()
    {
        initialEulerAngles = transform.localEulerAngles;
        currentBaseY = initialEulerAngles.y;

        StartCoroutine(RotateRoutine());
    }

    private void Update()
    {
        // Jalankan efek goyang HANYA saat tidak sedang flip
        if (enableSway && !isFlipping)
        {
            float swayZ = Mathf.Sin(Time.time * swaySpeed) * swayAngle;

            transform.localRotation = Quaternion.Euler(
                initialEulerAngles.x,
                currentBaseY,
                initialEulerAngles.z + swayZ
            );
        }
    }

    private IEnumerator RotateRoutine()
    {
        while (true)
        {
            // Tentukan waktu tunggu setiap kali siklus baru dimulai
            float currentWaitTime = useRandomInterval
                ? Random.Range(minInterval, maxInterval)
                : minInterval;

            yield return new WaitForSeconds(currentWaitTime);

            // Mulai proses flip
            isFlipping = true;

            float nextY = isAtTarget ? initialEulerAngles.y : targetAngle;
            Quaternion targetRot = Quaternion.Euler(initialEulerAngles.x, nextY, initialEulerAngles.z);

            if (smoothRotation)
            {
                yield return StartCoroutine(SmoothRotate(targetRot, smoothDuration));
            }
            else
            {
                transform.localRotation = targetRot;
            }

            currentBaseY = nextY;
            isAtTarget = !isAtTarget;

            // Selesai flip
            isFlipping = false;
        }
    }

    private IEnumerator SmoothRotate(Quaternion endRot, float duration)
    {
        Quaternion startRot = transform.localRotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(startRot, endRot, elapsed / duration);
            yield return null;
        }

        transform.localRotation = endRot;
    }

    // Fungsi opsional jika ingin mengubah interval dari script lain saat game berjalan
    public void SetFlipInterval(float min, float max, bool isRandom = true)
    {
        minInterval = min;
        maxInterval = max;
        useRandomInterval = isRandom;
    }
}
