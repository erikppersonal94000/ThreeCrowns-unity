using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The Duskmoor throne room. The background pans gently toward the mouse, and each part of the room
// (well, war table, archive, throne, door) glows and shows its name when hovered, and acts when clicked.
// Put it on the background sprite (the object with CoverCamera).
[RequireComponent(typeof(SpriteRenderer))]
public class ThroneRoom : MonoBehaviour
{
    [System.Serializable]
    public class Zone
    {
        public string name;
        public string subtitle;
        [Tooltip("Area in the image, 0-1: x = left, y = top, z = right, w = bottom.")]
        public Vector4 area;
        public Color glow = Color.white;
        [Tooltip("Scene to travel to when clicked. Leave empty to use On Click instead.")]
        public string sceneToLoad;
        public UnityEvent onClick = new UnityEvent();
        [HideInInspector] public float hover;
        [HideInInspector] public SpriteRenderer glowRenderer;

        public Zone() { }
        public Zone(string n, string s, Vector4 a, Color g, string scene = "")
        {
            name = n; subtitle = s; area = a; glow = g; sceneToLoad = scene;
        }
    }

    [Tooltip("Checked from top to bottom, so where areas overlap the first one wins.")]
    public List<Zone> zones = new List<Zone>
    {
        new Zone("The Soul Well", "Souls gathered from the fallen",
                 new Vector4(0.48f, 0.57f, 0.66f, 1f), new Color(0.624f, 0.961f, 0.784f, 1f)),          // #9FF5C8
        new Zone("The War Table", "Choose your next battle",
                 new Vector4(0.73f, 0.49f, 1f, 0.9f), new Color(1f, 0.769f, 0.42f, 1f)),                // #FFC46B
        new Zone("The Bone Archive", "Raise the souls you have taken",
                 new Vector4(0f, 0.09f, 0.33f, 0.74f), new Color(0.608f, 0.42f, 0.878f, 1f)),          // #9B6BE0
        new Zone("The Throne", "Morvane the Bone-Caller",
                 new Vector4(0.34f, 0.13f, 0.62f, 0.75f), new Color(0.541f, 0.31f, 0.847f, 1f)),       // #8A4FD8
        new Zone("The Great Door", "Return to the kingdoms",
                 new Vector4(0.645f, 0.22f, 0.79f, 0.63f), new Color(0.627f, 0.706f, 0.816f, 1f), "Title"), // #A0B4D0
    };

    [Header("Entrance")]
    [Tooltip("How big the room looks through the doorway before you walk in (1 = normal). Lower = farther away.")]
    public float entranceZoom = 0.7f;
    [Tooltip("Seconds to settle to normal after walking in.")]
    public float entranceTime = 0.6f;

    [Header("Pan")]
    [Tooltip("1 = the view slides all the way to each edge as the mouse reaches that side.")]
    [Range(0f, 1f)] public float panStrength = 1f;
    public float panSpeed = 3f;

    [Header("Hover")]
    [Range(0f, 1f)] public float glowStrength = 0.22f;
    [Tooltip("Shows every area's glow faintly, to check where they are.")]
    public bool showZones;

    [Header("Label")]
    public Canvas uiCanvas;
    public TMP_FontAsset headerFont;
    public TMP_FontAsset textFont;
    public float labelLift = 16f;

    [Header("Sound")]
    public AudioClip hoverSound;
    public AudioClip clickSound;

    SpriteRenderer bg;
    CoverCamera cover;
    Camera cam, uiCam;
    int hovered = -1, shownZone = -1;
    float comingSoonUntil;

    RectTransform labelRT;
    CanvasGroup labelGroup;
    PlateFrame labelFrame;
    TextMeshProUGUI labelName, labelSub;

    void Start()
    {
        bg = GetComponent<SpriteRenderer>();
        cover = GetComponent<CoverCamera>();
        cam = Camera.main;
        if (uiCanvas == null) uiCanvas = FindAnyObjectByType<Canvas>();
        if (uiCanvas != null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay) uiCam = uiCanvas.worldCamera;

        foreach (var z in zones) z.glowRenderer = MakeGlow(z);
        if (uiCanvas != null) BuildLabel();

        if (SceneLoader.EnteredThroughDoor && cover != null)
        {
            SceneLoader.EnteredThroughDoor = false;
            StartCoroutine(StepIn());
        }
    }

    // Arrive slightly zoomed in, then settle back as the room fades in, like stepping inside
    IEnumerator StepIn()
    {
        var loader = SceneLoader.Instance;
        cover.extraZoom = entranceZoom;
        while (loader != null && loader.IsBusy && !loader.Revealing) yield return null;
        for (float t = 0f; t < entranceTime; t += Time.unscaledDeltaTime)
        {
            float k = t / entranceTime;
            float e = 1f - (1f - k) * (1f - k) * (1f - k);
            cover.extraZoom = Mathf.Lerp(entranceZoom, 1f, e);
            yield return null;
        }
        cover.extraZoom = 1f;
    }

    SpriteRenderer MakeGlow(Zone z)
    {
        var go = new GameObject(z.name + " Glow");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RoomFx.GlowSprite;
        sr.sharedMaterial = bg.sharedMaterial;
        sr.sortingLayerID = bg.sortingLayerID;
        sr.sortingOrder = bg.sortingOrder + 5;

        Bounds b = bg.sprite.bounds;
        Vector2 c = RoomFx.ImageToLocal(bg, (z.area.x + z.area.z) * 0.5f, (z.area.y + z.area.w) * 0.5f);
        go.transform.localPosition = new Vector3(c.x, c.y, 0f);
        go.transform.localScale = new Vector3((z.area.z - z.area.x) * b.size.x * 1.3f,
                                              (z.area.w - z.area.y) * b.size.y * 1.3f, 1f);
        sr.color = new Color(z.glow.r, z.glow.g, z.glow.b, 0f);
        return sr;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null || bg == null || bg.sprite == null) return;
        Vector2 mp = mouse.position.ReadValue();
        float dt = Time.deltaTime;

        // Pan toward the mouse
        if (cover != null)
        {
            float t = Mathf.Clamp01(mp.x / Mathf.Max(1f, Screen.width));
            float target = 0.5f + (t - 0.5f) * panStrength;
            // Hold the center view during a transition, so the door video's last frame lines up
            if (SceneLoader.Instance != null && SceneLoader.Instance.IsBusy) target = 0.5f;
            cover.focusX = Mathf.Lerp(cover.focusX, target, 1f - Mathf.Exp(-panSpeed * dt));
        }

        // Which area is under the mouse? (not while the mouse is over a UI button)
        int prev = hovered;
        hovered = -1;
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (!overUI && Application.isFocused)
        {
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(mp.x, mp.y, transform.position.z - cam.transform.position.z));
            Vector2 img = RoomFx.LocalToImage(bg, transform.InverseTransformPoint(world));
            for (int i = 0; i < zones.Count; i++)
            {
                Vector4 a = zones[i].area;
                if (img.x >= a.x && img.x <= a.z && img.y >= a.y && img.y <= a.w) { hovered = i; break; }
            }
        }
        if (hovered != prev && hovered >= 0) Play(hoverSound, 0.5f);

        // Glows
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            z.hover = Mathf.MoveTowards(z.hover, i == hovered ? 1f : 0f, dt * 5f);
            float a = glowStrength * Mathf.Max(z.hover * pulse, showZones ? 0.6f : 0f);
            if (z.glowRenderer != null) z.glowRenderer.color = new Color(z.glow.r, z.glow.g, z.glow.b, a);
        }

        if (labelRT != null) UpdateLabel(dt);

        if (hovered >= 0 && mouse.leftButton.wasPressedThisFrame) Click(zones[hovered]);
    }

    void Click(Zone z)
    {
        Play(clickSound, 1f);
        if (!string.IsNullOrEmpty(z.sceneToLoad))
        {
            if (SceneLoader.Instance) SceneLoader.Instance.GoTo(z.sceneToLoad);
            return;
        }
        if (z.onClick != null && z.onClick.GetPersistentEventCount() > 0)
        {
            z.onClick.Invoke();
            return;
        }
        comingSoonUntil = Time.time + 1.5f; // nothing hooked up yet
    }

    void Play(AudioClip clip, float volume)
    {
        if (clip != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(clip, volume);
    }

    /* ---------- hover label ---------- */

    void BuildLabel()
    {
        labelRT = NewRect("Room Label", uiCanvas.transform);
        labelRT.anchorMin = labelRT.anchorMax = new Vector2(0.5f, 0.5f);
        labelRT.pivot = new Vector2(0.5f, 0f); // sits on top of the point it's placed at
        labelRT.sizeDelta = new Vector2(360f, 96f);
        labelGroup = labelRT.gameObject.AddComponent<CanvasGroup>();
        labelGroup.alpha = 0f;
        labelGroup.blocksRaycasts = false;
        labelGroup.interactable = false;

        var frameRT = NewRect("Frame", labelRT);
        Stretch(frameRT);
        labelFrame = frameRT.gameObject.AddComponent<PlateFrame>();
        labelFrame.raycastTarget = false;
        labelFrame.glow = 0.4f;

        var nameRT = NewRect("Name", labelRT);
        nameRT.anchorMin = new Vector2(0f, 1f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.pivot = new Vector2(0.5f, 1f);
        nameRT.sizeDelta = new Vector2(0f, 44f);
        nameRT.anchoredPosition = new Vector2(0f, -12f);
        labelName = AddText(nameRT, "", headerFont, 30f, new Color(0.949f, 0.933f, 0.965f, 1f)); // #F2EEF6
        labelName.characterSpacing = 3f;

        var subRT = NewRect("Subtitle", labelRT);
        subRT.anchorMin = new Vector2(0f, 0f);
        subRT.anchorMax = new Vector2(1f, 0f);
        subRT.pivot = new Vector2(0.5f, 0f);
        subRT.sizeDelta = new Vector2(0f, 34f);
        subRT.anchoredPosition = new Vector2(0f, 12f);
        labelSub = AddText(subRT, "", textFont, 22f, new Color(0.788f, 0.749f, 0.839f, 1f)); // #C9BFD6
    }

    void UpdateLabel(float dt)
    {
        labelGroup.alpha = Mathf.MoveTowards(labelGroup.alpha, hovered >= 0 ? 1f : 0f, dt * 6f);
        if (hovered < 0) return;

        var z = zones[hovered];
        string sub = Time.time < comingSoonUntil ? "Coming soon" : z.subtitle;
        if (shownZone != hovered || labelSub.text != sub)
        {
            shownZone = hovered;
            labelName.text = z.name;
            labelSub.text = sub;
            float w = Mathf.Max(labelName.GetPreferredValues(z.name).x, labelSub.GetPreferredValues(sub).x) + 64f;
            labelRT.sizeDelta = new Vector2(w, 96f);
            labelFrame.accent = z.glow;
            labelFrame.SetVerticesDirty();
        }

        // Place it just above the top-center of the area, kept on screen
        Vector2 local = RoomFx.ImageToLocal(bg, (z.area.x + z.area.z) * 0.5f, z.area.y);
        Vector2 screen = cam.WorldToScreenPoint(transform.TransformPoint(local));
        var canvasRT = (RectTransform)uiCanvas.transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screen, uiCam, out Vector2 pos)) return;
        pos.y += labelLift;
        Rect r = canvasRT.rect;
        Vector2 size = labelRT.sizeDelta;
        pos.x = Mathf.Clamp(pos.x, r.xMin + size.x * 0.5f + 20f, r.xMax - size.x * 0.5f - 20f);
        pos.y = Mathf.Clamp(pos.y, r.yMin + 20f, r.yMax - size.y - 20f);
        labelRT.localPosition = pos;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI AddText(RectTransform rt, string text, TMP_FontAsset font, float size, Color color)
    {
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }
}

// Shared helpers for the throne room: a soft glow sprite, and conversion between
// image coordinates (0-1 from the top-left of the picture) and the background's local space.
public static class RoomFx
{
    static Sprite glow;
    public static Sprite GlowSprite => glow != null ? glow : (glow = MakeGlow());

    static Sprite MakeGlow()
    {
        const int S = 128;
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
            float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
            float r = Mathf.Sqrt(u * u + v * v);
            float a = r >= 1f ? 0f : Mathf.Pow(1f - r, 2f);
            px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }

    public static Vector2 ImageToLocal(SpriteRenderer bg, float u, float vFromTop)
    {
        Bounds b = bg.sprite.bounds;
        return new Vector2(b.min.x + u * b.size.x, b.max.y - vFromTop * b.size.y);
    }

    public static Vector2 LocalToImage(SpriteRenderer bg, Vector2 local)
    {
        Bounds b = bg.sprite.bounds;
        return new Vector2((local.x - b.min.x) / b.size.x, (b.max.y - local.y) / b.size.y);
    }
}