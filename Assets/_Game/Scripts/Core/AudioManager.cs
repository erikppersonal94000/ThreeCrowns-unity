using System.Collections;
using UnityEngine;

// Music with crossfades, one-shot sound effects, and every volume in one place.
// Lives on the persistent Systems object.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Volume (drag these while the game is playing)")]
    [Range(0f, 1f)] public float masterVolume = 0.6f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    [Header("Music")]
    [SerializeField] float crossfadeTime = 1.5f;

    const string MuteKey = "tc-muted";
    const string MasterKey = "tc-master";
    const string MusicKey = "tc-music";
    const string SfxKey = "tc-sfx";

    class Channel
    {
        public AudioSource source;
        public float fade;             // 0..1, animated during crossfades
        public float trackVolume = 1f; // per-track level, set by each scene
        public Coroutine routine;
    }

    Channel a, b, current;
    AudioSource sfx;

    public bool Muted
    {
        get => AudioListener.volume == 0f;
        set
        {
            AudioListener.volume = value ? 0f : 1f;
            PlayerPrefs.SetInt(MuteKey, value ? 1 : 0);
        }
    }

    void Awake()
    {
        Instance = this;
        a = MakeChannel();
        b = MakeChannel();
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
        masterVolume = PlayerPrefs.GetFloat(MasterKey, masterVolume);
        musicVolume = PlayerPrefs.GetFloat(MusicKey, musicVolume);
        sfxVolume = PlayerPrefs.GetFloat(SfxKey, sfxVolume);
    }

    // Remembers the three volumes for the next time the game starts
    public void SaveVolumes()
    {
        PlayerPrefs.SetFloat(MasterKey, masterVolume);
        PlayerPrefs.SetFloat(MusicKey, musicVolume);
        PlayerPrefs.SetFloat(SfxKey, sfxVolume);
        PlayerPrefs.Save();
    }

    Channel MakeChannel()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = true;
        s.playOnAwake = false;
        return new Channel { source = s };
    }

    void Update()
    {
        Apply(a);
        Apply(b);
    }

    void Apply(Channel ch) =>
        ch.source.volume = ch.fade * ch.trackVolume * musicVolume * masterVolume;

    // Crossfade to a looping track. If it's already playing, just update its volume.
    public void PlayMusic(AudioClip clip, float trackVolume = 1f)
    {
        if (clip == null) return;

        if (current != null && current.source.clip == clip)
        {
            current.trackVolume = trackVolume;
            return;
        }

        var previous = current;
        current = (current == a) ? b : a;

        current.source.clip = clip;
        current.trackVolume = trackVolume;
        current.fade = 0f;
        current.source.Play();
        StartFade(current, 1f, false);

        if (previous != null) StartFade(previous, 0f, true);
    }

    public void StopMusic()
    {
        if (current == null) return;
        StartFade(current, 0f, true);
        current = null;
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip != null) sfx.PlayOneShot(clip, volume * sfxVolume * masterVolume);
    }

    void StartFade(Channel ch, float to, bool stopAtEnd)
    {
        if (ch.routine != null) StopCoroutine(ch.routine);
        ch.routine = StartCoroutine(Fade(ch, to, stopAtEnd));
    }

    IEnumerator Fade(Channel ch, float to, bool stopAtEnd)
    {
        float from = ch.fade;
        for (float t = 0f; t < crossfadeTime; t += Time.unscaledDeltaTime)
        {
            ch.fade = Mathf.Lerp(from, to, t / crossfadeTime);
            yield return null;
        }
        ch.fade = to;
        if (stopAtEnd) ch.source.Stop();
        ch.routine = null;
    }
}