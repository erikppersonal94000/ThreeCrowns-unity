using UnityEngine;
using UnityEngine.UI;

// Frostvale: heavy snow in three depths, gusting wind, blowing snow at the ground, cold haze.
public class PanelSnow : PanelWeather
{
    [System.Serializable]
    public class Layer
    {
        public int count;
        public Vector2 size;
        public Vector2 fall;
        [Range(0f, 1f)] public float alpha;
        public float sway;
        public float windScale;
        public Layer(int c, Vector2 s, Vector2 f, float a, float sw, float ws) { count = c; size = s; fall = f; alpha = a; sway = sw; windScale = ws; }
    }

    [Header("Snow")]
    public Layer far = new Layer(260, new Vector2(1.5f, 3f), new Vector2(22f, 40f), 0.45f, 10f, 0.4f);
    public Layer mid = new Layer(150, new Vector2(3f, 6f), new Vector2(45f, 80f), 0.75f, 22f, 0.8f);
    public Layer near = new Layer(28, new Vector2(9f, 18f), new Vector2(110f, 170f), 0.4f, 40f, 1.3f);
    public Color snowColor = new Color(0.949f, 0.969f, 1f, 1f);          // #F2F7FF

    [Header("Wind")]
    public float baseWind = 20f;
    public float gustStrength = 140f;
    public float gustSpeed = 0.12f;

    [Header("Blowing snow at the ground")]
    public int driftCount = 7;
    public Color driftColor = new Color(0.902f, 0.941f, 0.98f, 0.12f);   // #E6F0FA

    [Header("Cold haze")]
    public Color hazeBottom = new Color(0.659f, 0.784f, 0.941f, 0.05f);  // #A8C8F0
    public Color hazeTop = new Color(0.863f, 0.922f, 1f, 0.14f);         // #DCEBFF

    struct Flake { public float u, y, size, fall, phase, freq, rank; }
    struct Drift { public float u, y, w, h, speedScale, seed; }

    Layer[] layers;
    Flake[][] flakes;
    Drift[] drifts;
    float wind;

    protected override void Init()
    {
        layers = new[] { far, mid, near };
        flakes = new Flake[layers.Length][];
        for (int l = 0; l < layers.Length; l++)
        {
            flakes[l] = new Flake[layers[l].count];
            for (int i = 0; i < flakes[l].Length; i++)
            {
                flakes[l][i] = NewFlake(layers[l]);
                flakes[l][i].y = Random.Range(0f, Height);
            }
        }

        drifts = new Drift[driftCount];
        for (int i = 0; i < drifts.Length; i++)
        {
            drifts[i] = new Drift
            {
                u = Random.value,
                y = Random.Range(0f, Height * 0.12f),
                w = Random.Range(250f, 450f),
                h = Random.Range(30f, 60f),
                speedScale = Random.Range(1.2f, 2f),
                seed = Random.value * 100f
            };
        }
    }

    Flake NewFlake(Layer layer) => new Flake
    {
        u = Random.value,
        y = Height + Random.Range(0f, 40f),
        size = Random.Range(layer.size.x, layer.size.y),
        fall = Random.Range(layer.fall.x, layer.fall.y),
        phase = Random.value * 6.283f,
        freq = Random.Range(0.6f, 1.6f),
        rank = Random.value
    };

    protected override void Simulate(float dt)
    {
        float W = SpanWidth;
        wind = baseWind + gustStrength * Noise(3.3f, T * gustSpeed);

        for (int l = 0; l < layers.Length; l++)
        {
            Layer layer = layers[l];
            for (int i = 0; i < flakes[l].Length; i++)
            {
                ref Flake f = ref flakes[l][i];
                f.phase += f.freq * dt;
                f.y -= f.fall * dt;
                float dx = wind * layer.windScale + layer.sway * Mathf.Cos(f.phase);
                f.u = Mathf.Repeat(f.u + dx * dt / W, 1f);
                if (f.y < -f.size) f = NewFlake(layer);
            }
        }

        for (int i = 0; i < drifts.Length; i++)
            drifts[i].u = Mathf.Repeat(drifts[i].u + wind * drifts[i].speedScale * dt / W, 1f);
    }

    protected override void Draw(VertexHelper vh)
    {
        // Cold haze, stronger toward the top
        Band(vh, Bottom, Top, hazeBottom, hazeTop, Intensity);

        DrawLayer(vh, 0);

        // Blowing snow along the ground
        foreach (var d in drifts)
        {
            float x = X(d.u);
            float a = EdgeAlpha(x) * Intensity * (0.6f + 0.4f * Noise(d.seed, T * 0.5f));
            if (a <= 0f) continue;
            Color32 c = Col(driftColor, a);
            Quad(vh, new Vector2(x, Bottom + d.y), d.w, d.h, 0f, DotUV, c, c);
        }

        DrawLayer(vh, 1);
        DrawLayer(vh, 2);
    }

    void DrawLayer(VertexHelper vh, int l)
    {
        Layer layer = layers[l];
        foreach (var f in flakes[l])
        {
            float x = X(f.u);
            float a = EdgeAlpha(x) * Presence(f.rank) * layer.alpha;
            if (a <= 0f) continue;
            Color32 c = Col(snowColor, a);
            Quad(vh, new Vector2(x, Bottom + f.y), f.size, f.size, 0f, DotUV, c, c);
        }
    }
}