using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Shared IMPACTED styles for the game's runtime UI and immediate-mode HUD.</summary>
    public static class VoxelHudStyles
    {
        private static Font hudFont;

        // Extra horizontal inset on the height-scaled 1920x1080 HUD (about 1cm on a phone).
        public const float ControlEdgeInset = 160f;

        public static Vector2 InsetControlPosition(Vector2 position, bool rightEdge = false) =>
            position + new Vector2(rightEdge ? -ControlEdgeInset : ControlEdgeInset, 0f);

        public static float LeftTouchAreaRightEdge(float canvasWidth) => Mathf.Max(630f, canvasWidth * .5f - 240f);

        public static RectTransform CreateExpandedTouchArea(RectTransform button, Vector2 size)
        {
            var area = new GameObject("Expanded Touch Area", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            area.transform.SetParent(button, false);
            var rect = area.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            area.GetComponent<Image>().color = Color.clear;
            return rect;
        }

        public static RectTransform GetTouchArea(RectTransform button) =>
            button.Find("Expanded Touch Area") is RectTransform expanded ? expanded : button;

        public static void SetLeftTouchAreaWidth(RectTransform area, float canvasWidth, float buttonX)
        {
            float edge = LeftTouchAreaRightEdge(canvasWidth);
            area.sizeDelta = new Vector2(edge, area.sizeDelta.y);
            area.anchoredPosition = new Vector2(edge * .5f - buttonX, 0f);
        }

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
