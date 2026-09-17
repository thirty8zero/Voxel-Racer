using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelSecretCrateBuilder
    {
        public const string Folder="Assets/Prefabs/Obstacles/SecretCrate";
        public const string PrefabPath=Folder+"/TopSecretCrate.prefab";
        [MenuItem("Tools/Voxel Racer/Build Top Secret Crate")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var texture=new Texture2D(256,256,TextureFormat.RGB24,false) {name="Light wood stencil",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<256;y++) for(int x=0;x<256;x++)
            {
                bool frame=x<14||x>241||y<14||y>241;
                float grain=((x*17+y*83)%31)/310f;
                Color color=frame?new Color(.66f,.45f,.22f):new Color(.87f-grain,.68f-grain,.40f-grain);
                if(!frame && (y%43<2 || ((y*7+x/15)%37==0))) color*=.8f;
                texture.SetPixel(x,y,color);
            }
            Text(texture,"TOP",94,163,4);
            Text(texture,"SECRET",58,107,4);
            // Dark recessed nail heads at each corner of the timber frame.
            foreach(int x in new[]{7,248}) foreach(int y in new[]{7,248})
                for(int a=-2;a<=2;a++) for(int b=-2;b<=2;b++) texture.SetPixel(x+a,y+b,new Color(.18f,.15f,.10f));
            texture.Apply();
            var savedTexture=Save(texture,Folder+"/LightWoodStencil.asset");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/LightWood.mat");
            if(material==null) {material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Folder+"/LightWood.mat");}
            material.SetTexture("_BaseMap",savedTexture);material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(material);
            var root=new GameObject("Top Secret Crate");
            try
            {
                for(int x=-1;x<=1;x++) for(int y=0;y<3;y++) for(int z=-1;z<=1;z++)
                {
                    var go=new GameObject("Wood Crate Voxel",typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider));
                    go.transform.SetParent(root.transform,false);
                    go.transform.localPosition=new Vector3(x*.55f,.28f+y*.55f,z*.55f);go.transform.localScale=Vector3.one*.55f;
                    var mesh=Cube(x,y,z);go.GetComponent<MeshFilter>().sharedMesh=Save(mesh,Folder+"/Voxel_"+x+"_"+y+"_"+z+".asset");
                    go.GetComponent<MeshRenderer>().sharedMaterial=material;
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                var definition=AssetDatabase.LoadAssetAtPath<VoxelStaticObstacleDefinition>("Assets/Resources/StaticObstacles/VoxelBox.asset");
                Undo.RecordObject(definition,"Assign secret crate model");definition.modelPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
            }
            finally {Object.DestroyImmediate(root);}
            Render();
        }
        private static T Save<T>(T value,string path) where T:Object
        {
            value.name=Path.GetFileNameWithoutExtension(path);
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old==null) {AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,old);
            if(old is UnityEngine.Mesh mesh)
            {
                // CopySerialized does not invalidate an already uploaded mesh buffer.
                mesh.vertices=mesh.vertices;mesh.triangles=mesh.triangles;mesh.uv=mesh.uv;
                mesh.RecalculateNormals();mesh.UploadMeshData(false);
            }
            Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;
        }
        private static UnityEngine.Mesh Cube(int x,int y,int z)
        {
            var verts=new List<Vector3>();var uvs=new List<Vector2>();var tris=new List<int>();
            foreach(var normal in new[]{Vector3.forward,Vector3.back,Vector3.right,Vector3.left,Vector3.up,Vector3.down})
            {
                Vector3 up=Mathf.Abs(normal.y)>.5f?Vector3.forward:Vector3.up;
                Vector3 right=Vector3.Cross(normal,up);
                int start=verts.Count;
                foreach(var corner in new[]{new Vector2(-.5f,-.5f),new Vector2(.5f,-.5f),new Vector2(.5f,.5f),new Vector2(-.5f,.5f)})
                {
                    var v=normal*.5f+right*corner.x+up*corner.y;verts.Add(v);
                    var global=v+new Vector3(x,y-1,z);
                    uvs.Add(new Vector2(Vector3.Dot(global,right)/3+.5f,Vector3.Dot(global,up)/3+.5f));
                }
                tris.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            }
            var mesh=new UnityEngine.Mesh {name="Crate voxel mapped wood"};mesh.SetVertices(verts);mesh.SetUVs(0,uvs);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static void Text(Texture2D tex,string text,int left,int top,int scale)
        {
            var glyphs=new Dictionary<char,string> {
                {'T',"11111/00100/00100/00100/00100/00100/00100"},
                {'O',"01110/11011/11011/11011/11011/11011/01110"},
                {'P',"11110/11011/11011/11110/11000/11000/11000"},
                {'S',"01111/11000/11000/01110/00011/00011/11110"},
                {'E',"11111/11000/11000/11110/11000/11000/11111"},
                {'C',"01111/11000/11000/11000/11000/11000/01111"},
                {'R',"11110/11011/11011/11110/11100/11010/11011"} };
            for(int i=0;i<text.Length;i++)
            {
                var rows=glyphs[text[i]].Split('/');
                for(int y=0;y<7;y++) for(int x=0;x<5;x++) if(rows[y][x]=='1')
                    for(int a=0;a<scale;a++) for(int b=0;b<scale;b++)
                    {
                        // Thin horizontal bridge gives the painted lettering a stencil cut.
                        if(y==3 && b==1) continue;
                        tex.SetPixel(left+i*6*scale+x*scale+a,top-y*scale-b,new Color(.045f,.04f,.03f));
                    }
            }
        }
        public static void Render()
        {
            var preview=new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
                preview.camera.transform.position=new Vector3(3,2.8f,-4);preview.camera.transform.LookAt(new Vector3(0,.8f,0));preview.camera.fieldOfView=35;
                preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=30;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.14f,.16f);
                preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(35,140,0);
                preview.lights[1].intensity=1;preview.ambientColor=Color.gray;
                preview.BeginStaticPreview(new Rect(0,0,900,800));preview.Render(true);var image=preview.EndStaticPreview();File.WriteAllBytes("Temp/TopSecretCrate.png",image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            finally {preview.Cleanup();}
        }
    }
}
