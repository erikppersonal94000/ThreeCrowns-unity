using UnityEngine;

// Put one in each scene to set its music and that track's volume.
// Leave the clip empty to keep whatever is already playing (e.g. the card library).
public class SceneMusic : MonoBehaviour
{
    [SerializeField] AudioClip music;
    [SerializeField, Range(0f, 1f)] float volume = 1f;

    void Start()
    {
        if (music != null && AudioManager.Instance)
            AudioManager.Instance.PlayMusic(music, volume);
    }
}