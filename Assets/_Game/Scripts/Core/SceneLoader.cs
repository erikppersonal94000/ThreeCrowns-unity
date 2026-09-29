using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Moves between scenes behind a wall of smoke.
//   SceneLoader.Instance.GoTo("Duskmoor");
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Smoke")]
    [SerializeField] ParticleSystem smoke;
    [SerializeField] SpriteRenderer veil;   // dark layer under the smoke that finishes the cover
    [SerializeField] AudioClip smokeSound;

    [Header("Timing")]
    [SerializeField] float coverTime = 1.4f;
    [SerializeField] float revealTime = 1.6f;
    [SerializeField] AnimationCurve veilCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public bool IsBusy { get; private set; }

    void Awake()
    {
        Instance = this;
        SetVeil(0f);
    }

    // Keep the smoke centered on, and sized to, whatever camera the current scene uses
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;

        var p = cam.transform.position;
        transform.position = new Vector3(p.x, p.y, 0f);

        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        veil.transform.localScale = new Vector3(w + 2f, h + 2f, 1f);
    }

    public void GoTo(string sceneName)
    {
        if (!IsBusy) StartCoroutine(Run(sceneName));
    }

    IEnumerator Run(string sceneName)
    {
        IsBusy = true;

        // Start loading immediately, in the background, while the smoke rises.
        // Low priority = Unity spreads the work thinly across frames instead of in big chunks.
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        var load = SceneManager.LoadSceneAsync(sceneName);
        load.allowSceneActivation = false; // load it, but don't switch yet

        if (smokeSound && AudioManager.Instance) AudioManager.Instance.PlaySfx(smokeSound);
        if (smoke) smoke.Play();
        yield return FadeVeil(0f, 1f, coverTime);

        // Unity reports 0.9 when the scene is loaded and ready to switch
        while (load.progress < 0.9f) yield return null;

        // Switch while the screen is fully covered
        load.allowSceneActivation = true;
        while (!load.isDone) yield return null;
        yield return null; // give the new scene a frame to set itself up

        if (smoke) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        yield return FadeVeil(1f, 0f, revealTime);

        IsBusy = false;
    }

    IEnumerator FadeVeil(float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            SetVeil(Mathf.Lerp(from, to, veilCurve.Evaluate(t / time)));
            yield return null;
        }
        SetVeil(to);
    }

    void SetVeil(float alpha)
    {
        var c = veil.color;
        c.a = alpha;
        veil.color = c;
    }
}