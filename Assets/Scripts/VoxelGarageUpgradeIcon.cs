using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    public enum VoxelGarageIconKind { Armour, Engine, Guns, Missile, Wheels, Spikes, Boost, Plough, Next, Settings, StarSpikes }

    /// <summary>Small native UI meshes, without texture assets or per-icon materials.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoxelGarageUpgradeIcon : MaskableGraphic
    {
        public VoxelGarageIconKind kind;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch (kind)
            {
                case VoxelGarageIconKind.StarSpikes:
                    Ring(vh,.85f,.67f,20);
                    Ring(vh,.14f,0,5);
                    for (int i=0;i<5;i++)
                    {
                        float a=Mathf.PI*.5f+i*Mathf.PI*2/5;
                        Vector2 centre=Radial(a,.43f);
                        Polygon(vh,new[]{centre+Radial(a,.23f),centre+Radial(a+2.1f,.16f),centre+Radial(a-2.1f,.16f)});
                    }
                    break;
                case VoxelGarageIconKind.Armour:
                    Polygon(vh, new[] { new Vector2(-.72f,.68f), new Vector2(0,.91f), new Vector2(.72f,.68f),
                        new Vector2(.57f,-.29f), new Vector2(0,-.9f), new Vector2(-.57f,-.29f) });
                    break;
                case VoxelGarageIconKind.Engine:
                    Box(vh,-.65f,-.42f,1.3f,1.04f); Box(vh,-.28f,.63f,.56f,.20f);
                    Box(vh,-.91f,-.13f,.26f,.5f); Box(vh,.65f,-.36f,.28f,.73f);
                    Box(vh,-.51f,-.62f,.22f,.22f); Box(vh,.29f,-.62f,.22f,.22f);
                    break;
                case VoxelGarageIconKind.Guns:
                    for (int i=-1;i<=1;i++)
                    {
                        float x=i*.57f;
                        Polygon(vh,new[]{new Vector2(x-.17f,-.75f),new Vector2(x+.17f,-.75f),new Vector2(x+.17f,.42f),
                            new Vector2(x,.85f),new Vector2(x-.17f,.42f)});
                    }
                    break;
                case VoxelGarageIconKind.Missile:
                    Polygon(vh,new[]{new Vector2(-.78f,-.69f),new Vector2(-.55f,-.31f),new Vector2(-.57f,.16f),
                        new Vector2(-.16f,.05f),new Vector2(.47f,.75f),new Vector2(.89f,.90f),new Vector2(.76f,.45f),
                        new Vector2(.08f,-.19f),new Vector2(.17f,-.59f),new Vector2(-.29f,-.57f),new Vector2(-.68f,-.81f)});
                    break;
                case VoxelGarageIconKind.Wheels:
                case VoxelGarageIconKind.Spikes:
                    Ring(vh,.85f,.59f,20);
                    for(int i=0;i<5;i++)
                    {
                        float a=i*Mathf.PI*2/5;
                        Vector2 end=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.65f;
                        Line(vh,Vector2.zero,end,.14f);
                        if(kind==VoxelGarageIconKind.Spikes) Line(vh,end,end*1.5f,.17f);
                    }
                    break;
                case VoxelGarageIconKind.Boost:
                    Box(vh,-.24f,.47f,.48f,.34f); Box(vh,-.51f,-.65f,1.02f,1.11f);
                    Polygon(vh,new[]{new Vector2(-.51f,.46f),new Vector2(-.24f,.65f),new Vector2(.24f,.65f),new Vector2(.51f,.46f)});
                    break;
                case VoxelGarageIconKind.Plough:
                    Polygon(vh,new[]{new Vector2(-.9f,-.64f),new Vector2(-.65f,.61f),new Vector2(.65f,.61f),new Vector2(.9f,-.64f)});
                    Box(vh,-.95f,-.85f,1.9f,.17f);
                    break;
                case VoxelGarageIconKind.Next:
                    for(int i=0;i<2;i++)
                    {
                        float x=-.75f+i*.83f;
                        Polygon(vh,new[]{new Vector2(x,.7f),new Vector2(x+.34f,.7f),new Vector2(x+.78f,0),
                            new Vector2(x+.34f,-.7f),new Vector2(x,-.7f),new Vector2(x+.44f,0)});
                    }
                    break;
                case VoxelGarageIconKind.Settings:
                    Ring(vh,.68f,.30f,32);
                    for(int i=0;i<8;i++)
                    {
                        float angle=i*Mathf.PI*2/8;
                        Polygon(vh,new[]{Radial(angle-.25f,.60f),Radial(angle-.13f,.92f),
                            Radial(angle+.13f,.92f),Radial(angle+.25f,.60f)});
                    }
                    break;
            }
        }
        private static Vector2 Radial(float angle,float radius) => new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
        private void Polygon(VertexHelper vh, Vector2[] points)
        {
            int start=vh.currentVertCount;
            foreach(var p in points) vh.AddVert(rectTransform.rect.center + Vector2.Scale(p,rectTransform.rect.size*.5f),color,Vector2.zero);
            for(int i=1;i<points.Length-1;i++) vh.AddTriangle(start,start+i,start+i+1);
        }
        private void Box(VertexHelper vh,float x,float y,float w,float h) => Polygon(vh,new[]{new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h)});
        private void Line(VertexHelper vh,Vector2 a,Vector2 b,float width)
        {
            Vector2 d=b-a, n=new Vector2(-d.y,d.x).normalized*width*.5f;
            Polygon(vh,new[]{a-n,b-n,b+n,a+n});
        }
        private void Ring(VertexHelper vh,float outer,float inner,int count)
        {
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
                Vector2 pa=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),pb=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                Polygon(vh,new[]{pa*outer,pb*outer,pb*inner,pa*inner});
            }
        }
    }
}
