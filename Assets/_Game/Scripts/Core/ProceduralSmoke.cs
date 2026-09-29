using UnityEngine;
using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;
using MinMaxGradient = UnityEngine.ParticleSystem.MinMaxGradient;

// Smoke made entirely in code.
//  - Generates 4 soft, cloudy puff textures with Perlin noise (no image files needed)
//  - Sets up the particle system with a realistic preset
// Two layers: "Back" = big slow volumes, "Front" = lighter, faster wisps (a child of Back).
// Adding this component applies the preset. To re-apply: ⋮ menu → "Apply Smoke Preset".
[RequireComponent(typeof(ParticleSystem))]
public class ProceduralSmoke : MonoBehaviour
{
    public enum Layer { Back, Front }

    [Tooltip("A URP Particles/Unlit material set to Transparent. Its Base Map gets replaced.")]
    [SerializeField] Material material;
    [SerializeField] Layer layer = Layer.Back;

    [Header("Generated texture")]
    [SerializeField] int frameSize = 256;  // pixels per puff
    [SerializeField] int seed = 7;         // change to get different puff shapes

    const int Cols = 2, Rows = 2;          // 4 puffs in one texture

    // ---------- Runtime: build the texture and give it to the particles ----------
    void Awake()
    {
        if (material == null)
        {
            Debug.LogError("ProceduralSmoke: assign a material.", this);
            return;
        }

        var mat = new Material(material);                        // copy, so the asset isn't changed
        mat.SetTexture("_BaseMap", MakeSmokeSheet(frameSize, seed));
        GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
    }

    // ---------- Texture: 4 cloudy puffs in a 2x2 grid ----------
    static Texture2D MakeSmokeSheet(int size, int seed)
    {
        var tex = new Texture2D(size * Cols, size * Rows, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "ProceduralSmoke",
        };
        var px = new Color32[tex.width * tex.height];
        var rng = new System.Random(seed);

        for (int f = 0; f < Cols * Rows; f++)
        {
            // each puff samples a different patch of noise
            float ox = (float)rng.NextDouble() * 1000f;
            float oy = (float)rng.NextDouble() * 1000f;
            int baseX = (f % Cols) * size;
            int baseY = (f / Cols) * size;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // position across the puff, -1 to 1
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);

                // lumpy outline: noise pushes the edge in and out
                float edgeNoise = Fbm(u * 1.8f + ox, v * 1.8f + oy);
                float d = r + (edgeNoise - 0.5f) * 0.5f;
                float body = Mathf.Clamp01(1f - d);
                body = body * body * (3f - 2f * body);          // smooth falloff

                // billowy inside: thicker and thinner patches
                float detail = Fbm(u * 3.5f + oy, v * 3.5f + ox);
                float density = body * Mathf.Lerp(0.3f, 1f, Mathf.Pow(detail, 1.3f));

                // fade to nothing before the tile border, so no hard edges
                density *= Mathf.Clamp01((1f - r) / 0.2f);

                byte a = (byte)(Mathf.Clamp01(density) * 255f);
                byte g = (byte)(Mathf.Lerp(0.55f, 1f, detail) * 255f);   // light and shadow inside the puff
                px[(baseY + y) * tex.width + baseX + x] = new Color32(g, g, g, a);
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, true); // upload to the GPU and free the CPU copy
        return tex;
    }

    // Layered Perlin noise (fractal Brownian motion), roughly 0 to 1
    static float Fbm(float x, float y)
    {
        float sum = 0f, amp = 0.5f, freq = 1f;
        for (int i = 0; i < 4; i++)
        {
            sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / 0.9375f;
    }

    // ---------- Editor: set up the particle system ----------
    void Reset() => ApplyPreset();

    [ContextMenu("Apply Smoke Preset")]
    void ApplyPreset()
    {
        bool front = layer == Layer.Front;
        var ps = GetComponent<ParticleSystem>();
        var renderer = GetComponent<ParticleSystemRenderer>();

        if (front)
        {
            // Front is a child of the Back smoke: it inherits the position and upward direction
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            // Just below the screen, emitting upward
            transform.localPosition = new Vector3(0f, -7f, 0f);
            transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = front ? new MinMaxCurve(2f, 3f) : new MinMaxCurve(3f, 4.5f);
        main.startSpeed    = front ? new MinMaxCurve(12f, 16f) : new MinMaxCurve(10f, 14f);
        main.startSize     = front ? new MinMaxCurve(1.5f, 3.5f) : new MinMaxCurve(4f, 7f);
        main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = front
            ? new MinMaxGradient(new Color32(0x8A, 0x70, 0xAE, 255), new Color32(0x4A, 0x37, 0x66, 255))
            : new MinMaxGradient(new Color32(0x5E, 0x46, 0x80, 255), new Color32(0x22, 0x17, 0x2F, 255));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = front ? 400 : 900;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = front ? 60f : 110f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(28f, 0f, 1f);   // a wide strip, wider than ultrawide

        // Burst up fast, then slow down and hang, like real smoke
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.speedModifier = new MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.35f, 0.35f), new Keyframe(1f, 0.15f)));

        // Fade in fast, stay thick, fade out; darken as it cools
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.55f, 0.5f, 0.62f), 1f),
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(front ? 0.45f : 0.75f, 0.12f),
                new GradientAlphaKey(front ? 0.35f : 0.6f, 0.6f),
                new GradientAlphaKey(0f, 1f),
            });
        col.color = grad;

        // Expand quickly at first, then keep slowly spreading
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new MinMaxCurve(front ? 1.8f : 2.2f, new AnimationCurve(
            new Keyframe(0f, front ? 0.33f : 0.23f), new Keyframe(0.4f, 0.7f), new Keyframe(1f, 1f)));

        // Slow tumble
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        float spin = (front ? 25f : 12f) * Mathf.Deg2Rad;
        rot.z = new MinMaxCurve(-spin, spin);

        // The swirl and curl
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = front ? 2.2f : 1.6f;
        noise.frequency = front ? 0.45f : 0.2f;
        noise.scrollSpeed = front ? 0.6f : 0.35f;
        noise.octaveCount = front ? 2 : 3;
        noise.damping = true;
        noise.rotationAmount = front ? 0.5f : 0.3f;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        // Each particle picks one of the 4 generated puffs
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = Cols;
        sheet.numTilesY = Rows;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new MinMaxCurve(0f, 0.999f);
        sheet.cycleCount = 1;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.maxParticleSize = 3f;
        renderer.sortingLayerName = "Transition";
        renderer.sortingOrder = front ? 2 : 1;   // wisps draw over the volumes
        if (material != null) renderer.sharedMaterial = material;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(ps);
        UnityEditor.EditorUtility.SetDirty(renderer);
        UnityEditor.EditorUtility.SetDirty(transform);
#endif
    }
}