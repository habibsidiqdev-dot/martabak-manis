using System;
using UnityEngine;

namespace TrustSystem
{
    /// <summary>
    /// Fasad untuk TrustNpc: meneruskan perintah ke TrustStageView (world space) dan TrustHudView (screen),
    /// dan meneruskan event klik balik ke TrustNpc. Tidak ada logika permainan di sini.
    /// Pasang di objek yang SELALU aktif, lalu isi 'stage' dan 'hud'.
    /// </summary>
    public class TrustWorldEncounterUI : MonoBehaviour, ITrustEncounterView
    {
        [SerializeField] private TrustStageView stage;
        [SerializeField] private TrustHudView hud;

        public event Action<Approach> ApproachChosen;
        public event Action RetryClicked;
        public event Action CloseClicked;

        private void OnEnable()
        {
            if (stage != null) stage.ApproachChosen += RelayApproach;
            if (hud != null)
            {
                hud.RetryClicked += RelayRetry;
                hud.CloseClicked += RelayClose;
            }
        }

        private void OnDisable()
        {
            if (stage != null) stage.ApproachChosen -= RelayApproach;
            if (hud != null)
            {
                hud.RetryClicked -= RelayRetry;
                hud.CloseClicked -= RelayClose;
            }
        }

        private void RelayApproach(Approach approach) => ApproachChosen?.Invoke(approach);
        private void RelayRetry() => RetryClicked?.Invoke();
        private void RelayClose() => CloseClicked?.Invoke();

        public void Show(string npcName, Vector3 anchorWorldPosition, Camera viewer)
        {
            if (stage == null || hud == null)
            {
                Debug.LogError($"{name}: 'stage' atau 'hud' belum di-assign di Inspector.", this);
                return;
            }

            stage.Show(npcName, anchorWorldPosition, viewer);
            hud.Show();
        }

        public void Hide()
        {
            if (stage != null) stage.Hide();
            if (hud != null) hud.Hide();
        }

        public void SetDescription(string text)
        {
            if (stage != null) stage.SetHint(text);
        }

        public void SetDialogue(string text)
        {
            if (stage != null) stage.SetDialogue(text);
        }

        public void SetStatus(int resonance, int target, int turnsLeft)
        {
            if (hud != null) hud.SetStatus(resonance, target, turnsLeft);
        }

        public void SetInputEnabled(bool isEnabled)
        {
            if (stage != null) stage.SetInputEnabled(isEnabled);
        }

        public void ShowResult(bool won, string message)
        {
            if (hud != null) hud.ShowResult(won, message);
        }

        public void HideResult()
        {
            if (hud != null) hud.HideResult();
        }
    }
}
