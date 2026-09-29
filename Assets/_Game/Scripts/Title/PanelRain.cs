using UnityEngine;
using UnityEngine.UI;

// Stonehelm: three layers of rain, splashes on the ground, low mist,
// and a panel-wide flash synced to the banner's lightning.
public class PanelRain : PanelWeather
{
    [System.Serializable]
    public class Layer
    {
        public int count;
        public float width;
        public Vector2 length;
        public Vector2 speed;
        [Range(0f, 1f)] public float alpha;
        public Layer(int c, float w, Vector2 l, Vector2 s, float a) { count = c; width = w; length = l; speed = s; alpha = a; }
    }

    [Header("Rain")]
    public Layer far = new Layer(170, 1.5f, new Vector2(28f, 45f), new Vector2(1100f, 1400f), 0.16f);
    public Layer mid = new Layer(110, 2.2f, new Vector2(55f, 85f), new Vector2(1600f, 2000f), 0.24f);
    public Layer near = new Layer(40, 3.2f, new Vector2(110f, 170f), new Vector2(2300f, 2800f), 0.32f);
    [Tooltip("Degrees from straight down. Positive leans the rain to the right.")]
    public float slant = 10f;
    public Color rainColor = new Color(0.722f, 0.784f, 0.847f, 1f);     // #B8C8D8

    [Header("Ground splashes")]
    public float splashesPerSecond = 35f;
    [Range(0f, 1f)] public float groundHeight = 0.1f;
    public Color splashColor = new Color(0.8f, 0.863f, 0.925f, 0.55f);  // #CCDCEC

    [Header("Mist")]
    public int mistCount = 6;
    public Color mistColor = new Color(0.549f, 0.608f, 0.667f, 0.1f);   // #8C9BAA

    [Header("Lightning (synced to the banner)")]
    [Tooltip("Drag StonehelmBanner here. Its brightness flashes come from RainOnBanner.")]
    public Graphic lightningSource;
    public float baseBrightness = 0.85f;
    public Color flashColor = new Color(0.863f, 0.902f, 1f, 0.22f);     // #DCE6FF

    struct Drop { public float u, y, len, speed, rank; }
    struct Droplet { public float x, y, vx, vy, age, life, size; }
    struct Ripple { public float x, y, age, life; }
    struct Mist { public float u, y, wFrac, h, speed, seed; }

    Layer[] layers;
    Drop[][] drops;
    readonly Droplet[] droplets = new Droplet[300];
    readonly Ripple[] ripples = new Ripple[80];
    int nextDroplet, nextRipple;
    Mist[] mists;
    float splashTimer, flash;
    bool armed;

    protected override void Init()
    {
        layers = new[] { far, mid, near };
        drops = new Drop[layers.Length][];
        for (int l = 0; l < layers.Length; l++)
        {
            drops[l] = new Drop[layers[l].count];
            for (int i = 0; i < drops[l].Length; i++)
            {
                drops[l][i] = NewDrop(layers[l]);
                drops[l][i].y = Random.Range(0f, Height);
            }
        }

        mists = new Mist[mistCount];
        for (int i = 0; i < mists.Length; i++)
        {
            mists[i] = new Mist
            {
                u = Random.value,
                y = Random.Range(0f, Height * 0.15f),
                wFrac = Random.Range(0.35f, 0.6f),
                h = Random.Range(80f, 160f),
                speed = Random.Range(8f, 20f),
                seed = Random.value * 100f
            };
        }
    }

    Drop NewDrop(Layer layer) => new Drop
    {
        u = Random.value,
        y = Height + Random.Range(0f, Height * 0.2f),
        len = Random.Range(layer.length.x, layer.length.y),
        speed = Random.Range(layer.speed.x, layer.speed.y),
        rank = Random.value
    };

    protected override void Simulate(float dt)
    {
        float W = SpanWidth;
        float lean = Mathf.Tan(slant * Mathf.Deg2Rad);

        for (int l = 0; l < layers.Length; l++)
        {
            for (int i = 0; i < drops[l].Length; i++)
            {
                ref Drop d = ref drops[l][i];
                d.y -= d.speed * dt;
                d.u = Mathf.Repeat(d.u + d.speed * lean * dt / W, 1f);
                if (d.y < -d.len) d = NewDrop(layers[l]);
            }
        }

        // Splashes
        splashTimer += splashesPerSecond * Intensity * dt;
        while (splashTimer >= 1f) { splashTimer -= 1f; Splash(); }

        for (int i = 0; i < droplets.Length; i++)
        {
            ref Droplet p = ref droplets[i];
            if (p.age >= p.life) continue;
            p.age += dt;
            p.vy -= 900f * dt;
            p.x += p.vx * dt;
            p.y += p.vy * dt;
        }
        for (int i = 0; i < ripples.Length; i++)
            if (ripples[i].age < ripples[i].life) ripples[i].age += dt;

        for (int i = 0; i < mists.Length; i++)
            mists[i].u = Mathf.Repeat(mists[i].u + mists[i].speed * Mathf.Sign(slant + 0.001f) * dt / W, 1f);

        // Lightning: follow the banner's brightness (RainOnBanner moves it between baseBrightness and 1)
        flash = 0f;
        if (lightningSource != null && lightningSource.isActiveAndEnabled)
        {
            float r = lightningSource.color.r;
            if (r <= baseBrightness + 0.005f) armed = true;
            if (armed) flash = Mathf.InverseLerp(baseBrightness, 1f, r);
        }
        else armed = false;
    }

    void Splash()
    {
        float x = X(Random.value);
        float y = Bottom + Random.Range(0f, Height * groundHeight);

        ripples[nextRipple] = new Ripple { x = x, y = y, age = 0f, life = Random.Range(0.3f, 0.5f) };
        nextRipple = (nextRipple + 1) % ripples.Length;

        int n = Random.Range(2, 4);
        for (int k = 0; k < n; k++)
        {
            droplets[nextDroplet] = new Droplet
            {
                x = x,
                y = y,
                vx = Random.Range(-110f, 110f),
                vy = Random.Range(90f, 190f),
                age = 0f,
                life = Random.Range(0.25f, 0.4f),
                size = Random.Range(2.5f, 4f)
            };
            nextDroplet = (nextDroplet + 1) % droplets.Length;
        }
    }

    protected override void Draw(VertexHelper vh)
    {
        // Mist hugging the ground
        foreach (var m in mists)
        {
            float x = X(m.u);
            float a = EdgeAlpha(x) * Intensity * (0.75f + 0.25f * Noise(m.seed, T * 0.2f));
            if (a <= 0f) continue;
            Color32 c = Col(mistColor, a);
            Quad(vh, new Vector2(x, Bottom + m.y), SpanWidth * m.wFrac, m.h, 0f, DotUV, c, c);
        }

        // Rain streaks
        float angle = slant * Mathf.Deg2Rad;
        float boost = 1f + flash * 1.5f;
        for (int l = 0; l < layers.Length; l++)
        {
            Layer layer = layers[l];
            foreach (var d in drops[l])
            {
                float x = X(d.u);
                float a = EdgeAlpha(x) * Presence(d.rank) * layer.alpha * boost;
                if (a <= 0f) continue;
                Color32 c = Col(rainColor, a);
                Quad(vh, new Vector2(x, Bottom + d.y), layer.width, d.len, angle, StreakUV, c, c);
            }
        }

        // Ripples and splash droplets
        foreach (var r in ripples)
        {
            if (r.age >= r.life) continue;
            float t = r.age / r.life;
            float w = Mathf.Lerp(6f, 30f, t);
            Color32 c = Col(splashColor, (1f - t) * 0.6f * EdgeAlpha(r.x));
            Quad(vh, new Vector2(r.x, r.y), w, w * 0.28f, 0f, DotUV, c, c);
        }
        foreach (var p in droplets)
        {
            if (p.age >= p.life) continue;
            Color32 c = Col(splashColor, (1f - p.age / p.life) * EdgeAlpha(p.x));
            Quad(vh, new Vector2(p.x, p.y), p.size, p.size, 0f, DotUV, c, c);
        }

        // Lightning flash across the whole panel
        if (flash > 0f)
        {
            Color bottom = flashColor; bottom.a *= 0.6f;
            Band(vh, Bottom, Top, bottom, flashColor, flash);
        }
    }
}