using System;

namespace TrustSystem
{
    /// <summary>Empat pendekatan: OBS, KNO, ACT, SIL. Urutan angkanya dipakai sebagai index (jangan diubah).</summary>
    public enum Approach
    {
        Observation = 0,
        Knowledge = 1,
        Action = 2,
        Silence = 3
    }

    public enum NpcMood { VeryLike, Like, Neutral, Reject }

    public enum EncounterState { NotStarted, AwaitingChoice, Won, Lost }

    /// <summary>Satu "slot" mood di pool, beserta rentang nilainya. Contoh Suka: min 1, max 2.</summary>
    [Serializable]
    public struct MoodRule
    {
        public NpcMood mood;
        public int min;
        public int max;

        public MoodRule(NpcMood mood, int min, int max)
        {
            this.mood = mood;
            this.min = min;
            this.max = max;
        }
    }

    /// <summary>Petunjuk yang terlihat pemain: pendekatan ini disukai/ditolak. Mood lain tidak punya petunjuk.</summary>
    public readonly struct Clue
    {
        public readonly Approach Approach;
        public readonly NpcMood Mood;

        public Clue(Approach approach, NpcMood mood)
        {
            Approach = approach;
            Mood = mood;
        }
    }

    public readonly struct ChoiceResult
    {
        public readonly Approach Approach;
        public readonly NpcMood Mood;
        public readonly int Delta;
        public readonly EncounterState State;

        public ChoiceResult(Approach approach, NpcMood mood, int delta, EncounterState state)
        {
            Approach = approach;
            Mood = mood;
            Delta = delta;
            State = state;
        }
    }

    public static class TrustExtensions
    {
        public const int ApproachCount = 4;

        public static string ShortCode(this Approach approach)
        {
            switch (approach)
            {
                case Approach.Observation: return "OBS";
                case Approach.Knowledge: return "KNO";
                case Approach.Action: return "ACT";
                case Approach.Silence: return "SIL";
                default: return "???";
            }
        }

        public static string Label(this Approach approach)
        {
            switch (approach)
            {
                case Approach.Observation: return "Amati";
                case Approach.Knowledge: return "Pengetahuan";
                case Approach.Action: return "Tindakan";
                case Approach.Silence: return "Diam";
                default: return approach.ToString();
            }
        }
    }
}
