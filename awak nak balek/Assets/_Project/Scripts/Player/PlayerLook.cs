using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
public class PlayerLook : MonoBehaviour
{
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float maxPitch = 85f;

    private PlayerInputReader _input;
    private float _pitch;

    private void Awake()
    {
        _input = GetComponent<PlayerInputReader>();
    }

    // Cursor lock sekarang diatur PlayerInputReader.SetGameplayEnabled()

    private void Update()
    {
        Vector2 delta = _input.lookInput * sensitivity;

        transform.Rotate(Vector3.up, delta.x);                       // putar badan (kiri/kanan)
        _pitch = Mathf.Clamp(_pitch - delta.y, -maxPitch, maxPitch); // lihat atas/bawah
        cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }
}
