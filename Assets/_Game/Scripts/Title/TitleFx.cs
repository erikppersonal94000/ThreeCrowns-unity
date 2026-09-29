using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Base for effects that play on one title face's letters.
// Add the effect to the TitleText object and drag in that face's font asset.
public abstract class TitleFx : MonoBehaviour
{
    [Header("Face")]
    [Tooltip("The SDF font asset of the title face this effect plays on.")]
    public TMP_FontAsset faceFont;
    [Tooltip("Points sampled across each letter. Higher = follows the letter shapes more closely.")]
    [Range(6, 32)] public int columns = 16;

    [Header("Debug")]
    [Tooltip("Logs what the effect sees, draws white dots on the letter edges and paints the letter copy white.")]
    public bool debug;

    [Header("Build-up")]
    [Tooltip("Seconds to build to full strength while the face is showing.")]
    public float buildTime = 8f;
    [Tooltip("Seconds to reset after the face is hidden.")]
    public float resetTime = 1f;

    protected TMP_Text Face;
    protected FaceOverlay Overlay;
    protected FxLayer Layer;
    protected FxLayer Behind;
    protected readonly List<TitleLetter> Letters = new List<TitleLetter>();
    protected float Vis;       // how visible the face is (0..1), used for drawing
    protected float Progress;  // builds 0 -> 1 while the face is showing
    protected float T;
    protected float TextXMin, TextXMax, TextYMin, TextYMax;

    float signature = float.NaN;
    float searchTimer;

    protected virtual bool UseOverlay => true;
    protected virtual bool UseParticles => true;
    protected virtual bool UseBehind => false;
    public virtual void DrawBehind(VertexHelper vh) { }

    public virtual Color32 OverlayColor(int charIndex, int corner, Vector3 pos) => new Color32(0, 0, 0, 0);
    public virtual void DrawParticles(VertexHelper vh) { }
    protected virtual void Simulate(float dt) { }

    void Update()
    {
        if (Face == null)
        {
            if (Behind != null) { Destroy(Behind.gameObject); Behind = null; }
            searchTimer -= Time.unscaledDeltaTime;
            if (searchTimer > 0f) return;
            searchTimer = 0.5f;
            if (!FindFace()) return;
        }

        float dt = Time.deltaTime;
        T += dt;

        float vertexAlpha = FaceVertexAlpha();
        Vis = Face.isActiveAndEnabled ? vertexAlpha * Face.canvasRenderer.GetAlpha() : 0f;
        float shown = Vis * Face.canvasRenderer.GetInheritedAlpha();
        bool showing = shown > 0.5f;
        Progress = Mathf.MoveTowards(Progress, showing ? 1f : 0f,
                                     dt / Mathf.Max(0.01f, showing ? buildTime : resetTime));

        if (TitleGlyphs.Refresh(Face, columns, Letters, ref signature)) UpdateBounds();

        Simulate(dt);
        if (debug) DebugLog();
        if (Overlay != null) Overlay.SetVerticesDirty();
        if (Layer != null) Layer.SetVerticesDirty();
        if (Behind != null) { SyncBehind(); Behind.SetVerticesDirty(); }
    }

    float FaceVertexAlpha()
    {
        var info = Face.textInfo;
        if (info == null || info.meshInfo == null || info.meshInfo.Length == 0) return 0f;
        Color32[] cols = info.meshInfo[0].colors32;
        if (cols == null) return 0f;
        int best = 0;
        for (int i = 0; i < info.characterCount; i++)
        {
            var ci = info.characterInfo[i];
            if (!ci.isVisible || ci.materialReferenceIndex != 0 || ci.vertexIndex >= cols.Length) continue;
            best = Mathf.Max(best, cols[ci.vertexIndex].a);
        }
        return best / 255f;
    }

    bool FindFace()
    {
        if (faceFont == null) return false;
        Face = Search(GetComponentsInChildren<TMP_Text>(true));
        if (Face == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) Face = Search(canvas.GetComponentsInChildren<TMP_Text>(true));
        }
        if (Face == null) return false;

        signature = float.NaN;
        if (UseOverlay)
        {
            Overlay = MakeChild(GetType().Name + " Overlay").gameObject.AddComponent<FaceOverlay>();
            Overlay.Setup(Face, this);
        }
        if (UseParticles)
        {
            Layer = MakeChild(GetType().Name + " Particles").gameObject.AddComponent<FxLayer>();
            Layer.owner = this;
        }
        if (UseBehind) MakeBehind();
        SetChildren(enabled);
        return true;
    }

    TMP_Text Search(TMP_Text[] all)
    {
        foreach (var t in all) if (t.font == faceFont) return t;
        return null;
    }

    // Child of the face, same size and pivot, so it shares the face's local space
    RectTransform MakeChild(string childName)
    {
        var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = Face.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(Face.transform, false);
        // Anchored on the face's pivot point, so it shares the face's coordinates,
        // but with a large box so Unity never culls it as "empty"
        rt.anchorMin = Face.rectTransform.pivot;
        rt.anchorMax = Face.rectTransform.pivot;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(4000f, 3000f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.localPosition = Vector3.zero;
        return rt;
    }

    // A layer placed just before the face in the hierarchy, so it draws behind the letters.
    // It's a sibling (not a child), so it copies the face's position every frame.
    void MakeBehind()
    {
        var go = new GameObject(GetType().Name + " Behind", typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = Face.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(Face.transform.parent, false);
        rt.SetSiblingIndex(Face.transform.GetSiblingIndex());
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(4000f, 3000f);
        Behind = go.AddComponent<FxLayer>();
        Behind.owner = this;
        Behind.behind = true;
        SyncBehind();
    }

    void SyncBehind()
    {
        Behind.transform.SetPositionAndRotation(Face.transform.position, Face.transform.rotation);
        Behind.transform.localScale = Face.transform.localScale;
    }

    void UpdateBounds()
    {
        if (Letters.Count == 0) { TextXMin = TextXMax = TextYMin = TextYMax = 0f; return; }
        TextXMin = TextYMin = float.MaxValue;
        TextXMax = TextYMax = float.MinValue;
        foreach (var L in Letters)
        {
            TextXMin = Mathf.Min(TextXMin, L.xMin);
            TextXMax = Mathf.Max(TextXMax, L.xMax);
            TextYMin = Mathf.Min(TextYMin, L.yMin);
            TextYMax = Mathf.Max(TextYMax, L.yMax);
        }
    }

    void OnEnable() => SetChildren(true);
    void OnDisable() => SetChildren(false);

    void OnDestroy()
    {
        if (Overlay != null) Destroy(Overlay.gameObject);
        if (Layer != null) Destroy(Layer.gameObject);
        if (Behind != null) Destroy(Behind.gameObject);
    }

    void SetChildren(bool on)
    {
        if (Overlay != null) Overlay.gameObject.SetActive(on);
        if (Layer != null) Layer.gameObject.SetActive(on);
        if (Behind != null) Behind.gameObject.SetActive(on);
    }

    /* ---------- helpers for the effects ---------- */
        float debugTimer;

    void DebugLog()
    {
        debugTimer -= Time.unscaledDeltaTime;
        if (debugTimer > 0f) return;
        debugTimer = 1f;
        int tops = 0, inside = 0;
        foreach (var L in Letters)
        {
            foreach (var p in L.top) if (!float.IsNaN(p.y)) tops++;
            inside += L.inside.Length;
        }
        Debug.Log($"{GetType().Name} on '{Face.name}': vis {Vis:F2}, renderer alpha {Face.canvasRenderer.GetAlpha():F2}, " +
                  $"group alpha {Face.canvasRenderer.GetInheritedAlpha():F2}, progress {Progress:F2}, letters {Letters.Count}, " +
                  $"top points {tops}, inside points {inside}, text x {TextXMin:F0} to {TextXMax:F0}");
        if (Layer != null)
            Debug.Log($"   layer: active {Layer.isActiveAndEnabled}, canvas {(Layer.canvas != null ? Layer.canvas.name : "NONE")}, " +
                      $"depth {Layer.depth} (face depth {Face.depth}), cull {Layer.canvasRenderer.cull}, " +
                      $"alpha {Layer.canvasRenderer.GetAlpha():F2}, material {(Layer.materialForRendering != null ? Layer.materialForRendering.name : "NONE")}, " +
                      $"world pos {Layer.transform.position} (face {Face.transform.position})");
    }

    public void DrawDebug(VertexHelper vh)
    {
        Color32 w = new Color32(255, 255, 255, 255);
        foreach (var L in Letters)
        {
            foreach (var p in L.top) if (!float.IsNaN(p.y)) FxKit.Quad(vh, p, 4f, 4f, 0f, FxKit.DotUV, w, w);
            foreach (var p in L.bottom) if (!float.IsNaN(p.y)) FxKit.Quad(vh, p, 4f, 4f, 0f, FxKit.DotUV, w, w);
        }
        FxKit.Quad(vh, Vector2.zero, 40f, 40f, 0f, FxKit.DotUV, w, w); // big test dot at the face's center
    }

    /* ---------- helpers for the effects ---------- */

    protected bool RandomTop(out Vector2 p) => RandomPoint(true, out p);
    protected bool RandomBottom(out Vector2 p) => RandomPoint(false, out p);

    bool RandomPoint(bool top, out Vector2 p)
    {
        p = Vector2.zero;
        if (Letters.Count == 0) return false;
        for (int tries = 0; tries < 10; tries++)
        {
            var L = Letters[Random.Range(0, Letters.Count)];
            Vector2[] arr = top ? L.top : L.bottom;
            Vector2 q = arr[Random.Range(0, arr.Length)];
            if (!float.IsNaN(q.y)) { p = q; return true; }
        }
        return false;
    }

    // A column of a letter with both a top and a bottom (for water running down)
    protected bool RandomColumn(out Vector2 top, out Vector2 bottom)
    {
        top = bottom = Vector2.zero;
        if (Letters.Count == 0) return false;
        for (int tries = 0; tries < 10; tries++)
        {
            var L = Letters[Random.Range(0, Letters.Count)];
            int c = Random.Range(0, L.top.Length);
            Vector2 a = L.top[c], b = L.bottom[c];
            if (!float.IsNaN(a.y) && !float.IsNaN(b.y) && a.y - b.y > 6f) { top = a; bottom = b; return true; }
        }
        return false;
    }

    protected static bool IsTopCorner(int corner) => corner == 1 || corner == 2;
}