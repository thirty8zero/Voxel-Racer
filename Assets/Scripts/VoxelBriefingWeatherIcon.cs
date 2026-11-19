using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Simple outlined day/night symbols, drawn in the native UI mesh.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoxelBriefingWeatherIcon : MaskableGraphic
    {
        public bool Night { get; private set; }

        public void SetNight(bool night)
        {
            if (Night == night) return;
            Night = night;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            float unit = Mathf.Min(rect.width, rect.height), stroke = unit * .043f;
            Vector2 centre = rect.center;
            if (!Night)
            {
                Arc(vh, centre, unit * .27f, 0f, 2f * Mathf.PI, 48, stroke);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * .25f;
                    Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                    Line(vh, centre + direction * (unit * .385f), centre + direction * (unit * .47f), stroke);
                }
                return;
            }

            // Intersect two circles for an open, tilted crescent. Both arcs share
            // exact tips, so the outline remains clean without a background cutout.
            float radius = unit * .35f, cutRadius = unit * .337f;
            Vector2 offset = new(unit * .175f, unit * .105f);
            float distance = offset.magnitude, axis = Mathf.Atan2(offset.y, offset.x);
            float along = (radius * radius - cutRadius * cutRadius + distance * distance) / (2f * distance);
            float halfAngle = Mathf.Acos(along / radius);
            float start = axis + halfAngle, end = axis - halfAngle;
            Vector2 first = centre + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * radius;
            Vector2 last = centre + new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * radius;
            Arc(vh, centre, radius, start, end + 2f * Mathf.PI, 48, stroke);
            Vector2 cutCentre = centre + offset;
            float cutStart = Mathf.Atan2(last.y - cutCentre.y, last.x - cutCentre.x);
            float cutEnd = Mathf.Atan2(first.y - cutCentre.y, first.x - cutCentre.x);
            while (cutEnd > cutStart) cutEnd -= 2f * Mathf.PI;
            Arc(vh, cutCentre, cutRadius, cutStart, cutEnd, 32, stroke);
            Dot(vh, first, stroke * .5f); Dot(vh, last, stroke * .5f);
        }

        private void Arc(VertexHelper vh, Vector2 centre, float radius, float start, float end, int segments, float stroke)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)segments);
                float b = Mathf.Lerp(start, end, (i + 1f) / segments);
                Vector2 u = new(Mathf.Cos(a), Mathf.Sin(a)), v = new(Mathf.Cos(b), Mathf.Sin(b));
                Quad(vh, centre + u * (radius - stroke * .5f), centre + u * (radius + stroke * .5f),
                    centre + v * (radius + stroke * .5f), centre + v * (radius - stroke * .5f));
            }
        }

        private void Line(VertexHelper vh, Vector2 a, Vector2 b, float stroke)
        {
            Vector2 delta = (b - a).normalized;
            Vector2 normal = new Vector2(-delta.y, delta.x) * (stroke * .5f);
            Quad(vh, a - normal, a + normal, b + normal, b - normal);
            Dot(vh, a, stroke * .5f); Dot(vh, b, stroke * .5f);
        }

        private void Dot(VertexHelper vh, Vector2 centre, float radius)
        {
            int start = vh.currentVertCount;
            vh.AddVert(centre, color, Vector2.zero);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * .25f;
                vh.AddVert(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
            }
            for (int i = 0; i < 8; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 8);
        }

        private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
