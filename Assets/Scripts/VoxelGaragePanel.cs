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
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r = rectTransform.rect;
            float c = Mathf.Min(18, r.height * .13f), b = 3;
            Vector2[] p = { new(r.xMin+c,r.yMin), new(r.xMax-c,r.yMin), new(r.xMax,r.yMin+c),
                new(r.xMax,r.yMax-c), new(r.xMax-c,r.yMax), new(r.xMin+c,r.yMax), new(r.xMin,r.yMax-c), new(r.xMin,r.yMin+c) };
            vh.AddVert(r.center, color, Vector2.zero);
            for (int i=0;i<8;i++) vh.AddVert(p[i], color, Vector2.zero);
            for (int i=0;i<8;i++) vh.AddTriangle(0,i+1,(i+1)%8+1);
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
    }
}
