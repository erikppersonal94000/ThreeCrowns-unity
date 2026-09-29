using UnityEngine;

// A soft band of light that sweeps across the title letters every few seconds.
public class TitleShine : TitleFx
{
    [Header("Shine")]
    public Color shineColor = new Color(1f, 0.973f, 0.91f, 0.55f);   // #FFF8E8
    public float interval = 6f;
    public float sweepTime = 1.2f;
    [Tooltip("Band width as a fraction of the title width.")]
    [Range(0.05f, 0.5f)] public float width = 0.14f;
    [Tooltip("How much the band leans. 0 = straight up and down.")]
    public float lean = 0.5f;

    protected override bool UseParticles => false;

    public override Color32 OverlayColor(int charIndex, int corner, Vector3 pos)
    {
        float cycle = Mathf.Repeat(T, interval);
        float span = TextXMax - TextXMin;
        if (cycle > sweepTime || span <= 0f) return FxKit.Col(shineColor, 0f);

        float w = span * width;
        float height = TextYMax - TextYMin;
        float center = Mathf.Lerp(TextXMin - w * 2f, TextXMax + w * 2f + lean * height, cycle / sweepTime);
        float d = (pos.x + (pos.y - TextYMin) * lean - center) / w;
        return FxKit.Col(shineColor, Mathf.Exp(-d * d));
    }
}