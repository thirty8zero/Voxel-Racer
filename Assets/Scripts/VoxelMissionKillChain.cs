using UnityEngine;
using System.Collections.Generic;

namespace VoxelRacer
{
    public sealed partial class VoxelMissionProgress
    {
        public const float KillChainWindowSeconds = 3f;
        public const float KillChainMultiplierPerKill = .25f;
        private const float KillChainPulseGrowth = .01f;
        private const float KillChainPeakScale = 1f + KillChainPulseGrowth;
        private const float KillChainMaximumShake = 8f;
        private const float KillChainOutlinePadding = 3f;
        private const float KillChainFeedbackPadding = KillChainOutlinePadding + KillChainMaximumShake;
        private const float KillChainHudGap = 4f;
        private static readonly Vector2 KillChainXAxis = new(.8660254f, .5f), KillChainYAxis = new(-.5f, .8660254f);
        public int KillChainCount { get; private set; }
        public float KillChainSecondsRemaining { get; private set; }
        public int KillChainDisplayCount => IsKillChainAnnouncementShowing ? KillChainCount : 0;
        public float KillChainAnnouncementRemaining => IsKillChainAnnouncementShowing ? KillChainSecondsRemaining : 0f;
        public bool IsKillChainAnnouncementShowing => KillChainCount >= 2 && KillChainSecondsRemaining > 0;
        private int KillChainHudCount => KillChainCount;
        private float killChainFeedbackAge;
        private Rect killChainLayoutMission, killChainLayoutArea;
        private int killChainLayoutCount = -1, killChainLayoutWidth, killChainLayoutHeight;
        private RectTransform[] killChainHudControls;
        private int killChainLayoutVisibility;
        private bool killChainLayoutSchemeB;

        private bool CanTrackKillChain => Tuning != null && !IsComplete && !IsFailed &&
            !VoxelPauseMenu.IsPaused && (startCountdown == null || startCountdown.IsComplete);

        private void ResetKillChain()
        {
            KillChainCount = 0;
            KillChainSecondsRemaining = 0;
            killChainFeedbackAge = 0;
        }

        private void RegisterKillChainEnemy(Vector3? position)
        {
            if (!CanTrackKillChain) return;
            KillChainCount = KillChainSecondsRemaining > 0 ? KillChainCount + 1 : 1;
            KillChainSecondsRemaining = KillChainWindowSeconds;
            killChainFeedbackAge = 0;
        }

        private void CancelKillChain()
        {
            if (CanTrackKillChain) ResetKillChain();
        }

        private void AdvanceKillChainClock(float seconds)
        {
            if (KillChainCount == 0) return;
            killChainFeedbackAge += Mathf.Min(seconds, KillChainSecondsRemaining);
            KillChainSecondsRemaining = Mathf.Max(0, KillChainSecondsRemaining - seconds);
            if (KillChainSecondsRemaining <= .000001f) FinishKillChain();
        }

        private void FinishKillChain()
        {
            if (KillChainCount < 2) { ResetKillChain(); return; }
            Rect source = GetKillChainArea(GetMissionHudArea(IsBossEncounter));
            float bonus = (KillChainCount - 1) * KillChainMultiplierPerKill;
            ResetKillChain();
            // Hide the live combo before sending its one total reward in the same tick.
            float before = EffectiveTimeBonusMultiplier;
            ChangeMultiplier(bonus, "KILL CHAIN COMBO");
            if (EffectiveTimeBonusMultiplier > before && multiplierFlights.Count > 0)
            {
                int last = multiplierFlights.Count - 1;
                var flight = multiplierFlights[last];
                flight.origin = new Vector2(source.center.x / Mathf.Max(1, Screen.width),
                    source.center.y / Mathf.Max(1, Screen.height));
                multiplierFlights[last] = flight;
            }
        }

        private void BankPendingKillChains()
        {
            float bonus = Mathf.Max(0, KillChainCount - 1) * KillChainMultiplierPerKill;
            // A finishing kill must still contribute to the payout even if its presentation hasn't run.
            ChangeMultiplier(bonus, "KILL CHAIN COMBO");
            ResetKillChain();
        }

        private Rect GetKillChainArea(Rect missionArea)
        {
            if (killChainHudControls == null)
            {
                var controls = new List<RectTransform>();
                foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (rect.name == "Integrity Dial Backplate" || rect.name == "Timer Ring Background" ||
                        rect.name == "Move Left Button" || rect.name == "Move Right Button" || rect.name == "Brake Button" ||
                        rect.name == "Boost Button" || rect.name == "Fire Button" || rect.name == "Missile Button") controls.Add(rect);
                killChainHudControls = controls.ToArray();
            }
            int visibility = 0;
            foreach (var control in killChainHudControls)
                visibility = unchecked(visibility * 31 + (control != null && control.gameObject.activeInHierarchy ? 1 : 0));
            if (killChainLayoutCount == KillChainHudCount && killChainLayoutWidth == Screen.width &&
                killChainLayoutHeight == Screen.height && killChainLayoutMission == missionArea &&
                killChainLayoutVisibility == visibility && killChainLayoutSchemeB == VoxelMobileControls.UseControlSchemeB)
                return killChainLayoutArea;
            float scale = HeaderScale;
            Rect multiplier = GetMultiplierArea(missionArea);
            // Measure the compact lettering, then reserve its full rotated bounds at peak pulse.
            Vector2 textSize = KillChainTextSize(1f, out var words, out var count);
            Vector2 size = KillChainRotatedBounds(textSize, words, count).size * scale;
            float gap = KillChainHudGap * scale;
            // Start at the right edge, then tuck the three-line group beside the timer.
            Rect area = new Rect(Screen.width - gap - size.x,
                multiplier.yMax + gap, size.x, size.y);
            var obstacles = new List<Rect> { missionArea, multiplier };
            var circles = new List<Rect>();
            foreach (var control in killChainHudControls)
            {
                if (control == null || !control.gameObject.activeInHierarchy) continue;
                var corners = new Vector3[4]; control.GetWorldCorners(corners);
                var bounds = new Rect(corners[0].x, Screen.height - corners[1].y,
                    corners[2].x - corners[0].x, corners[1].y - corners[0].y);
                if (control.name == "Timer Ring Background") circles.Add(bounds);
                else obstacles.Add(bounds);
            }
            // Pull left only as far as needed on narrow screens, keeping a tiny gap
            // from right-hand weapon controls. Bounds include maximum shake/pulse/outline.
            for (int step = 0; step < Screen.width && area.x > gap; step++)
            {
                bool overlaps = false;
                foreach (var obstacle in obstacles)
                {
                    Rect padded = Rect.MinMaxRect(obstacle.xMin - gap, obstacle.yMin - gap, obstacle.xMax + gap, obstacle.yMax + gap);
                    if (!area.Overlaps(padded)) continue;
                    if (KillChainOverlaps(area, padded)) { overlaps = true; break; }
                }
                foreach (var circle in circles)
                    if (KillChainOverlapsCircle(area, circle.center, Mathf.Max(circle.width, circle.height) * .5f + gap))
                    { overlaps = true; break; }
                if (!overlaps) break;
                area.x -= scale;
            }
            killChainLayoutCount = KillChainHudCount; killChainLayoutWidth = Screen.width;
            killChainLayoutHeight = Screen.height; killChainLayoutMission = missionArea;
            killChainLayoutVisibility = visibility; killChainLayoutSchemeB = VoxelMobileControls.UseControlSchemeB;
            return killChainLayoutArea = area;
        }

        private void DrawKillChain(Rect missionArea, float alpha)
        {
            if (!IsKillChainAnnouncementShowing || IsComplete || IsFailed) return;
            Rect area = GetKillChainArea(missionArea);
            Vector2 baseTextSize = KillChainTextSize(1f, out var baseWords, out var baseCount);
            Rect baseBounds = KillChainRotatedBounds(baseTextSize, baseWords, baseCount);
            float scale = area.width / baseBounds.width;
            Vector2 pivot = area.position - baseBounds.position * scale + GetKillChainShakeOffset() * scale;
            Vector2 textSize = KillChainTextSize(scale, out var wordStyle, out var countStyle);
            float age = killChainFeedbackAge;
            float pulse = 1f + Mathf.Sin(Mathf.Clamp01(age / .3f) * Mathf.PI) * KillChainPulseGrowth;
            Rect[] lines = KillChainTextRects(textSize, wordStyle, countStyle);
            for (int i = 0; i < lines.Length; i++) lines[i].position += pivot;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(30f, pivot);
            GUIUtility.ScaleAroundPivot(Vector2.one * pulse, pivot);
            GUI.color = new Color(1, 1, 1, alpha);
            DrawKillChainOutlinedText(lines[0], "Kill Chain", wordStyle, scale);
            DrawKillChainOutlinedText(lines[1], "Combo!", wordStyle, scale);
            DrawKillChainOutlinedText(lines[2], "x" + KillChainDisplayCount, countStyle, scale);
            GUI.matrix = matrix;
            GUI.color = new Color(1, 1, 1, alpha);
        }

        private float GetKillChainShakeStrength()
        {
            float peak = Mathf.Min(KillChainMaximumShake, 2f + Mathf.Max(0, KillChainCount - 2) * 1.2f);
            // Every kill renews the kick; a smaller tremor remains throughout the active window.
            return peak * (.25f + .75f * Mathf.Exp(-killChainFeedbackAge / .45f));
        }

        private Vector2 GetKillChainShakeOffset()
        {
            float age = killChainFeedbackAge;
            return new Vector2(Mathf.Sin(age * 67f), Mathf.Sin(age * 83f + .7f)) * GetKillChainShakeStrength();
        }

        private Vector2 KillChainTextSize(float scale, out GUIStyle words, out GUIStyle count)
        {
            words = KillChainTextStyle(Mathf.RoundToInt(90f * scale));
            count = KillChainTextStyle(Mathf.RoundToInt(100f * scale));
            float wordWidth = Mathf.Max(words.CalcSize(new GUIContent("Kill Chain")).x,
                words.CalcSize(new GUIContent("Combo!")).x);
            float countWidth = count.CalcSize(new GUIContent("x" + KillChainHudCount)).x;
            return new Vector2(Mathf.Max(wordWidth, countWidth), 258f * scale);
        }

        private static GUIStyle KillChainTextStyle(int size) => new GUIStyle
        {
            font = VoxelHudStyles.HudFont, fontSize = Mathf.Max(1, size),
            alignment = TextAnchor.MiddleCenter, wordWrap = false,
            normal = { textColor = new Color(1f, .12f, .1f) }
        };

        private Rect KillChainRotatedBounds(Vector2 size, GUIStyle words, GUIStyle count)
        {
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Rect text in KillChainTextRects(size, words, count))
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 p = new Vector2((corner & 1) == 0 ? text.xMin : text.xMax,
                        (corner & 2) == 0 ? text.yMin : text.yMax);
                    Vector2 rotated = (KillChainXAxis * p.x + KillChainYAxis * p.y) * KillChainPeakScale;
                    minimum = Vector2.Min(minimum, rotated); maximum = Vector2.Max(maximum, rotated);
                }
            return Rect.MinMaxRect(minimum.x - KillChainFeedbackPadding, minimum.y - KillChainFeedbackPadding,
                maximum.x + KillChainFeedbackPadding, maximum.y + KillChainFeedbackPadding);
        }

        private Rect[] KillChainTextRects(Vector2 size, GUIStyle words, GUIStyle count)
        {
            Vector2 countSize = count.CalcSize(new GUIContent("x" + KillChainHudCount));
            Vector2 killSize = words.CalcSize(new GUIContent("Kill Chain"));
            Vector2 comboSize = words.CalcSize(new GUIContent("Combo!"));
            float scale = size.y / 258f;
            // Centre the count beneath Combo! and use these same measured lines for drawing/clearance.
            Rect first = new Rect(new Vector2(0, -88f * scale) - killSize * .5f, killSize);
            Rect second = new Rect(new Vector2(0, -6f * scale) - comboSize * .5f, comboSize);
            Rect number = new Rect(new Vector2(0, 82f * scale) - countSize * .5f, countSize);
            return new[] { first, second, number };
        }

        // Placement validation uses the angled lines, including peak pulse, outline and maximum shake.
        // Their enclosing rectangle includes empty corners that can safely sit near other HUDs.
        private bool KillChainOverlaps(Rect area, Rect obstacle)
        {
            Vector2 size = KillChainTextSize(1f, out var words, out var count);
            Rect bounds = KillChainRotatedBounds(size, words, count);
            float scale = area.width / bounds.width;
            Vector2 pivot = area.position - bounds.position * scale;
            float padding = KillChainFeedbackPadding * scale;
            obstacle = Rect.MinMaxRect(obstacle.xMin - padding, obstacle.yMin - padding,
                obstacle.xMax + padding, obstacle.yMax + padding);
            Vector2 xAxis = KillChainXAxis;
            Vector2 yAxis = KillChainYAxis;
            foreach (Rect line in KillChainTextRects(size, words, count))
            {
                Vector2 centre = pivot + (xAxis * line.center.x + yAxis * line.center.y) * (scale * KillChainPeakScale);
                Vector2 half = line.size * (scale * KillChainPeakScale * .5f);
                Vector2 delta = obstacle.center - centre;
                Vector2 otherHalf = obstacle.size * .5f;
                if (Mathf.Abs(delta.x) > otherHalf.x + Mathf.Abs(xAxis.x) * half.x + Mathf.Abs(yAxis.x) * half.y ||
                    Mathf.Abs(delta.y) > otherHalf.y + Mathf.Abs(xAxis.y) * half.x + Mathf.Abs(yAxis.y) * half.y ||
                    Mathf.Abs(Vector2.Dot(delta, xAxis)) > half.x + otherHalf.x * Mathf.Abs(xAxis.x) + otherHalf.y * Mathf.Abs(xAxis.y) ||
                    Mathf.Abs(Vector2.Dot(delta, yAxis)) > half.y + otherHalf.x * Mathf.Abs(yAxis.x) + otherHalf.y * Mathf.Abs(yAxis.y))
                    continue;
                return true;
            }
            return false;
        }

        private bool KillChainOverlapsCircle(Rect area, Vector2 obstacleCentre, float radius)
        {
            Vector2 size = KillChainTextSize(1f, out var words, out var count);
            Rect bounds = KillChainRotatedBounds(size, words, count);
            float scale = area.width / bounds.width;
            Vector2 pivot = area.position - bounds.position * scale;
            radius += KillChainFeedbackPadding * scale; // Includes the outline and maximum shake.
            Vector2 xAxis = KillChainXAxis;
            Vector2 yAxis = KillChainYAxis;
            foreach (Rect line in KillChainTextRects(size, words, count))
            {
                Vector2 centre = pivot + (xAxis * line.center.x + yAxis * line.center.y) * (scale * KillChainPeakScale);
                Vector2 half = line.size * (scale * KillChainPeakScale * .5f);
                Vector2 delta = obstacleCentre - centre;
                float x = Mathf.Max(0, Mathf.Abs(Vector2.Dot(delta, xAxis)) - half.x);
                float y = Mathf.Max(0, Mathf.Abs(Vector2.Dot(delta, yAxis)) - half.y);
                if (x * x + y * y <= radius * radius) return true;
            }
            return false;
        }

        private static void DrawKillChainOutlinedText(Rect rect, string text, GUIStyle style, float scale)
        {
            var outline = new GUIStyle(style);
            outline.normal.textColor = Color.black;
            float thickness = 1.5f * scale;
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    if (x != 0 || y != 0)
                        GUI.Label(new Rect(rect.x + x * thickness, rect.y + y * thickness, rect.width, rect.height), text, outline);
            GUI.Label(rect, text, style);
        }
    }
}
