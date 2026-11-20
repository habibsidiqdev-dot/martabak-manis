using UnityEngine;

public class WorldSpaceSpriteLookAt : MonoBehaviour
{
    [Header("Target Pengaturan")]
    [Tooltip("Biarkan kosong untuk otomatis menghadap Main Camera")]
    public Transform target;

    [Header("Pengaturan Smooth & Kecepatan")]
    [Tooltip("Semakin besar angkanya, semakin cepat sprite berputar (15 - 25 sangat responsif)")]
    public float smoothSpeed = 20f;

    [Header("Opsi Rotasi")]
    [Tooltip("Centang agar sprite tetap tegak lurus (tidak miring ke atas/bawah)")]
    public bool lockYAxis = true;

    [Tooltip("Centang jika sprite terlihat terbalik (membelakangi player)")]
    public bool invertDirection = false;

    void Start()
    {
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }
    }

    void LateUpdate()
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

        // Mencegah error jika jarak terlalu dekat (hampir 0)
        if (direction.sqrMagnitude > 0.001f)
        {
            // Menghitung rotasi tujuan
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // Melakukan interpolasi rotasi secara halus dan cepat
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRotation, 
                smoothSpeed * Time.deltaTime
            );
        }
    }
}