using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Drawing helpers for the campaign map: a circle sprite, lines, polygon fills, and hit testing.
public static class MapDraw
{
    static Sprite circle;
    public static Sprite Circle => circle != null ? circle : (circle = MakeCircle());

    static Sprite MakeCircle()
    {
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dx = x + 0.5f - S / 2f, dy = y + 0.5f - S / 2f;
            float a = Mathf.Clamp01(S / 2f - Mathf.Sqrt(dx * dx + dy * dy)); // soft 1-pixel edge
            px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
    }

    public static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float w, Color c)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) return;
        d /= len;
        a -= d * w * 0.5f; // overlap the ends so corners have no gaps
        b += d * w * 0.5f;
        Vector2 n = new Vector2(-d.y, d.x) * (w * 0.5f);
        int i = vh.currentVertCount;
        vh.AddVert(a - n, c, Vector2.zero);
        vh.AddVert(a + n, c, Vector2.zero);
        vh.AddVert(b + n, c, Vector2.zero);
        vh.AddVert(b - n, c, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }

    public static void Dashed(VertexHelper vh, Vector2 a, Vector2 b, float w, Color c, float dash, float gap)
    {
        float len = Vector2.Distance(a, b);
        if (len < 0.01f) return;
        Vector2 d = (b - a) / len;
        for (float t = 0f; t < len; t += dash + gap)
            Segment(vh, a + d * t, a + d * Mathf.Min(len, t + dash), w, c);
    }

    // Splits a polygon (any shape, no self-crossings) into triangles. Returns vertex indices.
    public static List<int> Triangulate(Vector2[] p)
    {
        var tris = new List<int>();
        int n = p.Length;
        if (n < 3) return tris;
        float area = 0f;
        for (int i = 0; i < n; i++) area += Cross(p[i], p[(i + 1) % n]);
        var idx = new List<int>();
        if (area > 0f) for (int i = 0; i < n; i++) idx.Add(i);
        else for (int i = n - 1; i >= 0; i--) idx.Add(i); // make it counter-clockwise

        for (int guard = 0; idx.Count > 3 && guard < 1000; guard++)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int i0 = idx[(i + idx.Count - 1) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                Vector2 a = p[i0], b = p[i1], c = p[i2];
                if (Cross(b - a, c - b) <= 0f) continue; // inward corner, not an ear
                bool inside = false;
                foreach (int j in idx)
                {
                    if (j == i0 || j == i1 || j == i2) continue;
                    if (InTriangle(p[j], a, b, c)) { inside = true; break; }
                }
                if (inside) continue;
                tris.Add(i0); tris.Add(i1); tris.Add(i2);
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break;
        }
        if (idx.Count == 3) { tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]); }
        return tris;
    }

    public static bool Inside(Vector2[] poly, Vector2 pt)
    {
        if (poly == null) return false;
        bool c = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > pt.y) != (poly[j].y > pt.y) &&
                pt.x < (poly[j].x - poly[i].x) * (pt.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                c = !c;
        }
        return c;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
        Cross(b - a, p - a) >= 0f && Cross(c - b, p - b) >= 0f && Cross(a - c, p - c) >= 0f;
}

// Province shapes over the world map: a fill and an outline each. Points are 0-1 across its rect.
public class ProvinceShapes : MaskableGraphic
{
    public class Shape { public Vector2[] pts; public Color fill; public Color line; public float width; }
    public readonly List<Shape> shapes = new List<Shape>();
    readonly Dictionary<Vector2[], List<int>> triCache = new Dictionary<Vector2[], List<int>>();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        foreach (var s in shapes)
        {
            if (s.pts == null || s.pts.Length < 3) continue;
            int n = s.pts.Length;
            var p = new Vector2[n];
            for (int i = 0; i < n; i++) p[i] = new Vector2(r.xMin + s.pts[i].x * r.width, r.yMin + s.pts[i].y * r.height);

            if (s.fill.a > 0.001f)
            {
                if (!triCache.TryGetValue(s.pts, out var tris)) triCache[s.pts] = tris = MapDraw.Triangulate(s.pts);
                int start = vh.currentVertCount;
                for (int i = 0; i < n; i++) vh.AddVert(p[i], s.fill, Vector2.zero);
                for (int i = 0; i + 2 < tris.Count; i += 3) vh.AddTriangle(start + tris[i], start + tris[i + 1], start + tris[i + 2]);
            }
            if (s.line.a > 0.001f && s.width > 0f)
                for (int i = 0; i < n; i++) MapDraw.Segment(vh, p[i], p[(i + 1) % n], s.width, s.line);
        }
    }
}

// Roads between locations on a province map. Points are 0-1 across its rect.
public class RouteLines : MaskableGraphic
{
    public class Line { public Vector2 a, b; public Color color; public float width; public bool dashed; }
    public readonly List<Line> lines = new List<Line>();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        foreach (var l in lines)
        {
            Vector2 a = new Vector2(r.xMin + l.a.x * r.width, r.yMin + l.a.y * r.height);
            Vector2 b = new Vector2(r.xMin + l.b.x * r.width, r.yMin + l.b.y * r.height);
            if (l.dashed) MapDraw.Dashed(vh, a, b, l.width, l.color, l.width * 3f, l.width * 2.2f);
            else MapDraw.Segment(vh, a, b, l.width, l.color);
        }
    }
}