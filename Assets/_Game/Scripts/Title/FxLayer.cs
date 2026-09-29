using UnityEngine;
using UnityEngine.UI;

// Particle layer for the title effects (embers, splashes, snow...). Drawn on top of the letters.
public class FxLayer : MaskableGraphic
{
    public TitleFx owner;
    public bool behind; // true = the layer drawn behind the letters

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public override Texture mainTexture => FxKit.Atlas;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (owner == null || !Application.isPlaying) return;
        if (behind) { owner.DrawBehind(vh); return; }
        if (owner.debug) owner.DrawDebug(vh);
        owner.DrawParticles(vh);
    }
}