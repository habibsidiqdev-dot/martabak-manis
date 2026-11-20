
using System.Collections.Generic;
using UnityEngine;

namespace TrustSystem
{
    /// <summary>
    /// Kalimat petunjuk bahasa tubuh untuk satu pendekatan.
    /// Diisi per NPC.
    /// </summary>
    [System.Serializable]
    public class ApproachHint
    {
        public Approach approach;

        [Tooltip("Dipakai saat pendekatan ini = Sangat Suka.")]
        [TextArea(1, 3)]
        public string[] likeLines;

        [Tooltip("Dipakai saat pendekatan ini = Ditolak.")]
        [TextArea(1, 3)]
        public string[] rejectLines;
    }

    /// <summary>
    /// Kumpulan Reaction untuk satu Approach.
    /// Setiap Approach memiliki empat kategori Mood.
    /// </summary>
    [System.Serializable]
    public class ApproachReactionSet
    {
        public Approach approach;

        [Header("Very Like Reactions")]
        [TextArea(1, 3)]
        public string[] veryLikeLines;

        [Header("Like Reactions")]
        [TextArea(1, 3)]
        public string[] likeLines;

        [Header("Neutral Reactions")]
        [TextArea(1, 3)]
        public string[] neutralLines;

        [Header("Reject Reactions")]
        [TextArea(1, 3)]
        public string[] rejectLines;

        public ApproachReactionSet()
        {
        }

        public ApproachReactionSet(Approach approach)
        {
            this.approach = approach;
        }

        /// <summary>
        /// Mengambil kumpulan Reaction berdasarkan Mood.
        /// </summary>
        public string[] GetLines(NpcMood mood)
        {
            switch (mood)
            {
                case NpcMood.VeryLike:
                    return veryLikeLines;

                case NpcMood.Like:
                    return likeLines;

                case NpcMood.Neutral:
                    return neutralLines;

                case NpcMood.Reject:
                    return rejectLines;

                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// Data satu NPC + aturan pertemuannya.
    /// Buat lewat Create > Trust > Encounter Data.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Trust/Encounter Data",
        fileName = "TrustEncounter_New"
    )]
    public class TrustEncounterData : ScriptableObject
    {
        [Header("NPC")]
        [SerializeField]
        private string npcName = "Penjaga Bar";

        [Header("Aturan")]
        [SerializeField, Min(1)]
        private int maxTurns = 5;

        [SerializeField]
        private int targetResonance = 10;

        [Tooltip("Peluang tiap petunjuk (Sangat Suka / Ditolak) muncul. 1 = selalu muncul.")]
        [SerializeField, Range(0f, 1f)]
        private float clueChance = 1f;

        [Tooltip("Minimal 4 entri. Tiap giliran 4 entri diacak ke OBS/KNO/ACT/SIL.")]
        [SerializeField]
        private MoodRule[] moodPool =
        {
            new MoodRule(NpcMood.VeryLike, 3, 3),
            new MoodRule(NpcMood.Like, 1, 2),
            new MoodRule(NpcMood.Neutral, 0, 0),
            new MoodRule(NpcMood.Reject, -2, -2),
        };

        [Header("Syarat & hadiah item")]
        [Tooltip("Item yang harus dimiliki pemain untuk bisa bicara dengan NPC ini. " +
                 "Item DIPAKAI (hilang dari inventori) saat pertemuan pertama dimulai. " +
                 "Kosong = tanpa syarat. Isi item yang sama dua kali kalau butuh dua buah.")]
        [SerializeField]
        private ItemData[] requiredItems;

        [Tooltip("Satu baris yang muncul saat pemain menekan Interact tapi item belum lengkap. " +
                 "Buat pendek (sekitar 45 huruf), tampil di bar prompt. Kosong = teks bawaan.")]
        [SerializeField, TextArea(1, 2)]
        private string lockedDialogue;

        [Tooltip("Item yang diterima pemain saat MENANG.")]
        [SerializeField]
        private ItemData[] rewardItems;

        [Header("Teks narasi")]
        [SerializeField, TextArea(2, 4)]
        private string introDescription;

        [SerializeField, TextArea(1, 3)]
        private string introDialogue;

        [SerializeField, TextArea(1, 3)]
        private string winDialogue;

        [SerializeField, TextArea(1, 3)]
        private string loseDialogue;

        [SerializeField, TextArea(1, 2)]
        private string noClueText =
            "Gerak-gerik NPC sulit dibaca kali ini.";

        [Header("Petunjuk bahasa tubuh (isi 4 entri: OBS, KNO, ACT, SIL)")]
        [SerializeField]
        private ApproachHint[] hints;

        [Header("Reaction berdasarkan Approach + Mood")]
        [SerializeField]
        private ApproachReactionSet[] approachReactions =
        {
            new ApproachReactionSet(Approach.Observation),
            new ApproachReactionSet(Approach.Knowledge),
            new ApproachReactionSet(Approach.Action),
            new ApproachReactionSet(Approach.Silence)
        };

        // ---------- Properties ----------

        public string NpcName => npcName;
        public int MaxTurns => maxTurns;
        public int TargetResonance => targetResonance;
        public float ClueChance => clueChance;
        public IReadOnlyList<MoodRule> MoodPool => moodPool;

        public string IntroDescription => introDescription;
        public string IntroDialogue => introDialogue;
        public string WinDialogue => winDialogue;
        public string LoseDialogue => loseDialogue;
        public string NoClueText => noClueText;

        public IReadOnlyList<ItemData> RequiredItems => requiredItems;
        public string LockedDialogue => lockedDialogue;
        public IReadOnlyList<ItemData> RewardItems => rewardItems;

        // ---------- Hint ----------

        public string GetClueText(Approach approach, NpcMood mood)
        {
            if (hints != null)
            {
                foreach (ApproachHint hint in hints)
                {
                    if (hint == null || hint.approach != approach)
                        continue;

                    string line = PickRandom(
                        mood == NpcMood.VeryLike
                            ? hint.likeLines
                            : hint.rejectLines
                    );

                    if (!string.IsNullOrEmpty(line))
                        return line;
                }
            }

            Debug.LogWarning(
                $"[{name}] Teks petunjuk kosong untuk {approach.ShortCode()} / {mood}.",
                this
            );

            return string.Empty;
        }

        // ---------- Reaction ----------

        /// <summary>
        /// Mengambil Reaction berdasarkan kombinasi Approach dan Mood.
        /// Contoh: Observation + Reject.
        /// </summary>
        public string GetReaction(Approach approach, NpcMood mood)
        {
            if (approachReactions != null)
            {
                foreach (ApproachReactionSet reactionSet in approachReactions)
                {
                    if (reactionSet == null)
                        continue;

                    if (reactionSet.approach != approach)
                        continue;

                    string reaction = PickRandom(
                        reactionSet.GetLines(mood)
                    );

                    if (!string.IsNullOrEmpty(reaction))
                        return reaction;

                    break;
                }
            }

            // Kategori ini belum diisi: TrustNpc melewati teks reaksi.
            return string.Empty;
        }

        // ---------- Utility ----------

        private static string PickRandom(string[] lines)
        {
            if (lines == null || lines.Length == 0)
                return string.Empty;

            return lines[Random.Range(0, lines.Length)];
        }

        private void EnsureApproachReactionSets()
        {
            if (approachReactions == null)
                approachReactions = new ApproachReactionSet[0];

            if (approachReactions.Length < TrustExtensions.ApproachCount)
            {
                System.Array.Resize(
                    ref approachReactions,
                    TrustExtensions.ApproachCount
                );
            }

            for (int i = 0; i < TrustExtensions.ApproachCount; i++)
            {
                if (approachReactions[i] == null)
                {
                    approachReactions[i] =
                        new ApproachReactionSet((Approach)i);
                }
            }
        }

        // ---------- Validation ----------

        private void OnValidate()
        {
            maxTurns = Mathf.Max(1, maxTurns);

            EnsureApproachReactionSets();

            if (moodPool == null ||
                moodPool.Length < TrustExtensions.ApproachCount)
            {
                Debug.LogWarning(
                    $"[{name}] moodPool butuh minimal {TrustExtensions.ApproachCount} entri.",
                    this
                );
            }
        }

#if UNITY_EDITOR

        // Klik kanan asset ini di Inspector > Simulasi balancing.
        // Hasilnya muncul di Console.
        [ContextMenu("Simulasi balancing (10.000 run)")]
        private void SimulateBalance()
        {
            const int runs = 10000;

            var rng = new System.Random(12345);

            int smartWins = 0;
            int randomWins = 0;

            for (int i = 0; i < runs; i++)
            {
                if (SimulateRun(true, rng))
                    smartWins++;

                if (SimulateRun(false, rng))
                    randomWins++;
            }

            Debug.Log(
                $"[{name}] {maxTurns} giliran, target {targetResonance}, " +
                $"clueChance {clueChance:0.##} -> " +
                $"pemain teliti menang {smartWins * 100f / runs:0.#}% | " +
                $"pemain asal pilih menang {randomWins * 100f / runs:0.#}% (tanpa item)",
                this
            );
        }

        private bool SimulateRun(bool smart, System.Random rng)
        {
            var encounter = new TrustEncounter(
                maxTurns,
                targetResonance,
                moodPool,
                clueChance,
                rng
            );

            encounter.Begin();

            while (encounter.State == EncounterState.AwaitingChoice)
            {
                Approach pick = smart
                    ? PickSmart(encounter, rng)
                    : (Approach)rng.Next(TrustExtensions.ApproachCount);

                encounter.Choose(pick);
            }

            return encounter.State == EncounterState.Won;
        }

        // Pemain teliti:
        // Ambil yang terlihat Sangat Suka.
        // Kalau tidak ada, hindari yang terlihat Ditolak.
        private static Approach PickSmart(
            TrustEncounter encounter,
            System.Random rng
        )
        {
            var allowed = new List<Approach>();

            for (int a = 0; a < TrustExtensions.ApproachCount; a++)
                allowed.Add((Approach)a);

            foreach (Clue clue in encounter.Clues)
            {
                if (clue.Mood == NpcMood.VeryLike)
                    return clue.Approach;

                if (clue.Mood == NpcMood.Reject)
                    allowed.Remove(clue.Approach);
            }

            if (allowed.Count == 0)
            {
                return (Approach)rng.Next(
                    TrustExtensions.ApproachCount
                );
            }

            return allowed[rng.Next(allowed.Count)];
        }

#endif
    }
}
