using UnityEngine;
using UnityEngine.UI;

// Rain soaking into a banner: the cloth slowly darkens from the top down, in uneven patches,
// while its kingdom is hovered, and dries when you leave.
// Put this on a child of the banner; give the banner a Mask so this stays on the cloth.
[RequireComponent(typeof(CanvasRenderer))]
public class WetSoak : MaskableGraphic
{
    [SerializeField] TitlePanels panels;
    [SerializeField] int kingdomIndex = 1;
    [SerializeField] WavingBanner banner; // optional: moves with the cloth

    [Header("Timing")]
    [SerializeField] float soakTime = 14f; // seconds of hovering to soak completely
    [SerializeField] float dryTime = 6f;

    [Header("Look")]
    [SerializeField, Range(8, 96)] int columns = 30;
    [SerializeField, Range(8, 128)] int rows = 44;
    [SerializeField] Color wetColor = new Color(0.02f, 0.03f, 0.06f, 0.45f); // dark, slightly blue
    [SerializeField] float edgeSoftness = 0.15f;
    [SerializeField] int seed = 5;

    public float Progress { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        if (panels == null) panels = FindFirstObjectByType<TitlePanels>();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        bool on = panels != null && panels.Focus == kingdomIndex;
        Progress = Mathf.Clamp01(Progress + Time.deltaTime * (on ? 1f / soakTime : -1f / dryTime));
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0f || Progress <= 0f) return;

        for (int y = 0; y <= rows; y++)
        {
            float v = y / (float)rows;
            float down = 1f - v; // 0 at the top
            for (int x = 0; x <= columns; x++)
            {
                float u = x / (float)columns;

                // The wet line creeps down from the top, unevenly
                float n = Mathf.PerlinNoise(u * 3f + seed, v * 5f + seed);
                float wet = Mathf.Clamp01((Progress * 1.3f - down - (n - 0.5f) * 0.45f) / edgeSoftness);
                // Some patches soak darker than others
                wet *= Mathf.Lerp(0.6f, 1f, Mathf.PerlinNoise(u * 7f + seed * 3f, v * 9f));

                Color c = wetColor;
                c.a *= wet;

                var pos = new Vector2(r.xMin + u * r.width, r.yMin + v * r.height);
                if (banner != null) pos += banner.Offset(u, v);
                vh.AddVert(pos, c, Vector2.zero);
            }
        }

        int stride = columns + 1;
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            int i = y * stride + x;
            vh.AddTriangle(i, i + stride, i + stride + 1);
            vh.AddTriangle(i, i + stride + 1, i + 1);
        }
    }
}