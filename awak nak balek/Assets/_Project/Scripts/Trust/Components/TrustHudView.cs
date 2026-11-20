using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrustSystem
{
    /// <summary>
    /// Bagian SCREEN SPACE dari encounter: Resonansi, Giliran, crosshair, dan panel hasil (menang/kalah).
    /// Pasang di Canvas Screen Space Overlay yang SELALU aktif; panel encounter di-toggle lewat 'encounterRoot'.
    /// </summary>
    public class TrustHudView : MonoBehaviour
    {
        [Header("Crosshair (tampil kecuali saat encounter)")]
        [SerializeField] private Image crosshairImage;

        [Header("Panel encounter (di-show/hide)")]
        [SerializeField] private GameObject encounterRoot;
        [SerializeField] private TMP_Text resonanceLabel;
        [SerializeField] private TMP_Text turnsLabel;

        [Header("Hasil (anak encounterRoot)")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultLabel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button closeButton;

        public event Action RetryClicked;
        public event Action CloseClicked;

        private bool _ready;

        private void Awake() => TryInitialize();

        private bool TryInitialize()
        {
            if (_ready) return true;
            if (encounterRoot == null || resonanceLabel == null || turnsLabel == null ||
                resultPanel == null || resultLabel == null || retryButton == null || closeButton == null)
                return false;

            retryButton.onClick.AddListener(() => RetryClicked?.Invoke());
            closeButton.onClick.AddListener(() => CloseClicked?.Invoke());

            resultPanel.SetActive(false);
            encounterRoot.SetActive(false);
            _ready = true;
            return true;
        }

        public void Show()
        {
            if (!TryInitialize())
            {
                Debug.LogError($"{name}: TrustHudView punya referensi Inspector yang kosong.", this);
                return;
            }

            HideResult();
            encounterRoot.SetActive(true);
            if (crosshairImage != null) crosshairImage.enabled = false;
        }

        public void Hide()
        {
            if (encounterRoot != null) encounterRoot.SetActive(false);
            if (crosshairImage != null) crosshairImage.enabled = true;
        }

        public void SetStatus(int resonance, int target, int turnsLeft)
        {
            if (resonanceLabel != null) resonanceLabel.text = $"Resonansi {resonance} / {target}";
            if (turnsLabel != null) turnsLabel.text = $"Giliran {turnsLeft}";
        }

        public void ShowResult(bool won, string message)
        {
            if (!_ready) return;
            resultPanel.SetActive(true);
            resultLabel.text = message;
            retryButton.gameObject.SetActive(!won);   // coba lagi hanya kalau kalah
        }

        public void HideResult()
        {
            if (resultPanel != null) resultPanel.SetActive(false);
        }
    }
}
