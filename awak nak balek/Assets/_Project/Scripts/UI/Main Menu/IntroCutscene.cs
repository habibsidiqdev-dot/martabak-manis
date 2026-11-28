using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Satu baris ucapan MC: teks + (opsional) voice over.</summary>
[Serializable]
public class CutsceneLine
{
    [TextArea(1, 3)] public string text;

    [Tooltip("Voice over baris ini. Kosong = durasi dihitung dari panjang teks.")]
    public AudioClip voice;

    [Tooltip("Jeda (detik) setelah teks selesai diketik, sebelum baris berikutnya. Teks tetap terlihat selama jeda ini.")]
    [Min(0f)] public float pauseAfter = 0.6f;
}

/// <summary>
/// Cutscene pembuka: MC di dalam WC, napas terengah, bicara sendiri, pintu terbuka, MC berjalan keluar.
/// Seluruh urutan ada di satu coroutine (Run), jadi alurnya bisa dibaca dari atas ke bawah.
///
/// Pasang di GameObject kosong (mis. "IntroCutscene") di scene Prototype, lalu isi referensinya.
/// Dipanggil oleh MainMenuController saat Start Game, atau lewat Play() dari script/UnityEvent lain.
///
/// Selama cutscene: gerak/look/interact pemain dikunci (PlayerInputReader.SetGameplayEnabled(false)),
/// kursor disembunyikan. Setelah MC tiba di titik terakhir, input dibuka lagi dan gameplay mulai.
///
/// Dialog: kotak dialog fade in saat baris pertama, teks diketik per huruf (typewriter), lalu kotak
/// fade out setelah baris terakhir.
/// Jalan: MC melewati Walk Targets berurutan. Di tiap titik MC menoleh sesuai arah panah biru (sumbu Z)
/// objek titik itu.
/// </summary>
public class IntroCutscene : MonoBehaviour
{
    [Header("Pemain")]
    [Tooltip("Kosong = dicari otomatis di scene.")]
    [SerializeField] private PlayerInputReader input;
    [Tooltip("Kosong = diambil dari objek Player.")]
    [SerializeField] private CharacterController playerController;
    [Tooltip("Kamera pemain (yang digoyang). Kosong = Camera di anak Player.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Napas terengah")]
    [Tooltip("AudioSource dengan klip napas, centang Loop. Jangan centang Play On Awake.")]
    [SerializeField] private AudioSource breathSource;
    [SerializeField, Range(0f, 1f)] private float breathVolume = 0.8f;
    [Tooltip("Lama napas terengah sebelum MC mulai bicara.")]
    [SerializeField, Min(0f)] private float introDelay = 2.5f;
    [Tooltip("Lama napas/goyangan membesar di awal.")]
    [SerializeField, Min(0.05f)] private float fadeInDuration = 1f;

    [Header("Kamera bergoyang")]
    [Tooltip("Goyangan sudut maksimum (derajat). Mulai dari kecil.")]
    [SerializeField, Min(0f)] private float shakeRotation = 0.7f;
    [Tooltip("Goyangan posisi maksimum (meter).")]
    [SerializeField, Min(0f)] private float shakePosition = 0.012f;
    [Tooltip("Kecepatan goyangan acak.")]
    [SerializeField, Min(0f)] private float shakeSpeed = 1.4f;
    [Tooltip("Denyut goyangan mengikuti napas (kali per detik).")]
    [SerializeField, Min(0f)] private float breathPulseRate = 0.6f;

    [Header("Menu (sebelum Start): goyangan pelan, tanpa suara")]
    [Tooltip("Kekuatan goyangan di menu (0-1, relatif terhadap goyangan terengah). Kecil = nyaris tak terasa. 0 = diam.")]
    [SerializeField, Range(0f, 1f)] private float idleIntensity = 0.3f;
    [Tooltip("Kecepatan goyangan acak di menu. Kecil = melayang pelan.")]
    [SerializeField, Min(0f)] private float idleShakeSpeed = 0.35f;
    [Tooltip("Naik-turun pelan di menu (kali per detik). 0.2 = satu siklus tiap 5 detik.")]
    [SerializeField, Min(0f)] private float idleBreathPulseRate = 0.2f;
    [Tooltip("Lama goyangan menu muncul perlahan saat menu tampil.")]
    [SerializeField, Min(0.05f)] private float idleFadeInDuration = 2f;

    [Header("Arah pandang (opsional: kosong = tidak diatur)")]
    [Tooltip("Arah pandang MC sejak cutscene mulai sampai pintu terbuka. " +
             "Putar objek ini di Scene view; MC menoleh dan menunduk/mendongak mengikuti arah panah birunya (Z).")]
    [SerializeField] private Transform dialogueLookTarget;
    [Tooltip("Arah pandang MC saat pintu terbuka (ikuti arah panah biru / sumbu Z objek ini).")]
    [SerializeField] private Transform doorLookTarget;
    [SerializeField, Min(5f)] private float lookTurnSpeed = 70f;

    [Header("Dialog (voice over + teks)")]
    [Tooltip("AudioSource untuk suara MC (bukan yang dipakai napas).")]
    [SerializeField] private AudioSource voiceSource;
    [Tooltip("Panel teks ucapan (kotak dialog). CanvasGroup ditambahkan otomatis kalau belum ada, untuk fade.")]
    [SerializeField] private GameObject subtitleRoot;
    [SerializeField] private TMP_Text subtitleLabel;
    [Tooltip("Lama kotak dialog fade in (saat baris pertama) dan fade out (setelah baris terakhir).")]
    [SerializeField, Min(0f)] private float subtitleFadeDuration = 0.4f;
    [Tooltip("Kecepatan typewriter: detik per karakter. Dipakai kalau baris belum punya voice over; " +
             "kalau ada voice over, teks diketik selama durasi voice over.")]
    [SerializeField, Min(0.01f)] private float secondsPerCharacter = 0.06f;
    [Tooltip("Durasi minimum sebuah baris tanpa voice over (teks yang sudah selesai diketik ditahan sampai durasi ini).")]
    [SerializeField, Min(0.5f)] private float minLineDuration = 1.6f;
    [SerializeField] private CutsceneLine[] lines =
    {
        new CutsceneLine { text = "Gileee aku mau pulang!" },
        new CutsceneLine { text = "Tapi kalau aku pamit begitu saja aku akan dikira...." },
        new CutsceneLine { text = "\"Tidak beradab\"" },
        new CutsceneLine { text = "Kalau aku tidak diundang lagi, kesempatanku untuk menjadi orang keren hancur!" },
        new CutsceneLine { text = "Sebaiknya aku cari alasan bagus untuk pulang." },
    };

    [Header("Lewati dialog")]
    [Tooltip("Centang: tekan E saat satu baris dialog tampil untuk langsung lanjut ke baris berikutnya. " +
             "Voice over baris itu dipotong. Napas, pintu, dan jalan keluar tidak ikut dilewati.")]
    [SerializeField] private bool allowSkip = true;
    [Tooltip("Opsional: objek petunjuk (mis. teks \"[E] Lewati\") yang tampil selama kotak dialog terlihat. " +
             "Kosong = tanpa petunjuk.")]
    [SerializeField] private GameObject skipHint;

    [Header("Pintu")]
    [SerializeField] private Door door;
    [Tooltip("Jeda setelah Door.Open() sebelum MC mulai jalan. Samakan dengan 'Open Duration' di Door.")]
    [SerializeField, Min(0f)] private float waitAfterDoorOpen = 1.2f;

    [Header("MC maju")]
    [Tooltip("Titik-titik tujuan MC, dilewati berurutan dari atas ke bawah (Element 0 dulu). " +
             "Arah panah biru (Z) tiap objek = arah hadap MC saat tiba di titik itu.")]
    [SerializeField] private Transform[] walkTargets;
    [Tooltip("Centang: setelah tiba di tiap titik, MC menoleh sesuai orientasi (yaw) objek titik itu. " +
             "Matikan kalau MC cukup menghadap arah jalannya saja.")]
    [SerializeField] private bool faceTargetOrientation = true;
    [SerializeField, Min(0.1f)] private float walkSpeed = 2.2f;
    [SerializeField, Min(0.01f)] private float stopDistance = 0.15f;
    [Tooltip("Kecepatan MC menoleh (derajat/detik): ke arah jalan dan ke orientasi titik.")]
    [SerializeField, Min(10f)] private float turnSpeed = 240f;
    [Tooltip("Pengaman PER TITIK: kalau MC tidak sampai dalam waktu ini (mis. tersangkut), lanjut ke titik berikutnya.")]
    [SerializeField, Min(1f)] private float walkTimeout = 10f;
    [Tooltip("Napas & goyangan mereda selama ini saat MC mulai jalan.")]
    [SerializeField, Min(0.05f)] private float calmDuration = 1.5f;

    [Header("Selesai")]
    [SerializeField] private UnityEvent onFinished;

    private bool _running;
    private float _intensity;          // 0-1: kekuatan napas + goyangan
    private Vector3 _camRestPos;
    private Quaternion _camRestRot;
    private bool _camCaptured;

    private CanvasGroup _subtitleGroup;
    private bool _subtitleVisible;
    private bool _idleActive;          // goyangan pelan sudah menyala sebelum Play() (dari menu)
    private Coroutine _idleRoutine;
    private float _noiseTime;          // waktu noise yang dikumpulkan (kecepatan boleh berubah tanpa loncat)
    private float _pulsePhase;         // fase denyut napas, dikumpulkan dengan cara yang sama
    private PlayerLook _look;
    private Coroutine _lookRoutine;
    private bool _lineActive;          // sebuah baris dialog sedang tampil (E hanya berlaku saat ini)
    private bool _skipRequested;       // E ditekan: baris yang sedang tampil harus diakhiri

    public bool IsRunning => _running;

    private void Awake()
    {
        if (subtitleRoot != null)
        {
            if (!subtitleRoot.TryGetComponent(out _subtitleGroup))
                _subtitleGroup = subtitleRoot.AddComponent<CanvasGroup>();
            _subtitleGroup.alpha = 0f;
            subtitleRoot.SetActive(false);
        }

        if (skipHint != null) skipHint.SetActive(false);
    }

    // Tombol dibaca langsung (seperti DialogueScreenUI) karena input gameplay dikunci selama cutscene.
    private void Update()
    {
        if (!allowSkip || !_lineActive) return;

        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame) _skipRequested = true;
    }

    /// <summary>Mulai cutscene. onDone dipanggil sekali saat gameplay sudah dibuka lagi.</summary>
    public void Play(Action onDone = null)
    {
        if (_running) return;
        StartCoroutine(Run(onDone));
    }

    /// <summary>
    /// Nyalakan goyangan kamera yang PELAN (naik-turun seperti bernapas biasa), tanpa suara napas,
    /// supaya menu tidak membocorkan bahwa MC sedang panik di WC.
    /// Dipanggil MainMenuController saat menu tampil. Saat Play() dipanggil, goyangan ini
    /// menguat jadi napas terengah (fadeInDuration), lalu mereda saat MC berjalan keluar.
    /// </summary>
    public void BeginIdleShake()
    {
        if (_running || _idleActive || idleIntensity <= 0f) return;
        if (!ResolveReferences()) return;

        _idleActive = true;
        _idleRoutine = StartCoroutine(Ramp(0f, idleIntensity, idleFadeInDuration));
    }

    private void StartBreathing()
    {
        if (breathSource == null) return;
        breathSource.volume = 0f;
        breathSource.loop = true;
        breathSource.Play();
    }

    [ContextMenu("Tes: mainkan cutscene (saat Play)")]
    private void TestPlay()
    {
        if (Application.isPlaying) Play();
        else Debug.Log("Tes cutscene hanya jalan saat Play Mode.", this);
    }

    // ================= URUTAN CUTSCENE =================

    private IEnumerator Run(Action onDone)
    {
        _running = true;
        if (!Prepare()) { _running = false; onDone?.Invoke(); yield break; }

        // 1. Kunci input, kursor disembunyikan
        input.SetGameplayEnabled(false, updateCursor: false);
        input.SetCursorFree(false);

        // Pandangan MC pelan-pelan beralih ke titik dialog (kosong = pandangan dibiarkan)
        if (dialogueLookTarget != null) LookAtTarget(dialogueLookTarget);

        // 2. Napas terengah + kamera bergoyang, lalu tenang sejenak sebelum bicara.
        //    Kalau goyangan pelan dari menu masih jalan, hentikan fade-in-nya dan menguat dari level sekarang.
        if (_idleRoutine != null) { StopCoroutine(_idleRoutine); _idleRoutine = null; }
        _idleActive = false;

        StartBreathing();
        yield return Ramp(_intensity, 1f, fadeInDuration);
        yield return new WaitForSeconds(introDelay);

        // 3. Ucapan MC satu per satu (kotak fade in di baris pertama, teks typewriter)
        for (int i = 0; i < lines.Length; i++)
            yield return SpeakLine(lines[i]);
        yield return HideSubtitleBox();

        // 4. Pintu terbuka
        if (door != null) door.Open();
        else Debug.LogWarning($"{name}: 'Door' kosong, pintu tidak dibuka.", this);
        if (doorLookTarget != null) LookAtTarget(doorLookTarget);
        yield return new WaitForSeconds(waitAfterDoorOpen);

        // 5. MC tenang, lalu maju melewati semua titik tujuan
        Coroutine calm = StartCoroutine(Ramp(_intensity, 0f, calmDuration, stopBreathAtEnd: true));
        if (dialogueLookTarget != null || doorLookTarget != null)
            LookAtTarget(null);   // saat berjalan, hanya pandangan atas/bawah yang kembali lurus; arah jalan diurus WalkRoute
        yield return WalkRoute();
        if (_lookRoutine != null) yield return _lookRoutine;   // pastikan pandangan sudah lurus sebelum gameplay
        yield return calm;   // kalau jalannya lebih singkat dari calmDuration, tunggu sampai napas/goyangan benar-benar berhenti

        // 6. Gameplay dimulai
        input.SetGameplayEnabled(true);
        _running = false;
        onFinished?.Invoke();
        onDone?.Invoke();
    }

    // ================= DIALOG =================

    private IEnumerator SpeakLine(CutsceneLine line)
    {
        if (line == null || string.IsNullOrEmpty(line.text)) yield break;

        // Kotak muncul (fade in) hanya kalau belum terlihat, jadi cuma di baris pertama.
        yield return ShowSubtitleBox();

        _skipRequested = false;   // E yang ditekan sebelum baris ini tampil tidak dihitung
        _lineActive = true;

        // Siapkan teks tapi belum ada huruf yang terlihat.
        int totalChars = line.text.Length;
        if (subtitleLabel != null)
        {
            subtitleLabel.text = line.text;
            subtitleLabel.maxVisibleCharacters = 0;
            subtitleLabel.ForceMeshUpdate();
            totalChars = subtitleLabel.textInfo.characterCount;   // tag rich text tidak dihitung
        }

        bool hasVoice = line.voice != null && voiceSource != null;
        float typeDuration;   // lama mengetik
        float lineDuration;   // lama total baris sebelum pauseAfter
        if (hasVoice)
        {
            voiceSource.Stop();
            voiceSource.clip = line.voice;
            voiceSource.Play();
            typeDuration = line.voice.length;   // teks selesai diketik bareng voice over
            lineDuration = typeDuration;
        }
        else
        {
            typeDuration = totalChars * secondsPerCharacter;
            lineDuration = Mathf.Max(minLineDuration, typeDuration);
        }

        // Typewriter
        if (subtitleLabel != null && typeDuration > 0f)
        {
            for (float t = 0f; t < typeDuration && !_skipRequested; t += Time.deltaTime)
            {
                subtitleLabel.maxVisibleCharacters = Mathf.CeilToInt(totalChars * (t / typeDuration));
                yield return null;
            }
        }
        if (subtitleLabel != null) subtitleLabel.maxVisibleCharacters = totalChars;

        // Tahan sisa durasi baris (kalau mengetik lebih cepat dari durasi minimum)
        float hold = lineDuration - typeDuration;
        if (hold > 0f) yield return WaitOrSkip(hold);

        // pauseAfter: teks yang sudah lengkap tetap terlihat, baru baris berikutnya menggantikannya.
        if (line.pauseAfter > 0f) yield return WaitOrSkip(line.pauseAfter);

        // Baris dilewati lewat E: potong voice over-nya (baris berikutnya memang memulai klip baru).
        if (_skipRequested && voiceSource != null) voiceSource.Stop();

        _lineActive = false;
        _skipRequested = false;
    }

    // Menunggu 'seconds' detik, atau berhenti lebih awal kalau E ditekan.
    private IEnumerator WaitOrSkip(float seconds)
    {
        for (float t = 0f; t < seconds && !_skipRequested; t += Time.deltaTime)
            yield return null;
    }

    private IEnumerator ShowSubtitleBox()
    {
        if (_subtitleVisible || subtitleRoot == null) yield break;
        _subtitleVisible = true;

        if (subtitleLabel != null) subtitleLabel.text = "";
        if (_subtitleGroup != null) _subtitleGroup.alpha = 0f;
        subtitleRoot.SetActive(true);
        if (allowSkip && skipHint != null) skipHint.SetActive(true);
        yield return FadeSubtitle(0f, 1f);
    }

    private IEnumerator HideSubtitleBox()
    {
        if (!_subtitleVisible || subtitleRoot == null) yield break;

        if (skipHint != null) skipHint.SetActive(false);
        yield return FadeSubtitle(1f, 0f);
        subtitleRoot.SetActive(false);
        _subtitleVisible = false;
    }

    private IEnumerator FadeSubtitle(float from, float to)
    {
        if (_subtitleGroup == null) yield break;

        if (subtitleFadeDuration <= 0f)
        {
            _subtitleGroup.alpha = to;
            yield break;
        }

        for (float t = 0f; t < subtitleFadeDuration; t += Time.deltaTime)
        {
            _subtitleGroup.alpha = Mathf.Lerp(from, to, t / subtitleFadeDuration);
            yield return null;
        }
        _subtitleGroup.alpha = to;
    }

    // ================= JALAN =================

    private IEnumerator WalkRoute()
    {
        if (walkTargets == null || walkTargets.Length == 0)
        {
            Debug.LogWarning($"{name}: 'Walk Targets' kosong, MC tidak berjalan.", this);
            yield break;
        }

        Transform body = playerController != null ? playerController.transform : input.transform;

        for (int i = 0; i < walkTargets.Length; i++)
        {
            Transform target = walkTargets[i];
            if (target == null)
            {
                Debug.LogWarning($"{name}: Walk Targets[{i}] kosong, dilewati.", this);
                continue;
            }

            yield return WalkTo(body, target, i);
            if (faceTargetOrientation) yield return TurnToTargetYaw(body, target);
        }
    }

    private IEnumerator WalkTo(Transform body, Transform target, int index)
    {
        float elapsed = 0f;

        while (elapsed < walkTimeout)
        {
            Vector3 toTarget = target.position - body.position;
            toTarget.y = 0f;
            if (toTarget.magnitude <= stopDistance) yield break;

            // Menoleh pelan ke arah jalan (hanya yaw; pitch kamera dibiarkan).
            Quaternion look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            body.rotation = Quaternion.RotateTowards(body.rotation, look, turnSpeed * Time.deltaTime);

            if (playerController != null)
                playerController.Move(toTarget.normalized * (walkSpeed * Time.deltaTime));
            else
                body.position += toTarget.normalized * (walkSpeed * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning($"{name}: MC tidak sampai ke Walk Targets[{index}] dalam {walkTimeout} detik (tersangkut?). Lanjut.", this);
    }

    // Setelah tiba di sebuah titik: putar badan MC (yaw saja) sampai searah panah biru objek titik itu.
    private IEnumerator TurnToTargetYaw(Transform body, Transform target)
    {
        Vector3 forward = target.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) yield break;   // objek menghadap lurus atas/bawah: tidak ada yaw

        Quaternion goal = Quaternion.LookRotation(forward.normalized, Vector3.up);

        while (Quaternion.Angle(body.rotation, goal) > 0.5f)
        {
            body.rotation = Quaternion.RotateTowards(body.rotation, goal, turnSpeed * Time.deltaTime);
            yield return null;
        }
        body.rotation = goal;
    }

    // ================= ARAH PANDANG =================

    // Mulai di latar belakang (cutscene tidak menunggu), menggantikan gerakan pandangan sebelumnya.
    // target == null: hanya pandangan atas/bawah yang dikembalikan lurus; badan (yaw) tidak disentuh.
    private void LookAtTarget(Transform target)
    {
        if (_look == null)
        {
            Debug.LogWarning($"{name}: PlayerLook tidak ditemukan di Player, arah pandang tidak diatur.", this);
            return;
        }

        StopLook();
        _lookRoutine = StartCoroutine(LookRoutine(target));
    }

    private void StopLook()
    {
        if (_lookRoutine == null) return;
        StopCoroutine(_lookRoutine);
        _lookRoutine = null;
    }

    private IEnumerator LookRoutine(Transform target)
    {
        Transform body = _look.transform;

        while (true)
        {
            float step = lookTurnSpeed * Time.deltaTime;
            float goalPitch = 0f;
            bool yawDone = true;

            if (target != null)
            {
                Vector3 dir = target.forward;
                float flat = new Vector2(dir.x, dir.z).magnitude;

                if (flat > 0.001f)
                {
                    float goalYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    float yaw = Mathf.MoveTowardsAngle(body.eulerAngles.y, goalYaw, step);
                    body.rotation = Quaternion.Euler(0f, yaw, 0f);
                    yawDone = Mathf.Abs(Mathf.DeltaAngle(yaw, goalYaw)) < 0.1f;
                }

                // Pitch Unity: positif = menunduk, jadi arah forward ke bawah (dir.y < 0) memberi pitch positif.
                goalPitch = Mathf.Atan2(-dir.y, flat) * Mathf.Rad2Deg;
            }

            float nextPitch = Mathf.MoveTowards(_look.Pitch, goalPitch, step);
            _look.Pitch = nextPitch;
            bool pitchDone = Mathf.Abs(_look.Pitch - goalPitch) < 0.1f || !Mathf.Approximately(_look.Pitch, nextPitch);

            if (yawDone && pitchDone) break;
            yield return null;
        }
    }

    // ================= NAPAS & GOYANGAN =================

    // Mengubah _intensity dari 'from' ke 'to'; volume napas ikut berubah.
    private IEnumerator Ramp(float from, float to, float duration, bool stopBreathAtEnd = false)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            _intensity = Mathf.Lerp(from, to, t / duration);
            ApplyBreathVolume();
            yield return null;
        }

        _intensity = to;
        ApplyBreathVolume();
        if (stopBreathAtEnd && breathSource != null) breathSource.Stop();
    }

    // 0 = goyangan pelan di menu (atau tenang), 1 = terengah penuh.
    private float Agitation => Mathf.InverseLerp(idleIntensity, 1f, _intensity);

    private void ApplyBreathVolume()
    {
        // Suara napas hanya terdengar saat mulai terengah; goyangan pelan di menu tanpa suara.
        if (breathSource != null) breathSource.volume = Agitation * breathVolume;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null) return;

        if (_intensity <= 0.0001f)
        {
            // Goyangan selesai: kembalikan kamera ke pose aslinya sekali saja.
            if (_camCaptured)
            {
                cameraTransform.localPosition = _camRestPos;
                cameraTransform.localRotation = _camRestRot;
                _camCaptured = false;
            }
            return;
        }

        if (!_camCaptured)
        {
            _camRestPos = cameraTransform.localPosition;
            _camRestRot = cameraTransform.localRotation;
            _camCaptured = true;
        }

        // Pelan di menu, makin cepat saat terengah. Waktu/fase dikumpulkan per frame supaya
        // perubahan kecepatan tidak membuat kamera loncat.
        float agitation = Agitation;
        _noiseTime += Time.deltaTime * Mathf.Lerp(idleShakeSpeed, shakeSpeed, agitation);
        _pulsePhase += Time.deltaTime * Mathf.Lerp(idleBreathPulseRate, breathPulseRate, agitation);

        float t = _noiseTime;
        Vector3 noise = new Vector3(
            Mathf.PerlinNoise(t, 0f),
            Mathf.PerlinNoise(0f, t + 17f),
            Mathf.PerlinNoise(t + 31f, t)) * 2f - Vector3.one;

        // Denyut mengikuti napas: goyangan lebih kuat saat "tarik napas".
        float pulse = Mathf.Lerp(0.5f, 1f, 0.5f + 0.5f * Mathf.Sin(_pulsePhase * Mathf.PI * 2f));
        float k = _intensity * pulse;

        cameraTransform.localPosition = _camRestPos + new Vector3(noise.x, noise.y, 0f) * (shakePosition * k);
        cameraTransform.localRotation = _camRestRot * Quaternion.Euler(
            noise.y * shakeRotation * k,
            noise.x * shakeRotation * k,
            noise.z * shakeRotation * k);
    }

    // ================= PERSIAPAN =================

    // Cari referensi pemain yang masih kosong. Dipakai Prepare() dan BeginIdleShake() (sebelum cutscene jalan).
    private bool ResolveReferences()
    {
        if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
        if (input == null)
        {
            Debug.LogError($"{name}: PlayerInputReader tidak ditemukan di scene.", this);
            return false;
        }

        if (playerController == null) playerController = input.GetComponent<CharacterController>();
        if (_look == null) _look = input.GetComponent<PlayerLook>();

        if (cameraTransform == null)
        {
            Camera cam = input.GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        return true;
    }

    private bool Prepare()
    {
        if (!ResolveReferences()) return false;

        if (voiceSource == null)
            Debug.LogWarning($"{name}: 'Voice Source' kosong, voice over tidak akan terdengar (teks tetap jalan).", this);

        return true;
    }

    private void OnDisable()
    {
        // Kalau objek dimatikan di tengah cutscene, jangan tinggalkan kamera miring atau kotak dialog menggantung.
        if (_camCaptured && cameraTransform != null)
        {
            cameraTransform.localPosition = _camRestPos;
            cameraTransform.localRotation = _camRestRot;
        }
        _camCaptured = false;
        _intensity = 0f;
        _running = false;
        _idleActive = false;
        _idleRoutine = null;
        _lookRoutine = null;

        if (subtitleRoot != null) subtitleRoot.SetActive(false);
        if (_subtitleGroup != null) _subtitleGroup.alpha = 0f;
        if (skipHint != null) skipHint.SetActive(false);
        _subtitleVisible = false;
        _lineActive = false;
        _skipRequested = false;
    }
}