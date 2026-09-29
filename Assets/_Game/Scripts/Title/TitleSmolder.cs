using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Duskmoor: the letters heat up red from the bottom, then catch fire. Flames grow out of the
// letters themselves and rise behind them, spreading letter to letter. Embers and smoke drift up.
public class TitleSmolder : TitleFx
{
    [Header("Size")]
    [Tooltip("Scales the smoke. Raise for bigger title text.")]
    public float sizeScale = 2.2f;

    [Header("Stage 1: letters heat up")]
    [Tooltip("Deep red the letters first glow.")]
    public Color emberRed = new Color(0.541f, 0.071f, 0.024f, 0.85f);    // #8A1206
    [Tooltip("Orange they reach when fully hot.")]
    public Color heatColor = new Color(1f, 0.353f, 0.078f, 0.8f);        // #FF5A14
    public float flickerSpeed = 2.5f;
    [Tooltip("How far through the build-up (0-1) the letters catch fire.")]
    [Range(0.1f, 0.9f)] public float catchPoint = 0.4f;

    [Header("Stage 2: flames rising from the letters")]
    [Tooltip("How many flames per letter (0 = none, 1 = lots).")]
    [Range(0f, 1f)] public float flameDensity = 0.22f;
    [Tooltip("Flame height at full burn, as a multiple of the letter height.")]
    public float flameHeight = 0.6f;
    [Tooltip("Flame width as a fraction of the letter width.")]
    public float flameWidth = 0.35f;
    public float fireFlicker = 5f;
    public Color fireDeep = new Color(0.604f, 0.11f, 0.024f, 0.8f);      // #9A1C06
    public Color fireBase = new Color(0.886f, 0.267f, 0.059f, 0.9f);     // #E2440F
    public Color fireTip = new Color(1f, 0.769f, 0.369f, 0.35f);         // #FFC45E
    public Color fireGlow = new Color(1f, 0.353f, 0.078f, 0.3f);         // #FF5A14

    [Header("Embers")]
    public float embersPerSecond = 25f;
    public Vector2 emberSize = new Vector2(3f, 6f);
    public Vector2 emberRise = new Vector2(40f, 110f);
    public Vector2 emberLife = new Vector2(1.5f, 3f);
    public Color emberCore = new Color(1f, 0.702f, 0.278f, 1f);          // #FFB347
    public Color emberGlow = new Color(1f, 0.353f, 0.078f, 0.3f);        // #FF5A14

    [Header("Smoke")]
    public float smokePerSecond = 6f;
    public Color smokeColor = new Color(0.102f, 0.078f, 0.094f, 0.2f);   // #1A1418

    class LFlame { public TitleLetter L; public int col; public float h, w, seed, rand; }
    struct Ember { public Vector2 pos; public float size, rise, age, life, seed; }
    struct Smoke { public Vector2 pos; public float age, life, seed; }

    readonly List<LFlame> flames = new List<LFlame>();
    readonly Ember[] embers = new Ember[120];
    readonly Smoke[] smoke = new Smoke[48];
    int nEmber, nSmoke;
    float emberTimer, smokeTimer;
    TitleLetter builtFor;
    float builtDensity = -1f;
    float igniteX;
    bool lit;

    protected override bool UseBehind => true;

    void Reset() { buildTime = 10f; }

    // Pick 2-3 spots along the top of each letter for flames to grow from
    void BuildFlames()
    {
        flames.Clear();
        builtFor = Letters.Count > 0 ? Letters[0] : null;
        builtDensity = flameDensity;
        foreach (var L in Letters)
        {
            int last = -10;
            for (int c = 0; c < L.top.Length; c++)
            {
                if (float.IsNaN(L.top[c].y)) continue;
                float h = FxKit.Hash(L.charIndex * 11.7f + c * 5.3f);
                if (h > flameDensity || c - last < 3) continue;
                last = c;
                flames.Add(new LFlame
                {
                    L = L, col = c,
                    h = 0.6f + 0.7f * FxKit.Hash(h * 31f),
                    w = 0.8f + 0.4f * FxKit.Hash(h * 57f),
                    seed = h * 100f,
                    rand = FxKit.Hash(h * 77f)
                });
            }
        }
    }

    // 0..1 how lit a spot is: catches at igniteX first, then spreads outward letter by letter
    float Burn(float x, float rand)
    {
        if (!lit) return 0f;
        float span = Mathf.Max(1f, TextXMax - TextXMin);
        float dist = Mathf.Abs(x - igniteX) / span;
        float remaining = 1f - catchPoint;
        float start = catchPoint + remaining * (0.45f * dist + 0.1f * rand);
        return FxKit.Smooth(start, start + remaining * 0.4f, Progress);
    }

    float FlameH(LFlame f, float burn)
    {
        float letterH = f.L.yMax - f.L.yMin;
        float flick = 0.75f + 0.3f * FxKit.Noise(f.seed, T * fireFlicker);
        return letterH * flameHeight * f.h * burn * flick;
    }

    protected override void Simulate(float dt)
    {
        if (Letters.Count > 0 && (builtFor != Letters[0] || !Mathf.Approximately(builtDensity, flameDensity)))
            BuildFlames();

        // Catch fire at a random letter once the heat reaches the catch point
        if (Progress < catchPoint * 0.5f) lit = false;
        else if (!lit && Progress >= catchPoint && Letters.Count > 0)
        {
            var L = Letters[Random.Range(0, Letters.Count)];
            igniteX = (L.xMin + L.xMax) * 0.5f;
            lit = true;
        }

        // Embers and smoke come off the tips of burning flames
        emberTimer += embersPerSecond * Vis * FxKit.Smooth(catchPoint, 1f, Progress) * dt;
        while (emberTimer >= 1f)
        {
            emberTimer -= 1f;
            if (!RandomTip(out Vector2 p)) { emberTimer = 0f; break; }
            embers[nEmber] = new Ember
            {
                pos = p,
                size = Random.Range(emberSize.x, emberSize.y),
                rise = Random.Range(emberRise.x, emberRise.y),
                age = 0f,
                life = Random.Range(emberLife.x, emberLife.y),
                seed = Random.value * 100f
            };
            nEmber = (nEmber + 1) % embers.Length;
        }
        for (int i = 0; i < embers.Length; i++)
        {
            ref Ember e = ref embers[i];
            if (e.age >= e.life) continue;
            e.age += dt;
            e.pos.y += e.rise * dt;
            e.pos.x += 30f * FxKit.Noise(e.seed, T * 0.8f) * dt;
        }

        smokeTimer += smokePerSecond * Vis * FxKit.Smooth(catchPoint, 1f, Progress) * dt;
        while (smokeTimer >= 1f)
        {
            smokeTimer -= 1f;
            if (!RandomTip(out Vector2 p)) { smokeTimer = 0f; break; }
            smoke[nSmoke] = new Smoke { pos = p, age = 0f, life = Random.Range(1.8f, 3f), seed = Random.value * 100f };
            nSmoke = (nSmoke + 1) % smoke.Length;
        }
        for (int i = 0; i < smoke.Length; i++)
        {
            ref Smoke s = ref smoke[i];
            if (s.age >= s.life) continue;
            s.age += dt;
            s.pos.y += 35f * dt;
            s.pos.x += 14f * FxKit.Noise(s.seed, T * 0.4f) * dt;
        }
    }

    // A point near the tip of a flame that's burning
    bool RandomTip(out Vector2 p)
    {
        p = Vector2.zero;
        if (flames.Count == 0) return false;
        for (int tries = 0; tries < 8; tries++)
        {
            var f = flames[Random.Range(0, flames.Count)];
            Vector2 top = f.L.top[f.col];
            float burn = Burn(top.x, f.rand);
            if (burn < 0.3f) continue;
            float h = FlameH(f, burn);
            p = new Vector2(top.x, top.y + h * Random.Range(0.3f, 0.8f));
            return true;
        }
        return false;
    }

    // Stage 1: red heat creeps up the letters from the bottom and brightens to orange
    public override Color32 OverlayColor(int charIndex, int corner, Vector3 pos)
    {
        float flick = 0.75f + 0.25f * FxKit.Noise(charIndex * 3.7f, T * flickerSpeed);
        float bottom = FxKit.Smooth(0f, catchPoint * 0.8f, Progress);
        float top = FxKit.Smooth(catchPoint * 0.6f, catchPoint + (1f - catchPoint) * 0.6f, Progress) * 0.6f;
        Color c = Color.Lerp(emberRed, heatColor, FxKit.Smooth(catchPoint * 0.5f, 1f, Progress));
        return FxKit.Col(c, (IsTopCorner(corner) ? top : bottom) * flick);
    }

    // Behind the letters: glow and flames
    public override void DrawBehind(VertexHelper vh)
    {
        if (Vis <= 0.001f || !lit) return;

        // Soft glow behind each burning letter
        foreach (var L in Letters)
        {
            float cx = (L.xMin + L.xMax) * 0.5f;
            float burn = Burn(cx, 0.5f);
            if (burn <= 0f) continue;
            float w = L.xMax - L.xMin, h = L.yMax - L.yMin;
            Color32 g = FxKit.Col(fireGlow, burn * Vis * (0.85f + 0.15f * FxKit.Noise(L.charIndex * 2.3f, T * 2f)));
            FxKit.Quad(vh, new Vector2(cx, L.yMin + h * 0.6f), w * 1.5f, h * (1.2f + flameHeight * burn), 0f, FxKit.DotUV, g, g);
        }

        // Flames growing out of the letter tops: deep red behind, bright in front
        for (int pass = 0; pass < 2; pass++)
        {
            bool back = pass == 0;
            foreach (var f in flames)
            {
                Vector2 top = f.L.top[f.col];
                float burn = Burn(top.x, f.rand);
                if (burn <= 0f) continue;
                float letterH = f.L.yMax - f.L.yMin;
                float letterW = f.L.xMax - f.L.xMin;
                float h = FlameH(f, burn) * (back ? 1.15f : 1f);
                if (h < 1f) continue;
                float w = letterW * flameWidth * f.w * (0.6f + 0.4f * burn) * (back ? 1.35f : 1f);
                float baseY = top.y - letterH * 0.15f;   // starts inside the letter, so it grows out of it
                float sway = FxKit.Noise(f.seed + 9f, T * 1.6f) * 0.2f * h;
                Color cb = back ? fireDeep : fireBase;
                Color ct = back ? fireBase : fireTip;
                if (back) ct.a *= 0.5f;
                FxKit.Quad(vh, new Vector2(top.x, baseY + h * 0.42f), w, h, 0f, FxKit.FlameUV,
                           FxKit.Col(cb, burn * Vis), FxKit.Col(ct, burn * Vis), sway);
            }
        }

        // Smoke
        float s = sizeScale;
        foreach (var sm in smoke)
        {
            if (sm.age >= sm.life) continue;
            float t = sm.age / sm.life;
            float size = Mathf.Lerp(10f, 40f, t) * s;
            Color32 c = FxKit.Col(smokeColor, Mathf.Sin(Mathf.PI * t) * Vis);
            FxKit.Quad(vh, sm.pos, size, size, 0f, FxKit.DotUV, c, c);
        }
    }

    // In front of the letters: embers
    public override void DrawParticles(VertexHelper vh)
    {
        if (Vis <= 0.001f) return;
        foreach (var e in embers)
        {
            if (e.age >= e.life) continue;
            float t = e.age / e.life;
            float a = FxKit.Smooth(0f, 0.1f, t) * FxKit.Smooth(1f, 0.6f, t)
                    * (0.65f + 0.35f * FxKit.Noise(e.seed, T * 7f)) * Vis;
            Color32 g = FxKit.Col(emberGlow, a), c = FxKit.Col(emberCore, a);
            FxKit.Quad(vh, e.pos, e.size * 5f, e.size * 5f, 0f, FxKit.DotUV, g, g);
            FxKit.Quad(vh, e.pos, e.size, e.size, 0f, FxKit.DotUV, c, c);
        }
    }
}