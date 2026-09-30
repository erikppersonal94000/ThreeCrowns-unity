using System.Collections.Generic;
using UnityEngine;

// Life for the throne room: flickering braziers and candles, soul wisps rising from the well,
// and fog drifting across the floor. Put it on the background sprite, next to ThroneRoom.
[RequireComponent(typeof(SpriteRenderer))]
public class RoomAmbience : MonoBehaviour
{
    [System.Serializable]
    public class Flame
    {
        [Tooltip("Position in the image, 0-1 from the top-left.")]
        public Vector2 pos;
        [Tooltip("Glow size as a fraction of the image height.")]
        public float size = 0.05f;
        public Color color = Color.white;

        public Flame() { }
        public Flame(float u, float v, float s, Color c) { pos = new Vector2(u, v); size = s; color = c; }
    }

    static readonly Color Violet = new Color(0.69f, 0.439f, 1f, 1f);  // #B070FF
    static readonly Color Candle = new Color(1f, 0.706f, 0.353f, 1f); // #FFB45A

    [Header("Braziers and candles")]
    public List<Flame> flames = new List<Flame>
    {
        new Flame(0.385f, 0.45f, 0.14f, Violet),   // left brazier
        new Flame(0.556f, 0.45f, 0.14f, Violet),   // right brazier
        new Flame(0.10f, 0.32f, 0.06f, Candle),    // archive candles
        new Flame(0.20f, 0.36f, 0.05f, Candle),
        new Flame(0.275f, 0.39f, 0.05f, Candle),
        new Flame(0.33f, 0.40f, 0.045f, Candle),
        new Flame(0.84f, 0.39f, 0.045f, Candle),   // right wall candles
        new Flame(0.93f, 0.35f, 0.05f, Candle),
        new Flame(0.843f, 0.59f, 0.07f, Candle),   // war table candles
        new Flame(0.953f, 0.70f, 0.08f, Candle),
    };
    [Range(0f, 1f)] public float flameStrength = 0.35f;
    public float flickerSpeed = 6f;

    [Header("Soul wisps from the well")]
    public Vector2 wellCenter = new Vector2(0.575f, 0.80f);
    public int wispCount = 36;
    public Color wispGreen = new Color(0.624f, 0.961f, 0.784f, 1f);   // #9FF5C8
    public Color wispViolet = new Color(0.706f, 0.549f, 1f, 1f);      // #B48CFF
    [Range(0f, 1f)] public float wispStrength = 0.6f;

    [Header("Floor fog")]
    public int fogCount = 9;
    public Color fogColor = new Color(0.549f, 0.498f, 0.627f, 0.12f); // #8C7FA0

    class Wisp { public Transform t; public SpriteRenderer sr; public Vector2 start; public float age, life, rise, size, seed; public Color color; }
    class Fog { public Transform t; public SpriteRenderer sr; public float u, v, w, h, speed; }
    class Glow { public Transform t; public SpriteRenderer sr; public Flame f; public float seed; }

    SpriteRenderer bg;
    readonly List<Glow> glows = new List<Glow>();
    readonly List<Wisp> wisps = new List<Wisp>();
    readonly List<Fog> fogs = new List<Fog>();

    void Start()
    {
        bg = GetComponent<SpriteRenderer>();
        if (bg.sprite == null) return;

        for (int i = 0; i < fogCount; i++)
        {
            var sr = Make("Fog", 1);
            fogs.Add(new Fog
            {
                t = sr.transform, sr = sr,
                u = Random.Range(-0.2f, 1.2f), v = Random.Range(0.78f, 0.98f),
                w = Random.Range(0.3f, 0.5f), h = Random.Range(0.08f, 0.14f),
                speed = Random.Range(0.008f, 0.02f) * (Random.value < 0.5f ? -1f : 1f)
            });
        }
        foreach (var f in flames)
        {
            var sr = Make("Flame Glow", 3);
            glows.Add(new Glow { t = sr.transform, sr = sr, f = f, seed = Random.value * 100f });
        }
        for (int i = 0; i < wispCount; i++)
        {
            var sr = Make("Wisp", 4);
            var w = new Wisp { t = sr.transform, sr = sr };
            Respawn(w);
            w.age = Random.Range(0f, w.life);
            wisps.Add(w);
        }
    }

    SpriteRenderer Make(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RoomFx.GlowSprite;
        sr.sharedMaterial = bg.sharedMaterial;
        sr.sortingLayerID = bg.sortingLayerID;
        sr.sortingOrder = bg.sortingOrder + order;
        return sr;
    }

    void Respawn(Wisp w)
    {
        w.start = wellCenter + new Vector2(Random.Range(-0.04f, 0.04f), Random.Range(-0.02f, 0.02f));
        w.age = 0f;
        w.life = Random.Range(2f, 4f);
        w.rise = Random.Range(0.12f, 0.28f);
        w.size = Random.Range(0.012f, 0.03f);
        w.seed = Random.value * 100f;
        w.color = Color.Lerp(wispGreen, wispViolet, Random.value);
    }

    void Update()
    {
        if (bg == null || bg.sprite == null) return;
        float dt = Time.deltaTime, T = Time.time;
        float H = bg.sprite.bounds.size.y, W = bg.sprite.bounds.size.x;

        foreach (var f in fogs)
        {
            f.u += f.speed * dt;
            if (f.u > 1.3f) f.u = -0.3f;
            if (f.u < -0.3f) f.u = 1.3f;
            Place(f.t, f.u, f.v, f.w * W, f.h * H);
            f.sr.color = fogColor;
        }

        foreach (var g in glows)
        {
            float n = Mathf.PerlinNoise(g.seed, T * flickerSpeed * 0.3f);
            float flick = 0.7f + 0.3f * n;
            float s = g.f.size * H * (0.92f + 0.12f * n);
            Place(g.t, g.f.pos.x, g.f.pos.y, s, s);
            g.sr.color = new Color(g.f.color.r, g.f.color.g, g.f.color.b, flameStrength * flick);
        }

        foreach (var w in wisps)
        {
            w.age += dt;
            if (w.age >= w.life) Respawn(w);
            float t = w.age / w.life;
            float u = w.start.x + Mathf.Sin(T * 1.5f + w.seed) * 0.012f * t;
            float v = w.start.y - w.rise * t;
            float s = w.size * H * (1f - 0.4f * t);
            Place(w.t, u, v, s, s);
            w.sr.color = new Color(w.color.r, w.color.g, w.color.b, wispStrength * Mathf.Sin(Mathf.PI * t));
        }
    }

    void Place(Transform t, float u, float v, float width, float height)
    {
        Vector2 p = RoomFx.ImageToLocal(bg, u, v);
        t.localPosition = new Vector3(p.x, p.y, 0f);
        t.localScale = new Vector3(width, height, 1f);
    }
}