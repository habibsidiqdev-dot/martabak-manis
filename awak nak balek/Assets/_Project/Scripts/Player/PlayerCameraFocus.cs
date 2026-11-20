using System;
using UnityEngine;

/// <summary>
/// Memindahkan kamera pemain ke sebuah titik fokus (Transform) dengan transisi halus, lalu mengembalikannya.
/// Pasang di root Player (satu GameObject dengan PlayerInteractor).
///
/// Kamera TETAP anak CameraPivot: selama fokus aktif, script ini menimpa pose DUNIA kamera di LateUpdate,
/// jadi PlayerLook tidak perlu diubah. Selama tidak ada fokus, script ini tidak menyentuh kamera sama sekali.
/// Pose "normal" dihitung dari parent kamera tiap frame, jadi saat kembali kamera mendarat tepat di arah
/// pandang terakhir pemain (input look sudah nol selama dikunci, jadi tidak ada lompatan).
/// </summary>
[DisallowMultipleComponent]
public class PlayerCameraFocus : MonoBehaviour
{
    [Tooltip("Kosong = Camera yang ada di anak Player.")]
    [SerializeField] private Camera viewCamera;

    [SerializeField, Min(0.05f)] private float blendDuration = 0.6f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Transform _cam;
    private Vector3 _restLocalPos;
    private Quaternion _restLocalRot;

    private Transform _focus;
    private float _weight;   // 0 = pose normal, 1 = di titik fokus
    private float _goal;
    private bool _active;
    private Action _onReached;

    public bool IsActive => _active;

    private void Awake()
    {
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        if (viewCamera == null)
        {
            Debug.LogError($"{name}: PlayerCameraFocus tidak menemukan Camera.", this);
            return;
        }

        _cam = viewCamera.transform;
        _restLocalPos = _cam.localPosition;
        _restLocalRot = _cam.localRotation;
    }

    private void OnDisable()
    {
        _onReached = null;
        ResetState();
    }

    /// <summary>Gerakkan kamera ke titik fokus. onReached dipanggil sekali saat kamera tiba.</summary>
    public void FocusOn(Transform point, Action onReached = null)
    {
        // Tanpa kamera/titik: langsung "tiba" supaya alur pemanggil tidak macet.
        if (_cam == null || point == null)
        {
            onReached?.Invoke();
            return;
        }

        _focus = point;
        _goal = 1f;
        _onReached = onReached;
        _active = true;
    }

    /// <summary>Kembalikan kamera ke pose normal. onReleased dipanggil sekali saat kamera sudah kembali.</summary>
    public void Release(Action onReleased = null)
    {
        if (!_active)
        {
            onReleased?.Invoke();
            return;
        }

        _goal = 0f;
        _onReached = onReleased;
    }

    /// <summary>Kembali seketika tanpa transisi (mis. saat NPC dinonaktifkan di tengah encounter).</summary>
    public void ReleaseImmediate()
    {
        _onReached = null;
        ResetState();
    }

    private void LateUpdate()
    {
        if (!_active) return;

        // unscaledDeltaTime: tetap jalan kalau suatu saat Time.timeScale dipakai untuk pause.
        _weight = Mathf.MoveTowards(_weight, _goal, Time.unscaledDeltaTime / blendDuration);
        float k = ease.Evaluate(_weight);

        Transform parent = _cam.parent;
        Vector3 restPos = parent != null ? parent.TransformPoint(_restLocalPos) : _restLocalPos;
        Quaternion restRot = parent != null ? parent.rotation * _restLocalRot : _restLocalRot;

        _cam.SetPositionAndRotation(
            Vector3.Lerp(restPos, _focus.position, k),
            Quaternion.Slerp(restRot, _focus.rotation, k));

        if (!Mathf.Approximately(_weight, _goal)) return;

        if (_goal <= 0f) ResetState();   // sudah kembali: lepas kendali atas kamera

        Action callback = _onReached;
        _onReached = null;
        callback?.Invoke();
    }

    private void ResetState()
    {
        _active = false;
        _weight = 0f;
        _goal = 0f;
        _focus = null;

        if (_cam != null)
        {
            _cam.localPosition = _restLocalPos;
            _cam.localRotation = _restLocalRot;
        }
    }
}