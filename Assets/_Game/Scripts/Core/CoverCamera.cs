using UnityEngine;

// Scales a background sprite to fit the camera view, like CSS "background-size".
//   Cover   = fill the whole screen (crops the overflow)
//   Contain = show the whole image (leaves empty space at the sides or top)
// focusX / focusY choose which part of the image stays in view when cropping:
//   focusX: 0 = left edge, 0.5 = center, 1 = right edge
//   focusY: 0 = top,       0.5 = center, 1 = bottom
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class CoverCamera : MonoBehaviour
{
    public enum Fit { Cover, Contain }
    public Fit fit = Fit.Cover;

    [Range(0f, 1f)] public float focusX = 0.5f;
    [Range(0f, 1f)] public float focusY = 0.25f;
    [Min(0.1f)] public float extraZoom = 1f;   // above 1 zooms in slightly

    SpriteRenderer sr;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (cam == null || sr.sprite == null) return;

        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;
        Vector2 size = sr.sprite.bounds.size; // sprite size before scaling

        float scale = (fit == Fit.Cover
            ? Mathf.Max(viewW / size.x, viewH / size.y)   // fill: crop the overflow
            : Mathf.Min(viewW / size.x, viewH / size.y))  // fit: show everything
            * extraZoom;
        transform.localScale = new Vector3(scale, scale, 1f);

        // Slide the overflow so the chosen focus point stays on screen
        float overX = size.x * scale - viewW;
        float overY = size.y * scale - viewH;
        Vector3 c = cam.transform.position;
        transform.position = new Vector3(
            c.x + overX * (0.5f - focusX),
            c.y - overY * (0.5f - focusY),
            transform.position.z);
    }
}