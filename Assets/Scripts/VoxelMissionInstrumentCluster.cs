using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>One native UI surface behind the existing timer and its multiplier wing.</summary>
    public sealed class VoxelMissionInstrumentCluster : MaskableGraphic
    {
        public const float WingLeft = -492f, WingRight = -190f, WingHalfHeight = 86f;
        private const float WingOffset = 54f, WingTop = WingHalfHeight + WingOffset, WingBottom = -WingHalfHeight + WingOffset;
        private static readonly Color Silver = new(.62f, .79f, .91f);
        private static readonly Color Navy = new(.018f, .028f, .052f, .94f);
        private readonly List<Vector2> perimeter = new(80);
        private readonly List<int> remaining = new(80);
        private Text readout, statusLabel, cashLabel;
        private float dialScale = 1, lastMultiplier = float.NaN;
        private int lastCash = -1;
        private string lastStatus;
        private Color edge = Silver, fill = Navy;

        public static Rect MultiplierArea(Rect timer)
        {
            float scale = timer.width / 280f;
            return new Rect(timer.center.x + WingLeft * scale, timer.center.y - WingTop * scale,
                (WingRight - WingLeft) * scale, WingHalfHeight * 2 * scale);
        }

        public static Rect ReadoutArea(Rect panel)
        {
            float scale = panel.height / (WingHalfHeight * 2);
            return new Rect(panel.x + 32 * scale, panel.y + 42 * scale, 260 * scale, 78 * scale);
        }

        public void Build()
        {
            raycastTarget = false;
            var mono = Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001") ?? VoxelHudStyles.HudFont;
            var heading = Resources.Load<Font>("Fonts/Square") ?? mono;
            Label("Multiplier Heading", "MULTIPLIER", new Vector2(-330, 58), new Vector2(268, 28), 20, mono).color = Silver;
            readout = Label("Multiplier Value", "--", new Vector2(-330, 5), new Vector2(260, 78), 76, heading);
            statusLabel = Label("Multiplier Status", "AT RISK", new Vector2(-407, -61), new Vector2(118, 34), 18, mono);
            cashLabel = Label("Multiplier Cash", "+$0", new Vector2(-263, -61), new Vector2(146, 38), 30, mono);
            cashLabel.color = new Color(.98f, .95f, .86f);
        }

        private Text Label(string name, string text, Vector2 position, Vector2 size, int fontSize, Font font)
        {
            var label = VoxelMenuUi.CreateText(transform, name, text, fontSize, TextAnchor.MiddleCenter,
                new Vector2(.5f, .5f), position + Vector2.up * WingOffset, size);
            label.font = font;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = fontSize;
            label.supportRichText = false;
            return label;
        }

        public void SetReadout(float multiplier, int cash, string status, Color statusColour, Color feedback, float pulse)
        {
            if (readout == null) return;
            if (lastMultiplier != multiplier) { lastMultiplier = multiplier; readout.text = VoxelMissionProgress.FormatMultiplier(multiplier); }
            if (lastCash != cash) { lastCash = cash; cashLabel.text = "+$" + cash; }
            if (lastStatus != status) { lastStatus = status; statusLabel.text = status; }
            statusLabel.color = statusColour;
            readout.color = pulse > 0 ? feedback : Color.white;
            readout.transform.localScale = Vector3.one * (1 + Mathf.Sin(pulse * Mathf.PI) * .12f);
            Color nextEdge = Color.Lerp(Silver, feedback, pulse * .65f);
            Color nextFill = Color.Lerp(Navy, new Color(feedback.r * .12f, feedback.g * .12f, feedback.b * .12f, Navy.a), pulse * .35f);
            if (edge == nextEdge && fill == nextFill) return;
            edge = nextEdge; fill = nextFill; SetVerticesDirty();
        }

        public void SetDialScale(float value)
        {
            if (Mathf.Approximately(dialScale, value)) return;
            dialScale = value; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); perimeter.Clear(); remaining.Clear();
            // Only the bezel follows the original dial's warning/extension pulse. Text and wing stay fixed.
            float radius = 140 * dialScale + 7;
            // Align the stationary wing with the resting ring's top. Each neck meets
            // the pulsing bezel at a fixed height, keeping the upper attachment tidy.
            float upperY = WingTop - 18, lowerY = WingBottom + 18;
            float upperX = -Mathf.Sqrt(radius * radius - upperY * upperY);
            float lowerX = -Mathf.Sqrt(radius * radius - lowerY * lowerY);
            float upperAngle = 180 - Mathf.Asin(upperY / radius) * Mathf.Rad2Deg;
            float lowerAngle = -180 - Mathf.Asin(lowerY / radius) * Mathf.Rad2Deg;
            perimeter.Add(new Vector2(WingLeft + 18, WingTop));
            perimeter.Add(new Vector2(upperX - 42, WingTop));
            perimeter.Add(new Vector2(upperX - 24, upperY));
            const int segments = 60;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(upperAngle, lowerAngle, i / (float)segments) * Mathf.Deg2Rad;
                perimeter.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            perimeter.Add(new Vector2(lowerX - 24, lowerY));
            perimeter.Add(new Vector2(lowerX - 42, WingBottom));
            perimeter.Add(new Vector2(WingLeft + 18, WingBottom));
            perimeter.Add(new Vector2(WingLeft, WingBottom + 18));
            perimeter.Add(new Vector2(WingLeft, WingTop - 18));
            for (int i = 0; i < perimeter.Count; i++)
            {
                Vector2 p = perimeter[i];
                Color shade = Color.Lerp(fill, new Color(fill.r + .025f, fill.g + .025f, fill.b + .03f, fill.a), Mathf.InverseLerp(-radius, radius, p.y));
                vh.AddVert(p, shade, Vector2.zero); remaining.Add(i);
            }
            // Ear clipping keeps the translucent concave wing/dial union free of overlapping fill faces.
            while (remaining.Count > 2)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross(perimeter[b] - perimeter[a], perimeter[c] - perimeter[b]) >= 0) continue;
                    bool contains = false;
                    foreach (int other in remaining)
                        if (other != a && other != b && other != c && Inside(perimeter[other], perimeter[a], perimeter[b], perimeter[c])) { contains = true; break; }
                    if (contains) continue;
                    vh.AddTriangle(a, b, c); remaining.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break;
            }
            for (int i = 0; i < perimeter.Count; i++)
                Line(vh, perimeter[i], perimeter[(i + 1) % perimeter.Count], 2.2f, edge);
            Line(vh, new Vector2(-466, -39 + WingOffset), new Vector2(-194, -39 + WingOffset), 1.2f, new Color(edge.r, edge.g, edge.b, .55f));
            int first = vh.currentVertCount;
            vh.AddVert(new Vector2(-475, 49 + WingOffset), edge, Vector2.zero); vh.AddVert(new Vector2(-439, 49 + WingOffset), edge, Vector2.zero);
            vh.AddVert(new Vector2(-429, 67 + WingOffset), edge, Vector2.zero); vh.AddVert(new Vector2(-465, 67 + WingOffset), edge, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
            Cross(b - a, p - a) <= 0 && Cross(c - b, p - b) <= 0 && Cross(a - c, p - c) <= 0;
        private static void Line(VertexHelper vh, Vector2 from, Vector2 to, float width, Color colour)
        {
            Vector2 delta = to - from, normal = new Vector2(-delta.y, delta.x).normalized * (width * .5f);
            int first = vh.currentVertCount;
            vh.AddVert(from - normal, colour, Vector2.zero); vh.AddVert(from + normal, colour, Vector2.zero);
            vh.AddVert(to + normal, colour, Vector2.zero); vh.AddVert(to - normal, colour, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
