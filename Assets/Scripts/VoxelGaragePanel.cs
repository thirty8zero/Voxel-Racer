using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoxelGarageHeaderShade : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;var dark=new Color(.008f,.015f,.025f,.94f);
            vh.AddVert(new Vector3(r.xMin,r.yMin),Color.clear,Vector2.zero);vh.AddVert(new Vector3(r.xMax,r.yMin),Color.clear,Vector2.zero);
            vh.AddVert(new Vector3(r.xMax,r.yMax),dark,Vector2.zero);vh.AddVert(new Vector3(r.xMin,r.yMax),dark,Vector2.zero);
            vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);
        }
    }

    /// <summary>Resolution-independent cut-corner panel with an accent border.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoxelGaragePanel : MaskableGraphic
    {
        public Color edgeColor = Color.gray;
        public float borderWidth = 3f;
        public bool useRarityFrame;
        public bool selectionGlow;
        public bool useGradient;
        public Color topColor = Color.white;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r = rectTransform.rect;
            float c = Mathf.Min(18, r.height * .13f), b = borderWidth;
            if (useRarityFrame && selectionGlow)
            {
                Color glow = edgeColor; glow.a *= .36f;
                AddFrameBand(vh, r, c, -6f, 0f, glow, glow, 0f, 1f);
            }
            Vector2[] p = { new(r.xMin+c,r.yMin), new(r.xMax-c,r.yMin), new(r.xMax,r.yMin+c),
                new(r.xMax,r.yMax-c), new(r.xMax-c,r.yMax), new(r.xMin+c,r.yMax), new(r.xMin,r.yMax-c), new(r.xMin,r.yMin+c) };
            vh.AddVert(r.center, useGradient ? Color.Lerp(color, topColor, .5f) : color, Vector2.zero);
            for (int i=0;i<8;i++) vh.AddVert(p[i], useGradient ? Color.Lerp(color, topColor,
                Mathf.InverseLerp(r.yMin, r.yMax, p[i].y)) : color, Vector2.zero);
            for (int i=0;i<8;i++) vh.AddTriangle(0,i+1,(i+1)%8+1);
            if (useRarityFrame)
            {
                if (selectionGlow)
                {
                    Color glow = edgeColor; glow.a *= .20f;
                    AddFrameBand(vh, r, c, b, b + 7f, glow, glow, 1f, 0f);
                }
                // Bevelled rarity band and a recessed pinstripe, kept inside the
                // card bounds so scrolling masks and pointer targets stay aligned.
                AddFrameBand(vh, r, c, 0f, 1f, Shade(edgeColor, .42f), Shade(edgeColor, .42f));
                AddFrameBand(vh, r, c, 1f, b - 1f, Shade(edgeColor, .78f), Color.Lerp(edgeColor, Color.white, .22f));
                AddFrameBand(vh, r, c, b - 1f, b, Shade(edgeColor, .38f), Shade(edgeColor, .60f));
                Color pinstripe = edgeColor; pinstripe.a *= .55f;
                AddFrameBand(vh, r, c, b + 3f, b + 4f, pinstripe, pinstripe);
                return;
            }
            for (int i=0;i<8;i++)
            {
                int n=(i+1)%8, start=vh.currentVertCount;
                Vector2 a=p[i], d=p[n];
                Vector2 ai=a+(r.center-a).normalized*b, di=d+(r.center-d).normalized*b;
                vh.AddVert(a,edgeColor,Vector2.zero); vh.AddVert(d,edgeColor,Vector2.zero);
                vh.AddVert(di,edgeColor,Vector2.zero); vh.AddVert(ai,edgeColor,Vector2.zero);
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
            }
        }

        private static Color Shade(Color value, float brightness) =>
            new(value.r * brightness, value.g * brightness, value.b * brightness, value.a);

        private static Vector2[] InsetCorners(Rect rect, float corner, float inset)
        {
            // Parallel inset of the 45-degree corners, rather than an inset
            // towards the centre which makes horizontal edges look thinner.
            rect = Rect.MinMaxRect(rect.xMin + inset, rect.yMin + inset,
                rect.xMax - inset, rect.yMax - inset);
            float c = Mathf.Max(0f, corner - (2f - Mathf.Sqrt(2f)) * inset);
            return new Vector2[] { new(rect.xMin+c,rect.yMin), new(rect.xMax-c,rect.yMin),
                new(rect.xMax,rect.yMin+c), new(rect.xMax,rect.yMax-c),
                new(rect.xMax-c,rect.yMax), new(rect.xMin+c,rect.yMax),
                new(rect.xMin,rect.yMax-c), new(rect.xMin,rect.yMin+c) };
        }

        private static void AddFrameBand(VertexHelper vh, Rect rect, float corner, float outerInset,
            float innerInset, Color bottom, Color top, float outerOpacity = 1f, float innerOpacity = 1f)
        {
            var outer = InsetCorners(rect, corner, outerInset);
            var inner = InsetCorners(rect, corner, innerInset);
            for (int i = 0; i < 8; i++)
            {
                int n = (i + 1) % 8, start = vh.currentVertCount;
                Color a = Color.Lerp(bottom, top, Mathf.InverseLerp(rect.yMin, rect.yMax, outer[i].y));
                Color d = Color.Lerp(bottom, top, Mathf.InverseLerp(rect.yMin, rect.yMax, outer[n].y));
                Color di = Color.Lerp(bottom, top, Mathf.InverseLerp(rect.yMin, rect.yMax, inner[n].y));
                Color ai = Color.Lerp(bottom, top, Mathf.InverseLerp(rect.yMin, rect.yMax, inner[i].y));
                a.a *= outerOpacity; d.a *= outerOpacity;
                di.a *= innerOpacity; ai.a *= innerOpacity;
                vh.AddVert(outer[i], a, Vector2.zero); vh.AddVert(outer[n], d, Vector2.zero);
                vh.AddVert(inner[n], di, Vector2.zero); vh.AddVert(inner[i], ai, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
