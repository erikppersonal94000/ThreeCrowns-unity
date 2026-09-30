using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Moves between scenes.
//   SceneLoader.Instance.GoTo("Title");                            fade to black and back in
//   SceneLoader.Instance.EnterThroughDoor("Duskmoor", zoomTarget);  rush into the castle, the great
//                                                                   doors swing open, and you walk inside
// The door art is loaded from Resources/Transitions (DoorFrame, DoorLeft, DoorRight).
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }
    public bool IsBusy { get; private set; }
    // True while walking through the doorway into the new scene (ThroneRoom grows the room then)
    public bool Revealing { get; private set; }
    // Set when a scene was entered through the door; that scene reads it and clears it
    public static bool EnteredThroughDoor;
    // How much the room behind the door has grown as you walk in (ThroneRoom reads this)
    public float RoomScale { get; private set; } = 1f;

    [Header("Fade")]
    [SerializeField] float fadeOutTime = 0.5f;
    [SerializeField] float fadeInTime = 0.6f;

    [Header("Door transition")]
    [SerializeField] float zoomTime = 0.6f;
    [SerializeField] float zoomAmount = 6f;
    [SerializeField] float approachTime = 0.8f;
    [SerializeField] float openTime = 1.3f;
    [SerializeField] float stepTime = 1.8f;
    [Tooltip("How much farther away the room is than the doorway. Higher = the room grows more slowly as you walk in.")]
    [SerializeField] float roomDistance = 9f;
    [Tooltip("How far you walk, as a fraction of the distance to the doorway.")]
    [SerializeField] float walkDistance = 0.9f;
    [Tooltip("How dark the room looks through the doorway before you walk in (0 = fully visible, 1 = black).")]
    [Range(0f, 1f)] [SerializeField] float doorwayDarkness = 0.85f;

    [Header("Door feel")]
    [Tooltip("The jolt when the doors unlatch, and the pause after it.")]
    [SerializeField] float unlatchTime = 0.3f;
    [SerializeField] float unlatchPause = 0.2f;
    [Tooltip("How hard the view shakes when the doors hit the walls, in screen pixels.")]
    [SerializeField] float shakeAmount = 10f;
    [SerializeField] Color lightColor = new Color(0.722f, 0.549f, 1f, 1f); // #B88CFF

    [Header("Door shape (fractions of the door picture)")]
    [SerializeField] float hingeLeft = 0.383f;
    [SerializeField] float hingeRight = 0.619f;
    [SerializeField] float seam = 0.501f;
    [SerializeField] float doorBottom = 0.099f;
    [SerializeField] float doorTop = 0.794f;
    [Tooltip("Middle of the doorway (0-1, from the bottom-left). The walk-in heads toward it.")]
    [SerializeField] Vector2 doorway = new Vector2(0.501f, 0.45f);

    [Header("Sounds (optional)")]
    [SerializeField] AudioClip whooshSound;
    [SerializeField] AudioClip doorOpenSound;
    [SerializeField] AudioClip doorBoomSound;

    RectTransform canvasRT, doorRoot;
    CanvasGroup doorGroup;
    RawImage shot, frame;
    DoorLeaf leftDoor, rightDoor;
    Image black, dark, shaft, floorLight;
    Vector2 doorBase;

    void Awake()
    {
        Instance = this;
        BuildOverlay();
    }

    /* ---------- public ---------- */

    public void GoTo(string sceneName)
    {
        if (!IsBusy) StartCoroutine(Fade(sceneName));
    }

    public void EnterThroughDoor(string sceneName, Vector2 zoomTarget)
    {
        if (IsBusy) return;
        if (frame.texture == null || leftDoor.texture == null || rightDoor.texture == null)
        {
            Debug.LogWarning("SceneLoader: door art missing in Resources/Transitions (" +
                             (frame.texture == null ? "DoorFrame " : "") +
                             (leftDoor.texture == null ? "DoorLeft " : "") +
                             (rightDoor.texture == null ? "DoorRight " : "") +
                             "), using a fade instead.");
            GoTo(sceneName);
            return;
        }
        StartCoroutine(Door(sceneName, zoomTarget));
    }

    /* ---------- transitions ---------- */

    IEnumerator Fade(string sceneName)
    {
        IsBusy = true;
        var load = Begin(sceneName);
        black.gameObject.SetActive(true);
        yield return FadeBlack(0f, 1f, fadeOutTime);
        yield return Activate(load);
        yield return FadeBlack(1f, 0f, fadeInTime);
        black.gameObject.SetActive(false);
        IsBusy = false;
    }

    IEnumerator Door(string sceneName, Vector2 zoomTarget)
    {
        IsBusy = true;
        var load = Begin(sceneName);

        // 1. Freeze the screen. The capture comes back tagged as linear, so copy it into
        //    an sRGB texture, otherwise the colors look washed out.
        yield return new WaitForEndOfFrame();
        var raw = ScreenCapture.CaptureScreenshotAsTexture();
        var still = new Texture2D(raw.width, raw.height, TextureFormat.RGBA32, false, false);
        still.SetPixels32(raw.GetPixels32());
        still.Apply(false);
        Destroy(raw);
        shot.texture = still;

        // Zoom around one fixed point, picked so the target ends up in the middle of the screen
        Vector2 middle = new Vector2(0.5f, 0.5f);
        Vector2 pivot = middle + (zoomTarget - middle) * (zoomAmount / (zoomAmount - 1f));
        shot.rectTransform.pivot = new Vector2(Mathf.Clamp01(pivot.x), Mathf.Clamp01(pivot.y));
        shot.rectTransform.anchoredPosition = Vector2.zero;
        shot.rectTransform.localScale = Vector3.one;
        shot.gameObject.SetActive(true);
        black.gameObject.SetActive(true);
        SetAlpha(black, 0f);
        Play(whooshSound);

        for (float t = 0f; t < zoomTime; t += Time.unscaledDeltaTime)
        {
            float k = t / zoomTime;
            shot.rectTransform.localScale = Vector3.one * Mathf.Pow(zoomAmount, k * k);
            float d = Mathf.Clamp01((k - 0.5f) / 0.5f);
            SetAlpha(black, d * d);
            yield return null;
        }
        SetAlpha(black, 1f);
        shot.gameObject.SetActive(false);
        shot.texture = null;
        Destroy(still);

        // 2. Arrive at the great doors and walk up to them
        FitDoor();
        doorRoot.localScale = Vector3.one;
        doorGroup.alpha = 1f;
        doorRoot.gameObject.SetActive(true);
        SetDoors(0f);
        SetAlpha(dark, doorwayDarkness);
        for (float t = 0f; t < approachTime; t += Time.unscaledDeltaTime)
        {
            float k = t / approachTime;
            SetAlpha(black, 1f - Mathf.Clamp01(k * 4f));
            doorRoot.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        SetAlpha(black, 0f);

        // Swap scenes behind the closed doors
        yield return Activate(load);
        EnteredThroughDoor = true;

        // 3a. Unlatch: the doors jolt open a crack, shudder, and hang there for a moment
        Play(doorOpenSound);
        const float crack = 0.035f;
        for (float t = 0f; t < unlatchTime; t += Time.unscaledDeltaTime)
        {
            float k = t / unlatchTime;
            float o = crack * (1f - (1f - k) * (1f - k)) + 0.006f * Mathf.Sin(k * 40f) * (1f - k);
            SetDoors(o);
            yield return null;
        }
        SetDoors(crack);
        yield return Wait(unlatchPause);

        // 3b. The heavy swing: slow to start, builds speed, slows as it reaches the walls
        for (float t = 0f; t < openTime; t += Time.unscaledDeltaTime)
        {
            float k = t / openTime;
            float e = k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) * 0.5f;
            SetDoors(Mathf.Lerp(crack, 1f, e));
            yield return null;
        }

        // 3c. Thud: the doors hit the walls, bounce back a little, and the view shakes
        Play(doorBoomSound);
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.35f;
            SetDoors(1f - 0.03f * Mathf.Sin(k * Mathf.PI) * (1f - k));
            doorRoot.anchoredPosition = doorBase + Random.insideUnitCircle * shakeAmount * (1f - k) * (1f - k);
            yield return null;
        }
        SetDoors(1f);
        doorRoot.anchoredPosition = doorBase;

        // 4. Walk in. The doorway is close, so it grows fast; the room is far, so it grows slowly.
        Revealing = true;
        RoomScale = 1f;
        for (float t = 0f; t < stepTime; t += Time.unscaledDeltaTime)
        {
            float k = t / stepTime;
            float d = walkDistance * k * k * (3f - 2f * k);
            doorRoot.localScale = Vector3.one * (1.12f / (1f - d));
            RoomScale = roomDistance / (roomDistance - d);
            SetAlpha(dark, doorwayDarkness * (1f - k * k * (3f - 2f * k))); // eyes adjust as you step inside
            doorGroup.alpha = 1f - Mathf.Clamp01((k - 0.75f) / 0.25f);   // the frame has passed you by now
            yield return null;
        }
        RoomScale = roomDistance / (roomDistance - walkDistance);

        doorRoot.gameObject.SetActive(false);
        black.gameObject.SetActive(false);
        Revealing = false;
        IsBusy = false;
    }

    AsyncOperation Begin(string sceneName)
    {
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        var load = SceneManager.LoadSceneAsync(sceneName);
        load.allowSceneActivation = false; // load now, switch later
        return load;
    }

    IEnumerator Activate(AsyncOperation load)
    {
        while (load.progress < 0.9f) yield return null; // 0.9 = loaded and ready
        load.allowSceneActivation = true;
        while (!load.isDone) yield return null;
        yield return null; // give the new scene a frame to set itself up
    }

    IEnumerator FadeBlack(float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            SetAlpha(black, Mathf.Lerp(from, to, k * k * (3f - 2f * k)));
            yield return null;
        }
        SetAlpha(black, to);
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    // 0 = shut, 1 = swung open. Also drives the light bursting through the gap and across the floor.
    void SetDoors(float open)
    {
        leftDoor.SetOpen(open);
        rightDoor.SetOpen(open);

        Vector2 size = doorRoot.sizeDelta;
        float doorsW = (hingeRight - hingeLeft) * size.x;
        float gap = doorsW * (1f - Mathf.Cos(open * leftDoor.maxAngle * Mathf.Deg2Rad));
        float doorH = (doorTop - doorBottom) * size.y;
        float burst = Mathf.Clamp01(open * 10f); // light floods in the moment the doors crack

        // Strong while the gap is a crack, gone by the time the doors are half open,
        // so you see into the dark hall instead of a wall of purple
        float fade = (1f - open) * (1f - open);
        shaft.rectTransform.sizeDelta = new Vector2(gap * 1.2f + size.x * 0.01f, doorH * 1.05f);
        shaft.color = WithAlpha(lightColor, 0.8f * burst * fade);
        floorLight.rectTransform.sizeDelta = new Vector2(doorsW * (0.4f + 1.6f * open), size.y * 0.09f);
        floorLight.color = WithAlpha(lightColor, 0.45f * burst);
    }

    // Size the door picture to cover the screen, with the doorway as the zoom point
    void FitDoor()
    {
        Vector2 screen = canvasRT.rect.size;
        float aspect = (float)frame.texture.width / frame.texture.height;
        Vector2 size = screen.x / screen.y > aspect
            ? new Vector2(screen.x, screen.x / aspect)
            : new Vector2(screen.y * aspect, screen.y);
        doorRoot.pivot = doorway;
        doorRoot.sizeDelta = size;
        doorRoot.anchoredPosition = new Vector2((doorway.x - 0.5f) * size.x, (doorway.y - 0.5f) * size.y);
        doorBase = doorRoot.anchoredPosition;
    }

    void Play(AudioClip clip)
    {
        if (clip != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(clip);
    }

    /* ---------- building the overlay ---------- */

    void BuildOverlay()
    {
        var go = new GameObject("Transition Overlay", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // above every scene's UI
        go.AddComponent<GraphicRaycaster>();
        canvasRT = (RectTransform)go.transform;

        shot = Full<RawImage>("Freeze Frame", canvasRT);

        doorRoot = NewRect("Door", canvasRT);
        doorRoot.anchorMin = doorRoot.anchorMax = new Vector2(0.5f, 0.5f);
        doorGroup = doorRoot.gameObject.AddComponent<CanvasGroup>();
        doorGroup.blocksRaycasts = false;

        // Drawing order, back to front: darkness over the room, light through the gap,
        // the doors, the stone frame, then the light on the floor in front of the doorway
        dark = Full<Image>("Doorway Dark", doorRoot);
        dark.color = new Color(0.02f, 0.01f, 0.04f, 1f);

        shaft = Glow("Light Shaft", new Vector2(seam, (doorBottom + doorTop) * 0.5f));
        leftDoor = Full<DoorLeaf>("Left Door", doorRoot);
        rightDoor = Full<DoorLeaf>("Right Door", doorRoot);
        frame = Full<RawImage>("Frame", doorRoot);
        floorLight = Glow("Floor Light", new Vector2(seam, doorBottom));

        black = Full<Image>("Black", canvasRT);
        black.color = Color.black;
        black.raycastTarget = true; // blocks clicks while a transition runs

        frame.texture = Resources.Load<Texture2D>("Transitions/DoorFrame");
        SetupLeaf(leftDoor, Resources.Load<Texture2D>("Transitions/DoorLeft"), hingeLeft);
        SetupLeaf(rightDoor, Resources.Load<Texture2D>("Transitions/DoorRight"), hingeRight);

        shot.gameObject.SetActive(false);
        doorRoot.gameObject.SetActive(false);
        black.gameObject.SetActive(false);
    }

    Image Glow(string name, Vector2 anchor)
    {
        var rt = NewRect(name, doorRoot);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = RoomFx.GlowSprite;
        img.raycastTarget = false;
        img.color = WithAlpha(lightColor, 0f);
        return img;
    }

    void SetupLeaf(DoorLeaf leaf, Texture tex, float hinge)
    {
        leaf.texture = tex;
        leaf.hingeU = hinge;
        leaf.freeU = seam;
        leaf.bottomV = doorBottom;
        leaf.topV = doorTop;
        leaf.eye = doorway;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static T Full<T>(string name, Transform parent) where T : Graphic
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var g = rt.gameObject.AddComponent<T>();
        g.raycastTarget = false;
        return g;
    }

    static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    static void SetAlpha(Graphic g, float a)
    {
        var c = g.color;
        c.a = a;
        g.color = c;
    }
}