using UnityEngine;

// Creates the persistent "Systems" object (audio, scene loading, smoke)
// before the first scene loads, so you can press Play from any scene.
public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        var prefab = Resources.Load<GameObject>("Systems");
        if (prefab == null)
        {
            Debug.LogError("Bootstrap: no prefab named 'Systems' in a Resources folder.");
            return;
        }

        var systems = Object.Instantiate(prefab);
        systems.name = "Systems";
        Object.DontDestroyOnLoad(systems);
    }
}