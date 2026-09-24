using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace VoxelRacer.Editor
{
    public static class VoxelBossEyesBuilder
    {
        static readonly Vector2[] Brow={new(0,.98f),new(.12f,.85f),new(.98f,.42f),new(1,.30f),new(.91f,.32f),new(.24f,.70f),new(.02f,.83f)};
        static readonly Vector2[] Eye={new(.02f,.62f),new(.30f,.60f),new(.66f,.37f),new(.96f,.16f),new(.58f,.05f),new(.30f,.09f),new(.12f,.28f)};
        static readonly Vector2[] Inner={new(.12f,.52f),new(.34f,.48f),new(.80f,.19f),new(.53f,.14f),new(.29f,.19f)};
        static readonly Vector2[] Pupil={new(.32f,.49f),new(.60f,.35f),new(.53f,.25f),new(.39f,.26f)};
        static bool Inside(Vector2 p,Vector2[] polygon)
        {
            bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
                if((polygon[i].y>p.y)!=(polygon[j].y>p.y) && p.x<(polygon[j].x-polygon[i].x)*(p.y-polygon[i].y)/(polygon[j].y-polygon[i].y)+polygon[i].x) inside=!inside;
            return inside;
        }
        public static void UpdatePrefab()
        {
            var root=PrefabUtility.LoadPrefabContents(VoxelRedVanBossBuilder.PrefabPath);
            try{AddEyes(root);PrefabUtility.SaveAsPrefabAsset(root,VoxelRedVanBossBuilder.PrefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        public static void AddEyes(GameObject root)
        {
            const string folder="Assets/Resources/Bosses/RearEyes";
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            string materialPath=folder+"/AngryRedEyes.mat";
            var red=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(red==null)
            {
                red=new Material(Shader.Find("Universal Render Pipeline/Unlit"));red.SetColor("_BaseColor",new Color(1f,.015f,.025f));
                AssetDatabase.CreateAsset(red,materialPath);
            }
            // Bake the artwork into each existing glass voxel: no extra damage targets or health.
            var cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            int index=0;
            foreach(var glass in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Rear door glass"))
            {
                var vertices=new List<Vector3>();var triangles=new List<int>();
                Vector3 centre=root.transform.InverseTransformPoint(glass.transform.position);
                int side=centre.x<0?-1:1;
                for(int x=0;x<28;x++) for(int y=0;y<18;y++)
                {
                    var uv=new Vector2((x+.5f)/28f,(y+.5f)/18f);
                    if(!Inside(uv,Brow) && !(Inside(uv,Eye)&&!Inside(uv,Inner)) && !Inside(uv,Pupil))continue;
                    float a=side*(.84f-x*.74f/28),b=side*(.84f-(x+1)*.74f/28);
                    float left=Mathf.Max(Mathf.Min(a,b),centre.x-.13f),right=Mathf.Min(Mathf.Max(a,b),centre.x+.13f);
                    float bottom=Mathf.Max(1.365f+y*.43f/18,centre.y-.12f),top=Mathf.Min(1.365f+(y+1)*.43f/18,centre.y+.12f);
                    if(right<=left || top<=bottom)continue;
                    int n=vertices.Count;
                    foreach(var p in new[]{new Vector3(left,bottom,-2.362f),new Vector3(left,top,-2.362f),new Vector3(right,top,-2.362f),new Vector3(right,bottom,-2.362f)})
                        vertices.Add(glass.transform.InverseTransformPoint(root.transform.TransformPoint(p)));
                    triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
                }
                var decal=new Mesh();decal.SetVertices(vertices);decal.SetTriangles(triangles,0);decal.RecalculateNormals();
                var combined=new Mesh{name="Rear window angry eye voxel"};
                combined.CombineMeshes(new[]{new CombineInstance{mesh=cube,transform=Matrix4x4.identity},new CombineInstance{mesh=decal,transform=Matrix4x4.identity}},false,true);
                string path=folder+"/WindowEye"+(index++)+".asset";
                combined.name=Path.GetFileNameWithoutExtension(path);
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){AssetDatabase.CreateAsset(combined,path);saved=combined;}
                else{EditorUtility.CopySerialized(combined,saved);Object.DestroyImmediate(combined);EditorUtility.SetDirty(saved);}
                glass.GetComponent<MeshFilter>().sharedMesh=saved;
                glass.sharedMaterials=new[]{glass.sharedMaterial,red};Object.DestroyImmediate(decal);
            }
        }
        public static void Render()
        {
            var preview=new PreviewRenderUtility();
            try
            {
                var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VoxelRedVanBossBuilder.PrefabPath));
                root.GetComponent<VoxelBossSpikeRig>().SetPose(0,0);preview.AddSingleGO(root);
                preview.camera.transform.position=new Vector3(0,2.4f,-9);preview.camera.transform.LookAt(new Vector3(0,1.2f,-1));
                preview.camera.fieldOfView=30;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.17f,.2f,.23f);
                preview.lights[0].intensity=2;preview.lights[0].transform.rotation=Quaternion.Euler(40,155,0);
                preview.lights[1].intensity=1.3f;preview.lights[1].transform.rotation=Quaternion.Euler(30,20,0);
                preview.ambientColor=new Color(.5f,.5f,.5f);
                preview.BeginStaticPreview(new Rect(0,0,1000,800));preview.Render(true);
                var image=preview.EndStaticPreview();File.WriteAllBytes("Temp/RedBossAngryEyes.png",image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            finally{preview.Cleanup();}
        }
    }
}
