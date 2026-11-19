using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>A planar wave of red spinning squares, each leaving a tapering square trail.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoxelMissileReadyEmbers : MaskableGraphic
    {
        internal const float Duration = .30f;
        public float Age { get; private set; } = -1f;
        private const int AngularSlices = 48, TrailSquares = 5;
        private const float TrailInterval = .018f;
        // The missile artwork is scaled 1.15x; leave a small gap above the gun below.
        private const float MaxRadius = 1.38f;
        private struct Ember
        {
            public Vector2 direction;
            public float size, angle, spin, heat;
        }
        private readonly Ember[] embers = BuildEmbers();

        internal void SetAge(float seconds)
        {
            if (Age == seconds) return;
            Age = seconds;
            enabled = seconds >= 0f && seconds < Duration;
            if (enabled) SetVerticesDirty();
        }

        private static float Variation(int index, uint salt)
        {
            uint value = unchecked((uint)(index + 1) * 747796405u + salt);
            value = ((value >> 16) ^ value) * 2246822519u;
            return (value & 65535u) / 65535f;
        }

        private static Ember[] BuildEmbers()
        {
            // Keep the original even-numbered sectors and their exact variations;
            // removing alternate heads also removes their associated square trails.
            var result = new Ember[AngularSlices / 2];
            for (int i = 0; i < AngularSlices; i += 2)
            {
                float angle = (i + (Variation(i, 7) - .5f) * .25f) * Mathf.PI * 2f / AngularSlices;
                result[i / 2] = new Ember
                {
                    direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                    size = Mathf.Lerp(.9f, 1.1f, Variation(i, 79)),
                    angle = Variation(i, 89) * 180f,
                    spin = Mathf.Lerp(540f, 1080f, Variation(i, 97)) * (i % 2 == 0 ? 1f : -1f),
                    heat = Variation(i, 31)
                };
            }
            return result;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Age < 0f || Age >= Duration) return;
            float progress = Mathf.Clamp01(Age / Duration);
            foreach (var ember in embers)
            for (int trail = 0; trail <= TrailSquares; trail++)
            {
                // Samples of this square's own earlier position and rotation form its
                // trail. No separate radial bands or frame-history allocations are needed.
                float sampleAge = Age - trail * TrailInterval;
                if (sampleAge < 0f) continue;
                float sampleProgress = Mathf.Clamp01(sampleAge / Duration);
                float travel = 1f - Mathf.Pow(1f - sampleProgress, 4f);
                float radius = Mathf.Lerp(1f, MaxRadius, travel);
                Vector2 position = new(ember.direction.x * 94f * radius, ember.direction.y * 76f * radius + 2f);
                float size = 20f * ember.size * Mathf.Pow(.90f, trail);
                float trailFraction = trail / (float)TrailSquares;
                float fadeStart = Mathf.Lerp(.55f, .12f, trailFraction);
                float fadeEnd = Mathf.Lerp(1f, .58f, trailFraction);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, fadeEnd, progress));
                float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sampleProgress / .06f));
                // Red heads lead varied burgundy/coral/orange tails, with no yellow.
                float heat = Mathf.Repeat(ember.heat + trail * .19f, 1f);
                Color tint = trail == 0
                    ? Color.Lerp(new Color(.72f, .01f, .025f), new Color(1f, .035f, .055f), heat)
                    : heat < .5f
                        ? Color.Lerp(new Color(.48f, .015f, .10f), new Color(1f, .08f, .12f), heat * 2f)
                        : Color.Lerp(new Color(1f, .08f, .12f), new Color(1f, .34f, .035f), (heat - .5f) * 2f);
                tint.a = attack * fade * .56f * Mathf.Pow(.92f, trail);
                float angle = ember.angle + ember.spin * sampleAge;
                Square(vh, position, size, angle, tint);
            }
        }

        private static void Square(VertexHelper vh, Vector2 centre, float size, float angle, Color tint)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector2 right = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (size * .5f);
            Vector2 up = new(-right.y, right.x);
            int start = vh.currentVertCount;
            vh.AddVert(centre - right - up, tint, Vector2.zero);
            vh.AddVert(centre - right + up, tint, Vector2.zero);
            vh.AddVert(centre + right + up, tint, Vector2.zero);
            vh.AddVert(centre + right - up, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
