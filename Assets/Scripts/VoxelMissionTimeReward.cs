using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelMissionProgress
    {
        private const float TimeRewardWidth = 326f, TimeRewardHeight = 294f, TimeRewardPeakScale = 1.04f;
        private static readonly Color TimeRewardCyan = new(.38f, .87f, 1f), TimeRewardSilver = new(.8f, .91f, 1f);
        private Texture2D timeRewardRing, timeRewardArc, timeRewardDisc, timeRewardStrip;
        private GUIStyle timeRewardNumberStyle, timeRewardSecondsStyle, timeRewardTitleStyle;
        private readonly GUIContent timeRewardNumber = new();
        private int timeRewardStyledAmount = -1;
        private RectTransform[] timeRewardControls;
        private readonly Vector3[] timeRewardCorners = new Vector3[4];
        private readonly Rect[] timeRewardObstacles = new Rect[12];
        private Rect timeRewardLayoutArea;
        private int timeRewardLayoutKey;
        private bool timeRewardHasLayout;

        private Rect GetTimeRewardArea(Rect timer)
        {
            if (timeRewardControls == null)
            {
                var controls = new List<RectTransform>();
                foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (rect.name == "Integrity Dial Backplate" || rect.name == "Move Left Button" || rect.name == "Move Right Button" ||
                        rect.name == "Brake Button" || rect.name == "Boost Button" || rect.name == "Fire Button" || rect.name == "Missile Button") controls.Add(rect);
                timeRewardControls = controls.ToArray();
            }
            float scale = HeaderScale, gap = 6f * scale;
            // Timer can grow by 25% for an extension, plus 18% during urgency. Also
            // reserve the badge's final 14px lift so it never crosses the pulsing dial.
            float top = timer.yMax + timer.height * (RemainingTime <= 10f ? .215f : .125f) + gap + 14f * scale;
            var header = GetMissionHudArea(IsBossEncounter);
            int obstacleCount = 0;
            timeRewardObstacles[obstacleCount++] = header;
            timeRewardObstacles[obstacleCount++] = GetMultiplierArea(header);
            foreach (var control in timeRewardControls)
            {
                if (control == null || !control.gameObject.activeInHierarchy || obstacleCount == timeRewardObstacles.Length) continue;
                control.GetWorldCorners(timeRewardCorners);
                timeRewardObstacles[obstacleCount++] = new Rect(timeRewardCorners[0].x, Screen.height - timeRewardCorners[1].y,
                    timeRewardCorners[2].x - timeRewardCorners[0].x, timeRewardCorners[1].y - timeRewardCorners[0].y);
            }
            Rect combo = IsKillChainAnnouncementShowing ? GetKillChainArea(header) : default;
            int layoutKey = timer.GetHashCode();
            unchecked
            {
                layoutKey = layoutKey * 397 ^ Screen.width;
                layoutKey = layoutKey * 397 ^ Screen.height;
                layoutKey = layoutKey * 397 ^ KillChainDisplayCount;
                layoutKey = layoutKey * 397 ^ (RemainingTime <= 10 ? 1 : 0);
                for (int i = 0; i < obstacleCount; i++) layoutKey = layoutKey * 397 ^ timeRewardObstacles[i].GetHashCode();
            }
            if (timeRewardHasLayout && layoutKey == timeRewardLayoutKey) return timeRewardLayoutArea;
            Rect area = default;
            // Prefer a large badge directly under the timer. Reduce only as necessary
            // for the right-hand controls/angled combo, preserving their existing layout.
            for (int attempt = 0; attempt < 48; attempt++)
            {
                float width = Mathf.Min(288f * scale * Mathf.Pow(.96f, attempt), Screen.width - 2f * gap);
                // Wrapped MULT/Combo layouts can occupy the dial's lower-left space.
                // Try the right gutter and a small downward offset before shrinking.
                for (int position = 0; position < 6; position++)
                {
                    float x = position == 0 ? timer.center.x - width * .5f : Screen.width - width - gap;
                    area = new Rect(Mathf.Clamp(x, gap, Mathf.Max(gap, Screen.width - width - gap)),
                        top + Mathf.Max(0, position - 1) * 32f * scale, width, width * TimeRewardHeight / TimeRewardWidth);
                    Rect padded = Rect.MinMaxRect(area.xMin - gap, area.yMin - gap, area.xMax + gap, area.yMax + gap);
                    bool clear = area.yMax + gap <= Screen.height;
                    for (int i = 0; i < obstacleCount && clear; i++) clear = !padded.Overlaps(timeRewardObstacles[i]);
                    if (clear && (!IsKillChainAnnouncementShowing || !KillChainOverlaps(combo, padded))) return CacheArea(area);
                }
            }
            return CacheArea(area);

            Rect CacheArea(Rect value)
            { timeRewardHasLayout = true; timeRewardLayoutKey = layoutKey; return timeRewardLayoutArea = value; }
        }

        private void DrawTimeExtension(Rect timer, float alpha)
        {
            float age = Time.unscaledTime - timeExtensionStartedAt;
            if (age < 0 || age > 2f || IsComplete || IsFailed || alpha <= 0) return;
            EnsureTimeRewardArt();
            Rect area = GetTimeRewardArea(timer);
            float fade = Mathf.Clamp01((age - 1.5f) / .5f);
            float entrance = Mathf.Clamp01(age / .18f);
            float pop = age < .18f ? Mathf.Lerp(.82f, TimeRewardPeakScale, 1f - Mathf.Pow(1f - entrance, 3))
                : Mathf.Lerp(TimeRewardPeakScale, 1f, Mathf.Clamp01((age - .18f) / .12f));
            float opacity = alpha * (1 - fade) * Mathf.Clamp01(age / .06f);
            float scale = area.width / TimeRewardWidth / TimeRewardPeakScale * pop;
            Vector2 origin = area.center - new Vector2(TimeRewardWidth, TimeRewardHeight) * scale * .5f - Vector2.up * (14f * HeaderScale * fade);
            var previous = GUI.matrix; Color previousColour = GUI.color;
            try
            {
                GUI.matrix = previous * Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one * scale);
                DrawTimeRewardTexture(timeRewardRing, new Rect(40, 9, 246, 246), new Color(.2f, .8f, 1f, .1f), opacity);
                DrawTimeRewardTexture(timeRewardRing, new Rect(52, 21, 222, 222), new Color(.2f, .8f, 1f, .2f), opacity);
                DrawTimeRewardTexture(timeRewardDisc, new Rect(69, 38, 188, 188), new Color(.015f, .045f, .075f, .97f), opacity);
                GUI.matrix *= Matrix4x4.Translate(new Vector3(163, 132, 0)) *
                    Matrix4x4.Rotate(Quaternion.Euler(0, 0, Mathf.Lerp(-65f, 0, Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .28f))))) *
                    Matrix4x4.Translate(new Vector3(-163, -132, 0));
                DrawTimeRewardTexture(timeRewardArc, new Rect(62, 31, 202, 202), TimeRewardCyan, opacity);
                GUI.matrix = previous * Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one * scale);
                DrawTimeRewardTexture(timeRewardRing, new Rect(71.5f, 40.5f, 183, 183), new Color(.4f, .7f, .85f, .3f), opacity);
                Fill(new Rect(143, 0, 40, 14), TimeRewardSilver, opacity);
                Fill(new Rect(152, 14, 22, 12), TimeRewardCyan, opacity);
                // IMPACTED has no usable plus glyph: draw the sign as crisp UI bars.
                Fill(new Rect(104, 124, 26, 5), Color.white, opacity);
                Fill(new Rect(114.5f, 113.5f, 5, 26), Color.white, opacity);
                GUI.color = new Color(1, 1, 1, opacity);
                GUI.Label(new Rect(132, 64, 123, 112), timeRewardNumber, timeRewardNumberStyle);
                GUI.Label(new Rect(63, 163, 200, 37), "SECONDS", timeRewardSecondsStyle);
                DrawTimeRewardTexture(timeRewardStrip, new Rect(0, 245, 326, 49), TimeRewardCyan, opacity);
                DrawTimeRewardTexture(timeRewardStrip, new Rect(3, 248, 320, 43), new Color(.025f, .1f, .15f), opacity);
                GUI.color = new Color(1, 1, 1, opacity);
                GUI.Label(new Rect(3, 249, 320, 38), "TIME EXTENDED!", timeRewardTitleStyle);
            }
            finally { GUI.matrix = previous; GUI.color = previousColour; }
        }

        private static void DrawTimeRewardTexture(Texture2D texture, Rect area, Color colour, float alpha)
        { GUI.color = new Color(colour.r, colour.g, colour.b, colour.a * alpha); GUI.DrawTexture(area, texture); }

        private void EnsureTimeRewardArt()
        {
            if (timeRewardRing == null)
            {
                timeRewardRing = CreateTimeRewardTexture(192, 192, 0);
                timeRewardArc = CreateTimeRewardTexture(192, 192, 1);
                timeRewardDisc = CreateTimeRewardTexture(192, 192, 2);
                timeRewardStrip = CreateTimeRewardTexture(128, 32, 3);
                timeRewardNumberStyle = TimeRewardStyle(98, Color.white);
                timeRewardSecondsStyle = TimeRewardStyle(25, TimeRewardCyan);
                timeRewardTitleStyle = TimeRewardStyle(28, TimeRewardSilver);
            }
            if (timeRewardStyledAmount == timeExtensionAmount) return;
            timeRewardStyledAmount = timeExtensionAmount;
            timeRewardNumber.text = timeExtensionAmount.ToString();
            timeRewardNumberStyle.fontSize = 98;
            float measured = timeRewardNumberStyle.CalcSize(timeRewardNumber).x;
            if (measured > 120) timeRewardNumberStyle.fontSize = Mathf.Max(12, Mathf.FloorToInt(98 * 120 / measured));
        }

        private static GUIStyle TimeRewardStyle(int size, Color colour) => new()
        { font = VoxelHudStyles.HudFont, fontSize = size, alignment = TextAnchor.MiddleCenter, wordWrap = false, normal = { textColor = colour } };

        private static Texture2D CreateTimeRewardTexture(int width, int height, int shape)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "Time Reward " + shape, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float dx = x + .5f - width * .5f, dy = y + .5f - height * .5f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy), alpha;
                if (shape == 3)
                {
                    float edge = Mathf.Min(x + .5f, width - x - .5f);
                    float cut = Mathf.Max(0, 8 - Mathf.Min(y + .5f, height - y - .5f));
                    alpha = Mathf.Clamp01(edge - cut);
                }
                else if (shape == 2) alpha = Mathf.Clamp01(94 - radius);
                else
                {
                    alpha = Mathf.Clamp01((radius - 77) * 1.5f) * Mathf.Clamp01((94 - radius) * 1.5f);
                    // Leave a deliberate stopwatch-ring gap, like the approved concept.
                    if (shape == 1 && Mathf.Repeat(Mathf.Atan2(dx, dy) / (Mathf.PI * 2) + 1, 1) > .86f) alpha = 0;
                }
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
            }
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }

        private void OnDestroy()
        {
            foreach (var texture in new[] { timeRewardRing, timeRewardArc, timeRewardDisc, timeRewardStrip })
                if (texture != null) { if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture); }
        }
    }
}
