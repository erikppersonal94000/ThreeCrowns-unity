using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Blood that seeps from points on the banner, runs down the cloth, and drips off the bottom.
// Put this on a child of the banner, stretched to the same size.
// Points are 0-1 across (x) and up (y) the banner.
[RequireComponent(typeof(CanvasRenderer))]
public class BloodDrips : MaskableGraphic
{
    [System.Serializable]
    public class Source
    {
        [Tooltip("Where the blood seeps out: 0-1 across (x) and up (y) the banner")]
        public Vector2 point = new Vector2(0.5f, 0.5f);
        [Tooltip("Height where drops fall off the banner (0-1 up the banner, e.g. the fringe tips)")]
        public float dropOffY = 0.04f;
        [Tooltip("Seconds between drops, random between these two")]
        public Vector2 interval = new Vector2(6f, 12f);
        [HideInInspector] public float nextTime;
    }

    [SerializeField] Source[] sources =
    {
        // Along the lower rim of the emblem, each with its own drop-off height and rhythm
        new Source { point = new Vector2(0.31f, 0.36f),   dropOffY = 0.075f, interval = new Vector2(6f, 15f) },
        new Source { point = new Vector2(0.41f, 0.298f),  dropOffY = 0.05f,  interval = new Vector2(8f, 19f) },
        new Source { point = new Vector2(0.575f, 0.298f), dropOffY = 0.045f, interval = new Vector2(5f, 13f) },
        new Source { point = new Vector2(0.68f, 0.35f),   dropOffY = 0.07f,  interval = new Vector2(10f, 22f) },
    };
    [SerializeField] WavingBanner banner; // optional: blood moves with the cloth

    [Header("Randomness")]
    [SerializeField, Range(0f, 1f)] float followUpChance = 0.3f; // chance a second drop quickly follows the first
    [SerializeField] float startJitter = 2f;                     // pixels a drop can start to either side

    [Header("Look")]
    [SerializeField] float dropSize = 2.5f;                                // radius of a full drop
    [SerializeField] Color bloodDark = new Color32(0x16, 0x00, 0x03, 255); // drying, almost black
    [SerializeField] Color bloodWet = new Color32(0x4a, 0x02, 0x0a, 255);  // fresh, deep crimson
    [SerializeField] Color gloss = new Color(1f, 0.55f, 0.55f, 0.35f);     // wet shine
    [SerializeField] float trailLife = 12f;                                // seconds for a trail to dry away

    [Header("Motion")]
    [SerializeField] Vector2 swellTime = new Vector2(1.5f, 3f); // how long a drop gathers before running
    [SerializeField] float runSpeed = 16f;                      // speed down the cloth
    [SerializeField] float gravity = 900f;                      // once it falls off

    class Drop
    {
        public Source src;
        public float x, y, fallX;
        public float size, swellDur, age, speed, vy, seed, stopUntil;
        public float fallAlpha = 1f, trailFade = 1f;
        public int state; // 0 swelling, 1 running, 2 falling, 3 gone
        public readonly List<Vector2> path = new();
    }

    readonly List<Drop> drops = new();
    readonly List<Vector2> tmp = new();

    protected override void Start()
    {
        base.Start();
        foreach (var s in sources) s.nextTime = Time.time + Random.Range(1f, 5f);
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        Rect r = rectTransform.rect;
        float dt = Time.deltaTime;

        foreach (var s in sources)
        {
            if (Time.time < s.nextTime) continue;
            Spawn(s, r);
            // Sometimes another drop follows right behind down the same wet trail
            s.nextTime = Random.value < followUpChance
                ? Time.time + Random.Range(1f, 2.5f)
                : Time.time + Random.Range(s.interval.x, s.interval.y);
        }

        for (int i = drops.Count - 1; i >= 0; i--)
        {
            var d = drops[i];
            d.age += dt;

            switch (d.state)
            {
                case 0: // gathering at the rim of the socket
                    d.size = Mathf.Clamp01(d.age / d.swellDur);
                    if (d.size >= 1f) d.state = 1;
                    break;

                case 1: // running down, sometimes catching on the weave
                    if (Time.time >= d.stopUntil)
                    {
                        d.y -= d.speed * dt * Mathf.Lerp(0.5f, 1.5f, Mathf.PerlinNoise(d.seed, Time.time * 0.7f));
                        if (Random.value < dt * 0.3f) d.stopUntil = Time.time + Random.Range(0.4f, 1.8f);
                    }
                    RecordPath(d);
                    if (d.y <= d.src.dropOffY * r.height)
                    {
                        d.state = 2;
                        d.fallX = WobbleX(d, d.y);
                        d.vy = 0f;
                    }
                    break;

                case 2: // falling off the fringe
                    d.vy += gravity * dt;
                    d.y -= d.vy * dt;
                    d.fallAlpha = Mathf.Clamp01(d.fallAlpha - dt * 1.5f);
                    if (d.fallAlpha <= 0f) d.state = 3;
                    break;
            }

            // Once the drop has moved on, its trail slowly dries away
            if (d.state >= 2) d.trailFade = Mathf.Clamp01(d.trailFade - dt / trailLife);
            if (d.state == 3 && d.trailFade <= 0f) drops.RemoveAt(i);
        }

        SetVerticesDirty();
    }

    void Spawn(Source s, Rect r)
    {
        var d = new Drop
        {
            src = s,
            x = s.point.x * r.width + Random.Range(-startJitter, startJitter),
            y = s.point.y * r.height,
            swellDur = Random.Range(swellTime.x, swellTime.y),
            speed = runSpeed * Random.Range(0.7f, 1.3f),
            seed = Random.value * 100f,
        };
        d.path.Add(new Vector2(WobbleX(d, d.y), d.y));
        drops.Add(d);
    }

    // A slight side-to-side wander as it runs down the cloth
    static float WobbleX(Drop d, float y) =>
        d.x + Mathf.Sin(y * 0.06f + d.seed) * 0.8f + (Mathf.PerlinNoise(d.seed, y * 0.03f) - 0.5f) * 2f;

    static void RecordPath(Drop d)
    {
        var last = d.path[d.path.Count - 1];
        if (last.y - d.y >= 2f) d.path.Add(new Vector2(WobbleX(d, d.y), d.y));
    }

    // ---------- Drawing ----------
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;

        foreach (var d in drops)
        {
            // Trail: thin and uneven, darker as it dries, with a wet shine while fresh
            if (d.trailFade > 0f)
            {
                tmp.Clear();
                foreach (var p in d.path) tmp.Add(ToLocal(p, r));
                if (d.state <= 1) tmp.Add(ToLocal(new Vector2(WobbleX(d, d.y), d.y), r));

                if (tmp.Count >= 2)
                {
                    float fade = d.trailFade;
                    AddTrail(vh, tmp, d.seed, dropSize * 0.35f, dropSize * 0.8f, 0f,
                        WithAlpha(bloodDark, 0.9f * fade), WithAlpha(bloodWet, 0.95f * fade));
                    // the shine dries faster than the blood
                    AddTrail(vh, tmp, d.seed, 0.6f, 0.9f, -dropSize * 0.2f,
                        WithAlpha(gloss, 0.4f * fade * fade), WithAlpha(gloss, fade * fade));
                }
            }

            // The drop: dark rim, deep red body, a small glint
            if (d.state < 3)
            {
                float s = dropSize * Mathf.Max(d.size, 0.05f);
                float rx = s * (d.state == 0 ? 0.9f : 0.8f);
                float ry = rx * (d.state == 0 ? 1.1f : d.state == 1 ? 1.4f : 1.8f); // stretches as it moves
                Vector2 head = d.state == 2 ? new Vector2(d.fallX, d.y) : new Vector2(WobbleX(d, d.y), d.y);
                Vector2 c = ToLocal(head, r) + new Vector2(0f, -ry * 0.35f);
                float a = d.fallAlpha;

                AddEllipse(vh, c, rx * 1.2f, ry * 1.15f, WithAlpha(bloodDark, a));
                AddEllipse(vh, c, rx, ry, WithAlpha(bloodWet, a));
                AddEllipse(vh, c + new Vector2(-rx * 0.35f, ry * 0.3f), rx * 0.3f, ry * 0.25f, WithAlpha(gloss, 1.6f * a));
            }
        }
    }

    Vector2 ToLocal(Vector2 p, Rect r)
    {
        var v = new Vector2(r.xMin + p.x, r.yMin + p.y);
        if (banner != null) v += banner.Offset(p.x / r.width, p.y / r.height);
        return v;
    }

    static Color WithAlpha(Color c, float a) { c.a *= a; return c; }

    // Filled oval with a soft 1-pixel edge
    static void AddEllipse(VertexHelper vh, Vector2 c, float rx, float ry, Color col)
    {
        const int segs = 14;
        int start = vh.currentVertCount;
        Color edge = col; edge.a = 0f;
        vh.AddVert(c, col, Vector2.zero);
        for (int i = 0; i <= segs; i++)
        {
            float ang = i / (float)segs * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            vh.AddVert(c + new Vector2(dir.x * rx, dir.y * ry), col, Vector2.zero);
            vh.AddVert(c + new Vector2(dir.x * (rx + 1f), dir.y * (ry + 1f)), edge, Vector2.zero);
        }
        for (int i = 0; i < segs; i++)
        {
            int inner = start + 1 + i * 2, outer = inner + 1;
            int nextInner = inner + 2, nextOuter = inner + 3;
            vh.AddTriangle(start, inner, nextInner);
            vh.AddTriangle(inner, outer, nextOuter);
            vh.AddTriangle(inner, nextOuter, nextInner);
        }
    }

    // A thin rivulet along the points: tapers from top to bottom, varies in width,
    // with a soft edge on both sides. xShift moves it sideways (used for the shine).
    static void AddTrail(VertexHelper vh, List<Vector2> pts, float seed, float wTop, float wBottom,
                         float xShift, Color top, Color bottom)
    {
        int n = pts.Count;
        int start = vh.currentVertCount;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            float w = Mathf.Lerp(wTop, wBottom, t) * Mathf.Lerp(0.6f, 1.3f, Mathf.PerlinNoise(seed, i * 0.35f)) * 0.5f;
            Color c = Color.Lerp(top, bottom, t);
            Color e = c; e.a = 0f;
            Vector2 p = pts[i] + new Vector2(xShift, 0f);
            vh.AddVert(p + new Vector2(-w - 0.8f, 0f), e, Vector2.zero); // soft left edge
            vh.AddVert(p + new Vector2(-w, 0f), c, Vector2.zero);
            vh.AddVert(p + new Vector2(w, 0f), c, Vector2.zero);
            vh.AddVert(p + new Vector2(w + 0.8f, 0f), e, Vector2.zero);  // soft right edge
        }
        for (int i = 0; i < n - 1; i++)
        {
            int b = start + i * 4, nb = b + 4;
            for (int k = 0; k < 3; k++)
            {
                vh.AddTriangle(b + k, nb + k, nb + k + 1);
                vh.AddTriangle(b + k, nb + k + 1, b + k + 1);
            }
        }
    }
}