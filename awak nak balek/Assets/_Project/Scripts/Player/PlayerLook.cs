using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
public class PlayerLook : MonoBehaviour
{
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float maxPitch = 85f;

    /// <summary>Sensitivitas mouse. Dipakai menu Settings (disimpan di PlayerPrefs oleh MainMenuController).</summary>
    public float Sensitivity
    {
        get => sensitivity;
        set => sensitivity = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Sudut lihat atas/bawah dalam derajat (positif = menunduk). Bisa diatur dari luar, mis. oleh IntroCutscene,
    /// dan gameplay melanjutkan dari nilai terakhir tanpa loncat.
    /// </summary>
    public float Pitch
    {
        get => _pitch;
        set
        {
            _pitch = Mathf.Clamp(value, -maxPitch, maxPitch);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }

    /// <summary>Titik mata (pivot kamera). Dipakai untuk menghitung arah ke target pandang.</summary>
    public Transform CameraPivot => cameraPivot;

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