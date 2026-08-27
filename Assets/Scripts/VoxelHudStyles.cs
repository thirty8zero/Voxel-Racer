using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Shared IMPACTED styles for the game's runtime UI and immediate-mode HUD.</summary>
    public static class VoxelHudStyles
    {
        private static Font hudFont;

        public static Font HudFont => hudFont != null ? hudFont : hudFont = Resources.Load<Font>("Fonts/IMPACTED");

        public static GUIStyle Box(int fontSize)
        {
            return new GUIStyle(GUI.skin.box)
            {
                font = HudFont,
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal
            };
        }

        public static GUIStyle Button(int fontSize)
        {
            return new GUIStyle(GUI.skin.button)
            {
                font = HudFont,
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal
            };
        }
    }
}
