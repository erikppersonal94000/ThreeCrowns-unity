using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Draws a copy of a title face's letters in the effect's own colors, right on top of the face.
// This is how the effects tint the letters themselves (heat glow, soaking, frost, shine).
public class FaceOverlay : MaskableGraphic
{
    TMP_Text face;
    TitleFx owner;
    Material mat;
    int[] vertChar = new int[0];

    static readonly List<Vector3> verts = new List<Vector3>();
    static readonly List<Vector4> uv0 = new List<Vector4>();
    static readonly List<Vector4> uv1 = new List<Vector4>();
    static readonly List<Vector3> normals = new List<Vector3>();
    static readonly List<Vector4> tangents = new List<Vector4>();
    static readonly List<Color32> colors = new List<Color32>();
    static readonly List<int> tris = new List<int>();

    public void Setup(TMP_Text f, TitleFx fx)
    {
        face = f;
        owner = fx;
        raycastTarget = false;
        mat = new Material(f.fontSharedMaterial)
        {
            name = f.fontSharedMaterial.name + " (overlay)",
            hideFlags = HideFlags.DontSave
        };
        mat.DisableKeyword("UNDERLAY_ON");
        mat.DisableKeyword("UNDERLAY_INNER");
        mat.DisableKeyword("GLOW_ON");
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        mat.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        material = mat;
    }

    public override Texture mainTexture => mat != null ? mat.mainTexture : null;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (face == null || owner == null) return;
        Mesh m = face.mesh;
        if (m == null || m.vertexCount == 0) return;

        m.GetVertices(verts);
        m.GetUVs(0, uv0);
        m.GetUVs(1, uv1);
        m.GetNormals(normals);
        m.GetTangents(tangents);
        m.GetColors(colors);
        m.GetTriangles(tris, 0);
        MapCharacters(verts.Count);

        float rendererAlpha = face.canvasRenderer.GetAlpha();
        for (int i = 0; i < verts.Count; i++)
        {
            Color32 faceColor = i < colors.Count ? colors[i] : new Color32(255, 255, 255, 255);
            int ci = vertChar[i];
            Color32 c = ci < 0 ? new Color32(0, 0, 0, 0) : owner.OverlayColor(ci, i & 3, verts[i]);
            c.a = (byte)Mathf.Clamp(c.a * (faceColor.a / 255f) * rendererAlpha, 0f, 255f);
            if (owner.debug && ci >= 0) c = new Color32(255, 255, 255, 170);

            vh.AddVert(new UIVertex
            {
                position = verts[i],
                color = c,
                uv0 = i < uv0.Count ? uv0[i] : Vector4.zero,
                uv1 = i < uv1.Count ? uv1[i] : Vector4.zero,
                normal = i < normals.Count ? normals[i] : Vector3.back,
                tangent = i < tangents.Count ? tangents[i] : new Vector4(-1f, 0f, 0f, 1f)
            });
        }
        for (int i = 0; i + 2 < tris.Count; i += 3) vh.AddTriangle(tris[i], tris[i + 1], tris[i + 2]);
    }

    void MapCharacters(int count)
    {
        if (vertChar.Length < count) vertChar = new int[count];
        for (int i = 0; i < count; i++) vertChar[i] = -1;
        var info = face.textInfo;
        for (int c = 0; c < info.characterCount; c++)
        {
            var ci = info.characterInfo[c];
            if (!ci.isVisible || ci.materialReferenceIndex != 0) continue;
            for (int k = 0; k < 4 && ci.vertexIndex + k < count; k++) vertChar[ci.vertexIndex + k] = c;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (mat != null) Destroy(mat);
    }
}