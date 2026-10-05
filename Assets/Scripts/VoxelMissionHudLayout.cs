using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelMissionProgress
    {
        private float HeaderScale => Mathf.Max(.1f, Screen.height / 1080f);

        private Rect GetMissionHudArea(bool compactBoss)
        {
            float scale = HeaderScale;
            float left = 310f * scale;
            float right = GetCountdownRect().xMin - 36f * scale;
            float available = Mathf.Max(1f, right - left);
            bool inlineMultiplier = available >= 1272f * scale;
            float width = Mathf.Min(990f * scale, available - (inlineMultiplier ? 282f * scale : 0));
            float x = Mathf.Clamp((Screen.width - 810f * scale) * .5f, left,
                Mathf.Max(left, right - width - (inlineMultiplier ? 282f * scale : 0)));
            return new Rect(x, 18f * scale, width, (compactBoss ? 90f : 105f) * scale);
        }

        private Rect GetMultiplierArea(Rect missionArea)
        {
            float scale = HeaderScale;
            float width = Mathf.Min(270f * scale, missionArea.width);
            bool fitsBeside = missionArea.xMax + 12f * scale + width <= GetCountdownRect().xMin - 36f * scale;
            return new Rect(fitsBeside ? missionArea.xMax + 12f * scale : missionArea.xMax - width,
                fitsBeside ? missionArea.y : missionArea.yMax + 12f * scale, width, 105f * scale);
        }

        private void DrawMissionHeader(Rect area, bool compactBoss, float alpha)
        {
            float scale = HeaderScale;
            GUI.Box(area, GUIContent.none, VoxelHudStyles.Box(30));
            Color colour = IsComplete ? new Color(.25f, 1f, .38f) : Color.white;
            float badgeWidth = Mathf.Min(180f * scale, area.width * .26f);
            var badge = new Rect(area.xMax - badgeWidth, area.y, badgeWidth, area.height);
            Fill(badge, new Color(.015f, .025f, .04f, .45f), alpha);
            Fill(new Rect(badge.x, badge.y + 12f * scale, 2f * scale, badge.height - 24f * scale),
                new Color(1f, .72f, .14f, .5f), alpha);
            GUI.color = new Color(1, 1, 1, alpha);

            var headingArea = new Rect(area.x + 36f * scale, area.y + 3f * scale,
                Mathf.Max(1, area.width - badgeWidth - 72f * scale), (compactBoss ? 45f : 63f) * scale);
            var nameStyle = HeaderStyle(Mathf.RoundToInt((compactBoss ? 42f : 60f) * scale), colour, VoxelHudStyles.HudFont);
            string name = IsComplete ? "MISSION COMPLETE" : DisplayName;
            if (IsComplete || IsBossEncounter)
            {
                FitHeaderText(nameStyle, name, headingArea.width);
                GUI.Label(headingArea, name, nameStyle);
            }
            else
            {
                percentageSymbolFont ??= Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
                string score = Points + "/" + Mathf.Max(1, Tuning.requiredPoints);
                var scoreStyle = HeaderStyle(Mathf.RoundToInt(36f * scale), colour, percentageSymbolFont);
                float scoreSpace = Mathf.Min(headingArea.width * .42f, scoreStyle.CalcSize(new GUIContent(score)).x);
                FitHeaderText(scoreStyle, score, scoreSpace);
                float scoreWidth = scoreStyle.CalcSize(new GUIContent(score)).x;
                float gap = 18f * scale;
                FitHeaderText(nameStyle, name, Mathf.Max(1, headingArea.width - scoreWidth - gap));
                float nameWidth = nameStyle.CalcSize(new GUIContent(name)).x;
                float start = headingArea.center.x - (nameWidth + gap + scoreWidth) * .5f;
                GUI.Label(new Rect(start, headingArea.y, nameWidth, headingArea.height), name, nameStyle);
                GUI.Label(new Rect(start + nameWidth + gap, headingArea.y, scoreWidth, headingArea.height), score, scoreStyle);
            }

            // IMPACTED has no percent glyph; the full readout uses the HUD's numeric font.
            percentageSymbolFont ??= Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
            int wholePercent = IsComplete ? 100 : Mathf.Min(99, Mathf.RoundToInt(Percent * 100f));
            string percentage = wholePercent + "%";
            var percentStyle = HeaderStyle(Mathf.RoundToInt(90f * scale), colour, percentageSymbolFont);
            FitHeaderText(percentStyle, percentage, Mathf.Max(1, badge.width - 16f * scale));
            GUI.Label(badge, percentage, percentStyle);
            PercentageScreenPosition = badge.center;

            var bar = new Rect(headingArea.x, area.y + (compactBoss ? 51f : 72f) * scale,
                headingArea.width, (compactBoss ? 22.5f : 19.5f) * scale);
            if (!IsBossEncounter || missionBoss != null || IsComplete)
            {
                Fill(bar, new Color(.08f, .09f, .12f), alpha);
                float value = IsBossEncounter ? (IsComplete ? 0 : missionBoss.HealthPercent) : Percent;
                Fill(new Rect(bar.x + 3f * scale, bar.y + 3f * scale,
                    Mathf.Max(0, (bar.width - 6f * scale) * value), Mathf.Max(0, bar.height - 6f * scale)),
                    IsComplete ? new Color(.25f, 1f, .38f) : new Color(1f, .72f, .14f), alpha);
            }
            GUI.color = new Color(1, 1, 1, alpha);
        }

        private static GUIStyle HeaderStyle(int size, Color colour, Font font) => new GUIStyle(GUI.skin.label)
        {
            font = font, fontSize = Mathf.Max(1, size), fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter, wordWrap = false, normal = { textColor = colour }
        };

        private static void FitHeaderText(GUIStyle style, string text, float width)
        {
            float measured = style.CalcSize(new GUIContent(text)).x;
            if (measured > width) style.fontSize = Mathf.Max(1, Mathf.FloorToInt(style.fontSize * width / measured));
        }
    }
}
