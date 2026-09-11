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
        private Vector2 timeExtensionOrigin;
        public float TimeExtensionPulse => Mathf.Clamp01(1f - (Time.unscaledTime - timeExtensionStartedAt) / 1.5f);

        public bool AddBonusTime(int seconds, Vector3? position = null)
        {
            // Extensions must be collected before expiry; losing the bonus remains final.
            if (Tuning == null || IsComplete || !TimeBonusAvailable || seconds <= 0 ||
                (startCountdown != null && !startCountdown.IsComplete)) return false;
            RemainingTime += seconds;
            timeExtensionAmount = Time.unscaledTime - timeExtensionStartedAt < .2f ? timeExtensionAmount + seconds : seconds;
            timeExtensionStartedAt = Time.unscaledTime;
            timeExtensionOrigin = new Vector2(.5f, .5f);
            if (position.HasValue && Camera.main != null)
            {
                var p = Camera.main.WorldToViewportPoint(position.Value);
                if (p.z > 0) timeExtensionOrigin = new Vector2(Mathf.Clamp(p.x, .15f, .85f), Mathf.Clamp(1-p.y, .35f, .8f));
            }
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
            var panel = new Rect(missionArea.xMax + 8, missionArea.y,
                Mathf.Max(1, timer.xMin - missionArea.xMax - 16), missionArea.height);
            float compactWidth = Mathf.Min(180, panel.width);
            panel.x += (panel.width - compactWidth) * .5f;
            panel.width = compactWidth;
            Fill(panel, new Color(.025f, .04f, .065f, .94f), alpha);
            Fill(new Rect(panel.x, panel.y, panel.width, 2), tone, alpha);
            int numberSize = Mathf.Clamp(Mathf.FloorToInt(panel.width / 4.8f), 12, 30);
            var target = new Rect(panel.x + 5, panel.y + 3, panel.width - 10, 34);
            float pulse = Mathf.Clamp01((multiplierPulseUntil - Time.unscaledTime) / .32f);
            float scale = 1 + Mathf.Sin(pulse * Mathf.PI) * .12f;
            var oldMatrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * scale, target.center);
            GUI.color = new Color(1, 1, 1, alpha);
            Color multiplierColour = expired ? LossColour : pulse > 0 ? (lastMultiplierWasNegative ? LossColour : GainColour) : Color.white;
            GUI.Label(target, displayedMultiplierBonus.ToString("0.00") + "x", BonusStyle(numberSize, multiplierColour));
            GUI.matrix = oldMatrix;
            int extra = expired ? 0 : Mathf.Max(0, Mathf.RoundToInt(Tuning.completionCurrencyAward * EffectiveTimeBonusMultiplier) - Tuning.completionCurrencyAward);
            GUI.Label(new Rect(panel.x + 4, panel.y + 37, panel.width - 8, 13), expired ? "LOST" : IsComplete ? "BANKED" : "AT RISK", BonusStyle(11, tone));
            GUI.Label(new Rect(panel.x + 4, panel.y + 49, panel.width - 8, 20), "+$" + extra, BonusStyle(Mathf.Min(numberSize, 19), tone));

            foreach (var flight in multiplierFlights)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - flight.startedAt) / MultiplierFlightDuration);
                // Briefly hold the labelled gain at its source, then visibly connect it to the counter.
                float travel = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .2f) / .8f));
                Vector2 from = new Vector2(flight.origin.x * Screen.width, flight.origin.y * Screen.height);
                Vector2 point = Vector2.Lerp(from, target.center, travel);
                Color colour = flight.amount < 0 ? LossColour : GainColour;
                for (int dot = 1; dot <= 5; dot++)
                {
                    Vector2 trail = Vector2.Lerp(from, point, 1 - dot * .035f);
                    Fill(new Rect(trail.x - 2, trail.y - 2, 4, 4), colour, alpha * (1 - dot / 6f) * .6f);
                }
                float opacity = alpha * (1 - Mathf.Clamp01((t - .9f) / .1f));
                GUI.color = new Color(1, 1, 1, opacity);
                OutlinedBonusLabel(new Rect(point.x - 90, point.y - 18, 180, 30),
                    (flight.amount > 0 ? "+" : "") + flight.amount.ToString("0.00") + "x", BonusStyle(24, colour));
            }
            DrawTimeExtension(timer, alpha);
            GUI.color = new Color(1, 1, 1, alpha);
        }

        private void DrawTimeExtension(Rect panel, float alpha)
        {
            float age = Time.unscaledTime - timeExtensionStartedAt;
            if (age < 0 || age > 1.5f) return;
            var target = panel.center;
            float travel = Mathf.SmoothStep(0, 1, Mathf.Clamp01((age - .2f) / .65f));
            var from = new Vector2(timeExtensionOrigin.x * Screen.width, timeExtensionOrigin.y * Screen.height);
            var point = Vector2.Lerp(from, target, travel);
            var cyan = new Color(.2f, .95f, 1f);
            if (age < .85f)
            {
                for (int i = 1; i <= 8; i++)
                {
                    var dot = Vector2.Lerp(from, point, 1 - i * .025f);
                    Fill(new Rect(dot.x - 3, dot.y - 3, 6, 6), cyan, alpha * (1-i/9f));
                }
                Fill(new Rect(point.x - 150, point.y - 42, 300, 84), new Color(.01f, .12f, .18f, .95f), alpha);
                GUI.color = new Color(1, 1, 1, alpha);
                GUI.Label(new Rect(point.x - 150, point.y - 40, 300, 45), "+" + timeExtensionAmount + " SEC", BonusStyle(38, cyan));
                GUI.Label(new Rect(point.x - 150, point.y + 5, 300, 30), "TIME EXTENDED!", BonusStyle(21, Color.white));
            }
            else
            {
                float burst = (age - .85f) / .65f;
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI * 2 / 16;
                    var dot = target + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (28 + burst * 65);
                    Fill(new Rect(dot.x - 3, dot.y - 3, 6, 6), cyan, alpha * (1-burst));
                }
                GUI.color = new Color(1, 1, 1, alpha * (1-burst));
                GUI.Label(new Rect(panel.x, panel.yMax + 4, panel.width, 28), "+" + timeExtensionAmount + " SEC", BonusStyle(22, cyan));
            }
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
