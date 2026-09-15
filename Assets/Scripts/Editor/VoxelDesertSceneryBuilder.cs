using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer.Editor
{
    public static class VoxelDesertSceneryBuilder
    {
        public const string Folder = "Assets/Prefabs/Scenery/Desert";
        public const string SetPath = "Assets/Resources/Scenery/DesertScenerySet.asset";
        private static readonly Dictionary<Vector3Int,int> cells = new();
        private const float Cell = .14f;
        private static readonly Color[] Palette = {
            new(.16f,.30f,.055f), new(.27f,.43f,.075f), new(.39f,.53f,.12f), new(.10f,.23f,.04f),
            new(.42f,.28f,.13f), new(.29f,.17f,.075f), new(.58f,.39f,.18f), new(.68f,.52f,.24f),
            new(.36f,.33f,.28f), new(.47f,.43f,.36f), new(.57f,.51f,.41f), new(.28f,.27f,.24f),
            new(.77f,.12f,.08f), new(.92f,.47f,.10f), new(.79f,.65f,.33f), new(.23f,.36f,.08f)
        };
        private static void Put(int x,int y,int z,int colour) { if(y>=0) cells[new Vector3Int(x,y,z)] = colour; }
        private static void Stem(int x,int z,int bottom,int height,int radius)
        {
            for(int y=bottom;y<bottom+height;y++) for(int a=-radius;a<=radius;a++) for(int b=-radius;b<=radius;b++)
                if(a*a+b*b<=radius*radius+1 && (y<bottom+height-1 || Mathf.Abs(a)+Mathf.Abs(b)<radius+1))
                    Put(x+a,y,z+b, (Mathf.Abs(a)==radius || Mathf.Abs(b)==radius) ? 2 : ((a+b)&1)==0 ? 1 : 0);
        }
        private static void Line(Vector3Int a,Vector3Int b,int colour,int thickness=0)
        {
            int steps=Mathf.CeilToInt(Vector3.Distance(a,b)*2);
            for(int i=0;i<=steps;i++)
            {
                var p=Vector3Int.RoundToInt(Vector3.Lerp(a,b,steps==0?0:(float)i/steps));
                for(int x=-thickness;x<=thickness;x++) for(int z=-thickness;z<=thickness;z++) Put(p.x+x,p.y,p.z+z,colour);
            }
        }
        private static void Arm(int side,int y,int rise,int reach,int z=0)
        {
            for(int x=0;x<=reach;x++) Stem(side*x,z,y,3,1);
            Stem(side*reach,z,y,rise,1);
        }
        private static void Flower(int x,int y,int z)
        {
            Put(x,y,z,13); Put(x-1,y,z,12); Put(x+1,y,z,12); Put(x,y,z-1,12); Put(x,y,z+1,12);
        }
        private static void Shape(int index)
        {
            cells.Clear();
            if(index<4)
            {
                int height=index==0?24:index==1?18:index==2?29:13;
                Stem(0,0,0,height,2);
                if(index!=3) Arm(-1,7,index==2?16:10,6);
                if(index!=1 && index!=3) Arm(1,12,8,6,1);
                if(index==2) Arm(1,5,10,4,-4);
                if(index==0 || index==3) Flower(0,height,0);
            }
            else if(index==4)
            {
                for(int y=0;y<8;y++) for(int x=-4;x<=4;x++) for(int z=-4;z<=4;z++)
                    if(x*x+z*z <= (y==0||y==7?8:15)) Put(x,y,z,((x+z)&1)==0?1:2);
                Flower(0,8,0);
                Stem(6,1,0,5,2);
            }
            else if(index==5)
            {
                foreach(var p in new[]{new Vector3Int(0,0,0),new Vector3Int(-4,3,0),new Vector3Int(4,4,1),new Vector3Int(6,8,1)})
                    for(int y=0;y<7;y++) for(int x=-2;x<=2;x++) for(int z=-1;z<=1;z++)
                        if((x*x)/6f+((y-3)*(y-3))/12f<=1) Put(p.x+x,p.y+y,p.z+z,z==-1?2:1);
                Flower(6,15,1);
            }
            else if(index<9)
            {
                int rx=index==6?5:index==7?8:3, ry=index==7?4:5, rz=index==7?5:4;
                for(int x=-rx;x<=rx;x++) for(int y=0;y<ry*2;y++) for(int z=-rz;z<=rz;z++)
                {
                    float n=((x*13+z*7+y*3)&7)*.025f;
                    if(x*x/(float)(rx*rx)+y*y/(float)(ry*ry)+z*z/(float)(rz*rz)<1.05f+n)
                        Put(x,y,z,8+Mathf.Abs(x*7+y*13+z*3)%4);
                }
                if(index==8) for(int x=5;x<9;x++) for(int z=0;z<3;z++) Put(x,0,z,9);
            }
            else if(index<11)
            {
                int height=index==9?5:8;
                for(int arm=0;arm<8;arm++)
                {
                    float angle=arm*Mathf.PI/4;
                    var end=new Vector3Int(Mathf.RoundToInt(Mathf.Cos(angle)*4),height-(arm%3),Mathf.RoundToInt(Mathf.Sin(angle)*4));
                    Line(Vector3Int.zero,end,arm%2==0?6:7);
                    Line(end-new Vector3Int(0,2,0),end+new Vector3Int(arm%2==0?2:-2,1,0),14);
                }
            }
            else
            {
                int height=index==11?24:index==12?17:12;
                Line(Vector3Int.zero,new Vector3Int(2,height,0),5,1);
                Line(new Vector3Int(0,0,0),new Vector3Int(1,height-1,-1),4);
                for(int branch=0;branch<4;branch++)
                {
                    int side=branch%2==0?-1:1, y=5+branch*3;
                    var start=new Vector3Int(1,y,0); var elbow=new Vector3Int(side*(4+branch),y+2,branch-2);
                    var tip=elbow+new Vector3Int(side*2,4,1);
                    Line(start,elbow,5); Line(elbow,tip,4);
                    Line(elbow,elbow+new Vector3Int(-side,3,-2),5);
                }
                for(int side=-1;side<=1;side+=2) Line(new Vector3Int(0,1,0),new Vector3Int(side*3,0,2),5);
            }
        }
        [MenuItem("Tools/Voxel Racer/Build Desert Scenery Collection")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder); Directory.CreateDirectory("Assets/Resources/Scenery"); AssetDatabase.Refresh();
            string palettePath=Folder+"/DesertPalette.asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath);
            if(texture==null) { texture=new Texture2D(16,1,TextureFormat.RGBA32,false); AssetDatabase.CreateAsset(texture,palettePath); }
            texture.SetPixels(Palette); texture.filterMode=FilterMode.Point; texture.wrapMode=TextureWrapMode.Clamp; texture.Apply(); EditorUtility.SetDirty(texture);
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/DesertScenery.mat");
            if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,Folder+"/DesertScenery.mat"); }
            material.SetTexture("_BaseMap",texture); material.SetColor("_BaseColor",Color.white); material.SetFloat("_Smoothness",.12f); material.enableInstancing=true; EditorUtility.SetDirty(material);
            string[] names={"SaguaroFlowering","SaguaroSingleArm","SaguaroTall","CactusYoung","BarrelCactusCluster","PricklyPear","DesertBoulder","LayeredRock","RockScatter","DryScrub","DryBush","DeadTreeTall","DeadTreeForked","DeadTreeStump"};
            var entries=new List<VoxelScenerySet.Entry>();
            for(int index=0;index<names.Length;index++)
            {
                Shape(index);
                var mesh=CreateMesh(names[index]);
                string meshPath=Folder+"/"+names[index]+"Mesh.asset";
                var existing=AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(meshPath);
                if(existing!=null) { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); mesh=existing; } else AssetDatabase.CreateAsset(mesh,meshPath);
                var go=new GameObject(names[index],typeof(MeshFilter),typeof(MeshRenderer));
                go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<MeshRenderer>().sharedMaterial=material;
                string prefabPath=Folder+"/"+names[index]+".prefab";
                var prefab=PrefabUtility.SaveAsPrefabAsset(go,prefabPath); Object.DestroyImmediate(go);
                float radius=new Vector2(Mathf.Max(Mathf.Abs(mesh.bounds.min.x),Mathf.Abs(mesh.bounds.max.x)),Mathf.Max(Mathf.Abs(mesh.bounds.min.z),Mathf.Abs(mesh.bounds.max.z))).magnitude;
                entries.Add(new VoxelScenerySet.Entry { prefab=prefab, radius=radius, weight=index<6?3:index<11?4:1,
                    scaleRange=index>=11?new Vector2(1.6f,2.5f):new Vector2(.8f,1.25f), roadClearance=index>=11?4:2, spacing=index>=11?2:.5f });
            }
            var set=AssetDatabase.LoadAssetAtPath<VoxelScenerySet>(SetPath);
            if(set==null) { set=ScriptableObject.CreateInstance<VoxelScenerySet>(); AssetDatabase.CreateAsset(set,SetPath); }
            set.entries=entries.ToArray(); EditorUtility.SetDirty(set);
            foreach(string path in new[]{"Assets/Resources/Tracks/Track01.asset","Assets/Resources/Tracks/Track02.asset","Assets/Resources/Tracks/Track03.asset"})
            {
                var track=AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(path);
                if(track!=null) { Undo.RecordObject(track,"Assign desert scenery set"); track.scenerySet=set; EditorUtility.SetDirty(track); }
            }
            AssetDatabase.SaveAssets(); Render();
            Debug.Log("Created 14 desert scenery prefabs: one mesh renderer each, shared palette material, exposed faces only, no colliders. Assigned to Track01–03.");
        }
        private static UnityEngine.Mesh CreateMesh(string name)
        {
            var vertices=new List<Vector3>(); var normals=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
            Vector3Int[] directions={Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,Vector3Int.forward,Vector3Int.back};
            foreach(var cell in cells) foreach(var direction in directions)
            {
                if(cells.ContainsKey(cell.Key+direction)) continue;
                Vector3 normal=direction;
                Vector3 u=Vector3.Cross(Mathf.Abs(normal.y)>.5f?Vector3.forward:Vector3.up,normal);
                Vector3 v=Vector3.Cross(normal,u);
                Vector3 center=(Vector3)cell.Key*Cell+Vector3.up*(Cell*.5f)+normal*Cell*.5f;
                int first=vertices.Count;
                foreach(var corner in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)})
                { vertices.Add(center+(u*corner.x+v*corner.y)*Cell*.5f); normals.Add(normal); uv.Add(new Vector2((cell.Value+.5f)/16,.5f)); }
                triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
            }
            var mesh=new UnityEngine.Mesh { name=name, indexFormat=IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds(); return mesh;
        }
        [MenuItem("Tools/Voxel Racer/Render Desert Scenery Collection")]
        public static void Render()
        {
            var set=AssetDatabase.LoadAssetAtPath<VoxelScenerySet>(SetPath);
            var preview=new PreviewRenderUtility();
            try
            {
                for(int i=0;i<set.entries.Length;i++)
                {
                    var model=Object.Instantiate(set.entries[i].prefab); preview.AddSingleGO(model);
                    model.transform.position=new Vector3((i%5-2)*3.8f,0,-(i/5)*5);
                }
                preview.camera.transform.position=new Vector3(14,16,23); preview.camera.transform.LookAt(new Vector3(0,.5f,-4.5f));
                preview.camera.orthographic=true; preview.camera.orthographicSize=10.5f; preview.camera.nearClipPlane=.1f; preview.camera.farClipPlane=100;
                preview.camera.clearFlags=CameraClearFlags.SolidColor; preview.camera.backgroundColor=new Color(.67f,.55f,.37f);
                preview.lights[0].intensity=1.3f; preview.lights[0].transform.rotation=Quaternion.Euler(45,35,0);
                preview.lights[1].intensity=.7f; preview.lights[1].transform.rotation=Quaternion.Euler(40,210,0);
                preview.ambientColor=new Color(.5f,.5f,.5f);
                preview.BeginStaticPreview(new Rect(0,0,1600,1000)); preview.Render(true); var image=preview.EndStaticPreview();
                File.WriteAllBytes("Temp/DesertSceneryCollection.png",image.EncodeToPNG()); Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
        }
    }
}
