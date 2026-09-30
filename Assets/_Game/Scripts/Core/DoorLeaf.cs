using UnityEngine;
using UnityEngine.UI;

// One of the great doors, drawn as vertical strips so it can swing open in perspective:
// the far edge moves away from the viewer, shrinking toward the eye point and darkening.
// The door's thickness shows as a dark wooden edge as it turns.
[RequireComponent(typeof(CanvasRenderer))]
public class DoorLeaf : MaskableGraphic
{
    public Texture texture;
    [Tooltip("Hinge edge and free edge of the door, as fractions of the picture's width.")]
    public float hingeU, freeU;
    [Tooltip("Bottom and top of the door, as fractions of the picture's height.")]
    public float bottomV, topV;
    [Tooltip("The viewer's eye point (0-1 of the picture). Perspective shrinks toward it.")]
    public Vector2 eye = new Vector2(0.5f, 0.45f);
    [Range(0f, 1f)] public float open;
    public float maxAngle = 85f;
    [Tooltip("Lower = stronger perspective.")]
    public float perspective = 1.1f;
    [Tooltip("Door thickness, as a fraction of the picture's width.")]
    public float thickness = 0.012f;
    [Tooltip("Light from inside the hall catching the door's inner edge.")]
    public Color rimColor = new Color(0.722f, 0.549f, 1f, 1f); // #B88CFF
    public int strips = 24;

    public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

    public void SetOpen(float value)
    {
        open = value;
        SetVerticesDirty();
    }

    Rect r;
    Vector2 eyeP;
    float F;

    // A point on the door in 3D (x across, z into the doorway, y up) projected onto the screen
    Vector2 Project(float x, float z, float y)
    {
        float s = F / (F + z);
        return new Vector2(eyeP.x + (x - eyeP.x) * s, eyeP.y + (y - eyeP.y) * s);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (texture == null) return;

        r = rectTransform.rect;
        float W = r.width, H = r.height;
        float theta = Mathf.Clamp01(open) * maxAngle * Mathf.Deg2Rad;
        float dir = Mathf.Sign(freeU - hingeU);
        float leafW = Mathf.Abs(freeU - hingeU) * W;
        float hingeX = r.xMin + hingeU * W;
        float yB = r.yMin + bottomV * H, yT = r.yMin + topV * H;
        eyeP = new Vector2(r.xMin + eye.x * W, r.yMin + eye.y * H);
        F = perspective * W;
        float cos = Mathf.Cos(theta), sin = Mathf.Sin(theta);

        // Front face, as strips
        for (int i = 0; i <= strips; i++)
        {
            float t = i / (float)strips;                  // 0 at the hinge, 1 at the free edge
            float x3 = hingeX + dir * t * leafW * cos;
            float z = t * leafW * sin;                    // how far this part has swung away
            Vector2 b = Project(x3, z, yB), top = Project(x3, z, yT);
            float u = Mathf.Lerp(hingeU, freeU, t);
            // Turns away from the torchlight outside, but only darkens a little so the wood stays visible
            float shade = 1f - 0.4f * sin * (0.3f + 0.7f * t);
            // The inner edge catches the light from inside the hall
            float rim = sin * Mathf.Pow(t, 5f) * 0.55f;
            Color lit = new Color(color.r * shade, color.g * shade, color.b * shade, color.a);
            Color32 c = Color.Lerp(lit, new Color(rimColor.r, rimColor.g, rimColor.b, color.a), rim);

            vh.AddVert(b, c, new Vector2(u, bottomV));
            vh.AddVert(top, c, new Vector2(u, topV));
            if (i > 0)
            {
                int k = i * 2;
                vh.AddTriangle(k - 2, k - 1, k + 1);
                vh.AddTriangle(k + 1, k, k - 2);
            }
        }

        // The free edge's thickness: a dark strip of wood that shows as the door turns
        if (thickness > 0f && theta > 0.01f)
        {
            float tk = thickness * W;
            float fx = hingeX + dir * leafW * cos, fz = leafW * sin;   // front of the free edge
            float bx = fx - dir * sin * tk, bz = fz + cos * tk;        // back of the free edge
            float u = freeU - dir * 0.003f;                            // a column of wood just inside the edge
            Color32 c = new Color(color.r * 0.35f, color.g * 0.32f, color.b * 0.38f, color.a);
            int v = vh.currentVertCount;
            vh.AddVert(Project(fx, fz, yB), c, new Vector2(u, bottomV));
            vh.AddVert(Project(fx, fz, yT), c, new Vector2(u, topV));
            vh.AddVert(Project(bx, bz, yT), c, new Vector2(u, topV));
            vh.AddVert(Project(bx, bz, yB), c, new Vector2(u, bottomV));
            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v + 2, v + 3, v);
        }
    }
}