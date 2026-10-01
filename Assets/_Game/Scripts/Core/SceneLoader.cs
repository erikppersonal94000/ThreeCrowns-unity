using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Moves between scenes.
//   SceneLoader.Instance.GoTo("Title");                            fade to black and back in
//   SceneLoader.Instance.EnterThroughDoor("Duskmoor", zoomTarget);  rush into the castle, darkness, a crack of
//                                                                   light widens like opening doors, and the
//                                                                   new scene fades in through it
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }
    public bool IsBusy { get; private set; }
    // True while the new scene is fading in (ThroneRoom starts its step-in then)
    public bool Revealing { get; private set; }
    // Set when a scene was entered through the door; that scene reads it and clears it
    public static bool EnteredThroughDoor;

    [Header("Fade")]
    [SerializeField] float fadeOutTime = 0.5f;
    [SerializeField] float fadeInTime = 0.6f;

    [Header("Rush into the castle")]
    [SerializeField] float zoomTime = 0.6f;
    [SerializeField] float zoomAmount = 6f;

    [Header("Doors opening in the dark")]
    [Tooltip("How long the screen stays dark before the light appears.")]
    [SerializeField] float darkHold = 0.4f;
    [Tooltip("How long the crack of light takes to widen.")]
    [SerializeField] float openTime = 0.9f;
    [Tooltip("How long the new scene takes to fade in through the light.")]
    [SerializeField] float revealTime = 1.1f;
    [SerializeField] Color lightColor = new Color(0.722f, 0.549f, 1f, 1f); // #B88CFF

    [Header("Sounds (optional)")]
    [SerializeField] AudioClip whooshSound;
    [Tooltip("Plays in the darkness: a heavy door creak (and boom) works best.")]
    [SerializeField] AudioClip doorSound;

    RectTransform canvasRT;
    RawImage shot;
    Image black, doorLight;

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
        if (!IsBusy) StartCoroutine(Door(sceneName, zoomTarget));
    }

    /* ---------- transitions ---------- */

    IEnumerator Fade(string sceneName)
    {
        IsBusy = true;
        var load = Begin(sceneName);
        black.gameObject.SetActive(true);
        yield return FadeAlpha(black, 0f, 1f, fadeOutTime);
        yield return Activate(load);
        yield return FadeAlpha(black, 1f, 0f, fadeInTime);
        black.gameObject.SetActive(false);
        IsBusy = false;
    }

    IEnumerator Door(string sceneName, Vector2 zoomTarget)
    {
        IsBusy = true;
        var load = Begin(sceneName);

        // 1. Freeze the screen (copied into an sRGB texture so the colors aren't washed out)
        yield return new WaitForEndOfFrame();
        var raw = ScreenCapture.CaptureScreenshotAsTexture();
        var still = new Texture2D(raw.width, raw.height, TextureFormat.RGBA32, false, false);
        still.SetPixels32(raw.GetPixels32());
        still.Apply(false);
        Destroy(raw);
        shot.texture = still;

        // Rush into the castle: zoom around one fixed point so the target ends up in the middle
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

        // 2. Darkness. The doors creak. Switch scenes while nothing is visible.
        Play(doorSound);
        EnteredThroughDoor = true;
        float darkStart = Time.unscaledTime;
        yield return Activate(load);
        while (Time.unscaledTime - darkStart < darkHold) yield return null;

        // 3. A crack of light appears in the middle and widens, like doors swinging open
        Vector2 screen = canvasRT.rect.size;
        doorLight.gameObject.SetActive(true);
        for (float t = 0f; t < openTime; t += Time.unscaledDeltaTime)
        {
            float k = t / openTime;
            float e = k * k * (3f - 2f * k);
            SetLight(Mathf.Lerp(0.015f, 0.9f, e * e) * screen.x, screen.y * 1.4f, 0.9f * Mathf.Clamp01(k * 4f));
            yield return null;
        }

        // 4. The new scene fades in through the light
        Revealing = true;
        for (float t = 0f; t < revealTime; t += Time.unscaledDeltaTime)
        {
            float k = t / revealTime;
            float e = k * k * (3f - 2f * k);
            SetAlpha(black, 1f - e);
            SetLight(Mathf.Lerp(0.9f, 2.2f, e) * screen.x, screen.y * 1.6f, 0.9f * (1f - e));
            yield return null;
        }

        doorLight.gameObject.SetActive(false);
        black.gameObject.SetActive(false);
        Revealing = false;
        IsBusy = false;
    }

    void SetLight(float width, float height, float alpha)
    {
        doorLight.rectTransform.sizeDelta = new Vector2(width, height);
        doorLight.color = new Color(lightColor.r, lightColor.g, lightColor.b, alpha);
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

    IEnumerator FadeAlpha(Graphic g, float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            SetAlpha(g, Mathf.Lerp(from, to, k * k * (3f - 2f * k)));
            yield return null;
        }
        SetAlpha(g, to);
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
        black = Full<Image>("Black", canvasRT);
        black.color = Color.black;
        black.raycastTarget = true; // blocks clicks while a transition runs

        // The crack of light: a soft glow in the middle of the screen, on top of the black
        var lightRT = NewRect("Door Light", canvasRT);
        lightRT.anchorMin = lightRT.anchorMax = new Vector2(0.5f, 0.5f);
        lightRT.pivot = new Vector2(0.5f, 0.5f);
        doorLight = lightRT.gameObject.AddComponent<Image>();
        doorLight.sprite = RoomFx.GlowSprite;
        doorLight.raycastTarget = false;

        shot.gameObject.SetActive(false);
        black.gameObject.SetActive(false);
        doorLight.gameObject.SetActive(false);
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

    static void SetAlpha(Graphic g, float a)
    {
        var c = g.color;
        c.a = a;
        g.color = c;
    }
}