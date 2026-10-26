using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelMissionProgress
    {
        private bool lastMultiplierWasNegative;
        private static readonly Color GainColour = new Color(.25f, 1f, .48f);
        private static readonly Color LossColour = new Color(1f, .24f, .18f);
        private float timeExtensionStartedAt = float.NegativeInfinity;
        private int timeExtensionAmount;
        public float TimeExtensionPulse => Mathf.Clamp01(1f - (Time.unscaledTime - timeExtensionStartedAt) / 1.5f);

        public bool AddBonusTime(int seconds, Vector3? position = null)
        {
            // Extensions must be collected before the countdown expires.
            if (Tuning == null || IsComplete || IsFailed || !TimeBonusAvailable || seconds <= 0 ||
                (startCountdown != null && !startCountdown.IsComplete)) return false;
            RemainingTime += seconds;
            timeExtensionAmount = Time.unscaledTime - timeExtensionStartedAt < .2f ? timeExtensionAmount + seconds : seconds;
            timeExtensionStartedAt = Time.unscaledTime;
            return true;
        }

        private GUIStyle BonusStyle(int size, Color colour) => new GUIStyle(GUI.skin.label)
        {
            font = percentageSymbolFont, fontSize = size, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = colour }
        };

        private void DrawMultiplier(Rect missionArea, float alpha)
        {
            percentageSymbolFont ??= Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
            bool expired = !TimeBonusAvailable;
            bool urgent = !IsComplete && !expired && RemainingTime <= Tuning.bonusWarningSeconds;
            Color tone = expired || urgent ? LossColour : new Color(1f, .8f, .2f);
            Rect timer = GetCountdownRect();
            var panel = GetMultiplierArea(missionArea);
            float layoutScale = panel.height / 70f;
            Fill(panel, new Color(.025f, .04f, .065f, .94f), alpha);
            Fill(new Rect(panel.x, panel.y, panel.width, 2 * layoutScale), tone, alpha);
            int numberSize = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(panel.width / 4.8f, 30 * layoutScale)));
            var target = new Rect(panel.x + 5 * layoutScale, panel.y + 3 * layoutScale, panel.width - 10 * layoutScale, 34 * layoutScale);
            float pulse = Mathf.Clamp01((multiplierPulseUntil - Time.unscaledTime) / .32f);
            float scale = 1 + Mathf.Sin(pulse * Mathf.PI) * .12f;
            var oldMatrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * scale, target.center);
            GUI.color = new Color(1, 1, 1, alpha);
            Color multiplierColour = pulse > 0 ? (lastMultiplierWasNegative ? LossColour : GainColour) : Color.white;
            GUI.Label(target, FormatMultiplier(displayedMultiplierBonus), BonusStyle(numberSize, multiplierColour));
            GUI.matrix = oldMatrix;
            int extra = Mathf.Max(0, Mathf.RoundToInt(Tuning.completionCurrencyAward * EffectiveTimeBonusMultiplier) - Tuning.completionCurrencyAward);
            string status = IsComplete ? "BANKED" : expired ? (EffectiveTimeBonusMultiplier > 1f ? "DECAYING" : "MINIMUM") : "AT RISK";
            GUI.Label(new Rect(panel.x + 4 * layoutScale, panel.y + 37 * layoutScale, panel.width - 8 * layoutScale, 13 * layoutScale), status, BonusStyle(Mathf.Max(1, Mathf.RoundToInt(11 * layoutScale)), tone));
            GUI.Label(new Rect(panel.x + 4 * layoutScale, panel.y + 49 * layoutScale, panel.width - 8 * layoutScale, 20 * layoutScale), "+$" + extra, BonusStyle(Mathf.Min(numberSize, Mathf.RoundToInt(19 * layoutScale)), tone));

            foreach (var flight in multiplierFlights)
            {
                float age = Time.unscaledTime - flight.startedAt;
                // Briefly hold the labelled gain at its source, then visibly connect it to the counter.
                float travelTime = Mathf.Clamp01((age - GetMultiplierHoldDuration(flight.reason)) / MultiplierTravelDuration);
                float travel = Mathf.SmoothStep(0, 1, travelTime);
                Vector2 from = new Vector2(flight.origin.x * Screen.width, flight.origin.y * Screen.height);
                // Start above the vehicle rather than over its body, while keeping clear of the header.
                if (flight.reason != "KILL CHAIN COMBO")
                    from.y = Mathf.Max(missionArea.yMax + 24f,
                        from.y - Mathf.Max(60f, Screen.height * .12f));
                Vector2 point = Vector2.Lerp(from, target.center, travel);
                Color colour = flight.amount < 0 ? LossColour : GainColour;
                for (int dot = 1; dot <= 5; dot++)
                {
                    Vector2 trail = Vector2.Lerp(from, point, 1 - dot * .035f);
                    Fill(new Rect(trail.x - 2, trail.y - 2, 4, 4), colour, alpha * (1 - dot / 6f) * .6f);
                }
                float opacity = alpha * (1 - Mathf.Clamp01((travelTime - .875f) / .125f));
                GUI.color = new Color(1, 1, 1, opacity);
                float labelScale = MultiplierPopupScale(flight.reason);
                OutlinedBonusLabel(new Rect(point.x - 90 * labelScale, point.y - 18 * labelScale, 180 * labelScale, 30 * labelScale),
                    (flight.amount > 0 ? "+" : "") + flight.amount.ToString("0.00") + "x", BonusStyle(Mathf.RoundToInt(24 * labelScale), colour));
            }
            DrawTimeExtension(timer, alpha);
            GUI.color = new Color(1, 1, 1, alpha);
        }

        internal static string FormatMultiplier(float value) => value <= 1f ? "--" : value.ToString("0.00") + "x";

        private static float MultiplierPopupScale(string reason) => IsVoxelMultiplier(reason)
            ? 1f : reason == "KILL CHAIN COMBO" ? 4f : 2f;

        private void DrawTimeExtension(Rect panel, float alpha)
        {
            float age = Time.unscaledTime - timeExtensionStartedAt;
            if (age < 0 || age > 2f || IsComplete || IsFailed) return;
            float scale = panel.width / 280f;
            float opacity = alpha * (1f - Mathf.Clamp01((age - 1.5f) / .5f));
            // Stay below even the enlarged urgency/extension pulse, without covering the digits.
            float width = 300f * scale;
            var area = new Rect(Mathf.Clamp(panel.center.x - width * .5f, 0f, Mathf.Max(0f, Screen.width - width)),
                panel.yMax + 66f * scale, width, 76f * scale);
            var cyan = new Color(.2f, .95f, 1f);
            Fill(area, new Color(.01f, .12f, .18f, .95f), opacity);
            Fill(new Rect(area.x, area.y, area.width, 2f * scale), cyan, opacity);
            GUI.color = new Color(1f, 1f, 1f, opacity);
            GUI.Label(new Rect(area.x, area.y + 3f * scale, area.width, 43f * scale),
                "+" + timeExtensionAmount + " SEC", BonusStyle(Mathf.Max(1, Mathf.RoundToInt(38f * scale)), cyan));
            GUI.Label(new Rect(area.x, area.y + 46f * scale, area.width, 26f * scale),
                "TIME EXTENDED!", BonusStyle(Mathf.Max(1, Mathf.RoundToInt(21f * scale)), Color.white));
        }

        private static void OutlinedBonusLabel(Rect rect, string text, GUIStyle style)
        {
            var outline = new GUIStyle(style);
            outline.normal.textColor = Color.black;
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    if (x != 0 || y != 0) GUI.Label(new Rect(rect.x + x * 2, rect.y + y * 2, rect.width, rect.height), text, outline);
            GUI.Label(rect, text, style);
        }

        private static Rect GetCountdownRect()
        {
            var display = Object.FindFirstObjectByType<VoxelMissionTimerDisplay>();
            if (display != null && display.TryGetScreenRect(out Rect rect)) return rect;
            float scale = Mathf.Sqrt(Screen.width / 1920f * Screen.height / 1080f);
            return new Rect(Screen.width - 295f * scale, 25f * scale, 280f * scale, 280f * scale);
        }

        private static void Fill(Rect rect, Color colour, float alpha)
        {
            GUI.color = new Color(colour.r, colour.g, colour.b, colour.a * alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }
    }
}
