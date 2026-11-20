using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using TrustSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builder scene TUNGGAL. Membangun scene baru "Prototype": Player (dengan inventori) + NPC Penjaga
/// yang butuh item + meja berisi item untuk diambil + UI world space (nama, hint, dialogue, pilihan)
/// + HUD screen (Resonansi, giliran, crosshair, hasil).
///
/// Alur uji: lihat Penjaga -> E (ditolak, butuh Surat Jalan) -> ambil Surat Jalan di meja kiri -> bicara lagi
/// (item hilang) -> menangkan encounter -> dapat Kunci Gerbang.
///
/// Tidak menyentuh scene lain, dan TD_NPC_Penjaga.asset tidak diubah: syarat item ada di salinannya
/// (TD_NPC_Penjaga_Item.asset). Aset yang sudah ada tidak ditimpa, jadi edit desainer aman.
/// Menu: Tools > Prototype > Build Scene (dan Validate Scene).
/// </summary>
public static class PrototypeSceneBuilder
{
    private const string ScenePath = "Assets/_Project/Scenes/Prototype.unity";
    private const string BaseNpcDataPath = "Assets/_Project/Data/NPC/TD_NPC_Penjaga.asset";        // sumber teks & petunjuk (tidak diubah)
    private const string GatedNpcDataPath = "Assets/_Project/Data/NPC/TD_NPC_Penjaga_Item.asset";  // salinan + syarat item
    private const string ItemFolderParent = "Assets/_Project/Data";
    private const string ItemFolderName = "Items";
    private const string ItemFolder = ItemFolderParent + "/" + ItemFolderName;
    private const string MaterialFolder = "Assets/_Project/Art/Materials";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    // Canvas world space dirancang seperti layar 1920x1080; titik tengahnya = titik dada NPC.
    // Satuan di bawah ini piksel canvas, relatif ke tengah canvas.
    private static readonly Vector2 CanvasSize = new Vector2(1920f, 1080f);
    private static readonly Vector2 HintPos = new Vector2(-640f, 300f);
    private static readonly Vector2 HintSize = new Vector2(640f, 250f);
    private static readonly Vector2 ChoicesPos = new Vector2(-640f, -120f);
    private static readonly Vector2 ChoicesSize = new Vector2(640f, 344f);
    private static readonly Vector2 DialoguePos = new Vector2(600f, 200f);
    private static readonly Vector2 DialogueSize = new Vector2(620f, 230f);
    private static readonly Vector2 NameTagPos = new Vector2(440f, 351f);
    private static readonly Vector2 NameTagSize = new Vector2(300f, 56f);
    private const float PreviewWorldPerPixel = .0027f;   // hanya untuk pratinjau di Scene view; saat Play dihitung ulang

    private static readonly Color Panel = new Color(.055f, .067f, .077f, .93f);
    private static readonly Color PanelRaised = new Color(.09f, .105f, .115f, .97f);
    private static readonly Color Gold = new Color(.78f, .59f, .30f, 1f);
    private static readonly Color Paper = new Color(.94f, .92f, .86f, 1f);

    private static TMP_FontAsset _font;

    // ------------------------------------------------------------------ Build

    [MenuItem("Tools/Prototype/Build Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Hentikan Play Mode dulu sebelum membangun scene.");
            return;
        }

        var baseData = AssetDatabase.LoadAssetAtPath<TrustEncounterData>(BaseNpcDataPath);
        if (baseData == null)
        {
            Debug.LogError($"Data NPC tidak ditemukan di {BaseNpcDataPath}. Buat/pindahkan asset TrustEncounterData ke path itu, " +
                           "atau ubah konstanta BaseNpcDataPath di PrototypeSceneBuilder.");
            return;
        }

        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Timpa scene?", $"{ScenePath} sudah ada. Bangun ulang dan timpa?", "Timpa", "Batal"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        try
        {
            BuildScene(baseData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Scene dibuat: {ScenePath}. Tekan Play: bicara dengan Penjaga (E), ambil Surat Jalan di meja kiri, lalu bicara lagi.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Gagal membangun Prototype scene: " + ex);
            throw;
        }
    }

    private static void BuildScene(TrustEncounterData baseData)
    {
        // Aset disiapkan SEBELUM scene baru dibuka. Yang sudah ada dipakai apa adanya (tidak ditimpa).
        ItemData letter = EnsureItem("Item_SuratJalan", "Surat Jalan",
            "Bukti izin lewat. Penjaga hanya mau bicara denganmu kalau kau membawanya.");
        ItemData key = EnsureItem("Item_KunciGerbang", "Kunci Gerbang",
            "Hadiah dari Penjaga setelah ia mempercayaimu.");
        TrustEncounterData data = EnsureGatedNpcData(baseData, letter, key);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        BuildEnvironment();
        BuildPlayer(out GameObject player, out Camera playerCamera, out PlayerInteractor interactor);
        TrustHudView hud = BuildHud(interactor);
        TrustStageView stage = BuildStage(playerCamera);
        TrustWorldEncounterUI facade = BuildFacade(stage, hud);
        BuildPickup(letter);
        BuildNpc(data, facade);
        BuildEventSystem();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = player;
    }

    // ------------------------------------------------------------ Environment

    private static void BuildEnvironment()
    {
        var environment = new GameObject("Environment");
        AddCube("Floor", environment.transform, new Vector3(0f, -.2f, 2.5f), new Vector3(14f, .4f, 18f),
            MakeMaterial("M_Stage_Floor", new Color(.22f, .25f, .24f)));
        AddCube("BackWall", environment.transform, new Vector3(0f, 2.5f, 7f), new Vector3(14f, 5f, .4f),
            MakeMaterial("M_Stage_Wall", new Color(.30f, .27f, .24f)));

        var lightObject = new GameObject("Directional Light");
        var sun = lightObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        lightObject.transform.rotation = Quaternion.Euler(48f, -30f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.5f, .5f, .55f);
    }

    private static void BuildPlayer(out GameObject player, out Camera camera, out PlayerInteractor interactor)
    {
        player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0f, -.5f);   // 2.6 m dari NPC: langsung dalam jangkauan interact

        var controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = .3f;
        controller.center = new Vector3(0f, .9f, 0f);

        player.AddComponent<PlayerInputReader>();
        player.AddComponent<PlayerMovement>();

        Transform pivot = new GameObject("CameraPivot").transform;
        pivot.SetParent(player.transform, false);
        pivot.localPosition = new Vector3(0f, 1.6f, 0f);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(pivot, false);
        camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.08f, .09f, .11f);
        cameraObject.AddComponent<AudioListener>();

        var look = player.AddComponent<PlayerLook>();
        var lookSo = new SerializedObject(look);
        Prop(lookSo, "cameraPivot").objectReferenceValue = pivot;
        Prop(lookSo, "sensitivity").floatValue = .1f;
        Prop(lookSo, "maxPitch").floatValue = 85f;
        lookSo.ApplyModifiedPropertiesWithoutUndo();

        interactor = player.AddComponent<PlayerInteractor>();
        var interactorSo = new SerializedObject(interactor);
        Prop(interactorSo, "viewOrigin").objectReferenceValue = cameraObject.transform;
        Prop(interactorSo, "interactDistance").floatValue = 3f;
        interactorSo.ApplyModifiedPropertiesWithoutUndo();

        // Item = syarat interaksi NPC (TrustEncounterData.requiredItems); hilang dari inventori saat dipakai.
        player.AddComponent<PlayerInventory>();
    }

    // -------------------------------------------------------------------- HUD

    private static TrustHudView BuildHud(PlayerInteractor interactor)
    {
        var canvasObject = new GameObject("HUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        Transform root = canvasObject.transform;

        // Crosshair
        RectTransform crosshairRect = NewRect("Crosshair", root);
        Place(crosshairRect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(8f, 8f));
        Image crosshair = AddImage(crosshairRect.gameObject, new Color(1f, 1f, 1f, .85f), false);

        // Prompt [E]
        RectTransform promptRect = NewRect("PromptRoot", root);
        Place(promptRect, new Vector2(.5f, .06f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(620f, 60f));
        AddImage(promptRect.gameObject, new Color(.025f, .035f, .043f, .88f), false);
        TMP_Text promptText = CreateText("PromptText", promptRect, "[E] Bicara", 25f, TextAlignmentOptions.Center, Paper);
        Stretch(promptText.rectTransform, 18f, 6f, 18f, 6f);
        promptRect.gameObject.SetActive(false);

        var interactorSo = new SerializedObject(interactor);
        Prop(interactorSo, "promptRoot").objectReferenceValue = promptRect.gameObject;
        Prop(interactorSo, "promptLabel").objectReferenceValue = promptText;
        interactorSo.ApplyModifiedPropertiesWithoutUndo();

        // Panel encounter (hanya tampil saat encounter)
        RectTransform encounterRoot = NewRect("EncounterRoot", root);
        Stretch(encounterRoot, 0f, 0f, 0f, 0f);

        RectTransform status = NewRect("StatusPanel", encounterRoot);
        Place(status, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -36f), new Vector2(600f, 64f));
        AddImage(status.gameObject, Panel, false);
        AddAccent(status);
        TMP_Text resonance = CreateText("ResonanceText", status, "Resonansi 0 / 10", 24f, TextAlignmentOptions.MidlineLeft, Paper, FontStyles.Bold);
        Stretch(resonance.rectTransform, 24f, 4f, 330f, 4f);
        TMP_Text turns = CreateText("TurnsText", status, "Giliran 5", 22f, TextAlignmentOptions.MidlineRight, Paper);
        turns.enableAutoSizing = true;
        turns.fontSizeMin = 16f;
        turns.fontSizeMax = 22f;
        Stretch(turns.rectTransform, 270f, 4f, 20f, 4f);

        RectTransform result = NewRect("ResultPanel", encounterRoot);
        Place(result, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(640f, 300f));
        AddImage(result.gameObject, new Color(.035f, .043f, .052f, .985f), true);
        AddAccent(result);
        TMP_Text header = CreateText("ResultHeader", result, "PERTEMUAN SELESAI", 22f, TextAlignmentOptions.Center, Gold, FontStyles.Bold);
        Place(header.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -36f), new Vector2(560f, 40f));
        TMP_Text resultText = CreateText("ResultText", result, "", 30f, TextAlignmentOptions.Center, Paper);
        resultText.enableAutoSizing = true;
        resultText.fontSizeMin = 22f;
        resultText.fontSizeMax = 32f;
        Place(resultText.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 12f), new Vector2(560f, 110f));
        Button retry = CreateSimpleButton("RetryButton", result, "Coba Lagi", new Vector2(-130f, 46f), new Vector2(220f, 58f));
        Button close = CreateSimpleButton("CloseButton", result, "Tutup", new Vector2(130f, 46f), new Vector2(220f, 58f));
        result.gameObject.SetActive(false);
        encounterRoot.gameObject.SetActive(false);

        var hud = canvasObject.AddComponent<TrustHudView>();
        var so = new SerializedObject(hud);
        Prop(so, "crosshairImage").objectReferenceValue = crosshair;
        Prop(so, "encounterRoot").objectReferenceValue = encounterRoot.gameObject;
        Prop(so, "resonanceLabel").objectReferenceValue = resonance;
        Prop(so, "turnsLabel").objectReferenceValue = turns;
        Prop(so, "resultPanel").objectReferenceValue = result.gameObject;
        Prop(so, "resultLabel").objectReferenceValue = resultText;
        Prop(so, "retryButton").objectReferenceValue = retry;
        Prop(so, "closeButton").objectReferenceValue = close;
        so.ApplyModifiedPropertiesWithoutUndo();
        return hud;
    }

    // ------------------------------------------------------------------ Stage

    private static TrustStageView BuildStage(Camera playerCamera)
    {
        var stageObject = new GameObject("TrustStage");
        stageObject.transform.position = new Vector3(0f, 1.2f, 2.1f);   // pratinjau di titik dada NPC; saat Play dipindah otomatis

        var canvasObject = new GameObject("StageCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(stageObject.transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = playerCamera;
        var raycaster = canvasObject.GetComponent<GraphicRaycaster>();
        raycaster.ignoreReversedGraphics = false;
        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = CanvasSize;
        canvasRect.localScale = Vector3.one * PreviewWorldPerPixel;

        // Hint (kiri-atas)
        RectTransform hintPanel = NewPanel("HintPanel", canvasRect, HintPos, HintSize);
        TMP_Text hintText = CreateText("HintText", hintPanel, "Petunjuk bahasa tubuh akan muncul di sini.", 28f, TextAlignmentOptions.TopLeft, Paper);
        hintText.enableAutoSizing = true;
        hintText.fontSizeMin = 20f;
        hintText.fontSizeMax = 30f;
        Stretch(hintText.rectTransform, 30f, 18f, 24f, 18f);

        // Nama NPC (di atas dialogue) + dialogue (kanan-atas)
        RectTransform nameTag = NewPanel("NameTag", canvasRect, NameTagPos, NameTagSize);
        TMP_Text nameText = CreateText("NPCNameText", nameTag, "PENJAGA", 24f, TextAlignmentOptions.MidlineLeft, Gold, FontStyles.Bold);
        Stretch(nameText.rectTransform, 24f, 4f, 16f, 4f);

        RectTransform dialoguePanel = NewPanel("DialoguePanel", canvasRect, DialoguePos, DialogueSize);
        TMP_Text dialogueText = CreateText("DialogueText", dialoguePanel, "", 30f, TextAlignmentOptions.TopLeft, Paper);
        dialogueText.enableAutoSizing = true;
        dialogueText.fontSizeMin = 22f;
        dialogueText.fontSizeMax = 32f;
        Stretch(dialogueText.rectTransform, 30f, 18f, 24f, 18f);

        // Pilihan (kiri-bawah)
        RectTransform choicesPanel = NewPanel("ChoicesPanel", canvasRect, ChoicesPos, ChoicesSize);
        var layout = choicesPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 14, 16, 16);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        string[] keyHints = { "1", "2", "3", "4" };
        string[] objectNames = { "OBSButton", "KNOButton", "ACTButton", "SILButton" };
        var buttons = new Button[TrustExtensions.ApproachCount];
        var labels = new TMP_Text[TrustExtensions.ApproachCount];
        for (int i = 0; i < buttons.Length; i++)
            buttons[i] = CreateChoiceButton(choicesPanel, objectNames[i], keyHints[i], out labels[i]);

        var stage = stageObject.AddComponent<TrustStageView>();
        var so = new SerializedObject(stage);
        Prop(so, "stageCanvas").objectReferenceValue = canvas;
        Prop(so, "npcNameLabel").objectReferenceValue = nameText;
        Prop(so, "hintLabel").objectReferenceValue = hintText;
        Prop(so, "dialogueLabel").objectReferenceValue = dialogueText;
        SerializedProperty array = Prop(so, "approachButtons");
        array.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++)
        {
            SerializedProperty element = array.GetArrayElementAtIndex(i);
            Rel(element, "approach").enumValueIndex = i;   // 0..3 = OBS, KNO, ACT, SIL
            Rel(element, "button").objectReferenceValue = buttons[i];
            Rel(element, "label").objectReferenceValue = labels[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return stage;
    }

    private static TrustWorldEncounterUI BuildFacade(TrustStageView stage, TrustHudView hud)
    {
        var go = new GameObject("EncounterUI");
        var facade = go.AddComponent<TrustWorldEncounterUI>();
        var so = new SerializedObject(facade);
        Prop(so, "stage").objectReferenceValue = stage;
        Prop(so, "hud").objectReferenceValue = hud;
        so.ApplyModifiedPropertiesWithoutUndo();
        return facade;
    }

    // ------------------------------------------------------------------ Item

    private static ItemData EnsureItem(string fileName, string displayName, string description)
    {
        if (!AssetDatabase.IsValidFolder(ItemFolder)) AssetDatabase.CreateFolder(ItemFolderParent, ItemFolderName);

        string path = $"{ItemFolder}/{fileName}.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item != null) return item;

        item = ScriptableObject.CreateInstance<ItemData>();
        var so = new SerializedObject(item);
        Prop(so, "displayName").stringValue = displayName;
        Prop(so, "description").stringValue = description;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(item, path);
        return item;
    }

    /// <summary>Salinan data Penjaga + syarat item + hadiah, supaya TD_NPC_Penjaga dan scene lama tidak berubah.</summary>
    private static TrustEncounterData EnsureGatedNpcData(TrustEncounterData source, ItemData required, ItemData reward)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TrustEncounterData>(GatedNpcDataPath);
        if (existing != null) return existing;   // jangan timpa edit desainer

        if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), GatedNpcDataPath))
            throw new InvalidOperationException($"Gagal menyalin {AssetDatabase.GetAssetPath(source)} ke {GatedNpcDataPath}.");

        var copy = AssetDatabase.LoadAssetAtPath<TrustEncounterData>(GatedNpcDataPath);
        var so = new SerializedObject(copy);

        SerializedProperty requiredArray = Prop(so, "requiredItems");
        requiredArray.arraySize = 1;
        requiredArray.GetArrayElementAtIndex(0).objectReferenceValue = required;

        Prop(so, "lockedDialogue").stringValue = "Tanpa surat jalan, kau tidak lewat.";

        SerializedProperty rewardArray = Prop(so, "rewardItems");
        rewardArray.arraySize = 1;
        rewardArray.GetArrayElementAtIndex(0).objectReferenceValue = reward;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(copy);
        return copy;
    }

    /// <summary>Meja di sisi kiri dengan item di atasnya. ±3 m dari titik awal pemain, jadi harus berjalan sedikit.</summary>
    private static void BuildPickup(ItemData item)
    {
        var props = new GameObject("Props");
        AddCube("Table", props.transform, new Vector3(-3.2f, .45f, .2f), new Vector3(.9f, .9f, .9f),
            MakeMaterial("M_Stage_Table", new Color(.25f, .19f, .14f)));

        GameObject pickup = AddCube("Pickup_SuratJalan", props.transform, new Vector3(-3.2f, 1.05f, .2f), new Vector3(.3f, .3f, .3f),
            MakeMaterial("M_Stage_Item", Gold));
        pickup.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

        var component = pickup.AddComponent<ItemPickup>();
        var so = new SerializedObject(component);
        Prop(so, "item").objectReferenceValue = item;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // -------------------------------------------------------------------- NPC

    private static void BuildNpc(TrustEncounterData data, TrustWorldEncounterUI facade)
    {
        var npc = new GameObject("NPC_Penjaga");
        npc.transform.position = new Vector3(0f, 0f, 2.1f);

        var capsule = npc.AddComponent<CapsuleCollider>();
        capsule.height = 2f;
        capsule.radius = .42f;
        capsule.center = new Vector3(0f, 1f, 0f);

        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(npc.transform, false);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        visual.transform.localScale = new Vector3(.8f, 1f, .8f);
        UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.GetComponent<Renderer>().sharedMaterial = MakeMaterial("M_Stage_NPC", new Color(.42f, .29f, .2f));

        var anchor = new GameObject("StageAnchor").transform;
        anchor.SetParent(npc.transform, false);
        anchor.localPosition = new Vector3(0f, 1.2f, 0f);

        var trustNpc = npc.AddComponent<TrustNpc>();
        var so = new SerializedObject(trustNpc);
        Prop(so, "data").objectReferenceValue = data;
        Prop(so, "ui").objectReferenceValue = facade;
        Prop(so, "stageAnchor").objectReferenceValue = anchor;
        Prop(so, "resultDelay").floatValue = 1.2f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildEventSystem()
    {
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var module = go.GetComponent<InputSystemUIInputModule>();
        MethodInfo assign = typeof(InputSystemUIInputModule).GetMethod("AssignDefaultActions", BindingFlags.Public | BindingFlags.Instance);
        if (assign != null) assign.Invoke(module, null);
    }

    // --------------------------------------------------------------- Validate

    [MenuItem("Tools/Prototype/Validate Scene")]
    public static void Validate()
    {
        var problems = new List<string>();

        var stage = UnityEngine.Object.FindFirstObjectByType<TrustStageView>();
        var hud = UnityEngine.Object.FindFirstObjectByType<TrustHudView>();
        var facade = UnityEngine.Object.FindFirstObjectByType<TrustWorldEncounterUI>();
        var npc = UnityEngine.Object.FindFirstObjectByType<TrustNpc>();
        var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        var modules = UnityEngine.Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None);

        if (stage == null) problems.Add("TrustStageView tidak ada di scene.");
        else
        {
            var canvas = Prop(new SerializedObject(stage), "stageCanvas").objectReferenceValue as Canvas;
            if (canvas == null) problems.Add("TrustStageView.stageCanvas kosong.");
            else
            {
                if (canvas.renderMode != RenderMode.WorldSpace) problems.Add("StageCanvas bukan World Space.");
                if (canvas.worldCamera == null) problems.Add("StageCanvas.worldCamera kosong (klik mouse tidak akan jalan).");
                if (canvas.GetComponent<GraphicRaycaster>() == null) problems.Add("StageCanvas tidak punya GraphicRaycaster.");
            }
        }

        if (hud == null) problems.Add("TrustHudView tidak ada di scene.");
        else
        {
            var canvas = hud.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                problems.Add("HUDCanvas bukan Screen Space Overlay.");
        }

        if (facade == null) problems.Add("TrustWorldEncounterUI tidak ada di scene.");
        if (npc == null) problems.Add("TrustNpc tidak ada di scene.");
        else
        {
            var so = new SerializedObject(npc);
            if (Prop(so, "data").objectReferenceValue == null) problems.Add("TrustNpc.data kosong.");
            if (!(Prop(so, "ui").objectReferenceValue is ITrustEncounterView)) problems.Add("TrustNpc.ui kosong atau bukan ITrustEncounterView.");
            if (Prop(so, "stageAnchor").objectReferenceValue == null) problems.Add("TrustNpc.stageAnchor kosong (akan pakai posisi NPC + 1.2 m).");
        }

        if (eventSystems.Length != 1) problems.Add($"Harus ada tepat 1 EventSystem, ditemukan {eventSystems.Length}.");
        if (modules.Length != 1) problems.Add($"Harus ada tepat 1 InputSystemUIInputModule, ditemukan {modules.Length}.");

        ValidateItems(problems);

        if (problems.Count == 0) Debug.Log("PROTOTYPE_VALIDATION OK");
        else Debug.LogError("PROTOTYPE_VALIDATION GAGAL:\n- " + string.Join("\n- ", problems));
    }

    /// <summary>Pastikan setiap item yang dibutuhkan NPC benar-benar bisa didapat di scene ini, dan bisa dijangkau.</summary>
    private static void ValidateItems(List<string> problems)
    {
        var inventory = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
        var npcs = UnityEngine.Object.FindObjectsByType<TrustNpc>(FindObjectsSortMode.None);
        var pickups = UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None);

        // Berapa buah tiap item yang bisa didapat pemain: pickup + item awal + hadiah NPC.
        var supply = new Dictionary<ItemData, int>();
        void AddSupply(ItemData item)
        {
            if (item == null) return;
            supply.TryGetValue(item, out int count);
            supply[item] = count + 1;
        }

        foreach (ItemPickup pickup in pickups)
        {
            if (pickup.GetComponentInChildren<Collider>() == null)
                problems.Add($"ItemPickup '{pickup.name}' tidak punya Collider (tidak bisa ditarget).");
            AddSupply(Prop(new SerializedObject(pickup), "item").objectReferenceValue as ItemData);
        }

        if (inventory != null)
        {
            SerializedProperty starting = Prop(new SerializedObject(inventory), "startingItems");
            for (int i = 0; i < starting.arraySize; i++)
                AddSupply(starting.GetArrayElementAtIndex(i).objectReferenceValue as ItemData);
        }

        var demand = new Dictionary<ItemData, int>();
        foreach (TrustNpc npc in npcs)
        {
            if (npc.GetComponentInChildren<Collider>() == null)
                problems.Add($"TrustNpc '{npc.name}' tidak punya Collider (tidak bisa ditarget).");

            var data = Prop(new SerializedObject(npc), "data").objectReferenceValue as TrustEncounterData;
            if (data == null) continue;

            if (data.RewardItems != null)
                foreach (ItemData reward in data.RewardItems) AddSupply(reward);

            if (data.RequiredItems == null) continue;
            foreach (ItemData required in data.RequiredItems)
            {
                if (required == null) continue;
                demand.TryGetValue(required, out int count);
                demand[required] = count + 1;
            }
        }

        if (demand.Count > 0 && inventory == null)
            problems.Add("Ada NPC yang butuh item, tapi tidak ada PlayerInventory di scene (syarat tidak akan pernah terpenuhi).");

        foreach (KeyValuePair<ItemData, int> need in demand)
        {
            supply.TryGetValue(need.Key, out int have);
            if (have < need.Value)
                problems.Add($"Item '{need.Key.DisplayName}' dibutuhkan {need.Value}x, tapi hanya ada {have} sumber di scene " +
                             "(pickup, item awal, atau hadiah NPC).");
        }
    }

    // ---------------------------------------------------------------- Helpers

    private static SerializedProperty Prop(SerializedObject so, string name)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property == null)
            throw new InvalidOperationException($"Field '{name}' tidak ditemukan di {so.targetObject.GetType().Name}. Nama field di script berubah?");
        return property;
    }

    private static SerializedProperty Rel(SerializedProperty parent, string name)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property == null)
            throw new InvalidOperationException($"Sub-field '{name}' tidak ditemukan di {parent.propertyPath}.");
        return property;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static Image AddImage(GameObject go, Color color, bool raycastTarget)
    {
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static void AddAccent(RectTransform parent)
    {
        RectTransform strip = NewRect("Accent", parent);
        strip.anchorMin = new Vector2(0f, 0f);
        strip.anchorMax = new Vector2(0f, 1f);
        strip.pivot = new Vector2(0f, .5f);
        strip.offsetMin = new Vector2(0f, 10f);
        strip.offsetMax = new Vector2(5f, -10f);
        AddImage(strip.gameObject, Gold, false);
        strip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;   // jangan ikut diatur LayoutGroup induknya
    }

    /// <summary>Panel gelap beraksen emas; posisi relatif ke tengah canvas.</summary>
    private static RectTransform NewPanel(string name, RectTransform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = NewRect(name, parent);
        Place(rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, size);
        AddImage(rect.gameObject, Panel, false);
        AddAccent(rect);
        return rect;
    }

    private static TMP_Text CreateText(string name, RectTransform parent, string value, float size,
        TextAlignmentOptions alignment, Color color, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (_font != null) text.font = _font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.raycastTarget = false;
        return text;
    }

    private static void StyleButton(Button button, Image image)
    {
        image.color = Color.white;   // warna sebenarnya dari tint di bawah
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = PanelRaised;
        colors.highlightedColor = new Color(.24f, .21f, .15f, 1f);
        colors.pressedColor = new Color(.42f, .32f, .18f, 1f);
        colors.selectedColor = PanelRaised;   // tidak ada warna "terpilih" yang menempel
        colors.disabledColor = new Color(.10f, .11f, .12f, .58f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = .1f;
        button.colors = colors;
    }

    private static Button CreateChoiceButton(RectTransform parent, string name, string keyHint, out TMP_Text label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 72f);
        var element = go.GetComponent<LayoutElement>();
        element.preferredHeight = 72f;
        element.minHeight = 64f;

        var image = go.GetComponent<Image>();
        image.raycastTarget = true;   // hanya tombol yang menangkap klik
        var button = go.GetComponent<Button>();
        StyleButton(button, image);

        var rect = go.GetComponent<RectTransform>();
        TMP_Text key = CreateText("KeyHint", rect, keyHint, 26f, TextAlignmentOptions.Center, Gold, FontStyles.Bold);
        key.rectTransform.anchorMin = new Vector2(0f, 0f);
        key.rectTransform.anchorMax = new Vector2(0f, 1f);
        key.rectTransform.offsetMin = new Vector2(10f, 6f);
        key.rectTransform.offsetMax = new Vector2(62f, -6f);

        label = CreateText("Label", rect, "", 26f, TextAlignmentOptions.MidlineLeft, Paper);
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 26f;
        Stretch(label.rectTransform, 74f, 6f, 16f, 6f);
        return button;
    }

    private static Button CreateSimpleButton(string name, RectTransform parent, string text, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, new Vector2(.5f, 0f), new Vector2(.5f, .5f), position, size);
        var image = go.GetComponent<Image>();
        image.raycastTarget = true;
        var button = go.GetComponent<Button>();
        StyleButton(button, image);
        TMP_Text label = CreateText("Label", rect, text, 24f, TextAlignmentOptions.Center, Paper);
        Stretch(label.rectTransform, 8f, 4f, 8f, 4f);
        return button;
    }

    private static GameObject AddCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, true);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    private static Material MakeMaterial(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) Directory.CreateDirectory(MaterialFolder);

        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Shader Lit/Standard tidak ditemukan.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        EditorUtility.SetDirty(material);
        return material;
    }
}
