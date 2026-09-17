using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Decorative display platform, kept outside the car's damage hierarchy.</summary>
    public sealed class VoxelCarTurntable : MonoBehaviour
    {
        public Transform car;
        private Transform deck;
        private readonly List<Object> owned = new();
        public static VoxelCarTurntable Create(Transform parent, Transform target, float radius=3.2f)
        {
            var root=new GameObject("Car Turntable"); root.transform.SetParent(parent,false);
            var table=root.AddComponent<VoxelCarTurntable>(); table.car=target; table.Build(radius); return table;
        }
        private Material Metal(string name,Color colour,float metallic,float smooth)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.name=name;
            mat.SetColor("_BaseColor",colour); mat.SetFloat("_Metallic",metallic); mat.SetFloat("_Smoothness",smooth);
            owned.Add(mat); return mat;
        }
        private void Build(float radius)
        {
            var edge=Metal("Turntable dark rim",new Color(.045f,.052f,.061f),.65f,.4f);
            var steel=Metal("Turntable brushed steel",new Color(.36f,.40f,.44f),.7f,.55f);
            var alternate=Metal("Turntable alternate steel",new Color(.26f,.30f,.34f),.7f,.5f);
            var trim=Metal("Turntable edge trim",new Color(.52f,.56f,.60f),.8f,.6f);
            Surface(transform,"Stationary outer rim",0,radius,0,.15f,0,360,edge);
            Surface(transform,"Silver perimeter",radius-.10f,radius-.06f,.15f,.165f,0,360,trim);
            deck=new GameObject("Rotating steel deck").transform; deck.SetParent(transform,false);
            for(int i=0;i<12;i++) Surface(deck,"Radial steel panel",.13f,radius-.14f,.15f,.18f,i*30+.15f,(i+1)*30-.15f,i%2==0?steel:alternate);
            Surface(deck,"Centre hub",0,.13f,.15f,.183f,0,360,trim);
            LateUpdate();
        }
        private void Surface(Transform parent,string name,float inner,float outer,float bottom,float top,float from,float to,Material mat)
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            int steps=Mathf.Max(1,Mathf.CeilToInt((to-from)/5));
            for(int i=0;i<steps;i++)
            {
                float a=Mathf.Lerp(from,to,(float)i/steps)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(float)(i+1)/steps)*Mathf.Deg2Rad;
                Vector3 p=new(Mathf.Sin(a),0,Mathf.Cos(a)),q=new(Mathf.Sin(b),0,Mathf.Cos(b));
                Quad(p*inner+Vector3.up*top,p*outer+Vector3.up*top,q*outer+Vector3.up*top,q*inner+Vector3.up*top,vertices,triangles);
                Quad(p*outer+Vector3.up*top,p*outer+Vector3.up*bottom,q*outer+Vector3.up*bottom,q*outer+Vector3.up*top,vertices,triangles);
            }
            var mesh=new Mesh { name=name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
        private static void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,List<Vector3> v,List<int> t)
        { int n=v.Count;v.AddRange(new[]{a,b,c,d});t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3}); }
        private void LateUpdate() { if(car!=null && deck!=null) deck.rotation=Quaternion.Euler(0,car.eulerAngles.y,0); }
        private void OnDestroy() { foreach(var item in owned) if(item!=null) {if(Application.isPlaying) Destroy(item);else DestroyImmediate(item);} }
    }
}
