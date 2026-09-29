using UnityEngine;
using UnityEngine.UI;

// Icicles that grow as a FrostCreep freezes the banner, and shrink back as it thaws.
[RequireComponent(typeof(CanvasRenderer))]
public class Icicles : MaskableGraphic
{
    [System.Serializable]
    public class Icicle
    {
        [Tooltip("Where it hangs from: 0-1 across (x) and up (y) the banner")]
        public Vector2 point;
        public float length = 16f;
        public float width = 4f;
        [Tooltip("How far into the freeze it starts growing (0 = right away)")]
        [Range(0f, 0.9f)] public float delay;
    }

    [SerializeField] FrostCreep frost;
    [SerializeField] WavingBanner banner;
    [SerializeField] Icicle[] icicles =
    {
        // Under the iron rod
        new Icicle { point = new Vector2(0.08f, 0.895f), length = 12f, width = 3f, delay = 0.2f },
        new Icicle { point = new Vector2(0.30f, 0.900f), length = 16f, width = 4f, delay = 0.35f },
        new Icicle { point = new Vector2(0.50f, 0.895f), length = 10f, width = 3f, delay = 0.5f },
        new Icicle { point = new Vector2(0.70f, 0.900f), length = 18f, width = 4f, delay = 0.25f },
        // Along the bottom edge of the cloth, among the tassels
        new Icicle { point = new Vector2(0.22f, 0.195f), length = 14f, width = 4f, delay = 0.4f },
        new Icicle { point = new Vector2(0.30f, 0.175f), length = 20f, width = 5f, delay = 0.3f },
        new Icicle { point = new Vector2(0.38f, 0.155f), length = 12f, width = 3f, delay = 0.55f },
        new Icicle { point = new Vector2(0.46f, 0.135f), length = 22f, width = 5f, delay = 0.2f },
        new Icicle { point = new Vector2(0.54f, 0.135f), length = 16f, width = 4f, delay = 0.45f },
        new Icicle { point = new Vector2(0.62f, 0.155f), length = 19f, width = 4f, delay = 0.35f },
        new Icicle { point = new Vector2(0.70f, 0.175f), length = 13f, width = 3f, delay = 0.6f },
        new Icicle { point = new Vector2(0.78f, 0.195f), length = 17f, width = 4f, delay = 0.3f },
    };
    [SerializeField] Color iceTop = new Color(0.88f, 0.95f, 1f, 0.95f);
    [SerializeField] Color iceTip = new Color(0.65f, 0.82f, 1f, 0.55f);

    void Update()
    {
        if (Application.isPlaying) SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (frost == null) return;
        Rect r = rectTransform.rect;
        float p = frost.Progress;

        foreach (var ic in icicles)
        {
            float g = Mathf.Clamp01((p - ic.delay) / (1f - ic.delay));
            g = g * g * (3f - 2f * g);
            if (g <= 0.01f) continue;

            var top = new Vector2(r.xMin + ic.point.x * r.width, r.yMin + ic.point.y * r.height);
            if (banner != null) top += banner.Offset(ic.point.x, ic.point.y);
            float len = ic.length * g;
            float w = ic.width * Mathf.Lerp(0.5f, 1f, g);
            Vector2 tip = top + new Vector2(0f, -len);

            // The icicle: a tapering spike, clear at the tip
            int s = vh.currentVertCount;
            vh.AddVert(top + new Vector2(-w * 0.5f, 0f), iceTop, Vector2.zero);
            vh.AddVert(top + new Vector2(w * 0.5f, 0f), iceTop, Vector2.zero);
            vh.AddVert(tip, iceTip, Vector2.zero);
            vh.AddTriangle(s, s + 1, s + 2);

            // A thin highlight down one side
            Color hl = new Color(1f, 1f, 1f, 0.7f * g);
            Color hl0 = hl; hl0.a = 0f;
            int h = vh.currentVertCount;
            vh.AddVert(top + new Vector2(-w * 0.25f, 0f), hl, Vector2.zero);
            vh.AddVert(top + new Vector2(-w * 0.05f, 0f), hl, Vector2.zero);
            vh.AddVert(tip + new Vector2(0f, len * 0.25f), hl0, Vector2.zero);
            vh.AddTriangle(h, h + 1, h + 2);
        }
    }
}