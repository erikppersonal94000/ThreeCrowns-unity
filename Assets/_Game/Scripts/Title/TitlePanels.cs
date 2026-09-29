using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Title screen: three kingdom panels that blend into each other.
// Hovering a panel widens it and dims the others. Panels fade in one by one,
// drift slowly (Ken Burns) and shift a little with the mouse (parallax).
// Everything is built in code when you press Play.
[RequireComponent(typeof(RectTransform))]
public class TitlePanels : MonoBehaviour
{
    [Serializable]
    public class Kingdom
    {
        public string id = "dark";
        public Sprite image;
        [Tooltip("Which part of the image stays in view. 0 = left/top, 1 = right/bottom.")]
        [Range(0f, 1f)] public float focusX = 0.5f;
        [Range(0f, 1f)] public float focusY = 0.3f;
        [Range(0f, 1.5f)] public float brightness = 0.7f;
        [Tooltip("Color laid over the image. Alpha = strength.")]
        public Color tint = new Color(0.3f, 0.12f, 0.47f, 0.25f);
    }

    [SerializeField] Kingdom[] kingdoms = new Kingdom[3];

    [Header("Layout")]
    [SerializeField] float fadeWidth = 260f;                 // soft blend between panels (in 1920x1080 pixels)
    [SerializeField] float focusWeight = 1.8f;               // how much wider the hovered panel gets
    [SerializeField] float layoutSpeed = 5f;                 // how fast panels resize and dim
    [SerializeField, Range(0f, 1f)] float dimAmount = 0.5f;  // brightness of the panels you're NOT hovering

    [Header("Intro")]
    [SerializeField] float[] introDelays = { 0.2f, 0.8f, 1.4f };
    [SerializeField] float introFadeTime = 1.8f;
    [SerializeField] float readyTime = 2.1f;                 // hover works after this

    [Header("Motion")]
    [SerializeField] float driftPeriod = 30f;                // seconds for one slow zoom in and out
    [SerializeField] float driftZoom = 0.08f;
    [SerializeField] Vector2 parallax = new Vector2(14f, 8f);

    [Header("Idle")]
    [SerializeField] float idleSeconds = 3.5f;

    public int Focus { get; private set; } = -1;             // -1 = no kingdom focused
    public bool IntroDone { get; private set; }
    public bool HoldFocus { get; set; }                      // plates set this while the pointer is on a button
    public event Action<int> FocusChanged;
    public event Action IntroFinished;

    class Panel
    {
        public RectTransform rect;
        public CanvasGroup group;
        public EdgeFadeGraphic image, tint, shade;
        public float fadeRight;
        public float weight = 1f;
        public float dim = 1f;
    }

    readonly List<Panel> panels = new();
    float[] boundaries;
    float[] centers;

    // Center of a kingdom's panel, measured from the middle of the screen (in 1920x1080 units)
    public float PanelCenterX(int i) => centers != null && i >= 0 && i < centers.Length ? centers[i] : 0f;    RectTransform root;
    Canvas canvas;
    float startTime;
    Vector2 lastMouse;
    float lastMoveTime = -999f;
    Vector2 parallaxNow;

    // ---------- Build ----------
    void Awake()
    {
        root = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        boundaries = new float[Mathf.Max(0, kingdoms.Length - 1)];
        centers = new float[kingdoms.Length];
        for (int i = 0; i < kingdoms.Length; i++)
            panels.Add(BuildPanel(kingdoms[i], i == kingdoms.Length - 1));
    }

    void Start() => startTime = Time.time;

    Panel BuildPanel(Kingdom k, bool isLast)
    {
        var p = new Panel();
        p.fadeRight = isLast ? 0f : fadeWidth; // the last panel runs off the screen, no right fade needed

        var go = new GameObject($"Panel_{k.id}", typeof(RectTransform), typeof(CanvasGroup));
        p.rect = (RectTransform)go.transform;
        p.rect.SetParent(root, false);
        p.rect.anchorMin = new Vector2(0f, 0f);
        p.rect.anchorMax = new Vector2(0f, 1f);
        p.rect.pivot = new Vector2(0f, 0.5f);
        p.rect.sizeDelta = Vector2.zero;
        p.group = go.GetComponent<CanvasGroup>();
        p.group.alpha = 0f;
        p.group.blocksRaycasts = false; // panels never block clicks

        Texture tex = null;
        if (k.image != null)
        {
            tex = k.image.texture;
            tex.wrapMode = TextureWrapMode.Clamp; // never tile the image at its edges
        }

        p.image = MakeLayer(p.rect, "Image", tex, Color.white, p.fadeRight);
        p.tint  = MakeLayer(p.rect, "Tint", null, k.tint, p.fadeRight);                 // kingdom color
        p.shade = MakeLayer(p.rect, "Shade", ShadeTexture(), Color.white, p.fadeRight); // dark top and bottom
        return p;
    }

    EdgeFadeGraphic MakeLayer(RectTransform parent, string name, Texture tex, Color color, float fadeRight)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(EdgeFadeGraphic));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var g = go.GetComponent<EdgeFadeGraphic>();
        g.raycastTarget = false;
        g.color = color;
        g.Set(tex, new Rect(0f, 0f, 1f, 1f), fadeWidth, fadeRight);
        return g;
    }

    // Vertical gradient: dark at the top, clear through the middle, darkest at the bottom
    static Texture2D shadeTex;
    static Texture2D ShadeTexture()
    {
        if (shadeTex) return shadeTex;
        const int h = 256;
        shadeTex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var dark = new Color(11 / 255f, 9 / 255f, 20 / 255f);
        for (int y = 0; y < h; y++)
        {
            float t = y / (h - 1f); // 0 = bottom, 1 = top
            float a = t < 0.5f
                ? Mathf.Lerp(0.92f, 0f, t / 0.5f)                        // bottom up to the middle
                : Mathf.Lerp(0f, 0.55f, Mathf.InverseLerp(0.7f, 1f, t)); // clear, then darker toward the top
            shadeTex.SetPixel(0, y, new Color(dark.r, dark.g, dark.b, a));
        }
        shadeTex.Apply();
        return shadeTex;
    }

    // ---------- Every frame ----------
    void Update()
    {
        float t = Time.time - startTime;
        float blend = 1f - Mathf.Exp(-layoutSpeed * Time.deltaTime); // smooth, frame-rate independent

        // Intro: panels fade in one after another
        for (int i = 0; i < panels.Count; i++)
        {
            float delay = i < introDelays.Length ? introDelays[i] : 0f;
            float x = Mathf.Clamp01((t - delay) / introFadeTime);
            panels[i].group.alpha = x * x * (3f - 2f * x);
        }
        if (!IntroDone && t >= readyTime)
        {
            IntroDone = true;
            IntroFinished?.Invoke();
        }

        UpdateFocus();

        // Widen the focused panel, dim the others
        for (int i = 0; i < panels.Count; i++)
        {
            var p = panels[i];
            p.weight = Mathf.Lerp(p.weight, Focus == i ? focusWeight : 1f, blend);
            p.dim = Mathf.Lerp(p.dim, (Focus >= 0 && Focus != i) ? dimAmount : 1f, blend);
            float b = kingdoms[i].brightness * p.dim;
            p.image.color = new Color(b, b, b, 1f);
        }

        // Mouse parallax (smoothed)
        var mouse = Mouse.current;
        if (mouse != null && Screen.width > 0)
        {
            Vector2 m = mouse.position.ReadValue();
            Vector2 target = new Vector2(
                (m.x / Screen.width - 0.5f) * -parallax.x,
                (m.y / Screen.height - 0.5f) * -parallax.y);
            parallaxNow = Vector2.Lerp(parallaxNow, target, 1f - Mathf.Exp(-3f * Time.deltaTime));
        }

        Layout(t);
    }

    void UpdateFocus()
    {
        var mouse = Mouse.current;
        if (!IntroDone || mouse == null) return;

        Vector2 pos = mouse.position.ReadValue();
        if ((pos - lastMouse).sqrMagnitude > 4f)
        {
            lastMouse = pos;
            lastMoveTime = Time.time;
        }

        bool inWindow = Application.isFocused &&
                        pos.x >= 0f && pos.y >= 0f && pos.x <= Screen.width && pos.y <= Screen.height;
        bool idle = idleSeconds > 0f && Time.time - lastMoveTime > idleSeconds; // 0 = never go idle

        int hovered = (inWindow && !idle) ? PanelAt(pos) : -1;
        if (hovered == -1 && HoldFocus) return; // keep the kingdom up while the pointer rests on its button
        SetFocus(hovered);
    }

    void SetFocus(int f)
    {
        if (f == Focus) return;
        Focus = f;
        FocusChanged?.Invoke(f);
    }

    int PanelAt(Vector2 screenPos)
    {
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPos, cam, out var local)) return -1;
        float x = local.x - root.rect.xMin;
        for (int i = 0; i < boundaries.Length; i++)
            if (x < boundaries[i]) return i;
        return panels.Count - 1;
    }

    // ---------- Layout ----------
    void Layout(float t)
    {
        float W = root.rect.width, H = root.rect.height;
        float total = 0f;
        foreach (var p in panels) total += p.weight;

        float x = 0f;
        for (int i = 0; i < panels.Count; i++)
        {
            var p = panels[i];
            float left = x;
            float right = x + W * p.weight / total;
            if (i < boundaries.Length) boundaries[i] = right;
            centers[i] = (left + right) * 0.5f - W * 0.5f;

            // Each panel overlaps the next one. Its right edge fades out just past the point
            // where the next panel is fully solid, so it's hidden once that panel is in,
            // and during the intro it fades softly into darkness.
            float visLeft = i == 0 ? -fadeWidth * 3f : left - fadeWidth * 0.5f;
            float visRight = i == panels.Count - 1 ? W + fadeWidth * 3f : right + fadeWidth * 1.5f;
            float panelW = visRight - visLeft;
            p.rect.anchoredPosition = new Vector2(visLeft, 0f);
            p.rect.sizeDelta = new Vector2(panelW, 0f);

            PlaceImage(p, kingdoms[i], i, W, H, panelW, (left + right) * 0.5f - visLeft, t);
            x = right;
        }
    }

    // Size the image to cover the widest the panel can get, so widening reveals more
    // of the picture instead of zooming it. Then apply focus point, drift and parallax,
    // and tell the panel which part of the image to show.
    void PlaceImage(Panel p, Kingdom k, int i, float W, float H, float panelW, float centerX, float t)
    {
        if (k.image == null) return;

        float maxW = W * focusWeight / (focusWeight + panels.Count - 1) + fadeWidth * 3f; // room for both fades
        float sw = k.image.rect.width, sh = k.image.rect.height;
        float phase = (t + i * driftPeriod / 3f) / driftPeriod * Mathf.PI * 2f;
        float zoom = 1f + driftZoom * (0.5f - 0.5f * Mathf.Cos(phase));
        float scale = Mathf.Max(maxW / sw, H / sh) * 1.04f * zoom; // 4% spare room for parallax

        float iw = sw * scale, ih = sh * scale;
        float overX = iw - maxW, overY = ih - H;

        // Image center, measured from the panel's left edge and bottom edge
        float cx = centerX + overX * (0.5f - k.focusX) + parallaxNow.x;
        float cy = H * 0.5f - overY * (0.5f - k.focusY) + parallaxNow.y;

        var uv = new Rect(
            -(cx - iw * 0.5f) / iw,
            -(cy - ih * 0.5f) / ih,
            panelW / iw,
            H / ih);
        p.image.Set(k.image.texture, uv, fadeWidth, p.fadeRight);
    }
}