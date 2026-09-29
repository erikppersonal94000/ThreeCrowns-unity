using UnityEngine;
using UnityEngine.UI;

// Frost that creeps in from the banner's edges while its kingdom is hovered, and thaws when you leave.
// Put this on a child of the banner, and give the banner a Mask component so the frost stays on the cloth.
[RequireComponent(typeof(CanvasRenderer))]
public class FrostCreep : MaskableGraphic
{
    [SerializeField] TitlePanels panels;
    [SerializeField] int kingdomIndex = 2;
    [SerializeField] WavingBanner banner; // optional: frost moves with the cloth

    [Header("Timing")]
    [SerializeField] float freezeTime = 7f; // seconds of hovering to freeze completely
    [SerializeField] float thawTime = 2f;   // seconds to thaw after you leave

    [Header("Look")]
    [SerializeField, Range(8, 96)] int columns = 40;
    [SerializeField, Range(8, 128)] int rows = 60;
    [SerializeField] Color frostColor = new Color(0.86f, 0.94f, 1f, 0.55f);
    [SerializeField] float edgeSoftness = 0.12f; // how soft the edge of the spreading frost is
    [SerializeField] float crystalScale = 2.5f;  // how many times the crystal pattern repeats across
    [SerializeField] int seed = 3;

    public float Progress { get; private set; } // 0 = thawed, 1 = fully frozen

    Texture2D tex;
    public override Texture mainTexture => tex != null ? tex : s_WhiteTexture;

    protected override void Awake()
    {
        base.Awake();
        if (Application.isPlaying) tex = MakeFrostTexture(256, seed);
        if (panels == null) panels = FindFirstObjectByType<TitlePanels>();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        bool on = panels != null && panels.Focus == kingdomIndex;
        Progress = Mathf.Clamp01(Progress + Time.deltaTime * (on ? 1f / freezeTime : -1f / thawTime));
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0f || Progress <= 0f) return;

        float aspect = r.height / r.width;
        for (int y = 0; y <= rows; y++)
        {
            float v = y / (float)rows;
            for (int x = 0; x <= columns; x++)
            {
                float u = x / (float)columns;

                // Distance from the nearest edge (0 at the edge, 1 in the middle)
                float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 2f;
                // An uneven front: noise makes the frost advance in fingers, not a straight line
                float n = Mathf.PerlinNoise(u * 4f + seed, v * 4f * aspect + seed * 2f);
                float frost = Mathf.Clamp01((Progress * 1.25f - d - (n - 0.5f) * 0.35f) / edgeSoftness);

                Color c = frostColor;
                c.a *= frost;

                var pos = new Vector2(r.xMin + u * r.width, r.yMin + v * r.height);
                if (banner != null) pos += banner.Offset(u, v);
                vh.AddVert(pos, c, new Vector2(u * crystalScale, v * crystalScale * aspect));
            }
        }

        int stride = columns + 1;
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            int i = y * stride + x;
            vh.AddTriangle(i, i + stride, i + stride + 1);
            vh.AddTriangle(i, i + stride + 1, i + 1);
        }
    }

    // A repeating frost pattern: thin crystal veins plus fine glittering specks
    static Texture2D MakeFrostTexture(int size, int seed)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
        };
        var px = new Color32[size * size];
        float o = seed * 17.3f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)size, v = y / (float)size;

            // Crystal veins at two scales: bright thin lines where the noise crosses its middle
            float veins = Mathf.Pow(Ridge(Tileable(u, v, 6f, o)), 10f) * 0.9f
                        + Mathf.Pow(Ridge(Tileable(u, v, 14f, o + 50f)), 14f) * 0.6f;
            // Fine glitter
            float speck = Mathf.Pow(Tileable(u, v, 60f, o + 90f), 6f) * 1.4f;

            float a = Mathf.Clamp01(0.15f + veins + speck);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }

        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    static float Ridge(float n) => 1f - Mathf.Abs(n * 2f - 1f);

    // Perlin noise blended so the texture repeats without visible seams
    static float Tileable(float u, float v, float scale, float o)
    {
        float s = scale;
        float a = Mathf.PerlinNoise(u * s + o, v * s + o);
        float b = Mathf.PerlinNoise((u - 1f) * s + o, v * s + o);
        float c = Mathf.PerlinNoise(u * s + o, (v - 1f) * s + o);
        float d = Mathf.PerlinNoise((u - 1f) * s + o, (v - 1f) * s + o);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }
}