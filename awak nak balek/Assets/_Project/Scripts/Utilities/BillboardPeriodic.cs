using System.Collections;
using UnityEngine;

public class BillboardPeriodicRotator : MonoBehaviour
{
    [Header("Target Pengaturan (LookAt & Follow)")]
    [Tooltip("Biarkan kosong untuk otomatis mengikuti & menghadap Main Camera")]
    public Transform target;

    [Header("Area Gerak Mengikuti Player")]
    [Tooltip("Centang agar objek bergeser mengikuti posisi player")]
    public bool enableFollowMovement = true;
    [Tooltip("Seberapa lebar objek bisa bergeser ke kiri & kanan mengikuti player")]
    public float barSlideRange = 2.0f;
    [Tooltip("Seberapa maju objek mendekat ke depan saat player berada di hadapannya")]
    public float forwardStepToCounter = 0.35f;
    [Tooltip("Kehalusan gerakan saat mengejar posisi player (makin kecil makin cepat)")]
    public float moveSmoothTime = 0.25f;

    [Header("Pengaturan Smooth & Kecepatan LookAt")]
    [Tooltip("Semakin besar angkanya, semakin cepat sprite berputar (15 - 25 sangat responsif)")]
    public float smoothSpeed = 20f;

    [Header("Opsi Rotasi LookAt")]
    [Tooltip("Centang agar sprite tetap tegak lurus (tidak miring ke atas/bawah)")]
    public bool lockYAxis = true;
    [Tooltip("Centang jika sprite terlihat terbalik (membelakangi player)")]
    public bool invertDirection = false;

    [Header("Pengaturan Rotasi (Flip)")]
    [Tooltip("Sudut tujuan flip pada sumbu Y relatif terhadap arah kamera")]
    [SerializeField] private float targetAngle = -180f;
    [Tooltip("Centang jika ingin putaran flip mulus")]
    [SerializeField] private bool smoothRotation = false;
    [Tooltip("Lama animasi flip jika smooth aktif")]
    [SerializeField] private float smoothDuration = 0.3f;

    [Header("Pengaturan Waktu Flip")]
    [Tooltip("Centang agar waktu jeda flip acak")]
    [SerializeField] private bool useRandomInterval = true;
    [SerializeField] private float minInterval = 1.5f;
    [SerializeField] private float maxInterval = 4.0f;

    [Header("Pengaturan Goyang (Idle Sway)")]
    [Tooltip("Centang untuk mengaktifkan goyangan saat tidak flip")]
    [SerializeField] private bool enableSway = true;
    [SerializeField] private float swayAngle = 5f;
    [SerializeField] private float swaySpeed = 4f;

    private Vector3 anchorPosition;
    private Vector3 barRightAxis;
    private Vector3 barForwardAxis;

    private Vector3 currentBasePosition;
    private Vector3 moveVelocity;
    private Quaternion currentLookRotation;

    private float currentFlipY = 0f;
    private bool isAtTarget = false;
    private bool isFlipping = false;
    private Coroutine rotateCoroutine;

    private void Start()
    {
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }

        // Simpan titik awal berdiri dan sumbu arah gerak berdasarkan orientasi awal objek
        anchorPosition = transform.position;
        barRightAxis = transform.right;
        barForwardAxis = invertDirection ? -transform.forward : transform.forward;

        currentBasePosition = anchorPosition;
        currentLookRotation = transform.rotation;
    }

    private void OnEnable()
    {
        isFlipping = false;
        rotateCoroutine = StartCoroutine(RotateRoutine());
    }

    private void OnDisable()
    {
        if (rotateCoroutine != null)
        {
            StopCoroutine(rotateCoroutine);
            rotateCoroutine = null;
        }
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            if (enableFollowMovement)
            {
                UpdatePositionAlongBar();
            }

            UpdateLookRotation();
        }

        float currentSwayZ = (enableSway && !isFlipping)
            ? Mathf.Sin(Time.time * swaySpeed) * swayAngle
            : 0f;

        transform.position = currentBasePosition;

        // Menggabungkan rotasi arah kamera dengan rotasi lokal flip (Y) dan sway (Z)
        Quaternion localOffsetRotation = Quaternion.Euler(0f, currentFlipY, currentSwayZ);
        transform.rotation = currentLookRotation * localOffsetRotation;
    }

    private void UpdatePositionAlongBar()
    {
        Vector3 toPlayer = target.position - anchorPosition;

        // Proyeksi posisi player ke sumbu kiri-kanan, lalu batasi agar tidak melewati batas area
        float lateralOffset = Mathf.Clamp(Vector3.Dot(toPlayer, barRightAxis), -barSlideRange, barSlideRange);

        // Cek apakah player berada di depan objek untuk menentukan langkah maju
        float forwardDot = Vector3.Dot(toPlayer, barForwardAxis);
        float forwardOffset = forwardDot > 0f ? Mathf.Min(forwardDot * 0.3f, forwardStepToCounter) : 0f;

        Vector3 desiredBasePos = anchorPosition
                               + (barRightAxis * lateralOffset)
                               + (barForwardAxis * forwardOffset);

        currentBasePosition = Vector3.SmoothDamp(
            currentBasePosition,
            desiredBasePos,
            ref moveVelocity,
            moveSmoothTime
        );
    }

    private void UpdateLookRotation()
    {
        Vector3 targetPosition = target.position;

        if (lockYAxis)
        {
            targetPosition.y = currentBasePosition.y;
        }

        Vector3 direction = targetPosition - currentBasePosition;

        if (invertDirection)
        {
            direction = -direction;
        }

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            currentLookRotation = Quaternion.Slerp(
                currentLookRotation,
                targetRotation,
                smoothSpeed * Time.deltaTime
            );
        }
    }

    private IEnumerator RotateRoutine()
    {
        while (true)
        {
            float currentWaitTime = useRandomInterval
                ? Random.Range(minInterval, maxInterval)
                : minInterval;

            yield return new WaitForSeconds(currentWaitTime);

            isFlipping = true;
            float nextY = isAtTarget ? 0f : targetAngle;

            if (smoothRotation && smoothDuration > 0f)
            {
                yield return StartCoroutine(SmoothFlip(nextY, smoothDuration));
            }
            else
            {
                currentFlipY = nextY;
            }

            isAtTarget = !isAtTarget;
            isFlipping = false;
        }
    }

    private IEnumerator SmoothFlip(float endY, float duration)
    {
        float startY = currentFlipY;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            currentFlipY = Mathf.Lerp(startY, endY, elapsed / duration);
            yield return null;
        }

        currentFlipY = endY;
    }

    // Fungsi opsional jika ingin mengubah interval flip dari script lain saat game berjalan
    public void SetFlipInterval(float min, float max, bool isRandom = true)
    {
        minInterval = min;
        maxInterval = max;
        useRandomInterval = isRandom;
    }

    // Visualisasi batas gerak kiri-kanan (cyan) dan batas maju (hijau) di Scene View
    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? anchorPosition : transform.position;
        Vector3 right = Application.isPlaying ? barRightAxis : transform.right;
        Vector3 forward = Application.isPlaying
            ? barForwardAxis
            : (invertDirection ? -transform.forward : transform.forward);

        Vector3 leftPoint = center - right * barSlideRange;
        Vector3 rightPoint = center + right * barSlideRange;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(leftPoint, rightPoint);
        Gizmos.DrawSphere(leftPoint, 0.08f);
        Gizmos.DrawSphere(rightPoint, 0.08f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(leftPoint + forward * forwardStepToCounter, rightPoint + forward * forwardStepToCounter);
    }
}