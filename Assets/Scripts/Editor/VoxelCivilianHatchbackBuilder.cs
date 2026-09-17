using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelCivilianHatchbackBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Cars/CivilianHatchback.prefab";
        private static Material paint, glass, rubber, rim, red, amber, lamp;

        [MenuItem("Tools/Voxel Racer/Build Civilian Hatchback")]
        public static void Build()
        {
            paint=Mat("CivilianHatchPaint",new Color(.85f,.87f,.84f),.15f);
            glass=Mat("CivilianHatchGlass",new Color(.075f,.14f,.18f),.55f);
            rubber=Mat("CivilianHatchTrim",new Color(.022f,.026f,.029f),.1f);
            rim=Mat("CivilianHatchRim",new Color(.61f,.65f,.66f),.4f);
            red=Mat("CivilianHatchTailLamp",new Color(.58f,.025f,.025f),.4f);
            amber=Mat("CivilianHatchIndicator",new Color(.95f,.36f,.035f),.4f);
            lamp=Mat("CivilianHatchLamp",new Color(.78f,.84f,.81f),.4f);
            var root=new GameObject("Civilian Hatchback");
            try
            {
                root.AddComponent<VoxelTrafficPaint>().bodyMaterial=paint;
                // Hollow shell with stepped shoulders and open wheel arches.
                for(int x=0;x<10;x++) for(int z=0;z<22;z++) for(int y=0;y<3;y++)
                {
                    if(x!=0 && x!=9 && z!=0 && z!=21 && y!=2) continue;
                    float px=(x-4.5f)*.2f,pz=(z-10.5f)*.2f,py=.44f+y*.18f;
                    bool arch=(x==0||x==9) && (Mathf.Abs(pz-1.3f)<.51f||Mathf.Abs(pz+1.3f)<.51f)
                        && py<.82f && new Vector2(Mathf.Abs(pz)-1.3f,py-.42f).magnitude<.53f;
                    if(!arch) B(root.transform,"Body voxel",new Vector3(px,py,pz),new Vector3(.2f,.18f,.2f),paint);
                }
                // Low wedge nose; short roof and long raked hatch glass.
                for(int row=0;row<5;row++)
                {
                    float y=.94f+row*.1f,front=.80f-row*.13f,back=-1.70f+row*.20f;
                    float half=.91f-row*.015f;
                    for(int col=-4;col<=4;col++)
                    {
                        B(root.transform,"Windscreen voxel",new Vector3(col*.19f,y,front),new Vector3(.19f,.1f,.16f),Mathf.Abs(col)==4?paint:glass);
                        B(root.transform,"Hatch glass voxel",new Vector3(col*.19f,y,back),new Vector3(.19f,.1f,.22f),Mathf.Abs(col)==4?paint:glass);
                    }
                    foreach(int side in new[]{-1,1})
                    {
                        int count=Mathf.CeilToInt((front-back)/.18f); float step=(front-back)/count;
                        for(int n=0;n<count;n++)
                        {
                            float z=back+(n+.5f)*step;
                            bool pillar=n==0 || n==count-1 || Mathf.Abs(z+.45f)<.085f;
                            B(root.transform,pillar?"Window pillar":"Side window voxel",new Vector3(side*half,y,z),new Vector3(.10f,.10f,step),pillar?paint:glass);
                        }
                    }
                }
                for(int x=-4;x<=4;x++) for(int z=0;z<5;z++)
                    B(root.transform,"Roof voxel",new Vector3(x*.19f,1.41f,-.72f+z*.20f),new Vector3(.19f,.08f,.20f),paint);
                foreach(int side in new[]{-1,1})
                {
                    for(int z=0;z<20;z++)
                        B(root.transform,"Side moulding",new Vector3(side*1.005f,.66f,-1.9f+z*.2f),new Vector3(.025f,.035f,.2f),rim);
                    B(root.transform,"Door seam",new Vector3(side*1.006f,.69f,-.53f),new Vector3(.012f,.32f,.015f),rubber);
                    B(root.transform,"Door handle recess",new Vector3(side*1.015f,.84f,-.36f),new Vector3(.025f,.065f,.16f),rubber);
                    B(root.transform,"Door handle",new Vector3(side*1.033f,.85f,-.36f),new Vector3(.024f,.025f,.12f),rim);
                    B(root.transform,"Mirror mount",new Vector3(side*.995f,1.02f,.60f),new Vector3(.16f,.09f,.12f),rubber);
                    B(root.transform,"Painted mirror",new Vector3(side*1.09f,1.06f,.59f),new Vector3(.19f,.13f,.23f),paint);
                    B(root.transform,"Mirror glass",new Vector3(side*1.09f,1.06f,.466f),new Vector3(.15f,.09f,.018f),rim);
                    foreach(float z in new[]{-1.3f,1.3f}) Wheel(root.transform,side,z);
                    // Closed pop-up headlight panels on the hood.
                    B(root.transform,"Headlight panel seam",new Vector3(side*.65f,.896f,1.65f),new Vector3(.48f,.016f,.50f),rubber);
                    B(root.transform,"Closed headlight cover",new Vector3(side*.65f,.906f,1.65f),new Vector3(.45f,.018f,.47f),paint);
                    B(root.transform,"Front indicator",new Vector3(side*.83f,.80f,2.212f),new Vector3(.32f,.085f,.035f),amber);
                    B(root.transform,"Fog light recess",new Vector3(side*.70f,.49f,2.236f),new Vector3(.40f,.14f,.035f),rubber);
                    for(int n=0;n<2;n++) B(root.transform,"Fog lamp",new Vector3(side*(.60f+n*.18f),.49f,2.259f),new Vector3(.145f,.09f,.023f),lamp);
                    for(int n=0;n<4;n++) B(root.transform,"Rear lamp voxel",new Vector3(side*(.49f+n*.13f),.77f,-2.218f),new Vector3(.13f,.16f,.035f),n==3?amber:n==0?lamp:red);
                    B(root.transform,"Spoiler foot",new Vector3(side*.69f,.96f,-1.99f),new Vector3(.11f,.18f,.13f),rubber);
                }
                for(int end=-1;end<=1;end+=2) for(int x=0;x<10;x++)
                {
                    B(root.transform,"Bumper voxel",new Vector3((x-4.5f)*.20f,.60f,end*2.215f),new Vector3(.20f,.13f,.12f),paint);
                    B(root.transform,"Bumper lower voxel",new Vector3((x-4.5f)*.19f,.38f,end*2.19f),new Vector3(.19f,.09f,.1f),paint);
                }
                for(int x=0;x<10;x++) B(root.transform,"Slim hatch spoiler",new Vector3((x-4.5f)*.20f,1.06f,-2.0f),new Vector3(.20f,.065f,.21f),rubber);
                B(root.transform,"Nose grille",new Vector3(0,.79f,2.221f),new Vector3(1.28f,.045f,.025f),rubber);
                B(root.transform,"Lower intake",new Vector3(0,.47f,2.23f),new Vector3(.82f,.14f,.045f),rubber);
                foreach(int end in new[]{-1,1})
                    B(root.transform,"Number plate",new Vector3(0,end==1?.48f:.75f,end*2.259f),new Vector3(.40f,.14f,.025f),lamp);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                var tuning=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>("Assets/Resources/EnemyVehicles/TrafficCarTuning.asset");
                Undo.RecordObject(tuning,"Assign civilian hatchback");
                tuning.modelPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                EditorUtility.SetDirty(tuning); AssetDatabase.SaveAssets();
                Debug.Log("Civilian hatchback saved: "+root.GetComponentsInChildren<MeshRenderer>().Length+" destructible pieces.");
            }
            finally { Object.DestroyImmediate(root); }
            Render();
        }
        private static void Wheel(Transform parent,int side,float z)
        {
            var wheel=new GameObject("Obstacle Voxel Wheel").transform;
            wheel.SetParent(parent,false); wheel.localPosition=new Vector3(side*.98f,.42f,z);
            int[] widths={3,5,7,7,7,5,3};
            for(int row=0;row<7;row++) B(wheel,"Stepped tyre",new Vector3(0,(row-3)*.12f,0),new Vector3(.28f,.12f,widths[row]*.12f),rubber);
            int[] hubs={3,5,5,5,3};
            for(int row=0;row<5;row++) B(wheel,"Disc wheel voxel",new Vector3(side*.149f,(row-2)*.105f,0),new Vector3(.035f,.105f,hubs[row]*.105f),rim);
            foreach(int y in new[]{-1,1}) foreach(int depth in new[]{-1,1})
                B(wheel,"Wheel vent",new Vector3(side*.171f,y*.16f,depth*.16f),new Vector3(.012f,.055f,.065f),rubber);
            B(wheel,"Hub centre",new Vector3(side*.18f,0,0),new Vector3(.03f,.15f,.15f),rim);
        }
        private static void B(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        private static Material Mat(string name,Color colour,float smooth)
        {
            string path="Assets/Resources/CarMaterials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null) return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor",colour);material.SetFloat("_Smoothness",smooth);
            AssetDatabase.CreateAsset(material,path);return material;
        }
        [MenuItem("Tools/Voxel Racer/Render Civilian Hatchback")]
        public static void Render()
        {
            for(int side=0;side<2;side++)
            {
                var preview=new PreviewRenderUtility();
                try
                {
                    preview.AddSingleGO(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
                    preview.camera.transform.position=new Vector3(6,3.1f,side==0?7.3f:-7.3f);
                    preview.camera.transform.LookAt(new Vector3(0,.8f,0));preview.camera.fieldOfView=34;
                    preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.17f,.20f,.23f);
                    preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(40,25,0);
                    preview.lights[1].intensity=1.3f;preview.lights[1].transform.rotation=Quaternion.Euler(30,210,0);
                    preview.ambientColor=new Color(.45f,.45f,.45f);
                    preview.BeginStaticPreview(new Rect(0,0,1100,700));preview.Render(true);
                    var image=preview.EndStaticPreview();File.WriteAllBytes("Temp/CivilianHatchback"+side+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
                finally {preview.Cleanup();}
            }
        }
    }
}
