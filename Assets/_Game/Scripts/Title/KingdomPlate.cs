using UnityEngine;

// Shows this object only while its kingdom is hovered on the title screen.
// Fades in while settling down into place, and follows the center of its panel.
[RequireComponent(typeof(RectTransform))]
public class KingdomPlate : MonoBehaviour
{
    [SerializeField] TitlePanels panels;
    [Tooltip("0 = Duskmoor, 1 = Stonehelm, 2 = Frostvale")]
    [SerializeField] int kingdomIndex = 0;
    [SerializeField] float fadeSpeed = 6f;
    [SerializeField] float dropDistance = 24f; // starts this far above its spot and settles down

    RectTransform rt;
    CanvasGroup group;
    float homeY;
    float shown;

    void Awake()
    {
        rt = (RectTransform)transform;
        if (!TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
        homeY = rt.anchoredPosition.y;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        if (panels == null) panels = FindFirstObjectByType<TitlePanels>();
    }

    void Update()
    {
        bool on = panels != null && panels.Focus == kingdomIndex;
        shown = Mathf.Lerp(shown, on ? 1f : 0f, 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime));

        float settle = 1f - (1f - shown) * (1f - shown); // ease out
        group.alpha = shown;
        rt.anchoredPosition = new Vector2(
            panels != null ? panels.PanelCenterX(kingdomIndex) : rt.anchoredPosition.x,
            homeY + (1f - settle) * dropDistance);

        group.blocksRaycasts = shown > 0.5f;
        group.interactable = shown > 0.5f;
    }
}