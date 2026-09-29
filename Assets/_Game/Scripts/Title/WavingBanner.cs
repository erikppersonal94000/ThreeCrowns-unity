using UnityEngine;
using UnityEngine.UI;

// A banner that hangs from its rod and moves gently in the air.
// Draws the image as a grid, bends the grid with slow waves (pinned at the top,
// freer toward the bottom), and shades the folds. Other effects can ask Offset()
// where a point on the cloth has moved to, so they move with it.
[RequireComponent(typeof(CanvasRenderer))]
public class WavingBanner : MaskableGraphic
{
    [SerializeField] Sprite sprite;

    [Header("Mesh")]
    [SerializeField, Range(4, 64)] int columns = 24;
    [SerializeField, Range(4, 96)] int rows = 36;
    [Tooltip("Top part that stays still (rod and scrollwork), as a fraction of the height")]
    [SerializeField, Range(0f, 0.5f)] float pinned = 0.12f;

    [Header("Movement")]
    [SerializeField] float windSpeed = 1.1f;      // how fast the ripples travel
    [SerializeField] float rippleAmount = 2.5f;   // how far the folds bend the cloth sideways
    [SerializeField] float ripplesAcross = 1.1f;  // how many folds across the width
    [SerializeField] float ripplesDown = 0.9f;    // how much the folds slant as they go down
    [SerializeField] float swingAmount = 3f;      // whole banner swaying at the bottom edge
    [SerializeField] float swingSpeed = 0.6f;
    [SerializeField] float liftAmount = 1f;       // bottom edge lifting as folds pass

    [Header("Folds")]
    [SerializeField, Range(0f, 1f)] float foldShade = 0.18f; // how dark the shaded side of each fold gets
    [SerializeField] float seed = 0f;

    public override Texture mainTexture => sprite != null ? sprite.texture : s_WhiteTexture;

    public Sprite Sprite
    {
        get => sprite;
        set { sprite = value; SetAllDirty(); }
    }

    float Now => (Application.isPlaying ? Time.time : 0f) + seed * 10f;

    void Update()
    {
        if (Application.isPlaying) SetVerticesDirty(); // re-bend the cloth every frame
    }

    // How far a point on the cloth has moved right now (u, v = 0-1 across and up the banner)
    public Vector2 Offset(float u, float v) => Wave(u, v, Now, out _);

    Vector2 Wave(float u, float v, float t, out float light)
    {
        float down = 1f - v; // 0 = top, 1 = bottom

        // How free the cloth is here: 0 at the rod, easing up to 1 at the bottom
        float free = Mathf.Clamp01((down - pinned) / (1f - pinned));
        free = free * free * (3f - 2f * free);

        // Slow sway of the whole banner, with an uneven second rhythm
        float swing = Mathf.Sin(t * swingSpeed) * swingAmount
                    + Mathf.Sin(t * swingSpeed * 2.3f + 1.7f) * swingAmount * 0.3f;

        // A fold: a wave running across the cloth, slanting downward
        float phase = (u * ripplesAcross + down * ripplesDown) * Mathf.PI * 2f - t * windSpeed;
        float fold = Mathf.Sin(phase);
        float slope = Mathf.Cos(phase); // which way this part of the fold faces the light

        light = 1f - foldShade * (0.5f + 0.5f * slope) * Mathf.Lerp(0.3f, 1f, free);
        return new Vector2(
            free * (swing + fold * rippleAmount),
            free * (1f - Mathf.Abs(slope)) * liftAmount * down);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (r.width <= 0f || r.height <= 0f) return;

        Vector4 uv = sprite != null ? UnityEngine.Sprites.DataUtility.GetOuterUV(sprite) : new Vector4(0f, 0f, 1f, 1f);
        float t = Now;
        Color32 baseColor = color;

        for (int y = 0; y <= rows; y++)
        {
            float v = y / (float)rows;
            for (int x = 0; x <= columns; x++)
            {
                float u = x / (float)columns;
                Vector2 off = Wave(u, v, t, out float light);

                var c = baseColor;
                c.r = (byte)(c.r * light);
                c.g = (byte)(c.g * light);
                c.b = (byte)(c.b * light);

                vh.AddVert(
                    new Vector3(r.xMin + u * r.width + off.x, r.yMin + v * r.height + off.y),
                    c,
                    new Vector2(Mathf.Lerp(uv.x, uv.z, u), Mathf.Lerp(uv.y, uv.w, v)));
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