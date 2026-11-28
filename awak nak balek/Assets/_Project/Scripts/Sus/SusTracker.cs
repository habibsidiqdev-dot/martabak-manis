using System;

namespace SusSystem
{
    /// <summary>
    /// Logika murni sus system: N bar yang hanya bisa naik, penuh = game over.
    /// Tidak bergantung ke UnityEngine, jadi mudah dites dan dipakai ulang
    /// (sama seperti TrustEncounter). Sengaja TIDAK ada fungsi untuk menurunkan bar:
    /// sus bersifat permanen sampai Reset().
    /// </summary>
    public sealed class SusTracker
    {
        public int Max { get; }
        public int Current { get; private set; }
        public bool IsMaxed => Current >= Max;

        public SusTracker(int max)
        {
            if (max < 1)
                throw new ArgumentOutOfRangeException(nameof(max), "Bar minimal 1.");

            Max = max;
        }

        /// <returns>True kalau pemanggilan ini yang membuat bar jadi penuh.</returns>
        public bool Add(int amount = 1)
        {
            if (amount <= 0 || IsMaxed) return false;

            Current = Math.Min(Max, Current + amount);
            return IsMaxed;
        }

        public void Reset() => Current = 0;
    }
}
