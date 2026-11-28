using System;
using UnityEngine;
using UnityEngine.Events;

namespace SusSystem
{
    /// <summary>
    /// Sus system GLOBAL: satu bar yang dipakai semua NPC. Pasang SATU saja di scene
    /// (mis. GameObject kosong "SusSystem"). Setiap NPC yang gagal dipercaya menambah 1 bar.
    /// Saat bar penuh (mis. 4/4), 'On Maxed' dipanggil: sambungkan ke EndingScreen > Show
    /// (buat satu EndingScreen khusus game over, judul/pesan sendiri).
    ///
    /// Sus permanen: tidak ada cara menurunkannya. Muat ulang scene (tombol "Main lagi") = sus mulai dari 0.
    /// </summary>
    public class SusManager : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxBars = 4;

        [Tooltip("Dipanggil sekali saat bar penuh. Sambungkan ke EndingScreen > Show untuk layar game over.")]
        [SerializeField] private UnityEvent onMaxed;

        private SusTracker _tracker;

        public static SusManager Instance { get; private set; }

        /// <summary>Dipakai builder Editor untuk menyambungkan game over. Di runtime cukup lewat Inspector.</summary>
        public UnityEvent OnMaxed => onMaxed;

        public int Current => _tracker.Current;
        public int Max => _tracker.Max;
        public bool IsMaxed => _tracker.IsMaxed;

        /// <summary>(current, max). Dipakai HUD.</summary>
        public event Action<int, int> Changed;

        // Aman kalau Domain Reload dimatikan di Project Settings.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"{name}: sudah ada SusManager lain di scene. Yang ini diabaikan.", this);
                return;
            }

            Instance = this;
            _tracker = new SusTracker(maxBars);
        }

        private void Start()
        {
            if (Instance == this && onMaxed.GetPersistentEventCount() == 0)
                Debug.LogWarning($"{name}: 'On Maxed' kosong. Saat bar penuh tidak akan ada game over " +
                                 "(sambungkan ke EndingScreen > Show).", this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Tambah sus. Aman dipanggil dari UnityEvent / script lain.</summary>
        public void Add(int amount = 1)
        {
            if (Instance != this || _tracker == null) return;
            if (amount <= 0 || _tracker.IsMaxed) return;

            bool reachedMax = _tracker.Add(amount);

            Changed?.Invoke(_tracker.Current, _tracker.Max);
            if (reachedMax) onMaxed.Invoke();
        }

        [ContextMenu("Tes: tambah 1 sus (saat Play)")]
        private void TestAdd()
        {
            if (Application.isPlaying) Add(1);
            else Debug.Log("Tes sus hanya jalan saat Play Mode.", this);
        }
    }
}
