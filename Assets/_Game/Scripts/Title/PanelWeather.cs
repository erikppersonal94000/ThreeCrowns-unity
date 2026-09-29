using UnityEngine;
using UnityEngine.UI;

// Shared base for the full-panel weather on the title screen (ash, rain, snow).
// Put each one on a stretched child of BackgroundCanvas, BELOW Panels in the hierarchy.
[RequireComponent(typeof(CanvasRenderer))]
public abstract class PanelWeather : MaskableGraphic
{
    [Header("Panel")]
    public TitlePanels panels;
    public int kingdomIndex;
    [Tooltip("Soft fade, in pixels, where the weather meets the next panel.")]
    public float edgeSoftness = 90f;
    [Tooltip("Draws thin white lines where this weather thinks its panel's edges are.")]
    public bool showSpan;

    [Header("Strength")]
    [Range(0f, 1f)] public float focusedLevel = 1f;
    [Range(0f, 1f)] public float idleLevel = 0.6f;
    [Range(0f, 1f)] public float otherFocusedLevel = 0.3f;
    public float changeSpeed = 1.2f;
    public float startDelay = 2f;

    protected float Intensity;
    protected float SpanLeft, SpanRight, Bottom, Top;
    protected float SpanWidth => Mathf.Max(1f, SpanRight - SpanLeft);
    protected float Height => Mathf.Max(1f, Top - Bottom);
    protected float T;

    bool started;
    bool fadeLeft, fadeRight;

    // Atlas cells: soft dot, ash flake, rain streak, flame tongue
    protected static readonly Rect DotUV = new Rect(0f, 0.5f, 0.5f, 0.5f);
    protected static readonly Rect FlakeUV = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
    protected static readonly Rect StreakUV = new Rect(0f, 0f, 0.5f, 0.5f);
    protected static readonly Rect FlameUV = new Rect(0.5f, 0f, 0.5f, 0.5f);
    protected static readonly Rect SolidUV = new Rect(0.25f, 0.75f, 0f, 0f);

    static Texture2D atlas;
    static Texture2D Atlas => atlas != null ? atlas : (atlas = BuildAtlas());
    public override Texture mainTexture => Atlas;

    protected abstract void Init();
    protected abstract void Simulate(float dt);
    protected abstract void Draw(VertexHelper vh);

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false; // never block the panel hover
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        T = 0f;
        Intensity = 0f;
        started = false;
        if (Application.isPlaying && panels == null) panels = FindAnyObjectByType<TitlePanels>();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        float dt = Time.deltaTime;
        T += dt;
        UpdateSpan();

        float target;
        if (T < startDelay) target = 0f;
        else if (panels == null || panels.Focus < 0) target = idleLevel;
        else target = panels.Focus == kingdomIndex ? focusedLevel : otherFocusedLevel;
        Intensity = Mathf.MoveTowards(Intensity, target, changeSpeed * dt);

        if (!started) { started = true; Init(); }
        Simulate(dt);
        SetVerticesDirty();
    }

    void UpdateSpan()
    {
        Rect r = rectTransform.rect;
        Bottom = r.yMin;
        Top = r.yMax;
        int idx = Mathf.Clamp(kingdomIndex, 0, 2);
        fadeLeft = idx != 0;
        fadeRight = idx != 2;

        // Fallback: equal thirds of the screen
        SpanLeft = Mathf.Lerp(r.xMin, r.xMax, idx / 3f);
        SpanRight = Mathf.Lerp(r.xMin, r.xMax, (idx + 1) / 3f);
        if (panels == null) return;

        float c0 = panels.PanelCenterX(0), c1 = panels.PanelCenterX(1), c2 = panels.PanelCenterX(2);
        if (c2 - c0 < 1f) return; // panels haven't laid out yet

        float origin = c0 < 0f ? r.center.x : r.xMin; // centers measured from the middle or from the left edge
        c0 += origin; c1 += origin; c2 += origin;

        // Panels sit side by side, so each far edge is the near edge mirrored across the center
        float b01 = 2f * c0 - r.xMin;
        if (b01 <= c0 || b01 >= c1) b01 = (c0 + c1) * 0.5f;
        float b12 = 2f * c1 - b01;
        if (b12 <= c1 || b12 >= c2) b12 = (c1 + c2) * 0.5f;

        SpanLeft = idx == 0 ? r.xMin : idx == 1 ? b01 : b12;
        SpanRight = idx == 0 ? b01 : idx == 1 ? b12 : r.xMax;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!Application.isPlaying || !started) return;
        if (Intensity > 0.001f) Draw(vh);
        if (showSpan)
        {
            Color32 white = new Color32(255, 255, 255, 160);
            float midY = (Bottom + Top) * 0.5f;
            Quad(vh, new Vector2(SpanLeft + 2f, midY), 3f, Height, 0f, SolidUV, white, white);
            Quad(vh, new Vector2(SpanRight - 2f, midY), 3f, Height, 0f, SolidUV, white, white);
        }
    }

    /* ---------- helpers for the subclasses ---------- */

    protected float X(float u) => SpanLeft + u * SpanWidth;

    // 0 at the panel's edge, 1 once you're edgeSoftness pixels inside
    protected float EdgeAlpha(float x)
    {
        float s = Mathf.Clamp(edgeSoftness, 1f, SpanWidth * 0.5f);
        float a = 1f;
        if (fadeLeft) a *= Smooth(SpanLeft, SpanLeft + s, x);
        if (fadeRight) a *= Smooth(SpanRight, SpanRight - s, x);
        return a;
    }

    // Lets weaker weather thin out smoothly instead of popping
    protected float Presence(float rank) => Mathf.Clamp01((Intensity - rank * 0.85f) * 8f);

    protected static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    protected static float Noise(float seed, float t) => Mathf.PerlinNoise(seed, t) * 2f - 1f;

    protected static Color32 Col(Color c, float alpha) => new Color(c.r, c.g, c.b, Mathf.Clamp01(c.a * alpha));

    // A rotated quad. topShift slides the top edge sideways (used for swaying flames).
    protected static void Quad(VertexHelper vh, Vector2 c, float w, float h, float angle, Rect uv,
                               Color32 bottom, Color32 top, float topShift = 0f)
    {
        float cs = Mathf.Cos(angle), sn = Mathf.Sin(angle);
        Vector2 ax = new Vector2(cs, sn) * (w * 0.5f);
        Vector2 ay = new Vector2(-sn, cs) * (h * 0.5f);
        Vector2 shift = new Vector2(cs, sn) * topShift;
        int i = vh.currentVertCount;
        vh.AddVert(c - ax - ay, bottom, new Vector2(uv.xMin, uv.yMin));
        vh.AddVert(c - ax + ay + shift, top, new Vector2(uv.xMin, uv.yMax));
        vh.AddVert(c + ax + ay + shift, top, new Vector2(uv.xMax, uv.yMax));
        vh.AddVert(c + ax - ay, bottom, new Vector2(uv.xMax, uv.yMin));
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }

    // A color gradient across the whole panel, faded at the panel edges
    protected void Band(VertexHelper vh, float y0, float y1, Color bottom, Color top, float alpha, int columns = 24)
    {
        if (alpha <= 0f) return;
        Vector2 uv = SolidUV.position;
        for (int j = 0; j < columns; j++)
        {
            float x0 = Mathf.Lerp(SpanLeft, SpanRight, j / (float)columns);
            float x1 = Mathf.Lerp(SpanLeft, SpanRight, (j + 1) / (float)columns);
            float e0 = EdgeAlpha(x0) * alpha, e1 = EdgeAlpha(x1) * alpha;
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y0), Col(bottom, e0), uv);
            vh.AddVert(new Vector3(x0, y1), Col(top, e0), uv);
            vh.AddVert(new Vector3(x1, y1), Col(top, e1), uv);
            vh.AddVert(new Vector3(x1, y0), Col(bottom, e1), uv);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }

    /* ---------- the particle texture, built in code ---------- */

    static Texture2D BuildAtlas()
    {
        const int S = 256, C = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            int cx = x / C, cy = y / C;
            float u = ((x % C) + 0.5f) / C * 2f - 1f;
            float v = ((y % C) + 0.5f) / C * 2f - 1f;
            float a;
            if (cy == 1 && cx == 0) a = DotShape(u, v);
            else if (cy == 1) a = FlakeShape(u, v);
            else if (cx == 0) a = StreakShape(u, v);
            else a = FlameShape(u, v);
            px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    static float DotShape(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v) / 0.9f;
        return r >= 1f ? 0f : Mathf.Pow(1f - r, 1.6f);
    }

    static float FlakeShape(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float ang = Mathf.Atan2(v, u);
        float edge = 0.6f + 0.16f * Mathf.Sin(3f * ang + 0.7f) + 0.09f * Mathf.Sin(7f * ang + 2.1f);
        float fill = 0.8f + 0.2f * Mathf.Sin(u * 9f + 1f) * Mathf.Sin(v * 11f);
        return Smooth(edge, edge - 0.1f, r) * fill;
    }

    static float StreakShape(float u, float v)
    {
        float across = Mathf.Clamp01(1f - Mathf.Abs(u) / 0.4f);
        float along = Smooth(0.92f, 0.55f, Mathf.Abs(v));
        float head = Mathf.Lerp(1f, 0.35f, (v + 1f) * 0.5f); // brighter at the leading (bottom) end
        return across * across * along * head;
    }

    static float FlameShape(float u, float v)
    {
        float y = (v + 1f) * 0.5f;
        float w = 0.8f * Mathf.Pow(1f - y, 1.3f) * Mathf.Sqrt(Smooth(0f, 0.25f, y));
        if (w < 0.001f) return 0f;
        float across = Mathf.Clamp01(1f - Mathf.Abs(u) / w);
        return Mathf.Pow(across, 1.2f) * Smooth(0.02f, 0.14f, y) * Smooth(0.95f, 0.7f, y);
    }
}