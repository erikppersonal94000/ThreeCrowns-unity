using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Rain on a banner: streaks falling past it, splashes where the rain hits the rod and the shield,
// and an occasional lightning flash that lights the banner up.
// (Water running down the cloth uses a BloodDrips component with water colors.)
[RequireComponent(typeof(CanvasRenderer))]
public class RainOnBanner : MaskableGraphic
{
    [System.Serializable]
    public class Ledge
    {
        [Tooltip("Height of the surface the rain hits (0-1 up the banner)")]
        public float y = 0.915f;
        [Tooltip("Left and right ends of the surface (0-1 across)")]
        public Vector2 xRange = new Vector2(0.05f, 0.95f);
        public float splashesPerSecond = 6f;
    }

    [SerializeField] WavingBanner banner;

    [Header("Rain")]
    [SerializeField] float dropsPerSecond = 70f;
    [SerializeField] float fallSpeed = 1100f;
    [SerializeField] float wind = -0.18f;                 // slant: sideways drift per unit of fall
    [SerializeField] Vector2 streakLength = new Vector2(14f, 26f);
    [SerializeField] Color rainColor = new Color(0.78f, 0.84f, 0.92f, 0.28f);
    [SerializeField] float margin = 0.2f;                 // rain reaches this far past the sides, fading out

    [Header("Splashes")]
    [SerializeField] Ledge[] ledges =
    {
        new Ledge { y = 0.915f, xRange = new Vector2(0.05f, 0.95f), splashesPerSecond = 7f }, // iron rod
        new Ledge { y = 0.735f, xRange = new Vector2(0.40f, 0.60f), splashesPerSecond = 2f }, // peaks above the shield
        new Ledge { y = 0.605f, xRange = new Vector2(0.34f, 0.66f), splashesPerSecond = 3f }, // top of the shield
    };
    [SerializeField] Color splashColor = new Color(0.85f, 0.9f, 0.97f, 0.6f);

    [Header("Lightning")]
    [SerializeField] Vector2 lightningEvery = new Vector2(7f, 15f);
    [SerializeField, Range(0f, 1f)] float baseBrightness = 0.85f; // banner brightness between flashes

    struct Streak { public float x, y, len, speed; }
    struct Droplet { public Vector2 pos, vel; public float life, maxLife, size; }

    readonly List<Streak> streaks = new();
    readonly List<Droplet> droplets = new();
    float spawnAcc;
    float[] ledgeAcc;
    float nextStrike, flash;

    protected override void Start()
    {
        base.Start();
        nextStrike = Time.time + Random.Range(lightningEvery.x, lightningEvery.y);
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        Rect r = rectTransform.rect;
        float dt = Time.deltaTime;

        // New rain streaks, starting above the banner
        spawnAcc += dropsPerSecond * dt;
        while (spawnAcc >= 1f)
        {
            spawnAcc -= 1f;
            float len = Random.Range(streakLength.x, streakLength.y);
            streaks.Add(new Streak
            {
                x = Random.Range(-margin, 1f + margin) * r.width,
                y = r.height + len + Random.Range(0f, r.height * 0.3f),
                len = len,
                speed = fallSpeed * Random.Range(0.85f, 1.15f),
            });
        }
        for (int i = streaks.Count - 1; i >= 0; i--)
        {
            var s = streaks[i];
            s.y -= s.speed * dt;
            s.x += s.speed * dt * wind;
            if (s.y < -s.len - 40f) streaks.RemoveAt(i);
            else streaks[i] = s;
        }

        // Splashes on the rod and the shield
        if (ledgeAcc == null || ledgeAcc.Length != ledges.Length) ledgeAcc = new float[ledges.Length];
        for (int l = 0; l < ledges.Length; l++)
        {
            ledgeAcc[l] += ledges[l].splashesPerSecond * dt;
            while (ledgeAcc[l] >= 1f)
            {
                ledgeAcc[l] -= 1f;
                Splash(ledges[l], r);
            }
        }
        for (int i = droplets.Count - 1; i >= 0; i--)
        {
            var d = droplets[i];
            d.vel.y -= 900f * dt;
            d.pos += d.vel * dt;
            d.life += dt;
            if (d.life >= d.maxLife) droplets.RemoveAt(i);
            else droplets[i] = d;
        }

        // Lightning: bright, dip, bright again, then fade
        if (Time.time >= nextStrike)
        {
            flash = 1f;
            nextStrike = Time.time + Random.Range(lightningEvery.x, lightningEvery.y);
        }
        if (flash > 0f) flash = Mathf.Max(0f, flash - dt * 2.5f);
        float f = flash > 0.8f || (flash > 0.5f && flash < 0.65f) ? flash : flash * 0.4f;
        if (banner != null)
        {
            float b = Mathf.Lerp(baseBrightness, 1f, f);
            banner.color = new Color(b, b, b, 1f);
        }

        SetVerticesDirty();
    }

    void Splash(Ledge l, Rect r)
    {
        var p = new Vector2(Random.Range(l.xRange.x, l.xRange.y) * r.width, l.y * r.height);
        int n = Random.Range(2, 5);
        for (int i = 0; i < n; i++)
        {
            droplets.Add(new Droplet
            {
                pos = p,
                vel = new Vector2(Random.Range(-70f, 70f), Random.Range(60f, 150f)),
                maxLife = Random.Range(0.18f, 0.32f),
                size = Random.Range(0.8f, 1.5f),
            });
        }
    }

    // ---------- Drawing ----------
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0f) return;

        // Rain streaks: bright at the head, fading along the tail, fading out past the banner's sides
        Vector2 dir = new Vector2(wind, -1f).normalized;
        foreach (var s in streaks)
        {
            float u = s.x / r.width;
            float edge = u < 0f ? 1f - Mathf.Clamp01(-u / margin)
                       : u > 1f ? 1f - Mathf.Clamp01((u - 1f) / margin) : 1f;
            if (edge <= 0f) continue;

            Vector2 head = new Vector2(r.xMin + s.x, r.yMin + s.y);
            Color ch = rainColor; ch.a *= edge;
            Color ct = ch; ct.a = 0f;
            AddLine(vh, head - dir * s.len, head, 1.1f, ct, ch);
        }

        // Splash droplets, fading as they fall
        foreach (var d in droplets)
        {
            Vector2 p = new Vector2(r.xMin + d.pos.x, r.yMin + d.pos.y);
            if (banner != null) p += banner.Offset(d.pos.x / r.width, d.pos.y / r.height);
            Color c = splashColor; c.a *= 1f - d.life / d.maxLife;
            AddQuad(vh, p, d.size, c);
        }
    }

    static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color ca, Color cb)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 n = new Vector2(-dir.y, dir.x) * (width * 0.5f);
        int s = vh.currentVertCount;
        vh.AddVert(a - n, ca, Vector2.zero);
        vh.AddVert(a + n, ca, Vector2.zero);
        vh.AddVert(b + n, cb, Vector2.zero);
        vh.AddVert(b - n, cb, Vector2.zero);
        vh.AddTriangle(s, s + 1, s + 2);
        vh.AddTriangle(s, s + 2, s + 3);
    }

    static void AddQuad(VertexHelper vh, Vector2 c, float size, Color col)
    {
        int s = vh.currentVertCount;
        vh.AddVert(c + new Vector2(-size, -size), col, Vector2.zero);
        vh.AddVert(c + new Vector2(-size, size), col, Vector2.zero);
        vh.AddVert(c + new Vector2(size, size), col, Vector2.zero);
        vh.AddVert(c + new Vector2(size, -size), col, Vector2.zero);
        vh.AddTriangle(s, s + 1, s + 2);
        vh.AddTriangle(s, s + 2, s + 3);
    }
}