using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SusSystem.EditorTools
{
    /// <summary>
    /// Menu: Tools > Sus > Buat UI Sus...
    /// Membuat sus system LENGKAP di scene yang sedang terbuka, sebagai objek biasa yang bisa kamu desain ulang:
    ///   - SusSystem : SusManager (jumlah bar sesuai pilihan).
    ///   - SusCanvas : Canvas Overlay 1920x1080 + SusHud, berisi Row (kiri atas) -> Label, Bar1..BarN, Counter.
    ///                 Tiap BarN punya anak "Empty" dan "Filled", sudah tersambung ke SusHud.
    ///   - GameOver  : EndingScreen, sudah tersambung ke SusManager > On Maxed (opsional).
    ///
    /// Setelah dibuat, ganti Image-nya dengan sprite-mu, ubah ukuran/posisi, atau hapus Row dan susun sendiri.
    /// Yang penting: objek "Filled" dan "Empty" tetap terdaftar di SusHud (Filled Objects / Empty Objects).
    /// Letakkan file ini di folder bernama "Editor" (mis. _Project/Editor/).
    /// </summary>
    public class SusUIBuilder : EditorWindow
    {
        private int _barCount = 4;
        private float _pipSize = 34f;
        private float _spacing = 10f;
        private Vector2 _margin = new Vector2(48f, 40f);
        private bool _createLabel = true;
        private bool _createCounter;
        private bool _createGameOver = true;
        private string _gameOverTitle = "Mereka mulai curiga.";
        private string _gameOverMessage = "Terlalu banyak kecurigaan. Tidak ada lagi yang mau mempercayaimu.";

        private static readonly Color BorderColor = new Color(.96f, .94f, .90f, .85f);
        private static readonly Color EmptyColor = new Color(.05f, .05f, .06f, .70f);
        private static readonly Color FilledColor = new Color(.86f, .20f, .16f, 1f);
        private static readonly Color TextColor = new Color(.96f, .94f, .90f, 1f);

        [MenuItem("Tools/Sus/Buat UI Sus...")]
        private static void Open()
        {
            var window = GetWindow<SusUIBuilder>(true, "Sus Builder", true);
            window.minSize = new Vector2(380f, 380f);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Membuat SusSystem + UI di scene yang sedang terbuka. Hasilnya objek biasa: " +
                "ganti gambar, ukuran, dan posisinya sesukamu.", MessageType.Info);

            EditorGUILayout.Space();
            _barCount = EditorGUILayout.IntSlider("Jumlah bar", _barCount, 1, 10);
            _pipSize = EditorGUILayout.Slider("Ukuran kotak", _pipSize, 16f, 120f);
            _spacing = EditorGUILayout.Slider("Jarak antar kotak", _spacing, 0f, 40f);
            _margin = EditorGUILayout.Vector2Field("Jarak dari kiri / atas", _margin);

            EditorGUILayout.Space();
            _createLabel = EditorGUILayout.Toggle("Teks \"SUS\"", _createLabel);
            _createCounter = EditorGUILayout.Toggle("Teks penghitung (mis. 2/4)", _createCounter);
            _createGameOver = EditorGUILayout.Toggle("Layar game over", _createGameOver);

            if (_createGameOver)
            {
                EditorGUI.indentLevel++;
                _gameOverTitle = EditorGUILayout.TextField("Judul", _gameOverTitle);
                EditorGUILayout.LabelField("Pesan");
                _gameOverMessage = EditorGUILayout.TextArea(_gameOverMessage, GUILayout.MinHeight(48f));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Buat di scene aktif", GUILayout.Height(32f)))
                Build();
        }

        // ---------- Membangun ----------

        private void Build()
        {
            SusManager existing = FindFirstObjectByType<SusManager>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Sus System sudah ada",
                    $"Scene ini sudah punya SusManager (\"{existing.name}\"). " +
                    "Hapus dulu objek sus yang lama kalau mau membuat ulang.", "OK");
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            // 1) Manager
            GameObject managerObject = new GameObject("SusSystem");
            Undo.RegisterCreatedObjectUndo(managerObject, "Buat Sus System");
            SusManager manager = managerObject.AddComponent<SusManager>();

            SerializedObject managerSo = new SerializedObject(manager);
            managerSo.FindProperty("maxBars").intValue = _barCount;
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            // 2) Canvas + HUD
            GameObject canvasObject = new GameObject("SusCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            Undo.RegisterCreatedObjectUndo(canvasObject, "Buat Sus System");

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;   // di atas UI encounter (10), di bawah layar ending (100)

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            // Tidak ada GraphicRaycaster: HUD sus tidak menghalangi klik.
            SusHud hud = canvasObject.AddComponent<SusHud>();

            RectTransform row = NewRect("Row", canvasObject.transform);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(_margin.x, -_margin.y);

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = _spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (_createLabel)
                AddText("Label", row, "SUS", _pipSize * .85f, FontStyles.Bold);

            var filledObjects = new GameObject[_barCount];
            var emptyObjects = new GameObject[_barCount];

            for (int i = 0; i < _barCount; i++)
            {
                // BarN = bingkai. Empty = isi saat kosong, Filled = isi saat terisi.
                RectTransform bar = NewRect($"Bar{i + 1}", row);
                Image border = bar.gameObject.AddComponent<Image>();
                border.color = BorderColor;
                border.raycastTarget = false;

                LayoutElement size = bar.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = _pipSize;
                size.preferredHeight = _pipSize;

                emptyObjects[i] = NewFill("Empty", bar, EmptyColor);
                filledObjects[i] = NewFill("Filled", bar, FilledColor);
            }

            TMP_Text counter = null;
            if (_createCounter)
                counter = AddText("Counter", row, $"0/{_barCount}", _pipSize * .85f, FontStyles.Bold);

            // Sambungkan ke SusHud (field private lewat SerializedObject).
            SerializedObject hudSo = new SerializedObject(hud);
            SetObjectArray(hudSo.FindProperty("filledObjects"), filledObjects);
            SetObjectArray(hudSo.FindProperty("emptyObjects"), emptyObjects);
            if (counter != null)
                hudSo.FindProperty("counterLabel").objectReferenceValue = counter;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // 3) Game over (opsional): EndingScreen > Show disambungkan ke SusManager > On Maxed.
            if (_createGameOver)
            {
                GameObject gameOverObject = new GameObject("GameOver");
                Undo.RegisterCreatedObjectUndo(gameOverObject, "Buat Sus System");
                EndingScreen ending = gameOverObject.AddComponent<EndingScreen>();

                SerializedObject endingSo = new SerializedObject(ending);
                endingSo.FindProperty("title").stringValue = _gameOverTitle;
                endingSo.FindProperty("message").stringValue = _gameOverMessage;
                endingSo.ApplyModifiedPropertiesWithoutUndo();

                UnityEventTools.AddVoidPersistentListener(manager.OnMaxed, ending.Show);
                EditorUtility.SetDirty(manager);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(managerObject.scene);

            Selection.activeGameObject = canvasObject;
            EditorGUIUtility.PingObject(canvasObject);
            Debug.Log($"[Sus] Sus System dibuat ({_barCount} bar). Desain ulang objek di SusCanvas sesukamu; " +
                      "pastikan 'Filled' dan 'Empty' tetap terdaftar di SusHud.", canvasObject);
        }

        // ---------- Helper ----------

        private static GameObject NewFill(string objectName, Transform parent, Color color)
        {
            RectTransform rt = NewRect(objectName, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(3f, 3f);
            rt.offsetMax = new Vector2(-3f, -3f);

            Image image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rt.gameObject;
        }

        private static TMP_Text AddText(string objectName, Transform parent, string content, float size, FontStyles style)
        {
            RectTransform rt = NewRect(objectName, parent);
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform NewRect(string objectName, Transform parent)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void SetObjectArray(SerializedProperty property, Object[] items)
        {
            property.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }
}
