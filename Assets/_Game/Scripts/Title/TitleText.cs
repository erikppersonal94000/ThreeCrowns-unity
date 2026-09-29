using System;
using TMPro;
using UnityEngine;

// Title text: "Three Crowns" in gold, crossfading to each kingdom's lettering on hover,
// plus the tagline and the breathing "Choose your kingdom" hint.
// Built in code when you press Play. Follows TitlePanels for the intro and hover.
[RequireComponent(typeof(RectTransform))]
public class TitleText : MonoBehaviour
{
    [Serializable]
    public class Face
    {
        public TMP_FontAsset font;
        [Tooltip("Size relative to Title Size")] public float sizeScale = 1f;
        public float letterSpacing = 0f;
        public Color top = Color.white;                        // gradient, top of the letters
        public Color bottom = Color.gray;                      // gradient, bottom of the letters
        public Color glow = new Color(0f, 0f, 0f, 0.6f);       // halo around the letters
    }

    [SerializeField] TitlePanels panels;

    [Header("Title faces: 0 = default, then one per kingdom in panel order")]
    [SerializeField] Face[] faces = new Face[4];
    [SerializeField] float titleSize = 150f;
    [SerializeField] float titleY = -170f;          // title center, measured down from the top of the screen

    [Header("Tagline and hint")]
    [SerializeField] TMP_FontAsset taglineFont;     // IM Fell English Italic
    [SerializeField] TMP_FontAsset hintFont;        // IM Fell English SC
    [SerializeField] string tagline = "Three kingdoms. One throne. Build the deck that claims it.";
    [SerializeField] string hint = "Choose your kingdom";

    [Header("Motion")]
    [SerializeField] float fadeSpeed = 5f;          // how fast the kingdom lettering crossfades

    class FaceText
    {
        public TextMeshProUGUI text;
        public float alpha;
        public float scale = 1f;
    }

    static readonly Color Parchment = new Color(239 / 255f, 227 / 255f, 200 / 255f);

    FaceText[] faceTexts;
    RectTransform titleRoot;
    CanvasGroup titleGroup;
    TextMeshProUGUI taglineText, hintText;
    float readyAt = -1f;
    float hintAlpha;
    bool touched;

    // ---------- Build ----------
    void Awake()
    {
        var root = (RectTransform)transform;

        // The title: four versions stacked on top of each other
        titleRoot = MakeRect(root, "Title", new Vector2(0f, titleY), new Vector2(1800f, titleSize * 1.6f));
        titleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
        titleGroup.alpha = 0f;
        titleGroup.blocksRaycasts = false;

        faceTexts = new FaceText[faces.Length];
        for (int i = 0; i < faces.Length; i++)
        {
            var f = faces[i];
            var t = MakeText(titleRoot, $"Face_{i}", "Three Crowns", f.font, titleSize * f.sizeScale);
            Stretch(t.rectTransform);
            t.characterSpacing = f.letterSpacing;
            t.color = Color.white;
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(f.top, f.top, f.bottom, f.bottom);
            Style(t, f.glow);
            faceTexts[i] = new FaceText { text = t, alpha = i == 0 ? 1f : 0f, scale = i == 0 ? 1f : 0.97f };
            t.alpha = faceTexts[i].alpha;
        }

        // Tagline under the title
        var tagRect = MakeRect(root, "Tagline", new Vector2(0f, titleY - 140f), new Vector2(1000f, 100f));
        taglineText = MakeText(tagRect, "Text", tagline, taglineFont, 36f);
        Stretch(taglineText.rectTransform);
        taglineText.textWrappingMode = TextWrappingModes.Normal;
        taglineText.color = Parchment;
        taglineText.alpha = 0f;
        Style(taglineText, new Color(0f, 0f, 0f, 0.6f));

        // "Choose your kingdom"
        var hintRect = MakeRect(root, "Hint", new Vector2(0f, titleY - 250f), new Vector2(800f, 50f));
        hintText = MakeText(hintRect, "Text", hint, hintFont, 26f);
        Stretch(hintText.rectTransform);
        hintText.color = Parchment;
        hintText.alpha = 0f;
        Style(hintText, new Color(0f, 0f, 0f, 0.6f));
    }

    void Start()
    {
        if (panels == null) panels = FindFirstObjectByType<TitlePanels>();
        if (panels == null) { Debug.LogError("TitleText: no TitlePanels found.", this); return; }

        panels.IntroFinished += OnReady;
        panels.FocusChanged += OnFocus;
        if (panels.IntroDone) OnReady();
    }

    void OnDestroy()
    {
        if (panels == null) return;
        panels.IntroFinished -= OnReady;
        panels.FocusChanged -= OnFocus;
    }

    void OnReady() { if (readyAt < 0f) readyAt = Time.time; }
    void OnFocus(int f) { if (f >= 0) touched = true; } // hides the hint for good

    // ---------- Every frame ----------
    void Update()
    {
        float k = 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime);
        float kScale = 1f - Mathf.Exp(-fadeSpeed * 0.7f * Time.deltaTime);

        // Which lettering to show: 0 = default gold, 1..3 = the hovered kingdom
        int focus = panels != null ? panels.Focus : -1;
        int active = focus >= 0 ? focus + 1 : 0;

        for (int i = 0; i < faceTexts.Length; i++)
        {
            var f = faceTexts[i];
            bool on = i == active;
            float targetScale = on ? 1f : (i == 0 ? 1.03f : 0.97f);
            f.alpha = Mathf.Lerp(f.alpha, on ? 1f : 0f, k);
            f.scale = Mathf.Lerp(f.scale, targetScale, kScale);
            f.text.alpha = f.alpha;
            f.text.rectTransform.localScale = Vector3.one * f.scale;
        }

        // Intro: title fades in and settles, then the tagline
        float t = readyAt < 0f ? -1f : Time.time - readyAt;
        if (t < 0f)
        {
            titleGroup.alpha = 0f;
            titleRoot.localScale = Vector3.one * 1.08f;
            taglineText.alpha = 0f;
        }
        else
        {
            titleGroup.alpha = Smooth(Mathf.Clamp01(t / 1.6f));
            titleRoot.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, EaseOut(Mathf.Clamp01(t / 2.4f)));
            taglineText.alpha = 0.9f * Smooth(Mathf.Clamp01((t - 0.4f) / 1.4f));
        }

        // Hint: breathes slowly until a kingdom is hovered, then fades away
        if (touched || t < 1.2f)
        {
            hintAlpha = Mathf.Lerp(hintAlpha, 0f, k);
        }
        else
        {
            float phase = (t - 1.2f) / 3.2f * Mathf.PI * 2f;
            hintAlpha = 0.25f + 0.55f * (0.5f - 0.5f * Mathf.Cos(phase));
        }
        hintText.alpha = hintAlpha;
    }

    // ---------- Helpers ----------
    static float Smooth(float x) => x * x * (3f - 2f * x);
    static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

    // A rectangle anchored to the top-center of the screen
    static RectTransform MakeRect(RectTransform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI MakeText(RectTransform parent, string name, string text, TMP_FontAsset font, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // Soft drop shadow plus a colored glow, set on this text's own copy of the font material
    static void Style(TMP_Text t, Color glow)
    {
        var m = t.fontMaterial;
        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.7f));
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);

        if (glow.a > 0f)
        {
            m.EnableKeyword(ShaderUtilities.Keyword_Glow);
            m.SetColor(ShaderUtilities.ID_GlowColor, glow);
            m.SetFloat(ShaderUtilities.ID_GlowOuter, 0.6f);
            m.SetFloat(ShaderUtilities.ID_GlowPower, 0.6f);
        }
        t.UpdateMeshPadding();
    }
}