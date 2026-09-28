using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Shared runtime/editor drawing for the radar objective. Artwork uses local GUI coordinates.</summary>
    public static class VoxelRadarObjectiveHud
    {
        public const string Instruction = "Find and destroy a radar car to locate the Boss";
        private static Texture2D silhouette, explosion;

        public static Rect Area(float screenWidth)
        {
            float width = Mathf.Min(620f, screenWidth - 24f);
            return new Rect((screenWidth - width) * .5f, 18f, width, 142f);
        }

        public static void Draw(Rect area, float alpha, bool destroyed, float secondsSinceDestruction)
        {
            if (alpha <= 0f) return;
            EnsureArtwork();
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Box(area, GUIContent.none, VoxelHudStyles.Box(30));
            var style = new GUIStyle(GUI.skin.label)
            {
                font = VoxelHudStyles.HudFont, fontSize = 28,
                alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }
            };
            style.fontSize = Mathf.Max(12, Mathf.FloorToInt(28f * Mathf.Min(1f,
                (area.width - 28f) / style.CalcSize(new GUIContent(Instruction)).x)));
            GUI.Label(new Rect(area.x + 12f, area.y + 7f, area.width - 24f, 38f), Instruction, style);
            GUI.color = new Color(.86f, .90f, .94f, alpha);
            GUI.DrawTexture(new Rect(area.center.x - 90f, area.y + 47f, 180f, 84f), silhouette);
            if (destroyed)
            {
                float pop = 1f + .25f * Mathf.Sin(Mathf.Clamp01(secondsSinceDestruction / .35f) * Mathf.PI);
                float size = 78f * pop;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(area.center.x - size * .5f + 9f,
                    area.y + 87f - size * .5f, size, size), explosion);
            }
            GUI.color = previous;
        }

        private static void EnsureArtwork()
        {
            if (silhouette == null)
            {
                // Stepped interceptor profile, two wheels, roof pedestal and tilted radar dish.
                silhouette = Raster(384, 180, (x, y) =>
                {
                    bool body = Inside(x, y, Body) || Inside(x, y, Dish) ||
                        (x >= 176 && x <= 187 && y >= 40 && y <= 71) ||
                        (x >= 190 && x <= 223 && y >= 30 && y <= 36) ||
                        (x - 86) * (x - 86) + (y - 139) * (y - 139) <= 23 * 23 ||
                        (x - 291) * (x - 291) + (y - 139) * (y - 139) <= 23 * 23;
                    return body ? Color.white : Color.clear;
                });
            }
            if (explosion == null)
                explosion = Raster(192, 192, (x, y) =>
                {
                    if (!Inside(x, y, Burst)) return Color.clear;
                    if (Inside(96 + (x - 96) / .38f, 96 + (y - 96) / .38f, Burst))
                        return new Color(1f, .98f, .75f);
                    if (Inside(96 + (x - 96) / .69f, 96 + (y - 96) / .69f, Burst))
                        return new Color(1f, .88f, .08f);
                    return new Color(1f, .37f, .025f);
                });
        }

        private static readonly Vector2[] Body = {
            new(20, 111), new(91, 105), new(121, 73), new(141, 66), new(229, 66),
            new(262, 101), new(346, 109), new(358, 120), new(358, 139), new(22, 139)
        };
        private static readonly Vector2[] Dish = {
            new(145, 12), new(156, 12), new(174, 29), new(199, 38),
            new(208, 39), new(197, 52), new(178, 52), new(157, 36)
        };
        private static readonly Vector2[] Burst = {
            new(15, 9), new(80, 48), new(98, 9), new(111, 57), new(185, 31),
            new(133, 86), new(169, 143), new(115, 118), new(91, 185),
            new(77, 124), new(15, 168), new(53, 106), new(3, 90), new(57, 72)
        };

        private static bool Inside(float x, float y, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[i]; var b = polygon[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static Texture2D Raster(int width, int height, System.Func<float, float, Color> sample)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "Radar objective artwork", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++) pixels[(height - 1 - y) * width + x] = sample(x + .5f, y + .5f);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
