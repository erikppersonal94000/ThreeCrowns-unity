using UnityEngine;
using UnityEngine.UI;

// A UI rectangle that shows part of a texture and fades smoothly along its
// left and/or right edge. Used for the title panels.
[RequireComponent(typeof(CanvasRenderer))]
public class EdgeFadeGraphic : MaskableGraphic
{
    [SerializeField] Texture texture;
    [SerializeField] Rect uvRect = new Rect(0f, 0f, 1f, 1f); // which part of the texture fills this rectangle
    [SerializeField] float fadeLeft = 0f;                     // width of the fade on the left edge
    [SerializeField] float fadeRight = 0f;                    // width of the fade on the right edge
    const int Steps = 16;                                     // smoothness of each fade

    public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

    public void Set(Texture tex, Rect uv, float left, float right)
    {
        if (tex != texture)
        {
            texture = tex;
            SetMaterialDirty();
        }
        uvRect = uv;
        fadeLeft = left;
        fadeRight = right;
        SetVerticesDirty();
    }

    // Builds the rectangle as vertical strips: see-through at a faded edge,
    // easing to solid across the fade width.
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0f || r.height <= 0f) return;

        // If both fades don't fit, shrink them to share the width
        float fl = Mathf.Max(0f, fadeLeft), fr = Mathf.Max(0f, fadeRight);
        if (fl + fr > r.width)
        {
            float k = r.width / (fl + fr);
            fl *= k;
            fr *= k;
        }

        Color32 baseColor = color;
        int columns = 0;

        void AddColumn(float x, float alpha)
        {
            float t = (x - r.xMin) / r.width;
            float u = uvRect.xMin + uvRect.width * t;
            var c = baseColor;
            c.a = (byte)Mathf.RoundToInt(baseColor.a * alpha);
            vh.AddVert(new Vector3(x, r.yMin), c, new Vector2(u, uvRect.yMin));
            vh.AddVert(new Vector3(x, r.yMax), c, new Vector2(u, uvRect.yMax));
            if (columns > 0)
            {
                int b = (columns - 1) * 2;
                vh.AddTriangle(b, b + 1, b + 3);
                vh.AddTriangle(b, b + 3, b + 2);
            }
            columns++;
        }

        static float Smooth(float s) => s * s * (3f - 2f * s);

        // Left edge: fade in
        if (fl > 0f)
            for (int i = 0; i <= Steps; i++)
            {
                float s = i / (float)Steps;
                AddColumn(r.xMin + fl * s, Smooth(s));
            }
        else
            AddColumn(r.xMin, 1f);

        // Right edge: fade out
        if (fr > 0f)
            for (int i = 0; i <= Steps; i++)
            {
                float s = i / (float)Steps;
                AddColumn(r.xMax - fr + fr * s, Smooth(1f - s));
            }
        else
            AddColumn(r.xMax, 1f);
    }
}