using System;
using UnityEngine;

namespace TrustSystem
{
    /// <summary>
    /// Kontrak antara TrustNpc dan tampilan encounter. TrustNpc hanya tahu interface ini,
    /// jadi tampilannya bisa diganti tanpa menyentuh logika (implementasi sekarang: TrustWorldEncounterUI).
    /// </summary>
    public interface ITrustEncounterView
    {
        event Action<Approach> ApproachChosen;
        event Action RetryClicked;
        event Action CloseClicked;

        /// <param name="anchorWorldPosition">Titik di NPC tempat UI world space berpusat (sekitar dada).</param>
        /// <param name="viewer">Kamera pemain; dipakai untuk menghadapkan dan menskalakan UI.</param>
        void Show(string npcName, Vector3 anchorWorldPosition, Camera viewer);
        void Hide();
        void SetDescription(string text);
        void SetDialogue(string text);
        void SetStatus(int resonance, int target, int turnsLeft);
        void SetInputEnabled(bool isEnabled);
        void ShowResult(bool won, string message);
        void HideResult();
    }
}
