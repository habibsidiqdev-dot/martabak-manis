using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrustSystem
{
    /// <summary>
    /// Bagian WORLD SPACE dari encounter: nama NPC, hint (deskripsi petunjuk), dialogue/reaksi, dan 4 pilihan.
    /// Pasang di objek yang SELALU aktif (mis. "TrustStage"); Canvas World Space-nya adalah anak yang di-show/hide.
    ///
    /// Saat Show(), seluruh panel dipindah ke NPC, dihadapkan SEKALI ke kamera pemain (hanya yaw, tetap tegak),
    /// lalu diam. Offset panel ada di ruang canvas (kiri/kanan relatif pemain), jadi tidak peduli
    /// dari sisi mana pemain datang. UI "bodoh": hanya menampilkan dan melempar event.
    /// </summary>
    public class TrustStageView : MonoBehaviour
    {
        [Serializable]
        private class ApproachButton
        {
            public Approach approach;
            public Button button;
            public TMP_Text label;
        }

        [Header("Canvas World Space (anak objek ini; yang di-show/hide)")]
        [SerializeField] private Canvas stageCanvas;

        [Header("Teks")]
        [SerializeField] private TMP_Text npcNameLabel;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private TMP_Text dialogueLabel;

        [Header("Pilihan (isi 4: OBS, KNO, ACT, SIL)")]
        [SerializeField] private ApproachButton[] approachButtons;

        [Header("Penempatan")]
        [Tooltip("Geser bidang UI ke arah kamera (meter) supaya tidak menembus badan NPC.")]
        [SerializeField, Min(0f)] private float towardCameraOffset = .25f;
        [Tooltip("ON: ukuran di layar dijaga konstan (1 px canvas ~ 1 px layar 1080p) selama jarak pemain di antara min/max.\nOFF: ukuran dunia tetap (Fixed World Per Pixel), jadi mengecil/membesar mengikuti jarak.")]
        [SerializeField] private bool matchScreenSize = true;
        [SerializeField, Min(.1f)] private float sizeMultiplier = 1f;
        [SerializeField, Min(.5f)] private float minDistance = 1.2f;
        [SerializeField, Min(1f)] private float maxDistance = 4f;
        [SerializeField, Min(.0001f)] private float fixedWorldPerPixel = .0027f;
        [SerializeField, Min(1f)] private float referenceScreenHeight = 1080f;

        [Header("Input")]
        [Tooltip("Tombol 1-4 di keyboard memilih pendekatan (selain klik mouse).")]
        [SerializeField] private bool enableNumberShortcuts = true;

        public event Action<Approach> ApproachChosen;

        private bool _ready;
        private bool _inputEnabled;

        private void Awake() => TryInitialize();

        private bool TryInitialize()
        {
            if (_ready) return true;
            if (stageCanvas == null || npcNameLabel == null || hintLabel == null || dialogueLabel == null ||
                approachButtons == null || approachButtons.Length != TrustExtensions.ApproachCount)
                return false;

            foreach (ApproachButton entry in approachButtons)
            {
                if (entry == null || entry.button == null) return false;
            }

            for (int i = 0; i < approachButtons.Length; i++)
            {
                ApproachButton current = approachButtons[i];
                Approach captured = current.approach;
                current.button.onClick.AddListener(() => Choose(captured));
                if (current.label != null)
                    current.label.text = $"[{captured.ShortCode()}]  {captured.Label()}";
            }

            stageCanvas.gameObject.SetActive(false);
            _ready = true;
            return true;
        }

        public void Show(string npcName, Vector3 anchorWorldPosition, Camera viewer)
        {
            if (!TryInitialize())
            {
                Debug.LogError($"{name}: TrustStageView punya referensi Inspector yang kosong atau tidak valid.", this);
                return;
            }

            if (viewer == null) viewer = Camera.main;
            if (viewer == null)
            {
                Debug.LogError($"{name}: tidak ada kamera untuk menempatkan UI.", this);
                return;
            }

            npcNameLabel.text = npcName;
            Place(anchorWorldPosition, viewer);
            stageCanvas.worldCamera = viewer;   // event camera untuk klik mouse
            stageCanvas.gameObject.SetActive(true);
        }

        public void Hide()
        {
            _inputEnabled = false;
            if (stageCanvas != null) stageCanvas.gameObject.SetActive(false);
        }

        public void SetHint(string text)
        {
            if (hintLabel != null) hintLabel.text = text;
        }

        public void SetDialogue(string text)
        {
            if (dialogueLabel != null) dialogueLabel.text = text;
        }

        /// <summary>Kunci/buka tombol pilihan (mis. saat animasi hasil berjalan).</summary>
        public void SetInputEnabled(bool isEnabled)
        {
            _inputEnabled = isEnabled;
            if (approachButtons == null) return;

            foreach (ApproachButton entry in approachButtons)
            {
                if (entry != null && entry.button != null) entry.button.interactable = isEnabled;
            }
        }

        private void Update()
        {
            if (!_inputEnabled || !enableNumberShortcuts) return;

            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) ChooseByIndex(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) ChooseByIndex(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) ChooseByIndex(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) ChooseByIndex(3);
        }

        private void ChooseByIndex(int index)
        {
            if (approachButtons == null || index < 0 || index >= approachButtons.Length) return;
            Choose(approachButtons[index].approach);
        }

        private void Choose(Approach approach)
        {
            if (!_inputEnabled) return;

            // Lepas seleksi supaya tombol tidak "menempel" dan tidak terpicu ulang oleh Submit.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            ApproachChosen?.Invoke(approach);
        }

        /// <summary>Pindahkan bidang UI ke NPC, hadapkan ke kamera (yaw saja), dan atur skala.</summary>
        private void Place(Vector3 anchorWorldPosition, Camera viewer)
        {
            Vector3 toAnchor = anchorWorldPosition - viewer.transform.position;
            float distance = toAnchor.magnitude;

            // Arah "menjauh dari kamera" di bidang datar. Sisi depan canvas (yang terbaca) menghadap kamera
            // kalau sumbu +Z canvas menunjuk menjauh dari pemain.
            Vector3 away = new Vector3(toAnchor.x, 0f, toAnchor.z);
            if (away.sqrMagnitude < .0001f)
            {
                Vector3 forward = viewer.transform.forward;
                away = new Vector3(forward.x, 0f, forward.z);
            }
            if (away.sqrMagnitude < .0001f) away = Vector3.forward;
            away.Normalize();

            transform.SetPositionAndRotation(
                anchorWorldPosition - away * towardCameraOffset,
                Quaternion.LookRotation(away, Vector3.up));

            float worldPerPixel = fixedWorldPerPixel;
            if (matchScreenSize)
            {
                float clamped = Mathf.Clamp(distance, minDistance, maxDistance);
                float halfFov = viewer.fieldOfView * .5f * Mathf.Deg2Rad;
                worldPerPixel = 2f * clamped * Mathf.Tan(halfFov) / referenceScreenHeight;
            }

            Transform canvasTransform = stageCanvas.transform;
            canvasTransform.localPosition = Vector3.zero;
            canvasTransform.localRotation = Quaternion.identity;
            canvasTransform.localScale = Vector3.one * (worldPerPixel * sizeMultiplier);
        }
    }
}
