using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Outline gaya potongan kertas (krem, tepi sedikit goyang ala stop-motion) di sekeliling objek
/// yang sedang disorot pemain. Hanya untuk objek di dunia, bukan UI.
///
/// Tidak perlu dipasang manual: PlayerInteractor menambahkan komponen ini otomatis ke interactable
/// yang sedang difokus (pintu, item, NPC, dll). Pasang manual HANYA kalau satu objek butuh tampilan
/// berbeda (warna, tebal, dsb); komponen yang sudah ada dipakai apa adanya.
///
/// Tidak butuh Renderer Feature atau setting URP apa pun, dan tidak mengubah material objek.
/// Outline digambar lewat Graphics.DrawMesh selama objek disorot:
///  - MeshRenderer: dua pass (PaperOutlineMask lalu PaperOutlineHull). Bayangan tembok di depan objek
///    tetap menutupi outline karena memakai depth test.
///  - SpriteRenderer (NPC): quad di belakang sprite, PaperOutlineSprite melebarkan alpha sprite.
///
/// Ketiga shader harus ada di folder bernama "Resources" (mis. _Project/Resources/PaperOutline/)
/// supaya ikut ke build.
/// </summary>
[DisallowMultipleComponent]
public class InteractableOutline : MonoBehaviour
{
    // Auto = pakai mesh kalau ada, kalau tidak ada mesh baru pakai sprite. Jadi pintu yang ditempeli
    // poster (sprite anak) tidak ikut mengoutline posternya, sedangkan NPC (hanya sprite) tetap kena.
    public enum Targets
    {
        Auto,
        MeshesOnly,
        SpritesOnly,
        Both
    }

    [Header("Tampilan kertas")]
    [SerializeField] private Color paperColor = new Color(0.96f, 0.92f, 0.82f, 1f);

    [Tooltip("Tebal outline dalam meter dunia, diukur pada jarak 2 meter dari kamera.")]
    [SerializeField, Min(0f)] private float thickness = 0.03f;

    [Tooltip("0 = tebal tetap dalam meter (jauh jadi tipis). 1 = tebal di layar selalu sama. " +
             "Antara keduanya cocok untuk kamera first-person.")]
    [SerializeField, Range(0f, 1f)] private float distanceCompensation = 0.6f;

    [Tooltip("Seberapa tidak rata tepi gunting. 0 = rapi.")]
    [SerializeField, Range(0f, 1f)] private float wobble = 0.2f;

    [Tooltip("Berapa kali per detik bentuk goyangan berganti. 0 = diam.")]
    [SerializeField, Min(0f)] private float boilFps = 6f;

    [SerializeField, Range(0f, 1f)] private float paperGrain = 0.12f;

    [Tooltip("Hanya untuk mesh: outline digeser sedikit ke kamera (meter) supaya tetap kelihatan di objek " +
             "yang rata dengan tembok, seperti pintu. Naikkan kalau outline hilang di sebagian sisi.")]
    [SerializeField, Min(0f)] private float depthBias = 0.1f;

    [Header("Animasi")]
    [Tooltip("Lama outline 'membesar' saat mulai disorot dan mengecil saat tidak.")]
    [SerializeField, Min(0.01f)] private float popTime = 0.15f;

    [Header("Yang di-outline")]
    [SerializeField] private Targets targets = Targets.Auto;

    // Tebal dihitung pada jarak ini, dan faktor jaraknya dibatasi.
    private const float ReferenceDistance = 2f;
    private const float MaxDistanceFactor = 2.5f;

    // Outline mesh terlihat sekitar 0,6-1x tebal asli di sudut kotak, jadi dikali agar mirip outline sprite.
    private const float HullThicknessScale = 1.4f;

    // Tebal maksimal sebagai pecahan dari diagonal objek, supaya item kecil tidak tertutup outline tebal.
    private const float MaxThicknessFraction = 0.08f;

    // EaseOutBack melewati 1 sedikit (sekitar 1,1); beri ruang di quad sprite.
    private const float OvershootMargin = 1.15f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
    private static readonly int DepthBiasId = Shader.PropertyToID("_DepthBias");
    private static readonly int WobbleId = Shader.PropertyToID("_Wobble");
    private static readonly int BoilFpsId = Shader.PropertyToID("_BoilFps");
    private static readonly int GrainId = Shader.PropertyToID("_Grain");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int RadiusUvId = Shader.PropertyToID("_RadiusUV");
    private static readonly int ClipRectUvId = Shader.PropertyToID("_ClipRectUV");

    private static MaterialPropertyBlock _block;

    private sealed class MeshItem
    {
        public MeshRenderer renderer;
        public Mesh mesh;   // salinan dengan smooth normal di UV3 (atau mesh asli kalau tidak bisa dibaca)
    }

    private sealed class SpriteItem
    {
        public SpriteRenderer renderer;
        public Mesh quad;
        public bool built;
        public Sprite builtFor;
        public Vector2 builtScale;
        public float builtMaxWorld;
        public Vector4 clipRectUv;
    }

    private readonly List<MeshItem> _meshes = new List<MeshItem>();
    private readonly List<SpriteItem> _sprites = new List<SpriteItem>();

    private bool _highlighted;
    private float _amount;                    // 0..1, maju-mundur sesuai _highlighted
    private float _sizeLimit = float.MaxValue;

    public bool IsHighlighted => _highlighted;

    /// <summary>Dipanggil PlayerInteractor saat fokus berpindah. Aman dipanggil berulang.</summary>
    public void SetHighlighted(bool on)
    {
        if (_highlighted == on) return;

        _highlighted = on;
        if (on) Collect();   // renderer dikumpulkan ulang tiap mulai disorot, bukan tiap frame
    }

    private void OnDisable()
    {
        _highlighted = false;
        _amount = 0f;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _sprites.Count; i++)
            if (_sprites[i].quad != null) Destroy(_sprites[i].quad);
    }

    private void LateUpdate()
    {
        float goal = _highlighted ? 1f : 0f;
        if (!Mathf.Approximately(_amount, goal))
            _amount = Mathf.MoveTowards(_amount, goal, Time.unscaledDeltaTime / popTime);

        if (_amount <= 0f) return;

        // Muncul: membal sedikit. Hilang: mengecil halus.
        float eased = _highlighted ? EaseOutBack(_amount) : _amount * _amount * (3f - 2f * _amount);

        if (_block == null) _block = new MaterialPropertyBlock();

        for (int i = 0; i < _meshes.Count; i++) DrawMesh(_meshes[i], eased);
        for (int i = 0; i < _sprites.Count; i++) DrawSprite(_sprites[i], eased);
    }

    // ---------- Kumpulkan renderer ----------

    private void Collect()
    {
        // Sprite yang sudah punya quad tidak perlu dibuat ulang; hanya buang yang renderernya hilang.
        List<SpriteItem> oldSprites = new List<SpriteItem>(_sprites);
        _meshes.Clear();
        _sprites.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
        Bounds total = default;
        bool hasBounds = false;

        List<MeshItem> foundMeshes = new List<MeshItem>();
        List<SpriteItem> foundSprites = new List<SpriteItem>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (!r.enabled) continue;

            if (r is MeshRenderer meshRenderer)
            {
                MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                foundMeshes.Add(new MeshItem
                {
                    renderer = meshRenderer,
                    mesh = PaperOutlineResources.GetHullMesh(filter.sharedMesh)
                });
            }
            else if (r is SpriteRenderer spriteRenderer && spriteRenderer.sprite != null)
            {
                SpriteItem reused = oldSprites.Find(s => s.renderer == spriteRenderer);
                if (reused != null) oldSprites.Remove(reused);

                foundSprites.Add(reused ?? new SpriteItem { renderer = spriteRenderer });
            }
        }

        bool useMeshes = targets != Targets.SpritesOnly;
        bool useSprites = targets != Targets.MeshesOnly;
        if (targets == Targets.Auto) useSprites = foundMeshes.Count == 0;

        if (useMeshes)
        {
            _meshes.AddRange(foundMeshes);
            for (int i = 0; i < foundMeshes.Count; i++) Encapsulate(ref total, ref hasBounds, foundMeshes[i].renderer.bounds);
        }

        if (useSprites)
        {
            _sprites.AddRange(foundSprites);
            for (int i = 0; i < foundSprites.Count; i++) Encapsulate(ref total, ref hasBounds, foundSprites[i].renderer.bounds);
        }

        // Quad milik sprite yang tidak dipakai lagi dibuang.
        for (int i = 0; i < oldSprites.Count; i++)
            if (oldSprites[i].quad != null) Destroy(oldSprites[i].quad);
        for (int i = 0; i < foundSprites.Count; i++)
            if (!useSprites && foundSprites[i].quad != null) { Destroy(foundSprites[i].quad); foundSprites[i].quad = null; foundSprites[i].built = false; }

        _sizeLimit = hasBounds ? Mathf.Max(0.005f, total.size.magnitude * MaxThicknessFraction) : float.MaxValue;
    }

    private static void Encapsulate(ref Bounds total, ref bool hasBounds, Bounds b)
    {
        if (!hasBounds) { total = b; hasBounds = true; }
        else total.Encapsulate(b);
    }

    // ---------- Gambar ----------

    // Tebal dalam meter dunia pada titik tertentu, sudah dikali animasi.
    private float WorldThickness(Vector3 point, float eased)
    {
        float t = thickness;

        Camera cam = Camera.main;
        if (cam != null && distanceCompensation > 0f)
        {
            float distance = Vector3.Distance(cam.transform.position, point);
            float factor = Mathf.Clamp(distance / ReferenceDistance, 0.5f, MaxDistanceFactor);
            t *= Mathf.Lerp(1f, factor, distanceCompensation);
        }

        return Mathf.Min(t, _sizeLimit) * eased;
    }

    private void DrawMesh(MeshItem item, float eased)
    {
        MeshRenderer r = item.renderer;
        if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || item.mesh == null) return;

        Material mask = PaperOutlineResources.Mask;
        Material hull = PaperOutlineResources.Hull;
        if (mask == null || hull == null) return;

        _block.Clear();
        _block.SetColor(ColorId, paperColor);
        _block.SetFloat(ThicknessId, WorldThickness(r.bounds.center, eased) * HullThicknessScale);
        _block.SetFloat(DepthBiasId, depthBias);
        _block.SetFloat(WobbleId, wobble);
        _block.SetFloat(BoilFpsId, boilFps);
        _block.SetFloat(GrainId, paperGrain);

        Matrix4x4 matrix = r.localToWorldMatrix;
        int layer = r.gameObject.layer;

        for (int s = 0; s < item.mesh.subMeshCount; s++)
        {
            Graphics.DrawMesh(item.mesh, matrix, mask, layer, null, s, null, false, false, false);
            Graphics.DrawMesh(item.mesh, matrix, hull, layer, null, s, _block, false, false, false);
        }
    }

    private void DrawSprite(SpriteItem item, float eased)
    {
        SpriteRenderer r = item.renderer;
        if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) return;

        Material material = PaperOutlineResources.Sprite;
        Sprite sprite = r.sprite;
        if (material == null || sprite == null) return;

        Texture2D texture = sprite.texture;
        if (texture == null) return;

        Vector3 scale = r.transform.lossyScale;
        float scaleX = Mathf.Max(Mathf.Abs(scale.x), 0.0001f);
        float scaleY = Mathf.Max(Mathf.Abs(scale.y), 0.0001f);

        EnsureQuad(item, sprite, texture, scaleX, scaleY);
        if (item.quad == null) return;

        // Radius outline: meter dunia -> unit lokal sprite -> piksel texture -> UV.
        float worldThickness = WorldThickness(r.bounds.center, eased);
        float ppu = sprite.pixelsPerUnit;
        Vector4 radiusUv = new Vector4(
            worldThickness / scaleX * ppu / texture.width,
            worldThickness / scaleY * ppu / texture.height, 0f, 0f);

        _block.Clear();
        _block.SetTexture(MainTexId, texture);
        _block.SetColor(ColorId, paperColor);
        _block.SetVector(RadiusUvId, radiusUv);
        _block.SetVector(ClipRectUvId, item.clipRectUv);
        _block.SetFloat(WobbleId, wobble);
        _block.SetFloat(BoilFpsId, boilFps);
        _block.SetFloat(GrainId, paperGrain);

        // flipX/flipY pada SpriteRenderer membalik sprite terhadap titik pivotnya.
        Matrix4x4 matrix = r.transform.localToWorldMatrix
                           * Matrix4x4.Scale(new Vector3(r.flipX ? -1f : 1f, r.flipY ? -1f : 1f, 1f));

        Graphics.DrawMesh(item.quad, matrix, material, r.gameObject.layer, null, 0, _block, false, false, false);
    }

    // Quad sedikit lebih besar dari seluruh rect sprite, UV-nya menunjuk ke texture sprite.
    // Hanya dibuat ulang kalau sprite, skala, atau tebal maksimum berubah.
    private void EnsureQuad(SpriteItem item, Sprite sprite, Texture2D texture, float scaleX, float scaleY)
    {
        float maxWorld = Mathf.Min(thickness * Mathf.Lerp(1f, MaxDistanceFactor, distanceCompensation), _sizeLimit)
                         * OvershootMargin * (1f + wobble);

        if (item.built
            && item.builtFor == sprite
            && Mathf.Approximately(item.builtScale.x, scaleX)
            && Mathf.Approximately(item.builtScale.y, scaleY)
            && Mathf.Approximately(item.builtMaxWorld, maxWorld))
            return;

        item.built = true;
        item.builtFor = sprite;
        item.builtScale = new Vector2(scaleX, scaleY);
        item.builtMaxWorld = maxWorld;

        if (sprite.packed)
        {
            // Sprite di dalam Sprite Atlas: posisi di texture tidak bisa dibaca dengan aman.
            Debug.LogWarning($"{name}: sprite '{sprite.name}' ada di Sprite Atlas, outline kertas dilewati. " +
                             "Keluarkan sprite NPC dari atlas.", this);
            if (item.quad != null) { Destroy(item.quad); item.quad = null; }
            return;
        }

        float ppu = sprite.pixelsPerUnit;
        Rect rect = sprite.rect;        // piksel di texture asli
        Vector2 pivot = sprite.pivot;   // piksel di dalam rect; titik (0,0) lokal sprite

        float padX = maxWorld / scaleX + 2f / ppu;
        float padY = maxWorld / scaleY + 2f / ppu;

        float minX = -pivot.x / ppu - padX;
        float minY = -pivot.y / ppu - padY;
        float maxX = (rect.width - pivot.x) / ppu + padX;
        float maxY = (rect.height - pivot.y) / ppu + padY;

        Vector3[] vertices =
        {
            new Vector3(minX, minY, 0f), new Vector3(maxX, minY, 0f),
            new Vector3(maxX, maxY, 0f), new Vector3(minX, maxY, 0f)
        };

        Vector2[] uvs = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            float px = rect.x + vertices[i].x * ppu + pivot.x;
            float py = rect.y + vertices[i].y * ppu + pivot.y;
            uvs[i] = new Vector2(px / texture.width, py / texture.height);
        }

        if (item.quad == null)
        {
            item.quad = new Mesh { name = "PaperOutlineQuad", hideFlags = HideFlags.HideAndDontSave };
            item.quad.MarkDynamic();
        }

        item.quad.Clear();
        item.quad.vertices = vertices;
        item.quad.uv = uvs;
        item.quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        item.quad.RecalculateBounds();

        item.clipRectUv = new Vector4(
            rect.xMin / texture.width, rect.yMin / texture.height,
            rect.xMax / texture.width, rect.yMax / texture.height);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}

/// <summary>Material bersama dan salinan mesh ber-smooth-normal untuk InteractableOutline.</summary>
internal static class PaperOutlineResources
{
    private const string MaskShaderName = "Interact/PaperOutlineMask";
    private const string HullShaderName = "Interact/PaperOutlineHull";
    private const string SpriteShaderName = "Interact/PaperOutlineSprite";

    private static Material _mask;
    private static Material _hull;
    private static Material _sprite;
    private static bool _shaderMissingLogged;

    // source -> salinan ber-smooth-normal (atau source itu sendiri kalau tidak bisa dibaca)
    private static readonly Dictionary<Mesh, Mesh> HullMeshes = new Dictionary<Mesh, Mesh>();

    // Biar jalan juga kalau Enter Play Mode Options mematikan domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _mask = null;
        _hull = null;
        _sprite = null;
        _shaderMissingLogged = false;
        HullMeshes.Clear();
    }

    // Antrian: tubuh objek (2000) -> mask stencil (2001) -> hull (2002); sprite tepat sebelum sprite aslinya (2999).
    public static Material Mask => Get(ref _mask, MaskShaderName, 2001);
    public static Material Hull => Get(ref _hull, HullShaderName, 2002);
    public static Material Sprite => Get(ref _sprite, SpriteShaderName, 2999);

    private static Material Get(ref Material slot, string shaderName, int renderQueue)
    {
        if (slot != null) return slot;

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            if (!_shaderMissingLogged)
            {
                _shaderMissingLogged = true;
                Debug.LogError($"[PaperOutline] Shader '{shaderName}' tidak ditemukan. Taruh ketiga file .shader " +
                               "(PaperOutlineMask, PaperOutlineHull, PaperOutlineSprite) di folder bernama " +
                               "\"Resources\", mis. Assets/_Project/Resources/PaperOutline/.");
            }
            return null;
        }

        slot = new Material(shader)
        {
            name = shaderName,
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = renderQueue
        };
        return slot;
    }

    /// <summary>
    /// Salinan mesh yang UV3-nya berisi "smooth normal": normal tiap vertex dirata-ratakan dengan vertex lain
    /// di posisi yang sama. Tanpa ini, pojok kotak (pintu) ikut terbelah saat mesh digemukkan.
    /// Butuh Read/Write aktif di import settings model; kalau tidak, dipakai mesh asli (pojok bisa renggang).
    /// </summary>
    public static Mesh GetHullMesh(Mesh source)
    {
        if (HullMeshes.TryGetValue(source, out Mesh cached) && cached != null) return cached;

        Mesh result = source;

        if (source.isReadable)
        {
            Mesh copy = Object.Instantiate(source);
            copy.name = source.name + " (PaperOutline)";
            copy.hideFlags = HideFlags.HideAndDontSave;

            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            if (normals == null || normals.Length != vertices.Length)
            {
                copy.RecalculateNormals();
                normals = copy.normals;
            }

            var sums = new Dictionary<Vector3Int, Vector3>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3Int key = Quantize(vertices[i]);
                sums.TryGetValue(key, out Vector3 sum);
                sums[key] = sum + normals[i];
            }

            var smooth = new List<Vector3>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
                smooth.Add(sums[Quantize(vertices[i])].normalized);

            copy.SetUVs(3, smooth);
            result = copy;
        }
        else
        {
            Debug.LogWarning($"[PaperOutline] Mesh '{source.name}' tidak bisa dibaca, jadi pojok outline mungkin renggang. " +
                             "Perbaikan: pilih model (FBX)-nya di Project, centang Read/Write di Inspector, lalu Apply.");
        }

        HullMeshes[source] = result;
        return result;
    }

    private static Vector3Int Quantize(Vector3 v)
    {
        return new Vector3Int(
            Mathf.RoundToInt(v.x * 10000f),
            Mathf.RoundToInt(v.y * 10000f),
            Mathf.RoundToInt(v.z * 10000f));
    }
}
