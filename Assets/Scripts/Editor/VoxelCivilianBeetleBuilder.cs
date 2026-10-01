using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace VoxelRacer.Editor
{
    public static class VoxelCivilianBeetleBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Cars/CivilianBeetle.prefab";
        public const string TuningPath = "Assets/Resources/EnemyVehicles/CivilianBeetleTuning.asset";
        private static Material paint, glass, rubber, chrome, lamp, red, amber;
        [MenuItem("Tools/Voxel Racer/Build Civilian Beetle")]
        public static void Build()
        {
            Material M(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CarMaterials/CivilianHatch" + name + ".mat");
            paint=M("Paint"); glass=M("Glass"); rubber=M("Trim"); chrome=M("Rim"); lamp=M("Lamp"); red=M("TailLamp"); amber=M("Indicator");
            var root=new GameObject("Civilian Beetle");
            try
            {
                root.AddComponent<VoxelTrafficPaint>().bodyMaterial=paint;
                // Narrow hollow lower body, with no invisible solid voxel filling.
                for(int x=-3;x<=3;x++) for(int z=-10;z<=10;z++) for(int y=0;y<3;y++)
                {
                    if(Mathf.Abs(x)!=3 && Mathf.Abs(z)!=10) continue;
                    float pz=z*.2f, width=1f-.22f*Mathf.Pow(Mathf.Abs(pz)/2f,3);
                    float lower = Mathf.Clamp01((Mathf.Abs(pz)-1f))* .20f;
                    // Put the old narrow interior ring on the visible outer shell.
                    float halfWidth=.80f*width;
                    float px=Mathf.Abs(x)==3?Mathf.Sign(x)*(halfWidth-.09f):x*(halfWidth*2/7);
                    B(root.transform,"Body voxel",new Vector3(px,.48f+y*.18f-lower,pz),new Vector3(Mathf.Abs(x)==3?.18f:halfWidth*2/7,.18f,.2f),paint);
                }
                // Continuous stepped shell: rounded roof, raked screens and two side windows.
                float[] half={.80f,.80f,.76f,.69f,.58f};
                float[] front={.90f,.76f,.58f,.36f,.10f};
                float[] rear={-1.55f,-1.45f,-1.25f,-1.03f,-.80f};
                for(int row=0;row<half.Length;row++)
                {
                    float y=.96f+row*.14f;
                    // Each raked strip reaches the preceding row; the steps are solid,
                    // while the cabin stays hollow rather than filling it with cubes.
                    float frontMin=front[row]-.12f, frontMax=(row==0?front[row]+.02f:front[row-1]-.08f);
                    float rearMin=(row==0?rear[row]-.02f:rear[row-1]+.08f), rearMax=rear[row]+.12f;
                    for(int x=-4;x<=4;x++)
                    {
                        float step=half[row]*2/9;
                        B(root.transform,"Front screen voxel",new Vector3(x*step,y,(frontMin+frontMax)*.5f),new Vector3(step,.14f,frontMax-frontMin),row>=1&&row<=3&&Mathf.Abs(x)<=3?glass:paint);
                        B(root.transform,"Rear screen voxel",new Vector3(x*step,y,(rearMin+rearMax)*.5f),new Vector3(step,.14f,rearMax-rearMin),row>=2&&row<=3&&Mathf.Abs(x)<=2?glass:paint);
                    }
                    foreach(int sign in new[]{-1,1})
                    {
                        int n=Mathf.CeilToInt((frontMin-rearMax)/.18f);float step=(frontMin-rearMax)/n;
                        for(int z=0;z<n;z++)
                        {
                            float depth=rearMax+(z+.5f)*step;
                            bool window=row>=1&&row<=3&&z>0&&z<n-1&&Mathf.Abs(depth+.30f)>.09f;
                            B(root.transform,window?"Side window voxel":"Cabin side voxel",new Vector3(sign*(half[row]-.06f),y,depth),new Vector3(.12f,.14f,step),window?glass:paint);
                        }
                    }
                }
                // Close only the opening inside the final perimeter, flush with its top.
                for(int x=0;x<5;x++)for(int z=0;z<4;z++)
                    B(root.transform,"Roof voxel",new Vector3((x-2)*.184f,1.54f,-.68f+(z+.5f)*.165f),new Vector3(.184f,.10f,.165f),paint);
                foreach(int end in new[]{-1,1})
                {
                    int rows=end==1?7:4;
                    for(int row=0;row<rows;row++) for(int x=-3;x<=3;x++)
                    {
                        float z=end*((end==1?.95f:1.49f)+row*.17f),y=.94f-row*(end==1?.045f:.075f);
                        float step=1.6f*(1f-.22f*Mathf.Pow(Mathf.Abs(z)/2f,3))/7;
                        B(root.transform,end==1?"Bonnet voxel":"Engine lid voxel",new Vector3(x*step,y-.012f*Mathf.Abs(x),z),new Vector3(step,.14f,.18f),paint);
                    }
                }
                foreach(int side in new[]{-1,1})
                {
                    foreach(float wheelZ in new[]{-1.27f,1.27f})
                    {
                        var wheel=new GameObject("Obstacle Voxel Wheel").transform; wheel.SetParent(root.transform,false);wheel.localPosition=new Vector3(side*.88f,.42f,wheelZ);
                        int[] widths={3,5,7,7,7,5,3};
                        for(int r=0;r<7;r++) B(wheel,"Stepped tyre",new Vector3(0,(r-3)*.115f,0),new Vector3(.25f,.115f,widths[r]*.115f),rubber);
                        int[] hub={3,5,5,5,3};
                        for(int r=0;r<5;r++) B(wheel,"Chrome hubcap",new Vector3(side*.137f,(r-2)*.09f,0),new Vector3(.04f,.09f,hub[r]*.09f),chrome);
                        // Raised wings wrap over open wheel arches, wider than the main body.
                        for(int z=-4;z<=4;z++)
                        {
                            float dz=z*.145f;
                            float top=.42f+Mathf.Sqrt(Mathf.Max(0,.66f*.66f-dz*dz));
                            float bottom=.42f+Mathf.Sqrt(Mathf.Max(0,.48f*.48f-dz*dz));
                            float width=.46f-.12f*Mathf.Abs(z)/4;
                            B(root.transform,"Rounded wing voxel",new Vector3(side*.85f,(top+bottom)*.5f,wheelZ+dz),new Vector3(width,top-bottom,.145f),paint);
                        }
                    }
                    for(int z=-7;z<=4;z++) B(root.transform,"Door shoulder voxel",new Vector3(side*.70f,.86f,z*.18f),new Vector3(.30f,.14f,.18f),paint);
                    for(int z=-3;z<=3;z++) B(root.transform,"Running board",new Vector3(side*.88f,.36f,z*.18f),new Vector3(.30f,.10f,.18f),rubber);
                    B(root.transform,"Door handle",new Vector3(side*.84f,1.02f,-.22f),new Vector3(.06f,.045f,.18f),chrome);
                    B(root.transform,"Mirror stalk",new Vector3(side*.88f,1.18f,.56f),new Vector3(.16f,.035f,.035f),chrome);
                    B(root.transform,"Mirror",new Vector3(side*.96f,1.22f,.56f),new Vector3(.08f,.14f,.14f),chrome);
                    // Pixel-round lamps on the front wings.
                    for(int row=-1;row<=1;row++)
                    {
                        float width=row==0?.32f:.22f;
                        B(root.transform,"Headlight chrome",new Vector3(side*.82f,.84f+row*.10f,1.99f),new Vector3(width+.05f,.105f,.07f),chrome);
                        B(root.transform,"Headlight lens",new Vector3(side*.82f,.84f+row*.10f,2.033f),new Vector3(width,.09f,.025f),lamp);
                    }
                    B(root.transform,"Wing indicator",new Vector3(side*.85f,1.01f,1.38f),new Vector3(.12f,.07f,.21f),amber);
                    B(root.transform,"Rear lamp",new Vector3(side*.84f,.76f,-1.86f),new Vector3(.15f,.22f,.07f),red);
                    B(root.transform,"Tailpipe",new Vector3(side*.34f,.30f,-2.12f),new Vector3(.09f,.09f,.20f),chrome);
                    foreach(int end in new[]{-1,1}) B(root.transform,"Bumper overrider",new Vector3(side*.63f,.53f,end*2.16f),new Vector3(.085f,.32f,.10f),chrome);
                }
                foreach(int end in new[]{-1,1})
                {
                    for(int x=-5;x<=5;x++) B(root.transform,"Chrome bumper",new Vector3(x*.18f,.48f,end*(2.18f-.025f*Mathf.Abs(x))),new Vector3(.18f,.09f,.10f),chrome);
                    B(root.transform,"Number plate",new Vector3(0,.65f,end*2.12f),new Vector3(.36f,.14f,.03f),lamp);
                }
                for(int x=-3;x<=3;x++) B(root.transform,"Rear cooling slot",new Vector3(x*.13f,.98f,-1.52f),new Vector3(.065f,.025f,.22f),rubber);
                B(root.transform,"Bonnet chrome strip",new Vector3(0,.95f,1.02f),new Vector3(.025f,.025f,.20f),chrome);
                VoxelCivilianDrivelineBuilder.AddStructure(root);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                var tuning=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>(TuningPath);
                if(tuning==null)
                {
                    tuning=Object.Instantiate(AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>("Assets/Resources/EnemyVehicles/TrafficCarTuning.asset"));
                    tuning.name="CivilianBeetleTuning";tuning.displayName="Civilian Beetle";
                    tuning.collisionHalfWidth=1.15f;tuning.collisionHalfLength=2.25f;
                    AssetDatabase.CreateAsset(tuning,TuningPath);
                }
                tuning.modelPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);EditorUtility.SetDirty(tuning);
                var hatch=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>("Assets/Resources/EnemyVehicles/TrafficCarTuning.asset");
                var van=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>("Assets/Resources/EnemyVehicles/CivilianVanTuning.asset");
                foreach(string guid in AssetDatabase.FindAssets("t:VoxelObstacleCarTuning").Concat(AssetDatabase.FindAssets("t:VoxelTrackDefinition")).Distinct())
                    foreach(var traffic in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<VoxelObstacleCarTuning>())
                    {
                        if(traffic.civilianVehiclePool!=null&&traffic.civilianVehiclePool.Contains(tuning))continue;
                        var existing=traffic.civilianVehiclePool;
                        traffic.civilianVehiclePool=(existing!=null&&existing.Length>0?existing:new[]{traffic.trafficCarEnemyTuning??hatch,traffic.semiTrailerEnemyTuning??van}).Concat(new[]{tuning}).Where(v=>v!=null).Distinct().ToArray();
                        EditorUtility.SetDirty(traffic);
                    }
                AssetDatabase.SaveAssets(); Debug.Log("Beetle built: "+root.GetComponentsInChildren<MeshRenderer>().Length+" pieces");
            }
            finally{Object.DestroyImmediate(root);}
            Render();
        }
        private static void B(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        [MenuItem("Tools/Voxel Racer/Render Civilian Beetle")]
        public static void Render()
        {
            Directory.CreateDirectory("Temp/Beetle");
            string[] names={"Front","Rear","Left","Right","FrontStraight","RearStraight","Top"};
            Vector3[] views={new Vector3(5,2.8f,6.8f),new Vector3(5,2.8f,-6.8f),new Vector3(-8,.85f,0),new Vector3(8,.85f,0),new Vector3(0,.85f,8),new Vector3(0,.85f,-8),new Vector3(0,8,.001f)};
            for(int view=0;view<views.Length;view++)
            {
                var preview=new PreviewRenderUtility();
                try
                {
                    preview.AddSingleGO(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
                    preview.camera.transform.position=views[view];preview.camera.transform.LookAt(new Vector3(0,.8f,0));preview.camera.fieldOfView=34;
                    preview.camera.orthographic=view>=2;preview.camera.orthographicSize=view==6?2.6f:1.65f;
                    preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.17f,.20f,.23f);
                    preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(40,25,0);preview.lights[1].intensity=1.3f;preview.lights[1].transform.rotation=Quaternion.Euler(30,210,0);preview.ambientColor=new Color(.45f,.45f,.45f);
                    preview.BeginStaticPreview(new Rect(0,0,1100,700));preview.Render(true);var image=preview.EndStaticPreview();File.WriteAllBytes("Temp/Beetle/"+names[view]+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
                finally{preview.Cleanup();}
            }
        }
    }
}
