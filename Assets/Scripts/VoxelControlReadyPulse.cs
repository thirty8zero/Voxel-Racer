using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Shared lightweight recharge shockwave for cockpit controls.</summary>
    internal static class VoxelControlReadyPulse
    {
        internal const float Duration = .30f;
        internal const float RippleDelay = .06f;

        internal static void AnimateWave(Image image, float progress, float maximumScale, float strength,
            Color colour, Color flashColour)
        {
            progress = Mathf.Clamp01(progress);
            float travel = 1f - Mathf.Pow(1f - progress, 4f);
            float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / .10f));
            float fade = Mathf.Pow(1f - progress, 1.35f);
            image.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, maximumScale, travel);
            Color tint = Color.Lerp(flashColour, colour, Mathf.Clamp01(progress / .28f));
            tint.a = strength * attack * fade;
            image.color = tint;
        }

        // The caller owns and releases the returned sprite and texture.
        internal static Sprite CreateHalo(string name, out Texture2D texture)
        {
            const int size = 256;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * (size * .5f)) / (size * .5f);
                float band = (distance - .784f) / .10f;
                float alpha = Mathf.Exp(-band * band * 2f) * Mathf.Clamp01((1f - distance) * 32f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size, 0, SpriteMeshType.FullRect);
        }
    }
}
