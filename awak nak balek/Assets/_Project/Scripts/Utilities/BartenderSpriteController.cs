using UnityEngine;

public class BartenderPolished : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Biarkan kosong untuk otomatis menghadap Main Camera")]
    public Transform target;

    [Header("Area Gerak di Belakang Meja")]
    [Tooltip("Seberapa lebar bartender bisa bergeser ke kiri & kanan mengikuti player")]
    public float barSlideRange = 2.0f;
    [Tooltip("Seberapa maju dia mendekat ke bibir meja bar")]
    public float forwardStepToCounter = 0.35f;
    [Tooltip("Kehalusan gerakan saat mengejar posisi player (makin kecil makin ngebut)")]
    public float moveSmoothTime = 0.25f;

    [Header("LookAt (Cylindrical Billboard)")]
    public float lookAtSmoothSpeed = 15f;
    public bool invertLookDirection = false;

    [Header("Animasi Goyang & Mantul (Vibe Bartender)")]
    public bool enableMotion = true;
    [Tooltip("Kemiringan goyang kiri-kanan (derajat)")]
    public float swayAngle = 4.5f;
    [Tooltip("Tinggi mantul naik-turun saat goyang")]
    public float bobHeight = 0.03f;
    [Tooltip("Kecepatan ritme goyang")]
    public float rhythmSpeed = 5.5f;

    [Header("2.5D Card Flip (Balik Arah Sprite)")]
    public bool enablePeriodicFlip = true;
    public float minFlipInterval = 2.0f;
    public float maxFlipInterval = 4.5f;
    [Tooltip("Kecepatan animasi gepeng saat sprite berbalik arah")]
    public float flipAnimSpeed = 14f;

    private Vector3 anchorPosition;
    private Vector3 barRightAxis;
    private Vector3 barForwardAxis;

    private Vector3 currentBasePosition;
    private Vector3 moveVelocity;
    private Quaternion currentLookRotation;

    private Vector3 initialScale;
    private float targetScaleSign = 1f;
    private float currentScaleX;
    private float flipTimer;

    private void Start()
    {
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }

        // Simpan titik awal berdiri dan arah orientasi meja bar
        anchorPosition = transform.position;
        barRightAxis = transform.right;
        barForwardAxis = invertLookDirection ? -transform.forward : transform.forward;

        currentBasePosition = anchorPosition;
        currentLookRotation = transform.rotation;

        initialScale = transform.localScale;
        currentScaleX = initialScale.x;

        ResetFlipTimer();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        UpdatePositionAlongBar();
        UpdateLookAtRotation();
        UpdateCardFlip();
        ApplyVisualRhythm();
    }

    private void UpdatePositionAlongBar()
    {
        Vector3 toPlayer = target.position - anchorPosition;

        // Proyeksi posisi player ke sumbu kiri-kanan meja bar, lalu batasi agar tidak lewat ujung meja
        float lateralOffset = Mathf.Clamp(Vector3.Dot(toPlayer, barRightAxis), -barSlideRange, barSlideRange);

        // Cek apakah player berada di depan meja untuk menentukan seberapa maju dia mendekat
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

    private void UpdateLookAtRotation()
    {
        Vector3 flatDirection = target.position - currentBasePosition;
        flatDirection.y = 0f; // Kunci sumbu Y penuh agar sprite tidak mendongak/menunduk

        if (invertLookDirection)
        {
            flatDirection = -flatDirection;
        }

        if (flatDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(flatDirection);
            currentLookRotation = Quaternion.Slerp(
                currentLookRotation,
                targetRot,
                lookAtSmoothSpeed * Time.deltaTime
            );
        }
    }

    private void UpdateCardFlip()
    {
        if (enablePeriodicFlip)
        {
            flipTimer -= Time.deltaTime;
            if (flipTimer <= 0f)
            {
                targetScaleSign *= -1f;
                ResetFlipTimer();
            }
        }
        else
        {
            targetScaleSign = 1f;
        }

        float desiredScaleX = Mathf.Abs(initialScale.x) * targetScaleSign;
        currentScaleX = Mathf.Lerp(currentScaleX, desiredScaleX, flipAnimSpeed * Time.deltaTime);

        // Tambah sedikit efek memanjang (stretch) saat sprite sedang tipis berbalik arah
        float flipProgress = 1f - Mathf.Abs(currentScaleX / Mathf.Max(0.001f, Mathf.Abs(initialScale.x)));
        float stretchY = initialScale.y * (1f + flipProgress * 0.08f);

        transform.localScale = new Vector3(currentScaleX, stretchY, initialScale.z);
    }

    private void ApplyVisualRhythm()
    {
        if (!enableMotion)
        {
            transform.position = currentBasePosition;
            transform.rotation = currentLookRotation;
            return;
        }

        // Gelombang sinus untuk miring kiri-kanan, dan nilai mutlaknya untuk mantul naik-turun
        float wave = Mathf.Sin(Time.time * rhythmSpeed);
        float swayZ = wave * swayAngle;
        float bobY = Mathf.Abs(wave) * bobHeight;

        transform.position = currentBasePosition + Vector3.up * bobY;
        transform.rotation = currentLookRotation * Quaternion.Euler(0f, 0f, swayZ);
    }

    private void ResetFlipTimer()
    {
        flipTimer = Random.Range(minFlipInterval, maxFlipInterval);
    }

    // Visualisasi garis rel gerak di Scene View agar mudah diatur
    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? anchorPosition : transform.position;
        Vector3 right = Application.isPlaying ? barRightAxis : transform.right;
        Vector3 forward = Application.isPlaying
            ? barForwardAxis
            : (invertLookDirection ? -transform.forward : transform.forward);

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