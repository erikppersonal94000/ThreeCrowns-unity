using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The name, motto and Enter button shown under a kingdom's banner on the title screen.
// Put it on the same object as KingdomPlate. It builds its UI when the game starts.
public class KingdomInfo : MonoBehaviour
{
    [Header("Kingdom")]
    public TitlePanels panels;
    public int kingdomIndex;
    public string kingdomName = "Duskmoor";
    [TextArea] public string motto = "The dead do not rest. Neither will you.";

    [Header("Fonts")]
    public TMP_FontAsset nameFont;
    public TMP_FontAsset mottoFont;
    [Tooltip("Leave empty to use the name font.")]
    public TMP_FontAsset buttonFont;

    [Header("Colors")]
    public Color nameTop = new Color(0.949f, 0.933f, 0.965f, 1f);       // #F2EEF6
    public Color nameBottom = new Color(0.353f, 0.314f, 0.4f, 1f);      // #5A5066
    public Color mottoColor = new Color(0.788f, 0.749f, 0.839f, 1f);    // #C9BFD6
    [Tooltip("Border and glow color of the button.")]
    public Color accent = new Color(0.541f, 0.31f, 0.847f, 1f);         // #8A4FD8

    [Header("Layout")]
    [Tooltip("Where the block sits, measured from the bottom-center of this object.")]
    public Vector2 offset = new Vector2(0f, -24f);
    public float width = 460f;
    public float nameSize = 60f;
    public float mottoSize = 26f;
    public float buttonTextSize = 26f;
    public Vector2 buttonSize = new Vector2(260f, 58f);

    [Header("Button")]
    public bool unlocked = true;
    public string enterLabel = "ENTER";
    public string lockedLabel = "COMING SOON";
    [Tooltip("Your existing button with SceneButton on it. It's moved into the plaque and made invisible; its click still does the scene change.")]
    public Button sceneButton;

    [Header("Timing")]
    public float appearDelay = 0.3f;
    public float stagger = 0.15f;
    public float fadeTime = 0.35f;

    RectTransform root, nameRT, mottoRT, buttonRT;
    CanvasGroup nameGroup, mottoGroup, buttonGroup;
    Vector2 nameBase, mottoBase, buttonBase;
    PlateFrame frame;
    TextMeshProUGUI label;
    Camera canvasCam;
    bool shown;
    float shownTime, hover;

    static readonly Color LabelIdle = new Color(0.851f, 0.827f, 0.878f, 1f);   // #D9D3E0
    static readonly Color LabelLocked = new Color(0.541f, 0.541f, 0.541f, 1f); // #8A8A8A

    void Start()
    {
        if (panels == null) panels = FindAnyObjectByType<TitlePanels>();
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) canvasCam = canvas.worldCamera;
        Build();
    }

    void Build()
    {
        root = NewRect("KingdomInfo", transform);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = offset;
        root.sizeDelta = new Vector2(width, 10f);

        float y = 0f;

        // Name
        nameRT = Block("Name", width, nameSize * 1.25f, ref y, out nameGroup, out nameBase);
        var nameText = AddText(nameRT, kingdomName, nameFont, nameSize, Color.white);
        nameText.enableVertexGradient = true;
        nameText.colorGradient = new VertexGradient(nameTop, nameTop, nameBottom, nameBottom);
        y -= 4f;

        // Motto
        mottoRT = Block("Motto", width, mottoSize * 2.6f, ref y, out mottoGroup, out mottoBase);
        var mottoText = AddText(mottoRT, motto, mottoFont, mottoSize, mottoColor);
        mottoText.textWrappingMode = TextWrappingModes.Normal;
        y -= 14f;

        // Button plaque
        buttonRT = Block("Button", buttonSize.x, buttonSize.y, ref y, out buttonGroup, out buttonBase);
        var frameRT = NewRect("Frame", buttonRT);
        Stretch(frameRT);
        frame = frameRT.gameObject.AddComponent<PlateFrame>();
        frame.raycastTarget = false;
        frame.maskable = false;
        frame.accent = unlocked ? accent : new Color(accent.r, accent.g, accent.b, accent.a * 0.4f);

        var labelRT = NewRect("Label", buttonRT);
        Stretch(labelRT);
        label = AddText(labelRT, unlocked ? enterLabel : lockedLabel, buttonFont != null ? buttonFont : nameFont,
                        buttonTextSize, unlocked ? LabelIdle : LabelLocked);
        label.characterSpacing = 6f;

        if (unlocked && sceneButton != null) AdoptButton();
        else if (unlocked) Debug.LogWarning($"KingdomInfo ({kingdomName}): Unlocked but no Scene Button assigned, so ENTER does nothing yet.");

        nameGroup.alpha = mottoGroup.alpha = buttonGroup.alpha = 0f;
    }

    // Moves your existing SceneButton into the plaque and hides its old look
    void AdoptButton()
    {
        var bt = (RectTransform)sceneButton.transform;
        bt.SetParent(buttonRT, false);
        Stretch(bt);
        bt.SetAsFirstSibling();

        foreach (var g in sceneButton.GetComponentsInChildren<Graphic>(true))
        {
            if (g is MaskableGraphic mg) mg.maskable = false;
            if (g == sceneButton.targetGraphic) continue;
            g.enabled = false;
        }
        if (sceneButton.targetGraphic == null) sceneButton.targetGraphic = sceneButton.gameObject.AddComponent<Image>();

        // Nearly invisible but still clickable (fully transparent graphics can get culled and stop taking clicks)
        var tg = sceneButton.targetGraphic;
        tg.color = new Color(1f, 1f, 1f, 0.004f);
        tg.raycastTarget = true;
        tg.canvasRenderer.cullTransparentMesh = false;
        sceneButton.transition = Selectable.Transition.None;
        sceneButton.gameObject.SetActive(true);
    }

    void Update()
    {
        if (root == null) return;

        bool focus = panels != null && panels.Focus == kingdomIndex;
        if (focus && !shown) { shown = true; shownTime = Time.time; }
        if (!focus) shown = false;
        float since = Time.time - shownTime;

        Fade(nameGroup, nameRT, nameBase, shown && since > appearDelay);
        Fade(mottoGroup, mottoRT, mottoBase, shown && since > appearDelay + stagger);
        Fade(buttonGroup, buttonRT, buttonBase, shown && since > appearDelay + stagger * 2f);

        bool clickable = unlocked && buttonGroup.alpha > 0.9f;
        buttonGroup.interactable = clickable;
        buttonGroup.blocksRaycasts = clickable;

        // Hover: glow, grow slightly, brighten the label
        bool over = clickable && IsMouseOver(buttonRT);
        hover = Mathf.MoveTowards(hover, over ? 1f : 0f, Time.deltaTime * 6f);
        float g = unlocked ? Mathf.Lerp(0.15f, 1f, hover) : 0f;
        if (!Mathf.Approximately(frame.glow, g)) { frame.glow = g; frame.SetVerticesDirty(); }
        buttonRT.localScale = Vector3.one * (1f + 0.04f * hover);
        if (unlocked) label.color = Color.Lerp(LabelIdle, Color.white, hover);
    }

    void Fade(CanvasGroup group, RectTransform rt, Vector2 basePos, bool on)
    {
        float speed = 1f / Mathf.Max(0.01f, on ? fadeTime : fadeTime * 0.5f);
        group.alpha = Mathf.MoveTowards(group.alpha, on ? 1f : 0f, Time.deltaTime * speed);
        float ease = 1f - (1f - group.alpha) * (1f - group.alpha);
        rt.anchoredPosition = basePos + new Vector2(0f, (1f - ease) * -10f); // rises into place
    }

    bool IsMouseOver(RectTransform rt)
    {
        if (Mouse.current == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, Mouse.current.position.ReadValue(), canvasCam);
    }

    /* ---------- building helpers ---------- */

    RectTransform Block(string name, float w, float h, ref float y, out CanvasGroup group, out Vector2 basePos)
    {
        var rt = NewRect(name, root);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, y);
        basePos = rt.anchoredPosition;
        y -= h;
        group = rt.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
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
        rt.localRotation = Quaternion.identity;
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
        t.maskable = false; // so the banner's Mask doesn't clip it
        return t;
    }
}