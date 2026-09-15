using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    /// <summary>Authoring only: the saved prefab is instantiated directly during gameplay.</summary>
    public static class VoxelEnemySedanBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Cars/BlackInterceptorEnemyCar.prefab";
        private static Material paint, glass, chrome, rubber, lamp, red, amber;

        [MenuItem("Tools/Voxel Racer/Build Black Interceptor Model")]
        public static void Build()
        {
            paint = Mat("InterceptorBlack", new Color(.035f,.045f,.039f), .35f, .55f);
            glass = Mat("InterceptorGlass", new Color(.105f,.18f,.19f), .3f, .75f);
            chrome = Mat("InterceptorChrome", new Color(.57f,.62f,.60f), .65f, .65f);
            rubber = Mat("InterceptorRubber", new Color(.016f,.019f,.021f), 0, .15f);
            lamp = Mat("InterceptorHeadlamp", new Color(.78f,.77f,.56f), .15f, .55f);
            red = Mat("InterceptorTailLamp", new Color(.5f,.025f,.018f), .1f, .5f);
            amber = Mat("InterceptorIndicator", new Color(.85f,.37f,.05f), .1f, .5f);
            var root = new GameObject("Black Interceptor Enemy Car");
            try
            {
                // Hollow .22 x .20 shell: spend pieces on the visible silhouette, not a solid interior.
                for (int x=0;x<10;x++) for(int y=0;y<3;y++) for(int z=0;z<22;z++)
                {
                    if(x!=0 && x!=9 && y!=2 && z!=0 && z!=21) continue;
                    float px=(x-4.5f)*.22f, py=.5f+y*.2f, pz=(z-10.5f)*.2f;
                    bool arch = y<2 && Mathf.Abs(px)>.85f &&
                        (new Vector2(pz-1.35f,py-.43f).magnitude<.55f || new Vector2(pz+1.35f,py-.43f).magnitude<.55f);
                    if(!arch) Block(root.transform,"Body voxel",new Vector3(px,py,pz),new Vector3(.22f,.2f,.2f),paint);
                }
                // Stepped sloping windscreens and narrowing roof, with black pillars and bright window surrounds.
                for(int row=0;row<4;row++)
                {
                    float y=1.04f+row*.08f, front=.78f-row*.14f, back=-1.15f+row*.075f;
                    float halfWidth=.96f;
                    for(int col=0;col<9;col++)
                    {
                        float x=(col-4)*.205f;
                        var frontPiece=Block(root.transform,"Windscreen voxel",new Vector3(x,y,front),new Vector3(.205f,.162f,.035f),col==0||col==8?chrome:glass);
                        frontPiece.localRotation=Quaternion.Euler(-60.25f,0,0);
                        var rearPiece=Block(root.transform,"Rear glass voxel",new Vector3(x,y,back),new Vector3(.205f,.110f,.035f),col==0||col==8?paint:glass);
                        rearPiece.localRotation=Quaternion.Euler(43.15f,0,0);
                    }
                    for(int side=-1;side<=1;side+=2)
                    {
                        int count=Mathf.CeilToInt((front-back)/.20f);
                        float step=(front-back)/count;
                        for(int n=0;n<count;n++)
                        {
                            float z=back+(n+.5f)*step;
                            bool pillar=Mathf.Abs(z+.25f)<.11f || n==0 || n==count-1;
                            Block(root.transform,pillar?"Door pillar":"Side glass voxel",new Vector3(side*halfWidth,y,z),new Vector3(.10f,.08f,step),pillar?paint:glass);
                        }
                    }
                }
                for(int x=0;x<9;x++) for(int z=0;z<6;z++)
                    Block(root.transform,"Roof voxel",new Vector3((x-4)*.21f,1.345f,-.84f+z*.22f),new Vector3(.21f,.05f,.22f),paint);
                for(int side=-1;side<=1;side+=2)
                {
                    Strip(root.transform,"Window sill chrome",new Vector3(side*1.055f,1.005f,-.18f),new Vector3(.035f,.035f,1.94f),chrome,8);
                    Strip(root.transform,"Side rubbing strip",new Vector3(side*1.108f,.73f,0),new Vector3(.026f,.045f,4.2f),rubber,16);
                    foreach(float z in new[]{.14f,-.65f})
                        Block(root.transform,"Chrome door handle",new Vector3(side*1.119f,.93f,z),new Vector3(.045f,.045f,.16f),chrome);
                    Block(root.transform,"Mirror stalk",new Vector3(side*1.13f,1.06f,.66f),new Vector3(.16f,.045f,.065f),chrome);
                    Block(root.transform,"Chrome mirror",new Vector3(side*1.22f,1.075f,.65f),new Vector3(.14f,.08f,.19f),chrome);
                    foreach(float z in new[]{1.35f,-1.35f}) Wheel(root.transform,side,z);
                    foreach(float z in new[]{1.35f,-1.35f})
                        for(int n=0;n<6;n++)
                        {
                            var trim=Sector(root.transform,"Wheel arch trim",.475f,.495f,.028f,chrome);
                            trim.localPosition=new Vector3(side*1.112f,.43f,z);
                            trim.localRotation=Quaternion.Euler(-n*30f,0,0);
                        }
                    foreach(float z in new[]{-.28f,-1.0f,.60f})
                        Block(root.transform,"Door seam",new Vector3(side*1.112f,.79f,z),new Vector3(.013f,.32f,.013f),rubber);
                    Block(root.transform,"Front side indicator",new Vector3(side*1.108f,.64f,1.94f),new Vector3(.025f,.07f,.18f),amber);
                }
                for(int end=-1;end<=1;end+=2)
                {
                    for(int n=0;n<10;n++)
                    {
                        Block(root.transform,"Chrome bumper",new Vector3((n-4.5f)*.225f,.46f,end*2.235f),new Vector3(.225f,.10f,.15f),chrome);
                        Block(root.transform,"Bumper rubber",new Vector3((n-4.5f)*.225f,.51f,end*2.285f),new Vector3(.225f,.045f,.06f),rubber);
                    }
                    Block(root.transform,"Number plate surround",new Vector3(0,.39f,end*2.33f),new Vector3(.45f,.19f,.045f),chrome);
                    Block(root.transform,"Number plate",new Vector3(0,.39f,end*2.359f),new Vector3(.38f,.13f,.018f),rubber);
                }
                Block(root.transform,"Grille recess",new Vector3(0,.77f,2.217f),new Vector3(1.24f,.30f,.04f),rubber);
                for(int n=0;n<5;n++) Block(root.transform,"Chrome grille slat",new Vector3(0,.64f+n*.062f,2.252f),new Vector3(1.25f,.016f,.035f),chrome);
                foreach(float x in new[]{-.63f,0,.63f}) Block(root.transform,"Grille upright",new Vector3(x,.77f,2.257f),new Vector3(.022f,.29f,.04f),chrome);
                foreach(int side in new[]{-1,1})
                {
                    Block(root.transform,"Headlamp surround",new Vector3(side*.85f,.77f,2.23f),new Vector3(.40f,.30f,.06f),chrome);
                    for(int n=0;n<2;n++) Block(root.transform,"Rectangular headlamp",new Vector3(side*(.755f+n*.19f),.77f,2.269f),new Vector3(.16f,.23f,.025f),lamp);
                    for(int n=0;n<3;n++) Block(root.transform,"Tail light",new Vector3(side*(.63f+n*.15f),.78f,-2.22f),new Vector3(.14f,.20f,.05f),n==2?amber:red);
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                var tuning=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>("Assets/Resources/EnemyVehicles/BlackInterceptorTuning.asset");
                Undo.RecordObject(tuning,"Assign enemy sedan model");
                tuning.modelPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                EditorUtility.SetDirty(tuning);
                AssetDatabase.SaveAssets();
                Debug.Log("Enemy sedan saved: "+root.GetComponentsInChildren<MeshRenderer>().Length+" detachable mesh pieces, zero per-piece colliders.");
            }
            finally { Object.DestroyImmediate(root); }
            Render();
        }

        private static void Wheel(Transform root,int side,float z)
        {
            var wheel=new GameObject("Obstacle Voxel Wheel").transform;
            wheel.SetParent(root,false); wheel.localPosition=new Vector3(side*1.065f,.43f,z);
            // Grid-aligned stepped silhouettes, like SpyCar2's stock wheels. Merge each
            // horizontal row to keep the piece count modest without smoothing the outline.
            int[] tyreWidths={3,5,7,7,7,5,3};
            for(int row=0;row<tyreWidths.Length;row++)
                Block(wheel,"Stepped tyre row",new Vector3(0,(row-3)*.12f,0),new Vector3(.29f,.12f,tyreWidths[row]*.12f),rubber);
            int[] rimWidths={3,5,5,5,3};
            for(int row=0;row<rimWidths.Length;row++)
                Block(wheel,"Voxel steel rim",new Vector3(side*.155f,(row-2)*.11f,0),new Vector3(.04f,.11f,rimWidths[row]*.11f),chrome);
            for(int row=-1;row<=1;row++)
                Block(wheel,"Stepped hubcap",new Vector3(side*.188f,row*.10f,0),new Vector3(.04f,.10f,row==0?.30f:.20f),chrome);
            foreach(int y in new[]{-1,1}) foreach(int depth in new[]{-1,1})
                Block(wheel,"Square wheel recess",new Vector3(side*.179f,y*.16f,depth*.16f),new Vector3(.012f,.065f,.065f),rubber);
        }
        private static Transform Sector(Transform parent,string name,float inner,float outer,float width,Material material)
        {
            string path="Assets/Prefabs/Cars/Interceptor"+(name=="Tyre facet"?"Tyre":"Arch")+"Sector.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
            bool newMesh=mesh==null;
            if(newMesh) mesh=new UnityEngine.Mesh { name=name+" mesh" };
            {
                var vertices=new Vector3[8];
                for(int side=0;side<2;side++) for(int r=0;r<2;r++) for(int a=0;a<2;a++)
                {
                    float radius=r==0?inner:outer, angle=a*30*Mathf.Deg2Rad;
                    vertices[side*4+r*2+a]=new Vector3((side-.5f)*width,Mathf.Sin(angle)*radius,Mathf.Cos(angle)*radius);
                }
                var indices=new[]{0,2,3,0,3,1,4,5,7,4,7,6,0,4,6,0,6,2,1,3,7,1,7,5,2,6,7,2,7,3,0,1,5,0,5,4};
                var flat=new Vector3[indices.Length]; var triangles=new int[indices.Length];
                for(int i=0;i<indices.Length;i++) { flat[i]=vertices[indices[i]]; triangles[i]=i; }
                mesh.Clear(); mesh.vertices=flat; mesh.triangles=triangles;
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                if(newMesh) AssetDatabase.CreateAsset(mesh,path); else EditorUtility.SetDirty(mesh);
            }
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(parent,false); go.GetComponent<MeshFilter>().sharedMesh=mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial=material; return go.transform;
        }
        private static void Disc(Transform parent,string name,float x,float radius,float depth,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name=name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false); go.transform.localPosition=new Vector3(x,0,0);
            go.transform.localRotation=Quaternion.Euler(0,0,90); go.transform.localScale=new Vector3(radius*2,depth*.5f,radius*2);
            go.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
        private static Transform Block(Transform parent,string name,Vector3 position,Vector3 size,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localScale=size;
            go.GetComponent<MeshRenderer>().sharedMaterial=mat;
            return go.transform;
        }
        private static void Strip(Transform p,string name,Vector3 pos,Vector3 size,Material mat,int count)
        {
            for(int n=0;n<count;n++) Block(p,name,pos+Vector3.forward*((n-(count-1)*.5f)*size.z/count),new Vector3(size.x,size.y,size.z/count),mat);
        }
        private static Material Mat(string name,Color color,float metal,float smooth)
        {
            string path="Assets/Resources/CarMaterials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m!=null) return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor",color); m.SetFloat("_Metallic",metal); m.SetFloat("_Smoothness",smooth);
            AssetDatabase.CreateAsset(m,path); return m;
        }
        [MenuItem("Tools/Voxel Racer/Render Black Interceptor Model")]
        public static void Render()
        {
            var preview=new PreviewRenderUtility();
            try
            {
                var car=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                preview.AddSingleGO(car);
                preview.camera.transform.position=new Vector3(6.1f,3.1f,7.3f);
                preview.camera.transform.LookAt(new Vector3(0,.85f,0));
                preview.camera.fieldOfView=34; preview.camera.nearClipPlane=.1f; preview.camera.farClipPlane=50;
                preview.camera.clearFlags=CameraClearFlags.SolidColor; preview.camera.backgroundColor=new Color(.17f,.20f,.23f);
                preview.lights[0].intensity=1.8f; preview.lights[0].transform.rotation=Quaternion.Euler(40,25,0);
                preview.lights[1].intensity=1.3f; preview.lights[1].transform.rotation=Quaternion.Euler(30,210,0);
                preview.ambientColor=new Color(.45f,.45f,.45f);
                preview.BeginStaticPreview(new Rect(0,0,1280,800)); preview.Render(true);
                var image=preview.EndStaticPreview();
                File.WriteAllBytes("Temp/BlackInterceptorEnemyCar.png",image.EncodeToPNG()); Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
        }
    }
}
