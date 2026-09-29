using UnityEngine;
using UnityEngine.UI;

// Soft glowing eyes that pulse like a slow heartbeat.
// Put this on a child of the banner, stretched to the same size.
[RequireComponent(typeof(CanvasRenderer))]
public class EyeGlow : MaskableGraphic
{
    [SerializeField] Vector2[] points = { new Vector2(0.465f, 0.505f), new Vector2(0.531f, 0.505f) };
    [SerializeField] WavingBanner banner;                                  // optional: glow moves with the cloth
    [SerializeField] float radius = 16f;
    [SerializeField] Color glowColor = new Color32(0xff, 0x22, 0x22, 170);  // outer glow
    [SerializeField] Color coreColor = new Color32(0xff, 0x8a, 0x70, 220);  // hot center
    [SerializeField] float beatsPerMinute = 40f;
    [SerializeField, Range(0f, 1f)] float minBrightness = 0.35f;

    void Update()
    {
        if (Application.isPlaying) SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float t = Application.isPlaying ? Time.time : 0f;

        // Heartbeat: a strong beat, a softer second beat, then rest
        float beat = (t * beatsPerMinute / 60f) % 1f;
        float pulse = Mathf.Exp(-Mathf.Pow((beat - 0.1f) / 0.06f, 2f))
                    + 0.6f * Mathf.Exp(-Mathf.Pow((beat - 0.3f) / 0.07f, 2f));
        float k = Mathf.Lerp(minBrightness, 1f, Mathf.Clamp01(pulse));

        foreach (var p in points)
        {
            var c = new Vector2(r.xMin + p.x * r.width, r.yMin + p.y * r.height);
            if (banner != null) c += banner.Offset(p.x, p.y);
            AddGlow(vh, c, radius * (0.8f + 0.4f * k), glowColor, k);
            AddGlow(vh, c, radius * 0.35f, coreColor, k);
        }
    }

    // A circle that fades from the center color to nothing at the edge
    static void AddGlow(VertexHelper vh, Vector2 c, float rad, Color col, float k)
    {
        const int segs = 24;
        int start = vh.currentVertCount;
        Color center = col; center.a *= k;
        Color edge = col; edge.a = 0f;
        vh.AddVert(c, center, Vector2.zero);
        for (int i = 0; i <= segs; i++)
        {
            float a = i / (float)segs * Mathf.PI * 2f;
            vh.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad, edge, Vector2.zero);
        }
        for (int i = 0; i < segs; i++) vh.AddTriangle(start, start + 1 + i, start + 2 + i);
    }
}