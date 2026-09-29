using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Tiny water beads that form on metal parts of a banner, catch the light,
// sometimes slide a short way, then disappear.
[RequireComponent(typeof(CanvasRenderer))]
public class RainBeads : MaskableGraphic
{
    [System.Serializable]
    public class Area
    {
        [Tooltip("Where beads form: X, Y = bottom-left corner, W, H = size (all 0-1 of the banner)")]
        public Rect rect = new Rect(0.34f, 0.33f, 0.32f, 0.28f);
        public float beadsPerSecond = 5f;
    }

    [SerializeField] WavingBanner banner; // optional: beads move with the cloth
    [SerializeField] Area[] areas =
    {
        new Area { rect = new Rect(0.34f, 0.33f, 0.32f, 0.28f), beadsPerSecond = 6f },   // the shield
        new Area { rect = new Rect(0.22f, 0.30f, 0.56f, 0.45f), beadsPerSecond = 4f },   // the star frame around it
        new Area { rect = new Rect(0.05f, 0.905f, 0.90f, 0.015f), beadsPerSecond = 3f }, // the iron rod
    };

    [Header("Beads")]
    [SerializeField] Vector2 beadSize = new Vector2(0.8f, 1.7f);
    [SerializeField] Vector2 beadLife = new Vector2(1.5f, 4f);
    [SerializeField, Range(0f, 1f)] float slideChance = 0.35f;
    [SerializeField] float slideSpeed = 22f;
    [SerializeField] int maxBeads = 120;
    [SerializeField] Color water = new Color(0.72f, 0.82f, 0.94f, 0.4f);
    [SerializeField] Color glint = new Color(1f, 1f, 1f, 0.9f);

    class Bead
    {
        public Vector2 pos, start;
        public float size, life, maxLife, slideAt, slideDist;
        public bool slides;
    }

    readonly List<Bead> beads = new();
    float[] acc;

    void Update()
    {
        if (!Application.isPlaying) return;
        Rect r = rectTransform.rect;
        float dt = Time.deltaTime;

        if (acc == null || acc.Length != areas.Length) acc = new float[areas.Length];
        for (int a = 0; a < areas.Length; a++)
        {
            acc[a] += areas[a].beadsPerSecond * dt;
            while (acc[a] >= 1f)
            {
                acc[a] -= 1f;
                if (beads.Count < maxBeads) Spawn(areas[a].rect, r);
            }
        }

        for (int i = beads.Count - 1; i >= 0; i--)
        {
            var b = beads[i];
            b.life += dt;
            if (b.slides && b.life > b.slideAt && b.start.y - b.pos.y < b.slideDist)
                b.pos.y -= slideSpeed * dt;
            if (b.life >= b.maxLife) beads.RemoveAt(i);
        }

        SetVerticesDirty();
    }

    void Spawn(Rect area, Rect r)
    {
        var p = new Vector2(Random.Range(area.xMin, area.xMax) * r.width, Random.Range(area.yMin, area.yMax) * r.height);
        var b = new Bead
        {
            pos = p,
            start = p,
            size = Random.Range(beadSize.x, beadSize.y),
            maxLife = Random.Range(beadLife.x, beadLife.y),
            slides = Random.value < slideChance,
            slideDist = Random.Range(5f, 18f),
        };
        b.slideAt = b.maxLife * Random.Range(0.2f, 0.5f);
        beads.Add(b);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;

        foreach (var b in beads)
        {
            float t = b.life / b.maxLife;
            float grow = Mathf.Clamp01(b.life / 0.3f);
            float fade = 1f - Mathf.Clamp01((t - 0.8f) / 0.2f);
            float s = b.size * grow;
            Vector2 c = ToLocal(b.pos, r);

            // A faint wet line where it slid
            if (b.slides && b.start.y - b.pos.y > 0.5f)
            {
                Color st = water; st.a *= 0.35f * fade;
                AddRect(vh, ToLocal(new Vector2(b.pos.x - s * 0.35f, b.pos.y), r),
                            ToLocal(new Vector2(b.pos.x + s * 0.35f, b.start.y), r), st);
            }

            // Tiny shadow, the bead, and its glint
            AddEllipse(vh, c + new Vector2(0f, -s * 0.35f), s, s * 0.8f, new Color(0f, 0f, 0f, 0.25f * fade));
            Color body = water; body.a *= fade;
            AddEllipse(vh, c, s, s * 0.9f, body);
            Color g = glint; g.a *= fade;
            AddEllipse(vh, c + new Vector2(-s * 0.3f, s * 0.3f), s * 0.3f, s * 0.3f, g);
        }
    }

    Vector2 ToLocal(Vector2 p, Rect r)
    {
        var v = new Vector2(r.xMin + p.x, r.yMin + p.y);
        if (banner != null) v += banner.Offset(p.x / r.width, p.y / r.height);
        return v;
    }

    static void AddRect(VertexHelper vh, Vector2 min, Vector2 max, Color col)
    {
        int s = vh.currentVertCount;
        vh.AddVert(new Vector2(min.x, min.y), col, Vector2.zero);
        vh.AddVert(new Vector2(min.x, max.y), col, Vector2.zero);
        vh.AddVert(new Vector2(max.x, max.y), col, Vector2.zero);
        vh.AddVert(new Vector2(max.x, min.y), col, Vector2.zero);
        vh.AddTriangle(s, s + 1, s + 2);
        vh.AddTriangle(s, s + 2, s + 3);
    }

    static void AddEllipse(VertexHelper vh, Vector2 c, float rx, float ry, Color col)
    {
        const int segs = 10;
        int start = vh.currentVertCount;
        vh.AddVert(c, col, Vector2.zero);
        for (int i = 0; i <= segs; i++)
        {
            float a = i / (float)segs * Mathf.PI * 2f;
            vh.AddVert(c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry), col, Vector2.zero);
        }
        for (int i = 0; i < segs; i++) vh.AddTriangle(start, start + 1 + i, start + 2 + i);
    }
}