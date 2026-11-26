using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrustSystem.EditorTools
{
    /// <summary>
    /// Menu: Tools > Trust > Buat UI Screen Space.
    /// Menyusun hierarki TrustScreenUI di scene yang sedang terbuka (Canvas Overlay 1920x1080, roda 4 pilihan,
    /// petunjuk, dialogue, label nama, panel hasil) dan mengisi semua field TrustScreenEncounterUI.
    ///
    /// Susunan:
    ///   - NameTag      : di pojok kanan atas layar, lebarnya mengikuti panjang nama NPC.
    ///   - WheelGroup   : satu parent untuk Wheel + HintBox, disusun VerticalLayoutGroup. Roda ukurannya tetap dan
    ///                    selalu di atas; HintBox tinggi menyesuaikan isi teks (ada tinggi minimum), tumbuh ke bawah,
    ///                    jadi roda tidak bergeser ketika teks petunjuk berganti.
    ///   - DialogueBox  : tetap di bawah panel.
    /// Letakkan file ini di folder bernama "Editor" (mis. _Project/Editor/).
    /// </summary>
    public static class TrustScreenUIBuilder
    {
        private static readonly Color PanelColor = new Color(.08f, .08f, .10f, .82f);
        private static readonly Color HintColor = new Color(.30f, .20f, .05f, .90f);
        private static readonly Color ButtonColor = new Color(.10f, .10f, .12f, .88f);
        private static readonly Color ButtonHover = new Color(.45f, .32f, .10f, .95f);
        private static readonly Color ButtonPressed = new Color(.95f, .72f, .25f, 1f);
        private static readonly Color TextColor = new Color(.96f, .94f, .90f, 1f);
        private static readonly Color MutedColor = new Color(.78f, .74f, .68f, 1f);
        private static readonly Color AccentColor = new Color(.98f, .80f, .38f, 1f);

        // ---------- Ukuran tata letak (satuan canvas, referensi 1920x1080) ----------

        // Tampak luar roda = tombol di posisi +-300 (lebar 260) dan +-210 (tinggi 72), jadi ~860 x ~492.
        private static readonly Vector2 WheelSize = new Vector2(880f, 500f);

        // Bar Resonansi bulat: Image persegi (cincin + ruang untuk aura). Radius cincin diatur di material ResonanceRing.
        private const float RingImageSize = 400f;
        private const string RingMaterialPath = "Assets/_Project/Art/Materials/M_ResonanceRing.mat";

        private const float PanelSideMargin = 40f;      // jarak panel ke tepi kiri/kanan
        private const float DialogueHeight = 200f;      // tinggi DialogueBox
        private const float DialogueBottom = 40f;       // jarak DialogueBox ke tepi bawah
        private const float GroupGap = 20f;             // jarak WheelGroup ke DialogueBox
        private const float GroupTopInset = 104f;       // sisakan ruang di atas untuk NameTag
        private const float GroupSpacing = 14f;         // jarak Wheel ke HintBox
        private const float HintMinHeight = 88f;        // tinggi minimum HintBox supaya tidak "loncat" untuk teks pendek

        private static readonly Vector2 NameTagMargin = new Vector2(32f, 32f);   // jarak NameTag dari pojok kanan atas
        private const float NameTagMinWidth = 220f;
        private const float NameTagMinHeight = 56f;

        [MenuItem("Tools/Trust/Buat UI Screen Space")]
        private static void Build()
        {
            if (Object.FindFirstObjectByType<TrustScreenEncounterUI>(FindObjectsInactive.Include) != null &&
                !EditorUtility.DisplayDialog("UI Trust screen space",
                    "Di scene ini sudah ada TrustScreenEncounterUI. Buat satu lagi?", "Buat lagi", "Batal"))
                return;

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                Debug.LogWarning("Scene tidak punya EventSystem: klik pada tombol tidak akan bekerja.");

            // ---------- Akar ----------
            GameObject root = new GameObject("TrustScreenUI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            TrustScreenEncounterUI ui = root.AddComponent<TrustScreenEncounterUI>();

            RectTransform encounterRoot = NewRect("EncounterRoot", root.transform);
            Anchors(encounterRoot, Vector2.zero, Vector2.one);

            // ---------- Panel setengah layar ----------
            RectTransform panelRoot = NewRect("PanelRoot", encounterRoot);
            Anchors(panelRoot, Vector2.zero, new Vector2(.5f, 1f));

            // ---------- WheelGroup: Wheel + HintBox dalam satu parent ----------
            // Diregangkan di antara DialogueBox (bawah) dan ruang NameTag (atas). VerticalLayoutGroup menyusun
            // anaknya dari atas ke bawah, jadi Wheel selalu di tempat yang sama dan HintBox yang menyesuaikan.
            RectTransform wheelGroup = NewRect("WheelGroup", panelRoot);
            Anchors(wheelGroup, Vector2.zero, Vector2.one);
            wheelGroup.offsetMin = new Vector2(PanelSideMargin, DialogueBottom + DialogueHeight + GroupGap);
            wheelGroup.offsetMax = new Vector2(-PanelSideMargin, -GroupTopInset);
            ConfigureGroup(wheelGroup.gameObject.AddComponent<VerticalLayoutGroup>(), new RectOffset(),
                GroupSpacing, TextAnchor.UpperCenter, false, false);

            // Roda (ukuran dikunci lewat LayoutElement; isinya diletakkan relatif ke tengah roda)
            RectTransform wheel = NewRect("Wheel", wheelGroup);
            AddLayoutElement(wheel, minHeight: WheelSize.y, preferredWidth: WheelSize.x, preferredHeight: WheelSize.y);

            // Bar Resonansi bulat (shader UI/ResonanceRing). Source Image sengaja KOSONG.
            RectTransform ringRect = NewRect("ResonanceRing", wheel);
            Place(ringRect, new Vector2(.5f, .5f), Vector2.zero, new Vector2(RingImageSize, RingImageSize));
            Image ringImage = AddImage(ringRect, Color.white, false);
            ringImage.material = GetOrCreateRingMaterial();
            ResonanceRing ring = ringRect.gameObject.AddComponent<ResonanceRing>();

            TextMeshProUGUI caption = AddText("ResonanceCaption", wheel, "Resonansi", 28f, TextAlignmentOptions.Center, MutedColor, FontStyles.Normal);
            Place(caption.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, 72f), new Vector2(260f, 40f));

            TextMeshProUGUI resonanceLabel = AddText("ResonanceLabel", wheel, "0 / 10", 64f, TextAlignmentOptions.Center, TextColor, FontStyles.Bold);
            Place(resonanceLabel.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, 8f), new Vector2(260f, 84f));

            TextMeshProUGUI turnsLabel = AddText("TurnsLabel", wheel, "Giliran 5", 30f, TextAlignmentOptions.Center, AccentColor, FontStyles.Normal);
            Place(turnsLabel.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, -66f), new Vector2(260f, 40f));

            // Urutan = Atas, Kanan, Bawah, Kiri  (OBS, KNO, ACT, SIL)
            Vector2[] positions =
            {
                new Vector2(0f, 210f), new Vector2(300f, 0f), new Vector2(0f, -210f), new Vector2(-300f, 0f)
            };
            string[] names = { "Button_OBS", "Button_KNO", "Button_ACT", "Button_SIL" };
            Button[] buttons = new Button[4];
            TextMeshProUGUI[] labels = new TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
                buttons[i] = AddButton(names[i], wheel, positions[i], new Vector2(260f, 72f), "Pilihan", out labels[i]);

            // Petunjuk: tingginya mengikuti teks (diukur VerticalLayoutGroup milik HintBox sendiri), minimal HintMinHeight.
            RectTransform hintBox = NewRect("HintBox", wheelGroup);
            AddImage(hintBox, HintColor, false);
            AddLayoutElement(hintBox, minHeight: HintMinHeight, flexibleWidth: 1f);
            ConfigureGroup(hintBox.gameObject.AddComponent<VerticalLayoutGroup>(), new RectOffset(24, 24, 12, 12),
                0f, TextAnchor.MiddleLeft, true, true);
            TextMeshProUGUI hintLabel = AddText("HintLabel", hintBox, "Petunjuk bahasa tubuh NPC.", 28f, TextAlignmentOptions.MidlineLeft, AccentColor, FontStyles.Italic);

            // ---------- Dialogue ----------
            RectTransform dialogueBox = NewRect("DialogueBox", panelRoot);
            Anchors(dialogueBox, Vector2.zero, new Vector2(1f, 0f));
            dialogueBox.offsetMin = new Vector2(PanelSideMargin, DialogueBottom);
            dialogueBox.offsetMax = new Vector2(-PanelSideMargin, DialogueBottom + DialogueHeight);
            AddImage(dialogueBox, PanelColor, false);
            TextMeshProUGUI dialogueLabel = AddText("DialogueLabel", dialogueBox, "Dialogue atau reaksi NPC.", 34f, TextAlignmentOptions.TopLeft, TextColor, FontStyles.Normal);
            Inset(dialogueLabel.rectTransform, 24f, 16f, 24f, 16f);

            // ---------- Label nama (anak langsung EncounterRoot, pojok kanan atas) ----------
            // Pivot dan anchor di pojok kanan atas, jadi lebar yang berubah mengikuti nama NPC selalu melebar ke kiri
            // dan pojoknya tidak bergeser. Lebar/tinggi diatur HorizontalLayoutGroup + ContentSizeFitter.
            RectTransform nameTag = NewRect("NameTag", encounterRoot);
            nameTag.anchorMin = new Vector2(1f, 1f);
            nameTag.anchorMax = new Vector2(1f, 1f);
            nameTag.pivot = new Vector2(1f, 1f);
            nameTag.anchoredPosition = new Vector2(-NameTagMargin.x, -NameTagMargin.y);
            AddImage(nameTag, PanelColor, false);
            AddLayoutElement(nameTag, minWidth: NameTagMinWidth, minHeight: NameTagMinHeight);
            ConfigureGroup(nameTag.gameObject.AddComponent<HorizontalLayoutGroup>(), new RectOffset(28, 28, 10, 10),
                0f, TextAnchor.MiddleCenter, true, true);
            ContentSizeFitter nameFitter = nameTag.gameObject.AddComponent<ContentSizeFitter>();
            nameFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            nameFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TextMeshProUGUI npcNameLabel = AddText("NpcNameLabel", nameTag, "Nama NPC", 32f, TextAlignmentOptions.Center, TextColor, FontStyles.Bold);

            // ---------- Panel hasil ----------
            RectTransform resultPanel = NewRect("ResultPanel", encounterRoot);
            Place(resultPanel, new Vector2(.5f, .5f), Vector2.zero, new Vector2(900f, 420f));
            AddImage(resultPanel, PanelColor, true);
            TextMeshProUGUI resultLabel = AddText("ResultLabel", resultPanel, "Hasil", 40f, TextAlignmentOptions.Center, TextColor, FontStyles.Normal);
            Inset(resultLabel.rectTransform, 40f, 150f, 40f, 40f);

            TextMeshProUGUI retryLabel;
            Button retryButton = AddButton("RetryButton", resultPanel, new Vector2(-170f, -140f), new Vector2(300f, 80f), "Coba lagi", out retryLabel);
            TextMeshProUGUI closeLabel;
            Button closeButton = AddButton("CloseButton", resultPanel, new Vector2(170f, -140f), new Vector2(300f, 80f), "Tutup", out closeLabel);
            resultPanel.gameObject.SetActive(false);

            // ---------- Isi field Inspector ----------
            SerializedObject so = new SerializedObject(ui);
            SetRef(so, "encounterRoot", encounterRoot.gameObject);
            SetRef(so, "panelRoot", panelRoot);
            SetRef(so, "nameTag", nameTag);
            SetRef(so, "npcNameLabel", npcNameLabel);
            SetRef(so, "hintLabel", hintLabel);
            SetRef(so, "dialogueLabel", dialogueLabel);
            SetRef(so, "resonanceLabel", resonanceLabel);
            SetRef(so, "turnsLabel", turnsLabel);
            SetRef(so, "resonanceRing", ring);
            SetRef(so, "resultPanel", resultPanel.gameObject);
            SetRef(so, "resultLabel", resultLabel);
            SetRef(so, "retryButton", retryButton);
            SetRef(so, "closeButton", closeButton);

            // Label nama tetap di pojok kanan atas (tidak ditempel ke NPC oleh script).
            SerializedProperty followNpc = so.FindProperty("nameTagFollowsNpc");
            if (followNpc != null)
                followNpc.boolValue = false;
            else
                Debug.LogWarning("Field 'nameTagFollowsNpc' tidak ditemukan: pakai TrustScreenEncounterUI.cs versi terbaru, " +
                                 "kalau tidak label nama akan ditimpa posisinya oleh script.");

            SerializedProperty array = so.FindProperty("approachButtons");
            if (array != null)
            {
                array.arraySize = 4;
                for (int i = 0; i < 4; i++)
                {
                    SerializedProperty element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("approach").enumValueIndex = i;   // 0..3 = OBS, KNO, ACT, SIL
                    element.FindPropertyRelative("button").objectReferenceValue = buttons[i];
                    element.FindPropertyRelative("label").objectReferenceValue = labels[i];
                }
            }

            // Crosshair: ambil dari TrustHudView lama kalau ada.
            TrustHudView oldHud = Object.FindFirstObjectByType<TrustHudView>(FindObjectsInactive.Include);
            if (oldHud != null)
            {
                SerializedProperty crosshair = new SerializedObject(oldHud).FindProperty("crosshairImage");
                if (crosshair != null) SetRef(so, "crosshairImage", crosshair.objectReferenceValue);
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // Hitung layout sekarang juga supaya hasilnya langsung rapi di Scene view.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(wheelGroup);
            LayoutRebuilder.ForceRebuildLayoutImmediate(nameTag);

            Undo.RegisterCreatedObjectUndo(root, "Buat UI Trust screen space");
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("TrustScreenUI dibuat. Isi field 'Ui' di setiap TrustNpc dengan objek ini.", root);
        }

        // ---------- Helper ----------

        private static Material GetOrCreateRingMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(RingMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("UI/ResonanceRing");
            if (shader == null)
            {
                Debug.LogError("Shader 'UI/ResonanceRing' tidak ditemukan. Pastikan ResonanceRing.shader ada di project.");
                return null;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, RingMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"Field '{field}' tidak ditemukan di TrustScreenEncounterUI.");
                return;
            }
            property.objectReferenceValue = value;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Anchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static void Inset(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Layout group yang mengatur ukuran anak-anaknya (control size) dan, kalau diminta, melebarkannya mengisi ruang.</summary>
        private static void ConfigureGroup(HorizontalOrVerticalLayoutGroup group, RectOffset padding, float spacing,
            TextAnchor alignment, bool expandWidth, bool expandHeight)
        {
            group.padding = padding;
            group.spacing = spacing;
            group.childAlignment = alignment;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = expandWidth;
            group.childForceExpandHeight = expandHeight;
        }

        /// <summary>Nilai -1 = tidak diatur (diabaikan layout group).</summary>
        private static LayoutElement AddLayoutElement(RectTransform rt, float minWidth = -1f, float minHeight = -1f,
            float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f, float flexibleHeight = -1f)
        {
            LayoutElement element = rt.gameObject.AddComponent<LayoutElement>();
            element.minWidth = minWidth;
            element.minHeight = minHeight;
            element.preferredWidth = preferredWidth;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = flexibleHeight;
            return element;
        }

        private static Image AddImage(RectTransform rt, Color color, bool raycast)
        {
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static TextMeshProUGUI AddText(string name, Transform parent, string text, float size,
            TextAlignmentOptions alignment, Color color, FontStyles style)
        {
            RectTransform rt = NewRect(name, parent);
            TextMeshProUGUI label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color;
            label.fontStyle = style;
            label.raycastTarget = false;
            return label;
        }

        private static Button AddButton(string name, Transform parent, Vector2 position, Vector2 size,
            string text, out TextMeshProUGUI label)
        {
            RectTransform rt = NewRect(name, parent);
            Place(rt, new Vector2(.5f, .5f), position, size);

            Image image = AddImage(rt, Color.white, true);   // warna akhir = white x ColorBlock
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHover;
            colors.pressedColor = ButtonPressed;
            colors.selectedColor = ButtonColor;
            colors.disabledColor = new Color(.10f, .10f, .12f, .40f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .08f;
            button.colors = colors;

            label = AddText("Label", rt, text, 30f, TextAlignmentOptions.Center, TextColor, FontStyles.Normal);
            Inset(label.rectTransform, 8f, 4f, 8f, 4f);
            return button;
        }
    }
}
