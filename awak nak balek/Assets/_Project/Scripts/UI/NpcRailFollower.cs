using UnityEngine;

public class NpcRailFollower : MonoBehaviour
{
    [Header("Referensi Visual Child")]
    [Tooltip("Biarkan kosong untuk otomatis mencari SpriteBillboardMotion di child")]
    [SerializeField] private SpriteBillboardMotion spriteVisual;

    [Header("Target")]
    [Tooltip("Biarkan kosong untuk otomatis mengikuti Main Camera")]
    public Transform target;

    [Header("Area Gerak di Belakang Meja")]
    public bool enableFollowMovement = true;
    [Tooltip("Seberapa lebar NPC bisa bergeser ke kiri & kanan mengikuti player")]
    public float barSlideRange = 2.0f;
    [Tooltip("Seberapa maju NPC mendekat ke bibir meja")]
    public float forwardStepToCounter = 0.35f;
    [Tooltip("Kehalusan gerakan saat mengejar posisi player (makin kecil makin cepat)")]
    public float moveSmoothTime = 0.25f;
    [Tooltip("Centang jika arah depan Z root menghadap ke belakang meja")]
    public bool invertForwardAxis = false;

    private Vector3 anchorPosition;
    private Vector3 barRightAxis;
    private Vector3 barForwardAxis;

    private Vector3 currentBasePosition;
    private Vector3 moveVelocity;
    private bool isInteracting = false;

    private void Start()
    {
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }

        if (spriteVisual == null)
        {
            spriteVisual = GetComponentInChildren<SpriteBillboardMotion>();
        }

        anchorPosition = transform.position;
        barRightAxis = transform.right;
        barForwardAxis = invertForwardAxis ? -transform.forward : transform.forward;
        currentBasePosition = anchorPosition;
    }

    private void LateUpdate()
    {
        // Berhenti bergeser saat sedang diajak berinteraksi
        if (target == null || !enableFollowMovement || isInteracting) return;

        Vector3 toPlayer = target.position - anchorPosition;

        float lateralOffset = Mathf.Clamp(Vector3.Dot(toPlayer, barRightAxis), -barSlideRange, barSlideRange);
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

        transform.position = currentBasePosition;
    }

    /// <summary>
    /// Panggil method ini dengan parameter true saat dialog mulai, dan false saat dialog selesai.
    /// </summary>
    public void SetInteracting(bool interacting)
    {
        isInteracting = interacting;
        moveVelocity = Vector3.zero;

        if (spriteVisual != null)
        {
            spriteVisual.SetInteracting(interacting);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? anchorPosition : transform.position;
        Vector3 right = Application.isPlaying ? barRightAxis : transform.right;
        Vector3 forward = Application.isPlaying
            ? barForwardAxis
            : (invertForwardAxis ? -transform.forward : transform.forward);

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