using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared particle texture and drawing helpers for the title letter effects.
public static class FxKit
{
    public static readonly Rect DotUV = new Rect(0f, 0.5f, 0.5f, 0.5f);
    public static readonly Rect FlakeUV = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
    public static readonly Rect StreakUV = new Rect(0f, 0f, 0.5f, 0.5f);
    public static readonly Rect FlameUV = new Rect(0.5f, 0f, 0.5f, 0.5f);

    static Texture2D atlas;
    public static Texture2D Atlas => atlas != null ? atlas : (atlas = BuildAtlas());

    public static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    public static float Noise(float seed, float t) => Mathf.PerlinNoise(seed, t) * 2f - 1f;

    // Stable pseudo-random 0..1 from a number (same input, same answer every frame)
    public static float Hash(float n) => Mathf.Repeat(Mathf.Sin(n * 12.9898f) * 43758.5453f, 1f);

    public static Color32 Col(Color c, float alpha) => new Color(c.r, c.g, c.b, Mathf.Clamp01(c.a * alpha));

    public static void Quad(VertexHelper vh, Vector2 c, float w, float h, float angle, Rect uv,
                            Color32 bottom, Color32 top, float topShift = 0f)
    {
        float cs = Mathf.Cos(angle), sn = Mathf.Sin(angle);
        Vector2 ax = new Vector2(cs, sn) * (w * 0.5f);
        Vector2 ay = new Vector2(-sn, cs) * (h * 0.5f);
        Vector2 shift = new Vector2(cs, sn) * topShift;
        int i = vh.currentVertCount;
        vh.AddVert(c - ax - ay, bottom, new Vector2(uv.xMin, uv.yMin));
        vh.AddVert(c - ax + ay + shift, top, new Vector2(uv.xMin, uv.yMax));
        vh.AddVert(c + ax + ay + shift, top, new Vector2(uv.xMax, uv.yMax));
        vh.AddVert(c + ax - ay, bottom, new Vector2(uv.xMax, uv.yMin));
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }

    static Texture2D BuildAtlas()
    {
        const int S = 256, C = 128;
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
            int cx = x / C, cy = y / C;
            float u = ((x % C) + 0.5f) / C * 2f - 1f;
            float v = ((y % C) + 0.5f) / C * 2f - 1f;
            float a;
            if (cy == 1 && cx == 0) a = DotShape(u, v);
            else if (cy == 1) a = FlakeShape(u, v);
            else if (cx == 0) a = StreakShape(u, v);
            else a = FlameShape(u, v);
            px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    static float DotShape(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v) / 0.9f;
        return r >= 1f ? 0f : Mathf.Pow(1f - r, 1.6f);
    }

    static float FlakeShape(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float ang = Mathf.Atan2(v, u);
        float edge = 0.6f + 0.16f * Mathf.Sin(3f * ang + 0.7f) + 0.09f * Mathf.Sin(7f * ang + 2.1f);
        float fill = 0.8f + 0.2f * Mathf.Sin(u * 9f + 1f) * Mathf.Sin(v * 11f);
        return Smooth(edge, edge - 0.1f, r) * fill;
    }

    static float StreakShape(float u, float v)
    {
        float across = Mathf.Clamp01(1f - Mathf.Abs(u) / 0.4f);
        float along = Smooth(0.92f, 0.55f, Mathf.Abs(v));
        float head = Mathf.Lerp(1f, 0.35f, (v + 1f) * 0.5f);
        return across * across * along * head;
    }

    static float FlameShape(float u, float v)
    {
        float y = (v + 1f) * 0.5f;
        float w = 0.8f * Mathf.Pow(1f - y, 1.3f) * Mathf.Sqrt(Smooth(0f, 0.25f, y));
        if (w < 0.001f) return 0f;
        float across = Mathf.Clamp01(1f - Mathf.Abs(u) / w);
        return Mathf.Pow(across, 1.2f) * Smooth(0.02f, 0.14f, y) * Smooth(0.95f, 0.7f, y);
    }
}

// One letter of a title face, in the face's local space.
public class TitleLetter
{
    public int charIndex;
    public float xMin, xMax, yMin, yMax;
    public Vector2[] top;     // points along the letter's top edge (y is NaN where a column is empty)
    public Vector2[] bottom;  // points along the letter's bottom edge
    public Vector2[] inside;  // points inside the letter's shape
}

// Reads the real outline of each letter from the font atlas (once per character), so effects
// can sit exactly on the letter shapes.
public static class TitleGlyphs
{
    const byte Edge = 110;       // SDF value where the letter's edge is
    const int InsideSamples = 30;

    class Profile { public float[] top, bottom; public Vector2[] inside; }
    class AtlasData { public byte[] a; public int w, h; }

    static readonly Dictionary<long, Profile> profiles = new Dictionary<long, Profile>();
    static readonly Dictionary<int, AtlasData> atlases = new Dictionary<int, AtlasData>();

    // Rebuilds 'letters' when the text layout changes. Returns true if it rebuilt.
    public static bool Refresh(TMP_Text text, int columns, List<TitleLetter> letters, ref float signature)
    {
        var info = text.textInfo;
        if (info == null) return false;

        float sig = info.characterCount * 7919f + columns;
        for (int i = 0; i < info.characterCount; i++)
        {
            var ci = info.characterInfo[i];
            if (!ci.isVisible) continue;
            sig += ci.vertex_BL.position.x * 0.731f + ci.vertex_TR.position.y * 1.37f + ci.character;
        }
        if (letters.Count > 0 && Mathf.Approximately(sig, signature)) return false;
        signature = sig;

        letters.Clear();
        for (int i = 0; i < info.characterCount; i++)
        {
            var ci = info.characterInfo[i];
            if (!ci.isVisible || ci.fontAsset == null) continue;
            Profile p = GetProfile(ci, columns);
            if (p == null) continue;

            Vector3 bl = ci.vertex_BL.position, tr = ci.vertex_TR.position;
            var L = new TitleLetter
            {
                charIndex = i,
                xMin = bl.x, xMax = tr.x, yMin = bl.y, yMax = tr.y,
                top = new Vector2[columns],
                bottom = new Vector2[columns],
                inside = new Vector2[p.inside.Length]
            };
            for (int c = 0; c < columns; c++)
            {
                float x = Mathf.Lerp(bl.x, tr.x, (c + 0.5f) / columns);
                L.top[c] = new Vector2(x, p.top[c] < 0f ? float.NaN : Mathf.Lerp(bl.y, tr.y, p.top[c]));
                L.bottom[c] = new Vector2(x, p.bottom[c] < 0f ? float.NaN : Mathf.Lerp(bl.y, tr.y, p.bottom[c]));
            }
            for (int k = 0; k < p.inside.Length; k++)
                L.inside[k] = new Vector2(Mathf.Lerp(bl.x, tr.x, p.inside[k].x), Mathf.Lerp(bl.y, tr.y, p.inside[k].y));
            letters.Add(L);
        }
        return true;
    }

    static Profile GetProfile(TMP_CharacterInfo ci, int columns)
    {
        TMP_FontAsset fa = ci.fontAsset;
        long key = ((long)fa.GetInstanceID() << 32) ^ ((long)ci.character << 8) ^ columns;
        if (profiles.TryGetValue(key, out Profile p)) return p;

        Texture2D[] textures = fa.atlasTextures;
        if (textures == null || textures.Length == 0) return null;
        int atlasIndex = 0;
        if (ci.textElement != null && ci.textElement.glyph != null) atlasIndex = ci.textElement.glyph.atlasIndex;
        AtlasData a = GetAtlas(textures[Mathf.Clamp(atlasIndex, 0, textures.Length - 1)]);
        if (a == null) return null;

        Vector2 uvA = ci.vertex_BL.uv, uvB = ci.vertex_TR.uv;
        float u0 = Mathf.Min(uvA.x, uvB.x) * a.w, u1 = Mathf.Max(uvA.x, uvB.x) * a.w;
        float v0 = Mathf.Min(uvA.y, uvB.y) * a.h, v1 = Mathf.Max(uvA.y, uvB.y) * a.h;
        int yLo = Mathf.Clamp(Mathf.FloorToInt(v0), 0, a.h - 1);
        int yHi = Mathf.Clamp(Mathf.CeilToInt(v1) - 1, 0, a.h - 1);

        p = new Profile { top = new float[columns], bottom = new float[columns] };
        for (int c = 0; c < columns; c++)
        {
            int x = Mathf.Clamp((int)Mathf.Lerp(u0, u1, (c + 0.5f) / columns), 0, a.w - 1);
            p.top[c] = -1f;
            p.bottom[c] = -1f;
            for (int y = yHi; y >= yLo; y--)
                if (a.a[y * a.w + x] >= Edge) { p.top[c] = Mathf.InverseLerp(v0, v1, y + 1f); break; }
            for (int y = yLo; y <= yHi; y++)
                if (a.a[y * a.w + x] >= Edge) { p.bottom[c] = Mathf.InverseLerp(v0, v1, y); break; }
        }

        var list = new List<Vector2>();
        var rng = new System.Random(ci.character * 131 + columns);
        for (int tries = 0; tries < 600 && list.Count < InsideSamples; tries++)
        {
            float fu = (float)rng.NextDouble(), fv = (float)rng.NextDouble();
            int x = Mathf.Clamp((int)Mathf.Lerp(u0, u1, fu), 0, a.w - 1);
            int y = Mathf.Clamp((int)Mathf.Lerp(v0, v1, fv), 0, a.h - 1);
            if (a.a[y * a.w + x] >= Edge + 20) list.Add(new Vector2(fu, fv));
        }
        p.inside = list.ToArray();

        profiles[key] = p;
        return p;
    }

    // Copies the font atlas off the GPU once so we can read the letter shapes
    static AtlasData GetAtlas(Texture2D tex)
    {
        if (tex == null) return null;
        int id = tex.GetInstanceID();
        if (atlases.TryGetValue(id, out AtlasData d)) return d;

        int w = tex.width, h = tex.height;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var prev = RenderTexture.active;
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;
        var read = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        read.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
        read.Apply(false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Color32[] px = read.GetPixels32();
        Object.Destroy(read);

        // The shape is normally in alpha; on some GPUs it comes back in red instead
        bool alphaUsed = false;
        for (int i = 0; i < px.Length; i += 97)
            if (px[i].a < 250) { alphaUsed = true; break; }

        d = new AtlasData { w = w, h = h, a = new byte[px.Length] };
        for (int i = 0; i < px.Length; i++) d.a[i] = alphaUsed ? px[i].a : px[i].r;
        atlases[id] = d;
        return d;
    }
}