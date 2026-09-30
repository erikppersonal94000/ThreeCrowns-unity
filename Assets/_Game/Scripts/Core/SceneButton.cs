using UnityEngine;
using UnityEngine.UI;

// Add to any Button to make it travel to a scene.
[RequireComponent(typeof(Button))]
public class SceneButton : MonoBehaviour
{
    [SerializeField] string sceneName;
    [Tooltip("Rush into the castle and open the great doors, instead of a plain fade.")]
    [SerializeField] bool throughDoor;
    [Tooltip("Where the zoom rushes toward, 0-1 from the bottom-left of the screen.")]
    [SerializeField] Vector2 zoomTarget = new Vector2(0.27f, 0.62f);

    void Awake() => GetComponent<Button>().onClick.AddListener(Go);

    void Go()
    {
        if (!SceneLoader.Instance) return;
        if (throughDoor) SceneLoader.Instance.EnterThroughDoor(sceneName, zoomTarget);
        else SceneLoader.Instance.GoTo(sceneName);
    }
}