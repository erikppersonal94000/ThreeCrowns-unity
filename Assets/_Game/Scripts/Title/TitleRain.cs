using UnityEngine;
using UnityEngine.UI;

// Stonehelm: rain lands on the letter tops and splashes off, water trickles down the letters
// and drips from their bottoms, and the letters darken as they soak.
public class TitleRain : TitleFx
{
    [Header("Size")]
    [Tooltip("Scales every drop, splash, trickle and drip. Raise for bigger title text.")]
    public float sizeScale = 2.5f;

    [Header("Rain landing on the letters")]
    public float dropsPerSecond = 25f;
    public float dropSpeed = 1800f;
    [Tooltip("Degrees from straight down. Match PanelRain's slant.")]
    public float slant = 10f;
    public Color dropColor = new Color(0.722f, 0.784f, 0.847f, 0.6f);    // #B8C8D8
    public Color splashColor = new Color(0.8f, 0.863f, 0.925f, 0.85f);   // #CCDCEC

    [Header("Water on the letters")]
    public float tricklesPerSecond = 4f;
    public Vector2 trickleSpeed = new Vector2(40f, 90f);
    public float dripsPerSecond = 3f;
    public Color waterColor = new Color(0.843f, 0.906f, 0.965f, 0.8f);   // #D7E7F6
    [Tooltip("Darkens the letters as they soak.")]
    public Color wetTint = new Color(0.125f, 0.173f, 0.227f, 0.35f);     // #202C3A

    struct Drop { public Vector2 pos, target; public bool live; }
    struct Droplet { public Vector2 pos, vel; public float age, life, size; }
    struct Puff { public Vector2 pos; public float age, life; }
    struct Trickle { public Vector2 pos; public float endY, speed, seed; public bool live; }
    struct Drip { public Vector2 pos; public float size, grow, age, vy, fallen; public bool live, falling; }

    readonly Drop[] drops = new Drop[64];
    readonly Droplet[] droplets = new Droplet[256];
    readonly Puff[] puffs = new Puff[64];
    readonly Trickle[] trickles = new Trickle[32];
    readonly Drip[] drips = new Drip[24];
    int nDrop, nDroplet, nPuff, nTrickle, nDrip;
    float dropTimer, trickleTimer, dripTimer;

    protected override void Simulate(float dt)
    {
        float lean = Mathf.Tan(slant * Mathf.Deg2Rad);
        float s = sizeScale;

        // Drops aimed at points on the letter tops
        dropTimer += dropsPerSecond * Vis * dt;
        while (dropTimer >= 1f)
        {
            dropTimer -= 1f;
            if (!RandomTop(out Vector2 p)) { dropTimer = 0f; break; }
            float fall = Random.Range(220f, 420f);
            drops[nDrop] = new Drop { pos = p + new Vector2(-lean * fall, fall), target = p, live = true };
            nDrop = (nDrop + 1) % drops.Length;
        }
        for (int i = 0; i < drops.Length; i++)
        {
            ref Drop d = ref drops[i];
            if (!d.live) continue;
            Vector2 to = d.target - d.pos;
            float step = dropSpeed * dt;
            if (to.magnitude <= step) { d.live = false; Splash(d.target); }
            else d.pos += to.normalized * step;
        }

        for (int i = 0; i < droplets.Length; i++)
        {
            ref Droplet p = ref droplets[i];
            if (p.age >= p.life) continue;
            p.age += dt;
            p.vel.y -= 900f * s * 0.6f * dt;
            p.pos += p.vel * dt;
        }
        for (int i = 0; i < puffs.Length; i++)
            if (puffs[i].age < puffs[i].life) puffs[i].age += dt;

        // Water trickling down the letters
        trickleTimer += tricklesPerSecond * Vis * Progress * dt;
        while (trickleTimer >= 1f)
        {
            trickleTimer -= 1f;
            if (!RandomColumn(out Vector2 top, out Vector2 bottom)) { trickleTimer = 0f; break; }
            trickles[nTrickle] = new Trickle
            {
                pos = top, endY = bottom.y,
                speed = Random.Range(trickleSpeed.x, trickleSpeed.y),
                seed = Random.value * 100f, live = true
            };
            nTrickle = (nTrickle + 1) % trickles.Length;
        }
        for (int i = 0; i < trickles.Length; i++)
        {
            ref Trickle t = ref trickles[i];
            if (!t.live) continue;
            t.pos.y -= t.speed * dt;
            t.pos.x += FxKit.Noise(t.seed, T * 2f) * 6f * dt;
            if (t.pos.y <= t.endY)
            {
                t.live = false;
                if (Random.value < 0.5f) StartDrip(new Vector2(t.pos.x, t.endY));
            }
        }

        // Drips from the letter bottoms
        dripTimer += dripsPerSecond * Vis * Progress * dt;
        while (dripTimer >= 1f)
        {
            dripTimer -= 1f;
            if (!RandomBottom(out Vector2 b)) { dripTimer = 0f; break; }
            StartDrip(b);
        }
        for (int i = 0; i < drips.Length; i++)
        {
            ref Drip d = ref drips[i];
            if (!d.live) continue;
            if (!d.falling)
            {
                d.age += dt;
                d.size = Mathf.Lerp(0.5f, 3.5f, Mathf.Clamp01(d.age / d.grow)) * s;
                if (d.age >= d.grow) d.falling = true;
            }
            else
            {
                d.vy -= 900f * dt;
                float dy = d.vy * dt;
                d.pos.y += dy;
                d.fallen -= dy;
                if (d.fallen > 160f) d.live = false;
            }
        }
    }

    void Splash(Vector2 p)
    {
        float s = sizeScale;
        puffs[nPuff] = new Puff { pos = p, age = 0f, life = Random.Range(0.25f, 0.4f) };
        nPuff = (nPuff + 1) % puffs.Length;
        int n = Random.Range(3, 6);
        for (int k = 0; k < n; k++)
        {
            droplets[nDroplet] = new Droplet
            {
                pos = p,
                vel = new Vector2(Random.Range(-110f, 110f), Random.Range(60f, 170f)) * Mathf.Sqrt(s),
                age = 0f,
                life = Random.Range(0.3f, 0.45f),
                size = Random.Range(1.8f, 3f) * s
            };
            nDroplet = (nDroplet + 1) % droplets.Length;
        }
    }

    void StartDrip(Vector2 p)
    {
        drips[nDrip] = new Drip { pos = p, size = 0.5f * sizeScale, grow = Random.Range(0.6f, 1.2f), live = true };
        nDrip = (nDrip + 1) % drips.Length;
    }

    public override Color32 OverlayColor(int charIndex, int corner, Vector3 pos)
    {
        return FxKit.Col(wetTint, Progress * (IsTopCorner(corner) ? 0.6f : 1f));
    }

    public override void DrawParticles(VertexHelper vh)
    {
        if (Vis <= 0.001f) return;
        float s = sizeScale;
        float angle = slant * Mathf.Deg2Rad;
        float len = 34f * s;
        Vector2 up = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * (len * 0.5f);

        Color32 dc = FxKit.Col(dropColor, Vis);
        foreach (var d in drops)
            if (d.live) FxKit.Quad(vh, d.pos + up, 2f * s, len, angle, FxKit.StreakUV, dc, dc);

        foreach (var p in puffs)
        {
            if (p.age >= p.life) continue;
            float t = p.age / p.life;
            float w = Mathf.Lerp(6f, 22f, t) * s;
            Color32 c = FxKit.Col(splashColor, (1f - t) * 0.7f * Vis);
            FxKit.Quad(vh, p.pos + new Vector2(0f, s), w, w * 0.3f, 0f, FxKit.DotUV, c, c);
        }
        foreach (var p in droplets)
        {
            if (p.age >= p.life) continue;
            Color32 c = FxKit.Col(splashColor, (1f - p.age / p.life) * Vis);
            FxKit.Quad(vh, p.pos, p.size, p.size, 0f, FxKit.DotUV, c, c);
        }

        foreach (var t in trickles)
        {
            if (!t.live) continue;
            Color32 c0 = FxKit.Col(waterColor, Vis);
            Color32 c1 = FxKit.Col(waterColor, 0.55f * Vis);
            Color32 c2 = FxKit.Col(waterColor, 0.25f * Vis);
            FxKit.Quad(vh, t.pos + new Vector2(0f, 10f * s), 1.6f * s, 5f * s, 0f, FxKit.DotUV, c2, c2);
            FxKit.Quad(vh, t.pos + new Vector2(0f, 5f * s), 2f * s, 6f * s, 0f, FxKit.DotUV, c1, c1);
            FxKit.Quad(vh, t.pos, 2.4f * s, 7f * s, 0f, FxKit.DotUV, c0, c0);
        }

        foreach (var d in drips)
        {
            if (!d.live) continue;
            Color32 c = FxKit.Col(waterColor, Vis);
            if (!d.falling)
                FxKit.Quad(vh, d.pos - new Vector2(0f, d.size * 0.6f), d.size, d.size * 1.4f, 0f, FxKit.DotUV, c, c);
            else
                FxKit.Quad(vh, d.pos, d.size, d.size * 1.6f, 0f, FxKit.DotUV, c, c);
        }
    }
}