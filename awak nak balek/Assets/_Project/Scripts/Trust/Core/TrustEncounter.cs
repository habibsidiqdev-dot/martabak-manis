using System;
using System.Collections.Generic;

namespace TrustSystem
{
    /// <summary>
    /// Logika murni satu pertemuan "dapatkan kepercayaan NPC". Tidak bergantung ke UnityEngine,
    /// jadi mudah dites dan dipakai ulang. UI/Input/Audio cukup membaca state-nya.
    ///
    /// Alur: Begin() -> (tiap giliran) baca Clues -> Choose(approach) -> ... -> State Won / Lost.
    /// Tiap giliran, mood keempat pendekatan diacak ulang dari pool.
    /// </summary>
    public sealed class TrustEncounter
    {
        private readonly MoodRule[] _pool;
        private readonly Random _rng;
        private readonly float _clueChance;
        private readonly MoodRule[] _ruleByApproach = new MoodRule[TrustExtensions.ApproachCount];
        private readonly List<Clue> _clues = new List<Clue>();

        public int MaxTurns { get; }
        public int TargetResonance { get; }
        public int TurnsLeft { get; private set; }
        public int Resonance { get; private set; }
        public EncounterState State { get; private set; } = EncounterState.NotStarted;

        /// <summary>Petunjuk giliran ini: hanya mood VeryLike dan Reject yang punya petunjuk.</summary>
        public IReadOnlyList<Clue> Clues => _clues;

        /// <param name="moodPool">Minimal 4 entri. Tiap giliran 4 entri diambil acak & dibagikan ke 4 pendekatan.</param>
        /// <param name="clueChance">0..1, peluang tiap petunjuk (VeryLike/Reject) benar-benar ditampilkan. 1 = selalu.</param>
        public TrustEncounter(int maxTurns, int targetResonance, IReadOnlyList<MoodRule> moodPool,
            float clueChance = 1f, Random rng = null)
        {
            if (maxTurns < 1)
                throw new ArgumentOutOfRangeException(nameof(maxTurns), "Giliran minimal 1.");
            if (moodPool == null || moodPool.Count < TrustExtensions.ApproachCount)
                throw new ArgumentException($"moodPool minimal {TrustExtensions.ApproachCount} entri.", nameof(moodPool));

            MaxTurns = maxTurns;
            TargetResonance = targetResonance;
            _clueChance = Math.Max(0f, Math.Min(1f, clueChance));
            _rng = rng ?? new Random();

            _pool = new MoodRule[moodPool.Count];
            for (int i = 0; i < _pool.Length; i++)
            {
                MoodRule rule = moodPool[i];
                if (rule.max < rule.min)
                {
                    int tmp = rule.min;
                    rule.min = rule.max;
                    rule.max = tmp;
                }
                _pool[i] = rule;
            }
        }

        public void Begin()
        {
            Resonance = 0;
            TurnsLeft = MaxTurns;
            State = EncounterState.AwaitingChoice;
            RollMoods();
        }

        public ChoiceResult Choose(Approach approach)
        {
            if (State != EncounterState.AwaitingChoice)
                throw new InvalidOperationException($"Tidak bisa memilih saat state = {State}.");

            MoodRule rule = _ruleByApproach[(int)approach];
            int delta = _rng.Next(rule.min, rule.max + 1);   // batas atas exclusive, jadi +1

            Resonance += delta;
            TurnsLeft--;

            if (Resonance >= TargetResonance) State = EncounterState.Won;
            else if (TurnsLeft <= 0) State = EncounterState.Lost;
            else RollMoods();   // mood diputar ulang untuk giliran berikutnya

            return new ChoiceResult(approach, rule.mood, delta, State);
        }

        private void RollMoods()
        {
            // Fisher-Yates shuffle pada index pool, lalu 4 pertama dibagikan ke 4 pendekatan
            int n = _pool.Length;
            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }

            _clues.Clear();
            for (int a = 0; a < TrustExtensions.ApproachCount; a++)
            {
                MoodRule rule = _pool[order[a]];
                _ruleByApproach[a] = rule;

                bool hasClue = rule.mood == NpcMood.VeryLike || rule.mood == NpcMood.Reject;
                if (hasClue && (_clueChance >= 1f || _rng.NextDouble() < _clueChance))
                    _clues.Add(new Clue((Approach)a, rule.mood));
            }
        }
    }
}
