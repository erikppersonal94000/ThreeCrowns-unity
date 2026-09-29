using UnityEngine;
using UnityEngine.UI;

// Frostvale: frost creeps in from both ends of the title, a frost crust forms on the letter edges,
// snow piles up on top, and icicles grow one by one from the bottoms of the letters.
public class TitleFrost : TitleFx
{
    [Header("Size")]
    [Tooltip("Scales snow, crystals, crust, icicles and sparkles. Raise for bigger title text.")]
    public float sizeScale = 2.2f;

    [Header("Frost creeping in from the sides")]
    public Color frostTint = new Color(0.902f, 0.957f, 1f, 0.85f);     // #E6F4FF
    public Color crystalColor = new Color(1f, 1f, 1f, 0.85f);          // #FFFFFF
    public Vector2 crystalSize = new Vector2(3f, 7f);
    [Tooltip("Frost crust along the top and bottom edges of the letters.")]
    public bool edgeCrust = true;

    [Header("Snow piling on top")]
    public float maxSnowDepth = 7f;
    public Color snowColor = new Color(0.957f, 0.973f, 1f, 0.95f);     // #F4F8FF
    public float flakesPerSecond = 8f;

    [Header("Icicles")]
    [Tooltip("How many icicles hang from each letter (0 = none, 1 = lots).")]
    [Range(0f, 1f)] public float icicleDensity = 0.45f;
    [Tooltip("Shortest and longest icicle, before Size Scale.")]
    public Vector2 icicleLength = new Vector2(8f, 26f);
    [Tooltip("Icicle width as a fraction of its length.")]
    [Range(0.1f, 0.5f)] public float icicleWidth = 0.22f;
    public Color iceColor = new Color(0.812f, 0.902f, 1f, 0.95f);      // #CFE6FF
    public Color iceTip = new Color(1f, 1f, 1f, 0.3f);                 // #FFFFFF, faded

    [Header("Sparkle")]
    public float sparklesPerSecond = 4f;

    struct Fall { public Vector2 from, to; public float age, life, seed; }
    struct Spark { public Vector2 pos; public float age, life; }

    readonly Fall[] falls = new Fall[32];
    readonly Spark[] sparks = new Spark[16];
    int nFall, nSpark;
    float fallTimer, sparkTimer;

    static readonly Vector2 SolidUV = new Vector2(0.25f, 0.75f); // center of the soft dot = fully opaque

    void Reset() { buildTime = 14f; }

    // 0 = no frost, 1 = fully frozen. Starts at both ends of the title and moves inward.
    float FrostAt(float x)
    {
        float half = (TextXMax - TextXMin) * 0.5f;
        if (half <= 0f) return 0f;
        float edge = Mathf.Min(x - TextXMin, TextXMax - x) / half;
        return Mathf.Clamp01((Progress * 1.3f - edge) * 3f);
    }

    protected override void Simulate(float dt)
    {
        float s = sizeScale;

        // Flakes drifting down and landing on the letters
        fallTimer += flakesPerSecond * Vis * dt;
        while (fallTimer >= 1f)
        {
            fallTimer -= 1f;
            if (!RandomTop(out Vector2 p)) { fallTimer = 0f; break; }
            falls[nFall] = new Fall
            {
                from = p + new Vector2(Random.Range(-40f, 40f) * s, Random.Range(60f, 110f) * s),
                to = p, age = 0f, life = Random.Range(1.5f, 2.5f), seed = Random.value * 100f
            };
            nFall = (nFall + 1) % falls.Length;
        }
        for (int i = 0; i < falls.Length; i++)
            if (falls[i].age < falls[i].life) falls[i].age += dt;

        // Glints on the frozen parts
        sparkTimer += sparklesPerSecond * Vis * FxKit.Smooth(0.3f, 0.8f, Progress) * dt;
        while (sparkTimer >= 1f)
        {
            sparkTimer -= 1f;
            if (Letters.Count == 0) { sparkTimer = 0f; break; }
            var L = Letters[Random.Range(0, Letters.Count)];
            if (L.inside.Length == 0) continue;
            Vector2 p = L.inside[Random.Range(0, L.inside.Length)];
            if (FrostAt(p.x) < 0.5f) continue;
            sparks[nSpark] = new Spark { pos = p, age = 0f, life = 0.45f };
            nSpark = (nSpark + 1) % sparks.Length;
        }
        for (int i = 0; i < sparks.Length; i++)
            if (sparks[i].age < sparks[i].life) sparks[i].age += dt;
    }

    public override Color32 OverlayColor(int charIndex, int corner, Vector3 pos)
    {
        return FxKit.Col(frostTint, FrostAt(pos.x));
    }

    public override void DrawParticles(VertexHelper vh)
    {
        if (Vis <= 0.001f) return;
        float s = sizeScale;

        DrawCrystals(vh, s);
        if (edgeCrust) DrawCrust(vh, s);
        DrawSnow(vh, s);
        DrawIcicles(vh, s);

        // Flakes drifting down onto the letters
        foreach (var f in falls)
        {
            if (f.age >= f.life) continue;
            float t = f.age / f.life;
            Vector2 pos = Vector2.Lerp(f.from, f.to, t) + new Vector2(Mathf.Sin(T * 2f + f.seed) * 6f * s * (1f - t), 0f);
            Color32 c = FxKit.Col(snowColor, FxKit.Smooth(0f, 0.15f, t) * Vis);
            FxKit.Quad(vh, pos, 3f * s, 3f * s, 0f, FxKit.DotUV, c, c);
        }

        // Glints
        foreach (var sp in sparks)
        {
            if (sp.age >= sp.life) continue;
            float k = Mathf.Sin(Mathf.PI * sp.age / sp.life);
            Color32 c = FxKit.Col(Color.white, k * Vis);
            FxKit.Quad(vh, sp.pos, 1.6f * s, 14f * s * k, 0f, FxKit.DotUV, c, c);
            FxKit.Quad(vh, sp.pos, 1.6f * s, 14f * s * k, Mathf.PI * 0.5f, FxKit.DotUV, c, c);
            FxKit.Quad(vh, sp.pos, 5f * s * k, 5f * s * k, 0f, FxKit.DotUV, c, c);
        }
    }

    // Frost crystals inside the letters, appearing as the freeze reaches them
    void DrawCrystals(VertexHelper vh, float s)
    {
        foreach (var L in Letters)
        {
            for (int k = 0; k < L.inside.Length; k++)
            {
                Vector2 p = L.inside[k];
                float h = FxKit.Hash(L.charIndex * 57.1f + k * 13.7f);
                float a = Mathf.Clamp01((FrostAt(p.x) - h * 0.9f) * 4f);
                if (a <= 0f) continue;
                float size = Mathf.Lerp(crystalSize.x, crystalSize.y, FxKit.Hash(h * 91.3f)) * s;
                Color32 cc = FxKit.Col(crystalColor, a * Vis);
                FxKit.Quad(vh, p, size, size, h * 6.283f, FxKit.FlakeUV, cc, cc);
            }
        }
    }

    // Rough frost crust along the letter edges
    void DrawCrust(VertexHelper vh, float s)
    {
        foreach (var L in Letters)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                Vector2[] edge = pass == 0 ? L.top : L.bottom;
                for (int c = 0; c < edge.Length; c++)
                {
                    Vector2 p = edge[c];
                    if (float.IsNaN(p.y)) continue;
                    float h = FxKit.Hash(L.charIndex * 23.9f + c * 7.3f + pass * 101f);
                    float a = Mathf.Clamp01((FrostAt(p.x) - h * 0.6f) * 3f);
                    if (a <= 0f) continue;
                    float size = (4f + 4f * FxKit.Hash(h * 37f)) * s * (0.6f + 0.4f * a);
                    Color32 cc = FxKit.Col(crystalColor, a * 0.9f * Vis);
                    FxKit.Quad(vh, p, size, size * 0.8f, h * 6.283f, FxKit.FlakeUV, cc, cc);
                }
            }
        }
    }

    // Snow piled on the letter tops (less on steep edges, where it would slide off)
    void DrawSnow(VertexHelper vh, float s)
    {
        float depth = maxSnowDepth * s * FxKit.Smooth(0.1f, 1f, Progress);
        if (depth < 0.3f) return;
        Color32 sc = FxKit.Col(snowColor, Vis);
        foreach (var L in Letters)
        {
            Vector2[] top = L.top;
            for (int c = 0; c < top.Length; c++)
            {
                Vector2 p = top[c];
                if (float.IsNaN(p.y)) continue;
                Vector2 a = c > 0 ? top[c - 1] : p;
                Vector2 b = c < top.Length - 1 ? top[c + 1] : p;
                if (float.IsNaN(a.y)) a = p;
                if (float.IsNaN(b.y)) b = p;
                float dx = Mathf.Abs(b.x - a.x);
                float slope = dx > 0.01f ? Mathf.Abs(b.y - a.y) / dx : 0f;
                float d = depth * FxKit.Smooth(1.5f, 0.4f, slope) * (0.6f + 0.4f * FxKit.Hash(L.charIndex * 31f + c));
                if (d < 0.5f) continue;
                float size = d * 2f + 3f * s;
                Vector2 center = new Vector2(p.x, p.y + d * 0.45f);
                FxKit.Quad(vh, center, size, size * 0.85f, 0f, FxKit.DotUV, sc, sc);
                FxKit.Quad(vh, center, size * 0.6f, size * 0.5f, 0f, FxKit.DotUV, sc, sc);
            }
        }
    }

    // Sharp icicles hanging from the letter bottoms. Each starts at its own time and grows.
    void DrawIcicles(VertexHelper vh, float s)
    {
        if (icicleDensity <= 0f) return;
        Color32 top = FxKit.Col(iceColor, Vis);
        Color32 tip = FxKit.Col(iceTip, Vis);
        Color32 shine = FxKit.Col(Color.white, 0.55f * Vis);

        foreach (var L in Letters)
        {
            Vector2[] bot = L.bottom;
            int lastCol = -10;
            for (int c = 0; c < bot.Length; c++)
            {
                Vector2 p = bot[c];
                if (float.IsNaN(p.y)) continue;
                float h = FxKit.Hash(L.charIndex * 17.3f + c * 3.1f);
                if (h > icicleDensity * 0.5f) continue;
                if (c - lastCol < 2) continue;

                // Only hang from flat or low spots, not from the side of a slope
                bool slopeL = c > 0 && !float.IsNaN(bot[c - 1].y) && bot[c - 1].y < p.y - 3f * s;
                bool slopeR = c < bot.Length - 1 && !float.IsNaN(bot[c + 1].y) && bot[c + 1].y < p.y - 3f * s;
                if (slopeL || slopeR) continue;
                lastCol = c;

                float start = 0.2f + 0.45f * FxKit.Hash(h * 53.7f);
                float grow = FxKit.Smooth(start, start + 0.35f, Progress);
                if (grow <= 0f) continue;

                float fullLen = Mathf.Lerp(icicleLength.x, icicleLength.y, FxKit.Hash(h * 91f + 7f)) * s;
                float len = fullLen * grow;
                float w = fullLen * icicleWidth * (0.5f + 0.5f * grow);
                float lean = (FxKit.Hash(h * 13f) - 0.5f) * w * 0.4f;

                Vector2 a = new Vector2(p.x - w * 0.5f, p.y + 1f);
                Vector2 b = new Vector2(p.x + w * 0.5f, p.y + 1f);
                Vector2 t = new Vector2(p.x + lean, p.y - len);
                Tri(vh, a, b, t, top, top, tip);

                // glassy highlight down one side
                FxKit.Quad(vh, new Vector2(p.x - w * 0.18f + lean * 0.3f, p.y - len * 0.35f),
                           Mathf.Max(1f, w * 0.18f), len * 0.6f, 0f, FxKit.DotUV, shine, shine);
            }
        }
    }

    static void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 ca, Color32 cb, Color32 cc)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, ca, SolidUV);
        vh.AddVert(b, cb, SolidUV);
        vh.AddVert(c, cc, SolidUV);
        vh.AddTriangle(i, i + 1, i + 2);
    }
}