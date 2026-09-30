using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A carved plaque: dark fill with chamfered corners, a thin border in the kingdom's color,
// a faint inner line, and an outer glow that fades in on hover.
[RequireComponent(typeof(CanvasRenderer))]
public class PlateFrame : MaskableGraphic
{
    public Color fillTop = new Color(0.102f, 0.075f, 0.125f, 0.92f);     // #1A1320
    public Color fillBottom = new Color(0.043f, 0.031f, 0.063f, 0.92f);  // #0B0810
    public Color accent = Color.white;
    [Range(0f, 1f)] public float glow;
    public float chamfer = 10f;
    public float border = 2f;
    public float glowSize = 16f;

    readonly List<Vector2> pts = new List<Vector2>(8);
    readonly List<Vector2> inner = new List<Vector2>(8);
    readonly List<Vector2> outer = new List<Vector2>(8);

    // The 8 corners of the plaque, grown outward (or inward if negative) by 'grow'
    void Shape(Rect r, float grow, List<Vector2> into)
    {
        into.Clear();
        float c = Mathf.Max(0f, chamfer + grow * 0.414f);
        float x0 = r.xMin - grow, x1 = r.xMax + grow, y0 = r.yMin - grow, y1 = r.yMax + grow;
        into.Add(new Vector2(x0 + c, y1));
        into.Add(new Vector2(x1 - c, y1));
        into.Add(new Vector2(x1, y1 - c));
        into.Add(new Vector2(x1, y0 + c));
        into.Add(new Vector2(x1 - c, y0));
        into.Add(new Vector2(x0 + c, y0));
        into.Add(new Vector2(x0, y0 + c));
        into.Add(new Vector2(x0, y1 - c));
    }

    void Ring(VertexHelper vh, Rect r, float g0, float g1, Color c0, Color c1)
    {
        Shape(r, g0, inner);
        Shape(r, g1, outer);
        Color32 a = c0 * color, b = c1 * color;
        for (int i = 0; i < 8; i++)
        {
            int j = (i + 1) % 8, v = vh.currentVertCount;
            vh.AddVert(inner[i], a, Vector2.zero);
            vh.AddVert(outer[i], b, Vector2.zero);
            vh.AddVert(outer[j], b, Vector2.zero);
            vh.AddVert(inner[j], a, Vector2.zero);
            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v + 2, v + 3, v);
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();

        // Outer glow
        if (glow > 0.001f)
        {
            Color g = accent; g.a *= 0.55f * glow;
            Color clear = accent; clear.a = 0f;
            Ring(vh, r, 0f, glowSize, g, clear);
        }

        // Fill, darker toward the bottom
        Shape(r, 0f, pts);
        int center = vh.currentVertCount;
        vh.AddVert(r.center, Color.Lerp(fillBottom, fillTop, 0.5f) * color, Vector2.zero);
        foreach (var p in pts)
        {
            float t = Mathf.InverseLerp(r.yMin, r.yMax, p.y);
            vh.AddVert(p, Color.Lerp(fillBottom, fillTop, t) * color, Vector2.zero);
        }
        for (int i = 0; i < 8; i++) vh.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % 8);

        // Border, then a faint inner line
        Color b = accent; b.a *= Mathf.Lerp(0.75f, 1f, glow);
        Ring(vh, r, -border, 0f, b, b);
        Color line = accent; line.a *= 0.3f;
        Ring(vh, r, -border - 4f, -border - 3f, line, line);
    }
}