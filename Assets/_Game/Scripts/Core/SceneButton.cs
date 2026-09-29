using UnityEngine;
using UnityEngine.UI;

// Add to any Button to make it travel to a scene through the smoke.
[RequireComponent(typeof(Button))]
public class SceneButton : MonoBehaviour
{
    [SerializeField] string sceneName;

    void Awake() => GetComponent<Button>().onClick.AddListener(Go);

    void Go()
    {
        if (SceneLoader.Instance) SceneLoader.Instance.GoTo(sceneName);
    }
}