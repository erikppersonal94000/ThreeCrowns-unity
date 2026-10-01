using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The War Table's campaign map: the realm with Stonehelm's four provinces. Click an open province to
// zoom into its own map, then choose a location to fight. Builds its whole UI when the game starts.
// Put it on a stretched, empty object under the scene's Canvas.
public class CampaignMap : MonoBehaviour
{
    [System.Serializable]
    public class Province
    {
        public string id;
        public string name;
        public string subtitle;
        public Sprite emblem;
        [Tooltip("Can be entered now. Unticked = locked.")]
        public bool available;
        [Tooltip("Outline on the world map, in pixels of the original 1584 x 672 map, from the top-left.")]
        public Vector2[] outline;
        [Tooltip("Where the emblem (or the name, without an emblem) sits, in world map pixels.")]
        public Vector2 badgePos;

        [Header("Zoomed in")]
        public Sprite detailMap;
        [Tooltip("Size of the original detail map the positions were measured on.")]
        public Vector2 detailPixels = new Vector2(1584, 672);
        [Tooltip("Where the road from Duskmoor enters the detail map, in its pixels.")]
        public Vector2 entry;
        public List<Location> locations = new List<Location>();
    }

    [System.Serializable]
    public class Location
    {
        public string id;
        public string name;
        [TextArea(2, 4)] public string description;
        [Tooltip("Position on the detail map, in its pixels, from the top-left.")]
        public Vector2 pos;
        [Tooltip("Ids of the locations that must be won first. Empty = open from the start.")]
        public string[] requires = new string[0];
        public bool optional;
        public bool boss;
        [Tooltip("Scene with this battle. Leave empty until battles exist; the button then marks it as won, for testing.")]
        public string battleScene;
    }

    [Header("Maps")]
    public Sprite worldMap;
    [Tooltip("Size of the original world map the outlines were measured on.")]
    public Vector2 worldPixels = new Vector2(1584, 672);
    public List<Province> provinces = new List<Province>();

    [Header("Fonts")]
    public TMP_FontAsset headerFont;
    public TMP_FontAsset textFont;

    [Header("Colors")]
    public Color routeColor = new Color(0.69f, 0.439f, 1f, 1f);         // #B070FF
    public Color lockedColor = new Color(0.431f, 0.408f, 0.471f, 1f);   // #6E6878
    public Color gold = new Color(0.788f, 0.643f, 0.361f, 1f);          // #C9A45C
    public Color conqueredTint = new Color(0.35f, 0.18f, 0.5f, 0.35f);  // #592E80 at 35%

    [Header("Feel")]
    public float zoomTime = 1.1f;
    [Range(0f, 1f)] public float panStrength = 0.6f;
    public float badgeSize = 120f;
    public string throneRoomScene = "Duskmoor";

    [Header("Sound")]
    public AudioClip clickSound;
    public AudioClip hoverSound;

    // Set this before loading a battle, so the map reopens on that province afterwards
    public static string ReturnToProvince;

    enum State { World, Zooming, Detail }

    class Badge { public Province p; public RectTransform rt; public Image ring, emblem; public float hover; }
    class Marker { public Location loc; public RectTransform rt; public Image ring, fill; public TextMeshProUGUI num; public float hover; }
    class Btn { public RectTransform rt; public Button button; public TextMeshProUGUI label; public PlateFrame frame; public float hover; }

    RectTransform root, world, worldLabels, detail, markersRT, ui, panel, tooltip;
    Image worldImg, detailImg;
    ProvinceShapes shapes;
    RouteLines routes;
    CanvasGroup worldLabelsGroup, detailGroup, panelGroup, tooltipGroup;
    TextMeshProUGUI title, subtitle, tipName, tipSub, pName, pTag, pDesc;
    PlateFrame tipFrame;
    Btn backBtn, beginBtn;
    Camera cam;
    State state = State.World;
    Province open;
    Location selected;
    bool panelOpen, built;
    Vector2 worldPos, detailPan;
    int hoverProvince = -1;
    object lastHover;
    readonly List<Badge> badges = new List<Badge>();
    readonly List<Marker> markers = new List<Marker>();
    readonly List<Btn> buttons = new List<Btn>();
    readonly Dictionary<Province, Vector2[]> normOutline = new Dictionary<Province, Vector2[]>();

    /* ---------- setup ---------- */

    void Start()
    {
        root = (RectTransform)transform;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) cam = canvas.worldCamera;
        if (worldMap == null) Debug.LogWarning("CampaignMap: no World Map sprite assigned.");
        Build();
        built = true;

        var back = provinces.Find(p => p.id == ReturnToProvince);
        ReturnToProvince = null;
        if (back != null && CanEnter(back)) JumpToDetail(back);
        else SetTitle("The Realm", "Choose where Duskmoor strikes");
    }

    void Build()
    {
        // The world
        world = NewRect("World", root);
        world.anchorMin = world.anchorMax = new Vector2(0.5f, 0.5f);
        worldImg = world.gameObject.AddComponent<Image>();
        worldImg.sprite = worldMap;
        worldImg.raycastTarget = false;

        worldLabels = NewRect("Provinces", world);
        Stretch(worldLabels);
        worldLabelsGroup = worldLabels.gameObject.AddComponent<CanvasGroup>();
        worldLabelsGroup.blocksRaycasts = false;
        shapes = NewRect("Shapes", worldLabels).gameObject.AddComponent<ProvinceShapes>();
        Stretch(shapes.rectTransform);
        shapes.raycastTarget = false;

        foreach (var p in provinces)
        {
            var pts = new Vector2[p.outline != null ? p.outline.Length : 0];
            for (int i = 0; i < pts.Length; i++) pts[i] = Norm(p.outline[i], worldPixels);
            normOutline[p] = pts;
            badges.Add(MakeBadge(p));
        }

        // The zoomed-in province
        detail = NewRect("Detail", root);
        detail.anchorMin = detail.anchorMax = new Vector2(0.5f, 0.5f);
        detailImg = detail.gameObject.AddComponent<Image>();
        detailImg.raycastTarget = false;
        detailGroup = detail.gameObject.AddComponent<CanvasGroup>();
        detailGroup.alpha = 0f;
        detailGroup.blocksRaycasts = false;
        routes = NewRect("Routes", detail).gameObject.AddComponent<RouteLines>();
        Stretch(routes.rectTransform);
        routes.raycastTarget = false;
        markersRT = NewRect("Markers", detail);
        Stretch(markersRT);
        detail.gameObject.SetActive(false);

        // UI on top
        ui = NewRect("UI", root);
        Stretch(ui);

        var titleRT = NewRect("Title", ui);
        titleRT.anchorMin = titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.sizeDelta = new Vector2(1200f, 70f);
        titleRT.anchoredPosition = new Vector2(0f, -28f);
        title = AddText(titleRT, "", headerFont, 54f, new Color(0.949f, 0.933f, 0.965f), TextAlignmentOptions.Center);
        Outline(title);
        var subRT = NewRect("Subtitle", ui);
        subRT.anchorMin = subRT.anchorMax = new Vector2(0.5f, 1f);
        subRT.pivot = new Vector2(0.5f, 1f);
        subRT.sizeDelta = new Vector2(1200f, 40f);
        subRT.anchoredPosition = new Vector2(0f, -98f);
        subtitle = AddText(subRT, "", textFont, 28f, new Color(0.863f, 0.784f, 0.627f), TextAlignmentOptions.Center);
        Outline(subtitle);

        backBtn = PlateButton(ui, "Throne Room", new Vector2(300f, 54f), Back);
        backBtn.rt.anchorMin = backBtn.rt.anchorMax = new Vector2(0f, 1f);
        backBtn.rt.pivot = new Vector2(0f, 1f);
        backBtn.rt.anchoredPosition = new Vector2(40f, -36f);

        BuildTooltip();
        BuildPanel();
    }

    Badge MakeBadge(Province p)
    {
        var b = new Badge { p = p };
        b.rt = NewRect(p.id, worldLabels);
        b.rt.anchorMin = b.rt.anchorMax = Norm(p.badgePos, worldPixels);
        b.rt.sizeDelta = Vector2.one * (p.emblem != null ? badgeSize : 10f);

        if (p.emblem != null)
        {
            b.ring = b.rt.gameObject.AddComponent<Image>();
            b.ring.sprite = MapDraw.Circle;
            b.ring.raycastTarget = false;
            var inner = NewRect("Inner", b.rt);
            Stretch(inner);
            inner.offsetMin = Vector2.one * 7f;
            inner.offsetMax = -Vector2.one * 7f;
            var innerImg = inner.gameObject.AddComponent<Image>();
            innerImg.sprite = MapDraw.Circle;
            innerImg.color = new Color(0.08f, 0.06f, 0.1f, 1f);
            innerImg.raycastTarget = false;
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var em = NewRect("Emblem", inner);
            Stretch(em);
            b.emblem = em.gameObject.AddComponent<Image>();
            b.emblem.sprite = p.emblem;
            b.emblem.preserveAspect = true;
            b.emblem.raycastTarget = false;
        }

        var nameRT = NewRect("Name", b.rt);
        nameRT.anchorMin = nameRT.anchorMax = new Vector2(0.5f, p.emblem != null ? 0f : 0.5f);
        nameRT.pivot = new Vector2(0.5f, p.emblem != null ? 1f : 0.5f);
        nameRT.sizeDelta = new Vector2(460f, 40f);
        nameRT.anchoredPosition = new Vector2(0f, p.emblem != null ? -8f : 14f);
        Outline(AddText(nameRT, p.name, headerFont, 30f, new Color(0.949f, 0.933f, 0.965f), TextAlignmentOptions.Center));

        var subRT = NewRect("Subtitle", b.rt);
        subRT.anchorMin = subRT.anchorMax = nameRT.anchorMin;
        subRT.pivot = nameRT.pivot;
        subRT.sizeDelta = new Vector2(460f, 30f);
        subRT.anchoredPosition = nameRT.anchoredPosition + new Vector2(0f, -38f);
        Outline(AddText(subRT, p.subtitle, textFont, 22f, new Color(0.863f, 0.784f, 0.627f), TextAlignmentOptions.Center));
        return b;
    }

    void BuildTooltip()
    {
        tooltip = NewRect("Tooltip", ui);
        tooltip.anchorMin = tooltip.anchorMax = new Vector2(0.5f, 0.5f);
        tooltip.pivot = new Vector2(0f, 1f);
        tooltipGroup = tooltip.gameObject.AddComponent<CanvasGroup>();
        tooltipGroup.alpha = 0f;
        tooltipGroup.blocksRaycasts = false;
        var frameRT = NewRect("Frame", tooltip);
        Stretch(frameRT);
        tipFrame = frameRT.gameObject.AddComponent<PlateFrame>();
        tipFrame.accent = gold;
        tipFrame.chamfer = 8f;
        tipFrame.raycastTarget = false;
        var nRT = NewRect("Name", tooltip);
        nRT.anchorMin = new Vector2(0f, 1f); nRT.anchorMax = new Vector2(1f, 1f);
        nRT.pivot = new Vector2(0.5f, 1f);
        nRT.sizeDelta = new Vector2(0f, 36f);
        nRT.anchoredPosition = new Vector2(0f, -10f);
        tipName = AddText(nRT, "", headerFont, 24f, Color.white, TextAlignmentOptions.Center);
        var sRT = NewRect("Sub", tooltip);
        sRT.anchorMin = new Vector2(0f, 0f); sRT.anchorMax = new Vector2(1f, 0f);
        sRT.pivot = new Vector2(0.5f, 0f);
        sRT.sizeDelta = new Vector2(0f, 30f);
        sRT.anchoredPosition = new Vector2(0f, 10f);
        tipSub = AddText(sRT, "", textFont, 20f, new Color(0.863f, 0.784f, 0.627f), TextAlignmentOptions.Center);
    }

    void BuildPanel()
    {
        panel = NewRect("Location Panel", ui);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.sizeDelta = new Vector2(720f, 330f);
        panel.anchoredPosition = new Vector2(0f, 40f);
        panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;
        var frameRT = NewRect("Frame", panel);
        Stretch(frameRT);
        var frame = frameRT.gameObject.AddComponent<PlateFrame>();
        frame.accent = gold;
        frame.chamfer = 18f;
        frame.glow = 0.3f;
        frame.raycastTarget = true; // blocks clicks on the map behind it

        pName = AddText(PanelItem(0f, -26f, 640f, 56f), "", headerFont, 42f, Color.white, TextAlignmentOptions.Center);
        pTag = AddText(PanelItem(0f, -84f, 640f, 32f), "", textFont, 24f, gold, TextAlignmentOptions.Center);
        pDesc = AddText(PanelItem(0f, -124f, 640f, 100f), "", textFont, 27f,
                        new Color(0.851f, 0.827f, 0.878f), TextAlignmentOptions.Top);
        pDesc.textWrappingMode = TextWrappingModes.Normal;

        beginBtn = PlateButton(panel, "Begin battle", new Vector2(330f, 56f), Begin);
        Place(beginBtn.rt, -100f, 30f);
        var close = PlateButton(panel, "Close", new Vector2(180f, 56f), ClosePanel);
        Place(close.rt, 180f, 30f);
    }

    RectTransform PanelItem(float x, float y, float w, float h)
    {
        var rt = NewRect("Item", panel);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        return rt;
    }

    static void Place(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
    }

    /* ---------- every frame ---------- */

    void Update()
    {
        if (!built) return;
        float dt = Time.unscaledDeltaTime;
        Vector2 rs = root.rect.size;
        world.sizeDelta = CoverSize(rs, worldPixels.x / worldPixels.y);
        if (open != null) detail.sizeDelta = CoverSize(rs, open.detailPixels.x / open.detailPixels.y);

        var mouse = Mouse.current;
        Vector2 mp = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width, Screen.height) * 0.5f;
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame && !overUI;
        if (panelOpen && click) { ClosePanel(); click = false; } // clicking the map closes the panel

        string tipA = null, tipB = null;
        object hovered = null;

        if (state == State.World)
        {
            worldPos = Vector2.Lerp(worldPos, PanTarget(world.sizeDelta, mp), 1f - Mathf.Exp(-4f * dt));
            world.anchoredPosition = worldPos;
            world.localScale = Vector3.one;
            hoverProvince = overUI ? -1 : ProvinceAt(mp);
            if (hoverProvince >= 0)
            {
                var p = provinces[hoverProvince];
                hovered = p;
                tipA = p.name;
                tipB = Status(p);
                if (click && CanEnter(p)) { Play(clickSound); StartCoroutine(ZoomIn(p)); }
            }
        }
        else hoverProvince = -1;

        if (state == State.Detail)
        {
            detailPan = Vector2.Lerp(detailPan, PanTarget(detail.sizeDelta, mp), 1f - Mathf.Exp(-4f * dt));
            detail.anchoredPosition = detailPan;
            Marker over = null;
            if (!overUI && !panelOpen)
                foreach (var m in markers)
                    if (RectTransformUtility.RectangleContainsScreenPoint(m.rt, mp, cam)) over = m;
            if (over != null)
            {
                hovered = over;
                tipA = over.loc.name;
                tipB = LocationStatus(over.loc);
                if (click && (IsOpen(over.loc) || CampaignProgress.IsCleared(over.loc.id))) { Play(clickSound); ShowPanel(over.loc); }
            }
            foreach (var m in markers) m.hover = Mathf.MoveTowards(m.hover, m == over ? 1f : 0f, dt * 8f);
        }

        if (hovered != null && hovered != lastHover) Play(hoverSound, 0.5f);
        lastHover = hovered;

        UpdateShapes();
        UpdateBadges(dt);
        UpdateMarkers();
        UpdateButtons(dt);
        UpdateTooltip(tipA, tipB, mp, dt);

        panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, panelOpen ? 1f : 0f, dt * 6f);
        panelGroup.blocksRaycasts = panelOpen;
        panelGroup.interactable = panelOpen;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (panelOpen) ClosePanel();
            else if (state == State.Detail) StartCoroutine(ZoomOut());
        }
    }

    void UpdateShapes()
    {
        shapes.shapes.Clear();
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.5f);
        for (int i = 0; i < provinces.Count; i++)
        {
            var p = provinces[i];
            bool hover = i == hoverProvince;
            var s = new ProvinceShapes.Shape { pts = normOutline[p] };
            if (!p.available)
            {
                s.fill = new Color(0f, 0f, 0f, 0.45f);
                s.line = new Color(0.85f, 0.85f, 0.85f, hover ? 0.5f : 0.3f);
                s.width = 2f;
            }
            else if (Conquered(p))
            {
                s.fill = conqueredTint;
                s.line = gold;
                s.width = hover ? 5f : 3f;
            }
            else
            {
                s.fill = new Color(1f, 1f, 1f, hover ? 0.14f : 0.03f);
                s.line = new Color(1f, 1f, 1f, hover ? 1f : 0.5f + 0.3f * pulse);
                s.width = hover ? 5f : 3f;
            }
            shapes.shapes.Add(s);
        }
        shapes.SetVerticesDirty();
    }

    void UpdateBadges(float dt)
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.5f);
        for (int i = 0; i < badges.Count; i++)
        {
            var b = badges[i];
            b.hover = Mathf.MoveTowards(b.hover, i == hoverProvince ? 1f : 0f, dt * 6f);
            bool live = CanEnter(b.p) && !Conquered(b.p);
            b.rt.localScale = Vector3.one * (1f + 0.12f * b.hover + (live ? 0.04f * pulse : 0f));
            if (b.ring == null) continue;
            b.ring.color = !b.p.available ? lockedColor : Conquered(b.p) ? routeColor : gold;
            b.emblem.color = b.p.available ? Color.white : new Color(0.42f, 0.42f, 0.42f, 1f);
        }
    }

    void UpdateMarkers()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
        foreach (var m in markers)
        {
            bool cleared = CampaignProgress.IsCleared(m.loc.id);
            bool openNow = IsOpen(m.loc);
            if (cleared)
            {
                m.ring.color = gold;
                m.fill.color = new Color(0.23f, 0.12f, 0.32f, 1f);    // #3B1F52
                m.num.color = gold;
            }
            else if (openNow)
            {
                m.ring.color = Color.Lerp(routeColor, Color.white, 0.3f * pulse);
                m.fill.color = new Color(0.1f, 0.05f, 0.15f, 1f);     // #1A0D26
                m.num.color = Color.white;
            }
            else
            {
                m.ring.color = new Color(lockedColor.r, lockedColor.g, lockedColor.b, 0.7f);
                m.fill.color = new Color(0.06f, 0.05f, 0.08f, 0.85f);
                m.num.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            }
            float breathe = openNow && !cleared ? 0.05f * pulse : 0f;
            m.rt.localScale = Vector3.one * (1f + 0.15f * m.hover + breathe);
        }
    }

    void UpdateButtons(float dt)
    {
        var mouse = Mouse.current;
        Vector2 mp = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        foreach (var b in buttons)
        {
            bool over = b.button.IsInteractable() && b.rt.gameObject.activeInHierarchy &&
                        RectTransformUtility.RectangleContainsScreenPoint(b.rt, mp, cam);
            b.hover = Mathf.MoveTowards(b.hover, over ? 1f : 0f, dt * 6f);
            b.label.color = Color.Lerp(new Color(0.851f, 0.827f, 0.878f), Color.white, b.hover);
            b.rt.localScale = Vector3.one * (1f + 0.04f * b.hover);
            float g = Mathf.Lerp(0.15f, 1f, b.hover);
            if (!Mathf.Approximately(b.frame.glow, g)) { b.frame.glow = g; b.frame.SetVerticesDirty(); }
        }
    }

    void UpdateTooltip(string a, string b, Vector2 mp, float dt)
    {
        bool show = a != null && state != State.Zooming;
        tooltipGroup.alpha = Mathf.MoveTowards(tooltipGroup.alpha, show ? 1f : 0f, dt * 8f);
        if (!show) return;
        if (tipName.text != a || tipSub.text != b)
        {
            tipName.text = a;
            tipSub.text = b ?? "";
            float w = Mathf.Max(tipName.GetPreferredValues(a).x, tipSub.GetPreferredValues(tipSub.text).x) + 48f;
            tooltip.sizeDelta = new Vector2(Mathf.Max(200f, w), 86f);
            tipFrame.SetVerticesDirty();
        }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(ui, mp, cam, out Vector2 lp))
        {
            Rect r = ui.rect;
            Vector2 size = tooltip.sizeDelta;
            Vector2 pos = lp + new Vector2(24f, -24f);
            pos.x = Mathf.Min(pos.x, r.xMax - size.x - 16f);
            pos.y = Mathf.Max(pos.y, r.yMin + size.y + 16f);
            tooltip.anchoredPosition = pos;
        }
    }

    /* ---------- zooming ---------- */

    IEnumerator ZoomIn(Province p)
    {
        state = State.Zooming;
        ClosePanel();
        OpenDetail(p);
        detailGroup.alpha = 0f;
        SetTitle(p.name, p.subtitle);
        backBtn.label.text = "The Realm";

        ZoomTarget(p, out float sT, out Vector2 posT);
        Vector2 pos0 = worldPos;
        Vector2 F = (posT - pos0 * sT) / (1f - sT); // the one point that stays still, so the zoom is a straight line

        for (float t = 0f; t < zoomTime; t += Time.unscaledDeltaTime)
        {
            float k = t / zoomTime, e = k * k * (3f - 2f * k);
            float s = Mathf.Pow(sT, e);
            world.localScale = Vector3.one * s;
            world.anchoredPosition = F - (F - pos0) * s;
            worldLabelsGroup.alpha = 1f - Mathf.Clamp01(k / 0.3f);
            detailGroup.alpha = Mathf.Clamp01((k - 0.55f) / 0.45f);
            yield return null;
        }
        world.localScale = Vector3.one * sT;
        world.anchoredPosition = posT;
        worldLabelsGroup.alpha = 0f;
        detailGroup.alpha = 1f;
        state = State.Detail;
    }

    IEnumerator ZoomOut()
    {
        state = State.Zooming;
        ClosePanel();
        SetTitle("The Realm", "Choose where Duskmoor strikes");
        backBtn.label.text = "Throne Room";

        float sT = world.localScale.x;
        Vector2 posT = world.anchoredPosition;
        Vector2 pos0 = PanTarget(world.sizeDelta, MousePos());
        Vector2 F = Mathf.Abs(1f - sT) > 0.001f ? (posT - pos0 * sT) / (1f - sT) : pos0;

        for (float t = 0f; t < zoomTime; t += Time.unscaledDeltaTime)
        {
            float k = t / zoomTime, e = k * k * (3f - 2f * k);
            float s = Mathf.Pow(sT, 1f - e);
            world.localScale = Vector3.one * s;
            world.anchoredPosition = F - (F - pos0) * s;
            detailGroup.alpha = 1f - Mathf.Clamp01(k / 0.35f);
            worldLabelsGroup.alpha = Mathf.Clamp01((k - 0.6f) / 0.4f);
            yield return null;
        }
        world.localScale = Vector3.one;
        worldPos = pos0;
        world.anchoredPosition = pos0;
        worldLabelsGroup.alpha = 1f;
        detail.gameObject.SetActive(false);
        open = null;
        state = State.World;
    }

    void JumpToDetail(Province p)
    {
        OpenDetail(p);
        world.sizeDelta = CoverSize(root.rect.size, worldPixels.x / worldPixels.y);
        ZoomTarget(p, out float sT, out Vector2 posT);
        world.localScale = Vector3.one * sT;
        world.anchoredPosition = posT;
        worldLabelsGroup.alpha = 0f;
        detailGroup.alpha = 1f;
        SetTitle(p.name, p.subtitle);
        backBtn.label.text = "The Realm";
        state = State.Detail;
    }

    // How much to zoom, and where to move, so the province fills the screen
    void ZoomTarget(Province p, out float sT, out Vector2 posT)
    {
        Vector2 S = world.sizeDelta, rs = root.rect.size;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var v in p.outline) { min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
        Vector2 c = (min + max) * 0.5f;
        Vector2 cLocal = new Vector2((c.x / worldPixels.x - 0.5f) * S.x, (0.5f - c.y / worldPixels.y) * S.y);
        float bw = (max.x - min.x) / worldPixels.x * S.x;
        float bh = (max.y - min.y) / worldPixels.y * S.y;
        sT = Mathf.Max(1.05f, Mathf.Min(rs.x / bw, rs.y / bh) * 0.9f);
        posT = -cLocal * sT;
    }

    void OpenDetail(Province p)
    {
        open = p;
        detailImg.sprite = p.detailMap;
        detail.gameObject.SetActive(true);
        detail.sizeDelta = CoverSize(root.rect.size, p.detailPixels.x / p.detailPixels.y);
        detailPan = PanTarget(detail.sizeDelta, MousePos());
        detail.anchoredPosition = detailPan;

        foreach (var m in markers) Destroy(m.rt.gameObject);
        markers.Clear();
        int n = 1;
        foreach (var loc in p.locations)
        {
            var m = new Marker { loc = loc };
            m.rt = NewRect(loc.id, markersRT);
            m.rt.anchorMin = m.rt.anchorMax = Norm(loc.pos, p.detailPixels);
            float size = loc.boss ? 78f : loc.optional ? 54f : 64f;
            m.rt.sizeDelta = Vector2.one * size;
            m.ring = m.rt.gameObject.AddComponent<Image>();
            m.ring.sprite = MapDraw.Circle;
            m.ring.raycastTarget = false;
            var inner = NewRect("Fill", m.rt);
            Stretch(inner);
            inner.offsetMin = Vector2.one * 6f;
            inner.offsetMax = -Vector2.one * 6f;
            m.fill = inner.gameObject.AddComponent<Image>();
            m.fill.sprite = MapDraw.Circle;
            m.fill.raycastTarget = false;
            var numRT = NewRect("Number", m.rt);
            Stretch(numRT);
            m.num = AddText(numRT, (n++).ToString(), headerFont, size * 0.42f, Color.white, TextAlignmentOptions.Center);
            markers.Add(m);
        }
        RefreshRoutes();
    }

    void RefreshRoutes()
    {
        routes.lines.Clear();
        if (open == null) return;
        foreach (var loc in open.locations)
        {
            Vector2 b = Norm(loc.pos, open.detailPixels);
            bool lit = IsOpen(loc) || CampaignProgress.IsCleared(loc.id);
            Color c = lit ? routeColor : new Color(lockedColor.r, lockedColor.g, lockedColor.b, 0.55f);
            if (loc.requires == null || loc.requires.Length == 0)
                routes.lines.Add(new RouteLines.Line { a = Norm(open.entry, open.detailPixels), b = b, color = c, width = 6f, dashed = loc.optional });
            else
                foreach (var req in loc.requires)
                {
                    var from = open.locations.Find(x => x.id == req);
                    if (from == null) continue;
                    routes.lines.Add(new RouteLines.Line { a = Norm(from.pos, open.detailPixels), b = b, color = c, width = 6f, dashed = loc.optional });
                }
        }
        routes.SetVerticesDirty();
    }

    /* ---------- the location panel ---------- */

    void ShowPanel(Location loc)
    {
        selected = loc;
        bool cleared = CampaignProgress.IsCleared(loc.id);
        bool hasBattle = !string.IsNullOrEmpty(loc.battleScene);
        pName.text = loc.name;
        string kind = loc.boss ? "Boss battle" : loc.optional ? "Optional battle" : "Battle";
        pTag.text = cleared ? kind + "  -  conquered" : kind;
        pDesc.text = loc.description;
        beginBtn.label.text = hasBattle ? (cleared ? "Fight again" : "Begin battle") : "Mark as won (test)";
        beginBtn.rt.gameObject.SetActive(hasBattle || !cleared);
        panelOpen = true;
    }

    void ClosePanel()
    {
        panelOpen = false;
        selected = null;
    }

    void Begin()
    {
        if (selected == null) return;
        if (!string.IsNullOrEmpty(selected.battleScene))
        {
            ReturnToProvince = open != null ? open.id : null;
            if (SceneLoader.Instance) SceneLoader.Instance.GoTo(selected.battleScene);
            return;
        }
        CampaignProgress.MarkCleared(selected.id); // no battles yet: mark it as won for testing
        ClosePanel();
        RefreshRoutes();
    }

    void Back()
    {
        if (state == State.Detail) StartCoroutine(ZoomOut());
        else if (state == State.World && SceneLoader.Instance) SceneLoader.Instance.GoTo(throneRoomScene);
    }

    [ContextMenu("Reset Campaign Progress")]
    void ResetProgress()
    {
        CampaignProgress.ResetAll();
        if (built) RefreshRoutes();
        Debug.Log("Campaign progress reset.");
    }

    /* ---------- rules ---------- */

    bool CanEnter(Province p) => p.available && p.detailMap != null;

    bool Conquered(Province p)
    {
        if (!p.available || p.locations.Count == 0) return false;
        foreach (var loc in p.locations)
            if (!loc.optional && !CampaignProgress.IsCleared(loc.id)) return false;
        return true;
    }

    bool IsOpen(Location loc)
    {
        if (loc.requires == null) return true;
        foreach (var r in loc.requires)
            if (!CampaignProgress.IsCleared(r)) return false;
        return true;
    }

    string Status(Province p)
    {
        if (!p.available) return p.subtitle;
        if (Conquered(p)) return "Conquered";
        return p.detailMap != null ? "Click to invade" : "No map assigned";
    }

    string LocationStatus(Location loc)
    {
        if (CampaignProgress.IsCleared(loc.id)) return "Conquered";
        if (!IsOpen(loc)) return "Locked";
        return loc.boss ? "Boss" : loc.optional ? "Optional" : "Open";
    }

    int ProvinceAt(Vector2 mp)
    {
        for (int i = 0; i < badges.Count; i++)
            if (badges[i].ring != null && RectTransformUtility.RectangleContainsScreenPoint(badges[i].rt, mp, cam)) return i;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(world, mp, cam, out Vector2 local)) return -1;
        Vector2 S = world.rect.size;
        Vector2 px = new Vector2((local.x / S.x + 0.5f) * worldPixels.x, (0.5f - local.y / S.y) * worldPixels.y);
        for (int i = 0; i < provinces.Count; i++)
            if (MapDraw.Inside(provinces[i].outline, px)) return i;
        return -1;
    }

    /* ---------- helpers ---------- */

    // Picture pixels (from the top-left) to 0-1 (from the bottom-left)
    static Vector2 Norm(Vector2 px, Vector2 size) => new Vector2(px.x / size.x, 1f - px.y / size.y);

    static Vector2 CoverSize(Vector2 area, float aspect) =>
        area.x / Mathf.Max(1f, area.y) > aspect ? new Vector2(area.x, area.x / aspect) : new Vector2(area.y * aspect, area.y);

    // Slide the oversized map toward the mouse, so its edges can be seen
    Vector2 PanTarget(Vector2 size, Vector2 mp)
    {
        Vector2 over = size - root.rect.size;
        float mx = Mathf.Clamp01(mp.x / Mathf.Max(1f, Screen.width));
        float my = Mathf.Clamp01(mp.y / Mathf.Max(1f, Screen.height));
        return new Vector2(-(mx - 0.5f) * over.x, -(my - 0.5f) * over.y) * panStrength;
    }

    Vector2 MousePos() =>
        Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width, Screen.height) * 0.5f;

    void SetTitle(string a, string b)
    {
        title.text = a;
        subtitle.text = b;
    }

    void Play(AudioClip clip, float volume = 1f)
    {
        if (clip != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(clip, volume);
    }

    Btn PlateButton(RectTransform parent, string text, Vector2 size, UnityAction onClick)
    {
        var rt = NewRect(text + " Button", parent);
        rt.sizeDelta = size;
        var hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0.004f); // nearly invisible but clickable
        hit.canvasRenderer.cullTransparentMesh = false;
        var frameRT = NewRect("Frame", rt);
        Stretch(frameRT);
        var frame = frameRT.gameObject.AddComponent<PlateFrame>();
        frame.accent = gold;
        frame.raycastTarget = false;
        var labelRT = NewRect("Label", rt);
        Stretch(labelRT);
        var label = AddText(labelRT, text, headerFont, 22f, Color.white, TextAlignmentOptions.Center);
        label.characterSpacing = 4f;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => { Play(clickSound); onClick(); });
        var b = new Btn { rt = rt, button = button, label = label, frame = frame };
        buttons.Add(b);
        return b;
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

    // A dark outline so text reads on top of the map
    static void Outline(TextMeshProUGUI t)
    {
        t.outlineWidth = 0.22f;
        t.outlineColor = new Color32(0, 0, 0, 255);
    }

    /* ---------- default campaign (filled in when the component is added) ---------- */

    void Reset()
    {
        provinces = new List<Province>
        {
            new Province
            {
                id = "farmlands", name = "The Farmlands", subtitle = "The peasants  -  Act I", available = true,
                outline = new[] { V(405, 300), V(560, 282), V(700, 295), V(705, 470), V(640, 505), V(560, 560), V(430, 560), V(395, 470), V(390, 400) },
                badgePos = V(478, 330),
                entry = V(150, 600),
                locations = new List<Location>
                {
                    L("windmill", "The Windmill", "The first farm at the edge of the dead wood. A handful of farmhands stand between Duskmoor and the fields.", V(310, 240)),
                    L("ashford", "Ashford", "The village at the crossroads. Its people have lit their torches.", V(620, 320), "windmill"),
                    L("watchtower", "The Watchtower", "Hunters watch the fields from the old tower. Expect arrows.", V(865, 70), "ashford", optional: true),
                    L("bridge", "The Stone Bridge", "The village militia holds the bridge over the river.", V(860, 410), "ashford"),
                    L("hamlet", "Pond Hamlet", "A quiet hamlet beside an old graveyard. Many souls rest here.", V(1165, 285), "bridge", optional: true),
                    L("hold", "Lord Harlan's Hold", "The lord of the Farmlands waits behind his walls. Break them, and the province falls.", V(1060, 470), "bridge", boss: true),
                }
            },
            Locked("highlands", "The Highlands", "Coming soon", V(805, 268),
                   V(700, 250), V(800, 235), V(905, 245), V(915, 375), V(820, 360), V(705, 400), V(700, 300)),
            Locked("mines", "The Mines", "Coming soon", V(1085, 420),
                   V(915, 285), V(1110, 300), V(1210, 360), V(1265, 560), V(1160, 610), V(1010, 580), V(930, 480), V(915, 375)),
            Locked("valley", "The River Valley", "Coming soon", V(250, 520),
                   V(0, 385), V(160, 372), V(300, 395), V(390, 400), V(395, 470), V(430, 560), V(560, 560), V(600, 600), V(600, 672), V(0, 672)),
            Locked("capital", "Stonehelm", "The capital  -  conquer all four provinces", V(805, 560),
                   V(705, 400), V(820, 360), V(915, 375), V(930, 480), V(1010, 580), V(960, 672), V(600, 672), V(600, 600), V(560, 560), V(640, 505), V(705, 470)),
        };
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);

    static Location L(string id, string name, string desc, Vector2 pos, string requires = null,
                      bool optional = false, bool boss = false) =>
        new Location
        {
            id = id, name = name, description = desc, pos = pos,
            requires = requires == null ? new string[0] : new[] { requires },
            optional = optional, boss = boss
        };

    static Province Locked(string id, string name, string sub, Vector2 badge, params Vector2[] outline) =>
        new Province { id = id, name = name, subtitle = sub, available = false, badgePos = badge, outline = outline };
}