using System.Collections;
using UnityEngine;

public class SpriteBillboardMotion : MonoBehaviour
{
    [Header("Target LookAt")]
    [Tooltip("Biarkan kosong untuk otomatis menghadap Main Camera")]
    public Transform target;

    [Header("Sinkronisasi Camera Focus")]
    [Tooltip("Drag CameraFocusPoint ke sini (otomatis mencari sibling bernama 'CameraFocusPoint' jika kosong)")]
    [SerializeField] private Transform cameraFocusPoint;
    [Tooltip("Centang agar posisi & arah hadap CameraFocusPoint mengikuti arah hadap NPC")]
    [SerializeField] private bool syncFocusPoint = true;
    [Tooltip("Centang agar arah hadap (sumbu Z) CameraFocusPoint menghadap ke NPC (berlawanan arah player)")]
    [SerializeField] private bool faceFocusTowardNpc = true;

    [Header("Pengaturan Smooth & Kecepatan LookAt")]
    public float smoothSpeed = 20f;
    public bool lockYAxis = true;
    public bool invertDirection = false;

    [Header("Pengaturan Rotasi (Flip)")]
    [SerializeField] private float targetAngle = -180f;
    [SerializeField] private bool smoothRotation = true;
    [SerializeField] private float smoothDuration = 0.25f;

    [Header("Pengaturan Waktu Flip")]
    [SerializeField] private bool useRandomInterval = true;
    [SerializeField] private float minInterval = 1.5f;
    [SerializeField] private float maxInterval = 4.0f;

    [Header("Pengaturan Goyang (Idle Sway)")]
    [SerializeField] private bool enableSway = true;
    [SerializeField] private float swayAngle = 5f;
    [SerializeField] private float swaySpeed = 4f;
    [Tooltip("Pengali intensitas goyang saat sedang berinteraksi (0 = diam, 0.4 = lebih kalem)")]
    [Range(0f, 1f)]
    [SerializeField] private float interactionSwayMultiplier = 0.4f;

    private Quaternion currentLookRotation;
    private Vector3 initialFocusLocalPos;
    private float currentFlipY = 0f;
    private bool isAtTarget = false;
    private bool isFlipping = false;
    private bool isInteracting = false;

    private Coroutine rotateCoroutine;
    private Coroutine flipAnimCoroutine;

    private void Start()
    {
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }

        if (cameraFocusPoint == null && transform.parent != null)
        {
            cameraFocusPoint = transform.parent.Find("CameraFocusPoint");
        }

        if (cameraFocusPoint != null)
        {
            initialFocusLocalPos = cameraFocusPoint.localPosition;
        }

        currentLookRotation = transform.rotation;
    }

    private void OnEnable()
    {
        isFlipping = false;
        rotateCoroutine = StartCoroutine(RotateRoutine());
    }

    private void OnDisable()
    {
        if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
        if (flipAnimCoroutine != null) StopCoroutine(flipAnimCoroutine);
        rotateCoroutine = null;
        flipAnimCoroutine = null;
    }

    private void LateUpdate()
    {
        UpdateLookRotation();

        float activeSwayAngle = isInteracting ? swayAngle * interactionSwayMultiplier : swayAngle;
        float currentSwayZ = (enableSway && !isFlipping)
            ? Mathf.Sin(Time.time * swaySpeed) * activeSwayAngle
            : 0f;

        Quaternion localOffsetRotation = Quaternion.Euler(0f, currentFlipY, currentSwayZ);
        transform.rotation = currentLookRotation * localOffsetRotation;

        UpdateCameraFocusPoint();
    }

    private void UpdateLookRotation()
    {
        if (target == null) return;

        Vector3 targetPosition = target.position;
        if (lockYAxis)
        {
            targetPosition.y = transform.position.y;
        }

        Vector3 direction = targetPosition - transform.position;
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

    private void UpdateCameraFocusPoint()
    {
        if (!syncFocusPoint || cameraFocusPoint == null) return;

        // Arah rotasi yang mengarah langsung ke player
        Quaternion faceToPlayer = invertDirection
            ? currentLookRotation * Quaternion.Euler(0f, 180f, 0f)
            : currentLookRotation;

        // Balik 180 derajat jika CameraFocusPoint harus menghadap ke arah NPC (berlawanan dengan player)
        cameraFocusPoint.rotation = faceFocusTowardNpc
            ? faceToPlayer * Quaternion.Euler(0f, 180f, 0f)
            : faceToPlayer;

        // Jika CameraFocusPoint punya jarak offset (misal ditaruh di depan wajah NPC),
        // ikut putar posisinya mengikuti sudut hadap horizontal NPC
        if (transform.parent != null)
        {
            float baseRootYaw = transform.parent.eulerAngles.y + (invertDirection ? 180f : 0f);
            float yawDelta = Mathf.DeltaAngle(baseRootYaw, faceToPlayer.eulerAngles.y);
            cameraFocusPoint.localPosition = Quaternion.Euler(0f, yawDelta, 0f) * initialFocusLocalPos;
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

            yield return new WaitWhile(() => isInteracting);

            isFlipping = true;
            float nextY = isAtTarget ? 0f : targetAngle;

            if (smoothRotation && smoothDuration > 0f)
            {
                flipAnimCoroutine = StartCoroutine(SmoothFlip(nextY, smoothDuration));
                yield return flipAnimCoroutine;
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

    public void SetInteracting(bool interacting)
    {
        isInteracting = interacting;

        if (isInteracting && (isAtTarget || Mathf.Abs(currentFlipY) > 0.1f))
        {
            if (flipAnimCoroutine != null)
            {
                StopCoroutine(flipAnimCoroutine);
            }

            isAtTarget = false;
            isFlipping = true;

            if (smoothRotation && smoothDuration > 0f)
            {
                flipAnimCoroutine = StartCoroutine(ResetFlipToFront());
            }
            else
            {
                currentFlipY = 0f;
                isFlipping = false;
            }
        }
    }

    private IEnumerator ResetFlipToFront()
    {
        yield return SmoothFlip(0f, smoothDuration);
        isFlipping = false;
    }

    public void SetFlipInterval(float min, float max, bool isRandom = true)
    {
        minInterval = min;
        maxInterval = max;
        useRandomInterval = isRandom;
    }
}