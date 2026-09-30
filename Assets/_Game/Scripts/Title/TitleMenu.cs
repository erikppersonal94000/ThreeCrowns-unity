using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Settings and Quit on the title screen: two small buttons in the bottom-right corner,
// and a Settings panel with volume sliders, display mode and resolution.
// Put it on UICanvas. It builds its own UI when the game starts.
public class TitleMenu : MonoBehaviour
{
    [Header("References")]
    public TitlePanels panels;

    [Header("Fonts")]
    [Tooltip("Buttons, labels and values (the italic tagline font).")]
    public TMP_FontAsset textFont;
    [Tooltip("The Settings heading and the BACK button.")]
    public TMP_FontAsset headerFont;

    [Header("Colors")]
    public Color textColor = new Color(0.788f, 0.749f, 0.682f, 1f);   // #C9BFAE
    public Color hoverColor = Color.white;                            // #FFFFFF
    public Color accent = new Color(0.788f, 0.643f, 0.361f, 1f);      // #C9A45C
    public Color dimColor = new Color(0f, 0f, 0f, 0.6f);              // black, 60%

    [Header("Sizes")]
    public Vector2 cornerMargin = new Vector2(48f, 36f);
    public float cornerTextSize = 30f;
    public float cornerSpacing = 36f;
    public float rowTextSize = 30f;

    [Header("Sound")]
    [Tooltip("Optional short click, played through AudioManager's Effects volume.")]
    public AudioClip clickSound;
    [Tooltip("Optional soft sound when the pointer moves onto a button.")]
    public AudioClip hoverSound;

    class Btn
    {
        public RectTransform rt;
        public Button button;
        public TextMeshProUGUI label;
        public PlateFrame frame;
        public Color idle;
        public float hover;
        public bool over;
    }

    readonly List<Btn> buttons = new List<Btn>();
    readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    RectTransform root;
    CanvasGroup cornerGroup, settingsGroup;
    Slider masterSlider, musicSlider, sfxSlider;
    TextMeshProUGUI displayValue, resolutionValue;
    Camera canvasCam;
    bool open;
    int resIndex;
    float lastTestSound;

    void Start()
    {
        if (panels == null) panels = FindAnyObjectByType<TitlePanels>();
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) canvasCam = canvas.worldCamera;
        LoadResolutions();
        Build();
    }

    /* ---------- building ---------- */

    void Build()
    {
        root = NewRect("TitleMenu", transform);
        Stretch(root);
        root.SetAsLastSibling();

        // Corner buttons
        var corner = NewRect("Corner", root);
        corner.anchorMin = corner.anchorMax = new Vector2(1f, 0f);
        corner.pivot = new Vector2(1f, 0f);
        corner.anchoredPosition = new Vector2(-cornerMargin.x, cornerMargin.y);
        corner.sizeDelta = new Vector2(10f, cornerTextSize * 1.4f);
        cornerGroup = corner.gameObject.AddComponent<CanvasGroup>();
        cornerGroup.alpha = 0f;

        var quit = TextButton(corner, "Quit", cornerTextSize, Quit);
        float x = PlaceBottomRight(quit, 0f);
        var settings = TextButton(corner, "Settings", cornerTextSize, OpenSettings);
        PlaceBottomRight(settings, x - cornerSpacing);

        // Settings panel over a dimmed screen
        var overlay = NewRect("Settings", root);
        Stretch(overlay);
        var dim = overlay.gameObject.AddComponent<Image>();
        dim.color = dimColor;
        dim.raycastTarget = true; // blocks clicks on everything behind it
        settingsGroup = overlay.gameObject.AddComponent<CanvasGroup>();
        settingsGroup.alpha = 0f;
        settingsGroup.blocksRaycasts = false;
        settingsGroup.interactable = false;

        var panel = NewRect("Panel", overlay);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(660f, 580f);
        var frame = panel.gameObject.AddComponent<PlateFrame>();
        frame.accent = accent;
        frame.chamfer = 22f;
        frame.glow = 0.35f;
        frame.glowSize = 28f;
        frame.raycastTarget = true;

        AddText(Item(panel, 0f, -70f, 560f, 70f), "Settings", headerFont, 48f,
                Color.Lerp(accent, Color.white, 0.5f), TextAlignmentOptions.Center);

        var am = AudioManager.Instance;
        float y = -160f;
        masterSlider = SliderRow(panel, "Master Volume", y, am ? am.masterVolume : 0.6f,
                                 v => { if (AudioManager.Instance) AudioManager.Instance.masterVolume = v; });
        y -= 68f;
        musicSlider = SliderRow(panel, "Music", y, am ? am.musicVolume : 0.5f,
                                v => { if (AudioManager.Instance) AudioManager.Instance.musicVolume = v; });
        y -= 68f;
        sfxSlider = SliderRow(panel, "Effects", y, am ? am.sfxVolume : 0.8f,
                              v => { if (AudioManager.Instance) AudioManager.Instance.sfxVolume = v; TestSound(); });
        y -= 68f;
        displayValue = CyclerRow(panel, "Display", y, () => ToggleDisplay(), () => ToggleDisplay());
        y -= 68f;
        resolutionValue = CyclerRow(panel, "Resolution", y, () => StepResolution(-1), () => StepResolution(1));

        PlateButton(panel, "BACK", new Vector2(200f, 54f), -515f, CloseSettings);

        RefreshDisplay(Screen.fullScreenMode);
    }

    Slider SliderRow(RectTransform panel, string name, float y, float value, UnityAction<float> onChange)
    {
        AddText(Item(panel, -150f, y, 240f, 50f), name, textFont, rowTextSize, textColor, TextAlignmentOptions.Left);
        var slider = MakeSlider(Item(panel, 140f, y, 280f, 30f), value);
        slider.onValueChanged.AddListener(onChange);
        return slider;
    }

    TextMeshProUGUI CyclerRow(RectTransform panel, string name, float y, UnityAction prev, UnityAction next)
    {
        AddText(Item(panel, -150f, y, 240f, 50f), name, textFont, rowTextSize, textColor, TextAlignmentOptions.Left);
        var value = AddText(Item(panel, 140f, y, 200f, 50f), "", textFont, rowTextSize, Color.white, TextAlignmentOptions.Center);
        var left = TextButton(panel, "<", rowTextSize, prev);
        PlaceTop(left, 140f - 130f, y);
        var right = TextButton(panel, ">", rowTextSize, next);
        PlaceTop(right, 140f + 130f, y);
        return value;
    }

    Slider MakeSlider(RectTransform rt, float value)
    {
        HitArea(rt.gameObject.AddComponent<Image>());

        var track = NewRect("Track", rt);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(0f, 6f);
        track.anchoredPosition = Vector2.zero;
        track.gameObject.AddComponent<Image>().color = new Color(0.165f, 0.137f, 0.188f, 1f); // #2A2330

        var fillArea = NewRect("Fill Area", rt);
        fillArea.anchorMin = track.anchorMin;
        fillArea.anchorMax = track.anchorMax;
        fillArea.sizeDelta = new Vector2(0f, 6f);
        fillArea.anchoredPosition = Vector2.zero;
        var fill = NewRect("Fill", fillArea);
        Stretch(fill);
        fill.gameObject.AddComponent<Image>().color = accent;

        var handleArea = NewRect("Handle Area", rt);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(7f, 0f);
        handleArea.offsetMax = new Vector2(-7f, 0f);
        var handle = NewRect("Handle", handleArea);
        handle.sizeDelta = new Vector2(14f, 0f);
        var handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.color = new Color(0.929f, 0.89f, 0.812f, 1f); // #EDE3CF

        var slider = rt.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.transition = Selectable.Transition.None;
        slider.SetValueWithoutNotify(value);
        return slider;
    }

    Btn TextButton(RectTransform parent, string text, float size, UnityAction onClick)
    {
        var rt = NewRect(text + " Button", parent);
        var hit = rt.gameObject.AddComponent<Image>();
        HitArea(hit);
        var labelRT = NewRect("Label", rt);
        Stretch(labelRT);
        var label = AddText(labelRT, text, textFont, size, textColor, TextAlignmentOptions.Center);
        rt.sizeDelta = new Vector2(label.GetPreferredValues(text).x + 16f, size * 1.4f);
        return AddButton(rt, hit, label, null, textColor, onClick);
    }

    Btn PlateButton(RectTransform parent, string text, Vector2 size, float y, UnityAction onClick)
    {
        var rt = Item(parent, 0f, y, size.x, size.y);
        var hit = rt.gameObject.AddComponent<Image>();
        HitArea(hit);
        var frameRT = NewRect("Frame", rt);
        Stretch(frameRT);
        var frame = frameRT.gameObject.AddComponent<PlateFrame>();
        frame.accent = accent;
        frame.raycastTarget = false;
        var labelRT = NewRect("Label", rt);
        Stretch(labelRT);
        var label = AddText(labelRT, text, headerFont, 24f, textColor, TextAlignmentOptions.Center);
        label.characterSpacing = 6f;
        return AddButton(rt, hit, label, frame, textColor, onClick);
    }

    Btn AddButton(RectTransform rt, Graphic target, TextMeshProUGUI label, PlateFrame frame, Color idle, UnityAction onClick)
    {
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = target;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => { PlayClick(); onClick(); });
        var b = new Btn { rt = rt, button = button, label = label, frame = frame, idle = idle };
        buttons.Add(b);
        return b;
    }

    /* ---------- actions ---------- */

    void OpenSettings()
    {
        var am = AudioManager.Instance;
        if (am != null)
        {
            masterSlider.SetValueWithoutNotify(am.masterVolume);
            musicSlider.SetValueWithoutNotify(am.musicVolume);
            sfxSlider.SetValueWithoutNotify(am.sfxVolume);
        }
        RefreshDisplay(Screen.fullScreenMode);
        open = true;
        if (panels != null) panels.enabled = false; // freeze the kingdoms behind the panel
    }

    void CloseSettings()
    {
        if (!open) return;
        open = false;
        if (panels != null) panels.enabled = true;
        if (AudioManager.Instance != null) AudioManager.Instance.SaveVolumes();
    }

    void Quit()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SaveVolumes();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ToggleDisplay()
    {
        var mode = Screen.fullScreenMode == FullScreenMode.Windowed
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;
        Screen.fullScreenMode = mode;
        RefreshDisplay(mode);
    }

    void StepResolution(int dir)
    {
        if (resolutions.Count == 0) return;
        resIndex = (resIndex + dir + resolutions.Count) % resolutions.Count;
        var r = resolutions[resIndex];
        Screen.SetResolution(r.x, r.y, Screen.fullScreenMode);
        RefreshDisplay(Screen.fullScreenMode);
    }

    void RefreshDisplay(FullScreenMode mode)
    {
        displayValue.text = mode == FullScreenMode.Windowed ? "Windowed" : "Fullscreen";
        if (resolutions.Count > 0)
        {
            var r = resolutions[resIndex];
            resolutionValue.text = r.x + " x " + r.y;
        }
    }

    void LoadResolutions()
    {
        resolutions.Clear();
        foreach (var r in Screen.resolutions)
        {
            var v = new Vector2Int(r.width, r.height);
            if (!resolutions.Contains(v)) resolutions.Add(v);
        }
        var current = new Vector2Int(Screen.width, Screen.height);
        if (!resolutions.Contains(current)) resolutions.Add(current);
        resIndex = resolutions.IndexOf(current);
    }

    void PlayClick()
    {
        if (clickSound != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(clickSound);
    }

    // A click while dragging the Effects slider, so you can hear the level
    void TestSound()
    {
        if (Time.unscaledTime - lastTestSound < 0.12f) return;
        lastTestSound = Time.unscaledTime;
        PlayClick();
    }

    /* ---------- every frame ---------- */

    void Update()
    {
        if (root == null) return;

        bool ready = panels == null || panels.IntroDone;
        Fade(cornerGroup, ready && !open, 0.6f);
        Fade(settingsGroup, open, 0.25f);

        if (open && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseSettings();

        foreach (var b in buttons)
        {
            bool over = b.button.IsInteractable() && IsMouseOver(b.rt);
            if (over && !b.over && hoverSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(hoverSound, 0.5f);
            b.over = over;
            b.hover = Mathf.MoveTowards(b.hover, over ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            b.label.color = Color.Lerp(b.idle, hoverColor, b.hover);
            b.rt.localScale = Vector3.one * (1f + 0.04f * b.hover);
            if (b.frame != null)
            {
                float g = Mathf.Lerp(0.15f, 1f, b.hover);
                if (!Mathf.Approximately(b.frame.glow, g)) { b.frame.glow = g; b.frame.SetVerticesDirty(); }
            }
        }
    }

    static void Fade(CanvasGroup g, bool on, float time)
    {
        g.alpha = Mathf.MoveTowards(g.alpha, on ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, time));
        g.interactable = on && g.alpha > 0.9f;
        g.blocksRaycasts = on;
    }

    bool IsMouseOver(RectTransform rt)
    {
        if (Mouse.current == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, Mouse.current.position.ReadValue(), canvasCam);
    }

    /* ---------- layout helpers ---------- */

    static float PlaceBottomRight(Btn b, float x)
    {
        b.rt.anchorMin = b.rt.anchorMax = new Vector2(1f, 0f);
        b.rt.pivot = new Vector2(1f, 0f);
        b.rt.anchoredPosition = new Vector2(x, 0f);
        return x - b.rt.sizeDelta.x;
    }

    static void PlaceTop(Btn b, float x, float y)
    {
        b.rt.anchorMin = b.rt.anchorMax = new Vector2(0.5f, 1f);
        b.rt.pivot = new Vector2(0.5f, 0.5f);
        b.rt.anchoredPosition = new Vector2(x, y);
    }

    // A box positioned from the top-center of its parent
    static RectTransform Item(RectTransform parent, float x, float y, float w, float h)
    {
        var rt = NewRect("Item", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        return rt;
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
        rt.localScale = Vector3.one;
    }

    // Nearly invisible but still clickable (fully transparent graphics can get culled and stop taking clicks)
    static void HitArea(Image img)
    {
        img.color = new Color(1f, 1f, 1f, 0.004f);
        img.raycastTarget = true;
        img.canvasRenderer.cullTransparentMesh = false;
    }

    static TextMeshProUGUI AddText(RectTransform rt, string text, TMP_FontAsset font, float size, Color color,
                                   TextAlignmentOptions align)
    {
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }
}