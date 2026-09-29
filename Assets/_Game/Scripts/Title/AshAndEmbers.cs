using UnityEngine;
using UnityEngine.UI;

// Duskmoor: ash drifting up on the heat, glowing embers, low flames along the bottom.
public class AshAndEmbers : PanelWeather
{
    [Header("Ash")]
    public int ashCount = 140;
    public Vector2 ashSize = new Vector2(3f, 9f);
    public Vector2 ashRise = new Vector2(10f, 40f);
    public float ashWander = 35f;
    public float wind = 15f;
    public Color ashDark = new Color(0.18f, 0.157f, 0.157f, 0.85f);   // #2E2828
    public Color ashLight = new Color(0.42f, 0.384f, 0.36f, 0.7f);    // #6B625C
    [Range(0f, 1f)] public float burningChance = 0.25f;
    public Color burnColor = new Color(1f, 0.478f, 0.133f, 1f);       // #FF7A22

    [Header("Embers")]
    public int emberCount = 45;
    public Vector2 emberSize = new Vector2(2.5f, 5f);
    public Vector2 emberRise = new Vector2(50f, 130f);
    public Vector2 emberLife = new Vector2(3f, 7f);
    public Color emberCore = new Color(1f, 0.702f, 0.278f, 1f);       // #FFB347
    public Color emberGlow = new Color(1f, 0.353f, 0.078f, 0.3f);     // #FF5A14
    public float glowScale = 5f;

    [Header("Flames (bottom edge)")]
    public int flameCount = 12;
    public Vector2 flameHeight = new Vector2(60f, 150f);
    public Vector2 flameWidth = new Vector2(50f, 100f);
    public Color flameBase = new Color(1f, 0.353f, 0.102f, 0.55f);    // #FF5A1A
    public Color flameTip = new Color(1f, 0.769f, 0.369f, 0.3f);      // #FFC45E
    public float flickerSpeed = 3f;

    [Header("Ground glow")]
    [Range(0f, 1f)] public float glowHeight = 0.3f;
    public Color groundGlow = new Color(0.722f, 0.2f, 0.055f, 0.28f); // #B8330E

    struct Ash { public float u, y, size, rise, angle, spin, seed, rank, shade; public bool burning; }
    struct Ember { public float u, y, size, rise, age, life, seed, rank; }
    struct Flame { public float u, h, w, seed, rank; }

    Ash[] ash;
    Ember[] embers;
    Flame[] flames;

    protected override void Init()
    {
        ash = new Ash[ashCount];
        for (int i = 0; i < ash.Length; i++)
        {
            ash[i] = NewAsh();
            ash[i].y = Random.Range(0f, Height);
        }

        embers = new Ember[emberCount];
        for (int i = 0; i < embers.Length; i++)
        {
            embers[i] = NewEmber();
            embers[i].age = Random.Range(0f, embers[i].life);
            embers[i].y += embers[i].rise * embers[i].age;
        }

        flames = new Flame[flameCount];
        for (int i = 0; i < flames.Length; i++)
        {
            flames[i] = new Flame
            {
                u = (i + Random.Range(0.2f, 0.8f)) / flameCount,
                h = Random.Range(flameHeight.x, flameHeight.y),
                w = Random.Range(flameWidth.x, flameWidth.y),
                seed = Random.value * 100f,
                rank = Random.value
            };
        }
    }

    Ash NewAsh() => new Ash
    {
        u = Random.value,
        y = -10f,
        size = Random.Range(ashSize.x, ashSize.y),
        rise = Random.Range(ashRise.x, ashRise.y),
        angle = Random.value * 6.283f,
        spin = Random.Range(-2.5f, 2.5f),
        seed = Random.value * 100f,
        rank = Random.value,
        shade = Random.value,
        burning = Random.value < burningChance
    };

    Ember NewEmber() => new Ember
    {
        u = Random.value,
        y = Random.Range(0f, Height * 0.2f),
        size = Random.Range(emberSize.x, emberSize.y),
        rise = Random.Range(emberRise.x, emberRise.y),
        age = 0f,
        life = Random.Range(emberLife.x, emberLife.y),
        seed = Random.value * 100f,
        rank = Random.value
    };

    protected override void Simulate(float dt)
    {
        float W = SpanWidth, H = Height;

        for (int i = 0; i < ash.Length; i++)
        {
            ref Ash a = ref ash[i];
            a.y += (a.rise + 12f * Noise(a.seed, T * 0.5f)) * dt;
            a.u = Mathf.Repeat(a.u + (wind + ashWander * Noise(a.seed + 31f, T * 0.3f)) * dt / W, 1f);
            a.angle += a.spin * dt;
            if (a.y > H + 20f) a = NewAsh();
        }

        for (int i = 0; i < embers.Length; i++)
        {
            ref Ember e = ref embers[i];
            e.age += dt;
            e.y += e.rise * dt;
            e.u = Mathf.Repeat(e.u + (wind * 1.5f + 50f * Noise(e.seed, T * 0.8f)) * dt / W, 1f);
            if (e.age > e.life || e.y > H + 10f) e = NewEmber();
        }
    }

    protected override void Draw(VertexHelper vh)
    {
        float H = Height;

        // Warm glow rising from the bottom
        float pulse = 0.85f + 0.15f * Noise(5.5f, T * 2f);
        Color glowTop = groundGlow; glowTop.a = 0f;
        Band(vh, Bottom, Bottom + H * glowHeight, groundGlow, glowTop, Intensity * pulse);

        // Flames
        foreach (var f in flames)
        {
            float p = Presence(f.rank);
            if (p <= 0f) continue;
            float x = X(f.u);
            float edge = EdgeAlpha(x) * p;
            if (edge <= 0f) continue;
            float flick = 0.7f + 0.35f * Noise(f.seed, T * flickerSpeed);
            float h = f.h * flick;
            float sway = Noise(f.seed + 7f, T * 1.3f) * 0.3f * h;
            Quad(vh, new Vector2(x, Bottom + h * 0.45f), f.w, h, 0f, FlameUV,
                 Col(flameBase, edge * flick), Col(flameTip, edge), sway);
        }

        // Ash
        foreach (var a in ash)
        {
            float x = X(a.u);
            float edge = EdgeAlpha(x) * Presence(a.rank);
            if (edge <= 0f) continue;
            Color c = Color.Lerp(ashDark, ashLight, a.shade);
            if (a.burning)
            {
                float heat = Smooth(H * 0.6f, 0f, a.y) * (0.6f + 0.4f * Noise(a.seed, T * 5f));
                c = Color.Lerp(c, burnColor, heat);
            }
            float fade = Smooth(H + 20f, H * 0.75f, a.y); // thin out near the top
            Color32 col = Col(c, edge * fade);
            Quad(vh, new Vector2(x, Bottom + a.y), a.size, a.size * 0.8f, a.angle, FlakeUV, col, col);
        }

        // Embers (glow, then bright core)
        foreach (var e in embers)
        {
            float x = X(e.u);
            float edge = EdgeAlpha(x) * Presence(e.rank);
            if (edge <= 0f) continue;
            float life = e.age / e.life;
            float a = Smooth(0f, 0.1f, life) * Smooth(1f, 0.6f, life) * (0.65f + 0.35f * Noise(e.seed, T * 7f)) * edge;
            Vector2 pos = new Vector2(x, Bottom + e.y);
            Color32 g = Col(emberGlow, a), c = Col(emberCore, a);
            Quad(vh, pos, e.size * glowScale, e.size * glowScale, 0f, DotUV, g, g);
            Quad(vh, pos, e.size, e.size, 0f, DotUV, c, c);
        }
    }
}