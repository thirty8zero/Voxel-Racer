using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Authors a hollow split-screen civilian microbus using standard detachable traffic pieces.</summary>
    public static class VoxelCivilianKombiBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Cars/CivilianKombi.prefab";
        public const string TuningPath = "Assets/Resources/EnemyVehicles/CivilianKombiTuning.asset";
        public const int PieceBudget = 650;
        private static Material paint, cream, glass, rubber, chrome, lamp, red, amber;

        [MenuItem("Tools/Voxel Racer/Build Civilian Kombi")]
        public static void Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Build outside Play Mode.");
            Material Shared(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CarMaterials/CivilianHatch"+name+".mat");
            paint=Mat("KombiMintPaint",new Color(.31f,.64f,.54f));cream=Mat("KombiCream",new Color(.86f,.84f,.72f));
            glass=Shared("Glass");rubber=Shared("Trim");chrome=Shared("Rim");lamp=Shared("Lamp");red=Shared("TailLamp");amber=Shared("Indicator");
            var root=new GameObject("Civilian Kombi");
            try
            {
                root.AddComponent<VoxelTrafficPaint>().bodyMaterial=paint;
                // Panel strips only; the cabin and lower body remain hollow.
                foreach(int side in new[]{-1,1})
                {
                    for(int row=0;row<8;row++)
                    {
                        float front=Front(row),back=-2.02f,step=(front-back)/18;
                        for(int col=0;col<18;col++)
                        {
                            float z=back+(col+.5f)*step,y=.55f+row*.18f;
                            if(row<3 && new[]{-1.30f,1.30f}.Any(w=>Mathf.Abs(z-w)<.46f && y<.42f+Mathf.Sqrt(Mathf.Max(0,.49f*.49f-(z-w)*(z-w)))))continue;
                            bool window=row>=5 && col>2 && col!=6 && col!=10 && col!=14 && col!=17;
                            B(root.transform,window?"Side window voxel":"Side body voxel",new Vector3(side*(row>=5?.93f:.96f),y,z),new Vector3(.16f,.18f,step),window?glass:row>=5?cream:paint);
                        }
                    }
                    foreach(float z in new[]{1.08f,.05f,-.90f}) B(root.transform,"Door seam",new Vector3(side*1.047f,.97f,z),new Vector3(.012f,.65f,.014f),rubber);
                    foreach(float z in new[]{.86f,-.12f}) B(root.transform,"Chrome door handle",new Vector3(side*1.065f,1.18f,z),new Vector3(.05f,.035f,.16f),chrome);
                    B(root.transform,"Mirror arm",new Vector3(side*1.14f,1.49f,1.54f),new Vector3(.24f,.035f,.035f),chrome);
                    Round(root.transform,"Round mirror",new Vector3(side*1.27f,1.57f,1.54f),.19f,.14f,chrome);
                    for(int row=0;row<5;row++) B(root.transform,"Rear cooling vent",new Vector3(side*1.018f,1.40f+row*.086f,-1.69f),new Vector3(.02f,.025f,.38f),rubber);
                    foreach(float z in new[]{-1.30f,1.30f})Wheel(root.transform,side,z);
                }
                for(int row=0;row<8;row++)
                {
                    float y=.55f+row*.18f,width=row>=5?1.96f:2.08f;
                    for(int col=-4;col<=4;col++)
                    {
                        float x=col*width/9;bool window=row>=5&&col!=0&&Mathf.Abs(col)!=4;
                        bool vee=row<5&&Mathf.Abs(x)<.08f+row*.23f;
                        B(root.transform,window?"Split windscreen voxel":"Front body voxel",new Vector3(x,y,Front(row)),new Vector3(width/9,.18f,row>=5?.20f:.14f),window?glass:row>=5||vee?cream:paint);
                    }
                    for(int col=0;col<8;col++)
                    {
                        bool window=row>=5&&row<=6&&col>0&&col<7;
                        B(root.transform,window?"Rear window voxel":"Rear body voxel",new Vector3((col-3.5f)*width/8,y,-2.02f),new Vector3(width/8,.18f,.14f),window?glass:row>=5?cream:paint);
                    }
                }
                // Flush underside closes the cabin; only the outer top steps up towards the centre.
                for(int x=0;x<8;x++)for(int z=0;z<6;z++)
                {
                    float height=x==0||x==7?.12f:x==1||x==6?.16f:.18f;
                    B(root.transform,"Cream roof voxel",new Vector3((x-3.5f)*.245f,1.90f+height*.5f,-2.09f+(z+.5f)*4.06f/6),new Vector3(.245f,height,4.06f/6),cream);
                }
                foreach(int side in new[]{-1,1})
                {
                    Round(root.transform,"Headlamp chrome",new Vector3(side*.75f,.87f,2.112f),.35f,.055f,chrome);
                    Round(root.transform,"Headlamp lens",new Vector3(side*.75f,.87f,2.15f),.28f,.025f,lamp);
                    Round(root.transform,"Front indicator",new Vector3(side*.77f,1.20f,2.115f),.17f,.035f,amber);
                    Round(root.transform,"Rear lamp chrome",new Vector3(side*.82f,.80f,-2.11f),.19f,.04f,chrome);
                    Round(root.transform,"Rear lamp",new Vector3(side*.82f,.80f,-2.14f),.14f,.035f,red);
                    B(root.transform,"Rear engine lid hinge",new Vector3(side*.33f,.95f,-2.105f),new Vector3(.08f,.03f,.025f),chrome);
                }
                Round(root.transform,"Nose badge surround",new Vector3(0,1.16f,2.117f),.34f,.04f,chrome);
                Round(root.transform,"Nose badge inset",new Vector3(0,1.16f,2.145f),.27f,.02f,paint);
                foreach(int side in new[]{-1,1})
                {
                    var v=B(root.transform,"Nose badge V",new Vector3(side*.055f,1.16f,2.16f),new Vector3(.026f,.16f,.02f),cream);v.localRotation=Quaternion.Euler(0,0,-side*28);
                }
                B(root.transform,"Rear hatch seam",new Vector3(0,1.30f,-2.102f),new Vector3(1.64f,.015f,.018f),rubber);
                B(root.transform,"Rear hatch latch",new Vector3(0,1.18f,-2.12f),new Vector3(.045f,.08f,.035f),chrome);
                foreach(int side in new[]{-1,1}) B(root.transform,"Engine lid seam",new Vector3(side*.57f,.76f,-2.102f),new Vector3(.014f,.45f,.018f),rubber);
                B(root.transform,"Rear engine lid latch",new Vector3(0,.60f,-2.12f),new Vector3(.045f,.07f,.03f),chrome);
                B(root.transform,"Tailpipe",new Vector3(.55f,.29f,-2.24f),new Vector3(.09f,.09f,.22f),chrome);
                foreach(int end in new[]{-1,1})
                {
                    for(int x=0;x<4;x++) B(root.transform,"Cream bumper",new Vector3((x-1.5f)*.55f,.44f,end*2.22f),new Vector3(.55f,.14f,.16f),cream);
                    B(root.transform,"Number plate",new Vector3(0,.69f,end*2.105f),new Vector3(.40f,.14f,.025f),rubber);
                }
                VoxelCivilianDrivelineBuilder.AddStructure(root);
                int count=root.GetComponentsInChildren<MeshRenderer>().Length;
                if(count>PieceBudget)throw new InvalidOperationException("Kombi exceeds piece budget: "+count);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                var tuning=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>(TuningPath);
                if(tuning==null)
                {
                    tuning=Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning"));
                    tuning.name="CivilianKombiTuning";tuning.displayName="Civilian Kombi";tuning.collisionHalfWidth=1.4f;tuning.collisionHalfLength=2.5f;
                    AssetDatabase.CreateAsset(tuning,TuningPath);
                }
                tuning.modelPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);EditorUtility.SetDirty(tuning);
                Register(tuning);AssetDatabase.SaveAssets();Debug.Log("Kombi built: "+count+" pieces including three protected driveline pieces.");
            }
            finally{Object.DestroyImmediate(root);}
            Render();
        }
        private static float Front(int row) => row<5?2.02f:2.01f-(row-5)*.07f;
        private static void Register(VoxelEnemyVehicleTuning kombi)
        {
            var hatch=Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning");var van=Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/CivilianVanTuning");var beetle=Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/CivilianBeetleTuning");
            foreach(string guid in AssetDatabase.FindAssets("t:VoxelTrackDefinition").Concat(AssetDatabase.FindAssets("t:VoxelObstacleCarTuning")).Distinct())
                foreach(var traffic in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<VoxelObstacleCarTuning>())
                {
                    var existing=traffic.civilianVehiclePool;if(existing!=null&&existing.Contains(kombi))continue;
                    // Preserve existing entries and their weighting; append the new civilian once.
                    traffic.civilianVehiclePool=(existing!=null&&existing.Length>0?existing:new[]{traffic.trafficCarEnemyTuning??hatch,traffic.semiTrailerEnemyTuning??van,beetle}).Concat(new[]{kombi}).Where(v=>v!=null).ToArray();EditorUtility.SetDirty(traffic);
                }
        }
        private static Material Mat(string name,Color colour)
        {
            string path="Assets/Resources/CarMaterials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",colour);material.SetFloat("_Smoothness",.32f);AssetDatabase.CreateAsset(material,path);return material;
        }
        private static Transform B(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
        private static void Round(Transform parent,string name,Vector3 centre,float diameter,float depth,Material material)
        {
            for(int row=-1;row<=1;row++)B(parent,name,centre+Vector3.up*(row*diameter/3),new Vector3(row==0?diameter:diameter*.72f,diameter/3,depth),material);
        }
        private static void Wheel(Transform parent,int side,float z)
        {
            var wheel=new GameObject("Obstacle Voxel Wheel").transform;wheel.SetParent(parent,false);wheel.localPosition=new Vector3(side*.985f,.42f,z);
            int[] widths={3,5,7,7,7,5,3};for(int row=0;row<7;row++)B(wheel,"Stepped tyre",new Vector3(0,(row-3)*.115f,0),new Vector3(.25f,.115f,widths[row]*.115f),rubber);
            int[] rim={3,5,5,5,3};for(int row=0;row<5;row++)B(wheel,"Cream wheel rim",new Vector3(side*.14f,(row-2)*.09f,0),new Vector3(.03f,.09f,rim[row]*.09f),cream);
            for(int row=-1;row<=1;row++)B(wheel,"Chrome hubcap",new Vector3(side*.165f,row*.085f,0),new Vector3(.035f,.085f,row==0?.30f:.22f),chrome);
        }
        [MenuItem("Tools/Voxel Racer/Render Civilian Kombi")]
        public static void Render()
        {
            Directory.CreateDirectory("Temp/Kombi");string[] names={"Front","Rear","Left","Right","FrontStraight","RearStraight","Top"};
            Vector3[] views={new(5,3.4f,7),new(-5,3.4f,-7),new(-8,1.1f,0),new(8,1.1f,0),new(0,1.1f,8),new(0,1.1f,-8),new(0,8,.001f)};
            for(int i=0;i<views.Length;i++)
            {
                var preview=new PreviewRenderUtility();
                try
                {
                    preview.AddSingleGO(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));preview.camera.transform.position=views[i];preview.camera.transform.LookAt(new Vector3(0,1.02f,0));preview.camera.fieldOfView=34;preview.camera.orthographic=i>=2;preview.camera.orthographicSize=i==6?2.65f:1.70f;
                    preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.17f,.20f,.23f);
                    preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(40,25,0);preview.lights[1].intensity=1.3f;preview.lights[1].transform.rotation=Quaternion.Euler(30,210,0);preview.ambientColor=new Color(.45f,.45f,.45f);
                    preview.BeginStaticPreview(new Rect(0,0,1100,750));preview.Render(true);var image=preview.EndStaticPreview();File.WriteAllBytes("Temp/Kombi/"+names[i]+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
                finally{preview.Cleanup();}
            }
        }
    }
}
