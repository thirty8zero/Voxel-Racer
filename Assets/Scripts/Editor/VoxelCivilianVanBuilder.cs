using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelCivilianVanBuilder
    {
        public const string PrefabPath="Assets/Prefabs/Cars/CivilianTransitVan.prefab";
        public const string TuningPath="Assets/Resources/EnemyVehicles/CivilianVanTuning.asset";
        private static Material paint, glass, black, steel, lamp, amber, red;

        [MenuItem("Tools/Voxel Racer/Build Civilian Transit Van")]
        public static void Build()
        {
            paint=Mat("TransitPaint",new Color(.06f,.48f,.65f));
            glass=Mat("TransitGlass",new Color(.085f,.15f,.19f));
            black=Mat("TransitTrim",new Color(.025f,.03f,.035f));
            steel=Mat("TransitSteel",new Color(.48f,.53f,.55f));
            lamp=Mat("TransitLamp",new Color(.87f,.88f,.75f));
            amber=Mat("TransitAmber",new Color(1f,.36f,.02f));
            red=Mat("TransitRed",new Color(.65f,.025f,.02f));
            var root=new GameObject("Civilian Transit Van");
            try
            {
                root.AddComponent<VoxelTrafficPaint>().bodyMaterial=paint;
                // Hollow shell: use short panel strips rather than fill the van with hidden cubes.
                // Coarse, grid-aligned strips keep the voxel silhouette and destructible surface affordable.
                foreach(int side in new[]{-1,1})
                {
                    for(int row=0;row<7;row++) for(int z=0;z<18;z++)
                    {
                        float y=.5f+row*.24f, depth=-2.21f+z*.26f;
                        if(row<2 && (Mathf.Abs(depth-1.43f)<.54f || Mathf.Abs(depth+1.43f)<.54f)) continue;
                        if(row>=3 && depth>1.32f) continue;
                        bool cabWindow=row>=4 && depth>.2f;
                        float x=side*(row>=4?1.005f:1.04f);
                        B(root.transform,cabWindow?"Cab side glass":"Van body voxel",new Vector3(x,y,depth),new Vector3(.13f,.24f,.26f),cabWindow?glass:paint);
                    }
                    // Thin contrasting belt line and sliding-door seam.
                    for(int z=0;z<9;z++)
                        B(root.transform,"Side belt trim",new Vector3(side*1.115f,1.16f,-2.08f+z*.52f),new Vector3(.02f,.035f,.52f),steel);
                    B(root.transform,"Sliding door seam",new Vector3(side*1.116f,1.02f,-.35f),new Vector3(.015f,.92f,.018f),black);
                    B(root.transform,"Cab door seam",new Vector3(side*1.116f,1.02f,.27f),new Vector3(.015f,.92f,.018f),black);
                    B(root.transform,"Door handle",new Vector3(side*1.13f,1.27f,.34f),new Vector3(.06f,.045f,.15f),black);
                    B(root.transform,"Mirror arm",new Vector3(side*1.2f,1.48f,1.16f),new Vector3(.25f,.05f,.05f),black);
                    B(root.transform,"Tall mirror",new Vector3(side*1.32f,1.62f,1.16f),new Vector3(.13f,.29f,.15f),black);
                    foreach(float depth in new[]{-1.43f,1.43f}) Wheel(root.transform,side,depth);
                }
                // Roof ends in a stepped windscreen; flat front bonnet is intentionally short.
                for(int x=0;x<8;x++) for(int z=0;z<7;z++)
                    B(root.transform,"Roof voxel",new Vector3((x-3.5f)*.26f,2.105f,-2.08f+z*.52f),new Vector3(.26f,.09f,.52f),paint);
                for(int row=0;row<3;row++) for(int x=0;x<8;x++)
                    B(root.transform,"Windscreen voxel",new Vector3((x-3.5f)*.26f,1.4f+row*.26f,1.48f-row*.13f),
                        new Vector3(.26f,.26f,.20f),x==0||x==7?paint:glass);
                for(int x=0;x<8;x++) for(int z=0;z<3;z++)
                    B(root.transform,"Bonnet voxel",new Vector3((x-3.5f)*.26f,1.18f,1.56f+z*.26f),new Vector3(.26f,.16f,.26f),paint);
                for(int x=0;x<8;x++) for(int row=0;row<3;row++)
                    B(root.transform,"Front body voxel",new Vector3((x-3.5f)*.26f,.57f+row*.24f,2.25f),new Vector3(.26f,.24f,.14f),paint);
                for(int x=0;x<8;x++) for(int row=0;row<7;row++)
                {
                    bool window=row>=4 && row<=5 && x!=0 && x!=7;
                    B(root.transform,window?"Rear door glass":"Rear door voxel",new Vector3((x-3.5f)*.26f,.5f+row*.24f,-2.3f),new Vector3(.26f,.24f,.12f),window?glass:paint);
                }
                B(root.transform,"Rear door centre seam",new Vector3(0,1.28f,-2.371f),new Vector3(.022f,1.6f,.015f),black);
                B(root.transform,"Rear door handle",new Vector3(.16f,1.21f,-2.39f),new Vector3(.21f,.05f,.055f),black);
                foreach(float x in new[]{-.98f,.98f})
                {
                    B(root.transform,"Rear lamp housing",new Vector3(x,.89f,-2.385f),new Vector3(.18f,.53f,.08f),black);
                    for(int row=0;row<3;row++) B(root.transform,"Rear lamp",new Vector3(x,.73f+row*.16f,-2.435f),new Vector3(.12f,.14f,.035f),row==1?amber:red);
                }
                foreach(int end in new[]{-1,1})
                {
                    for(int x=0;x<4;x++) B(root.transform,"Bumper segment",new Vector3((x-1.5f)*.55f,.42f,end*2.4f),new Vector3(.55f,.16f,.16f),black);
                    B(root.transform,"Number plate",new Vector3(0,.57f,end*2.411f),new Vector3(.5f,.16f,.025f),end<0?amber:lamp);
                }
                B(root.transform,"Grille recess",new Vector3(0,.94f,2.34f),new Vector3(1.25f,.42f,.05f),black);
                for(int row=0;row<5;row++) B(root.transform,"Grille slat",new Vector3(0,.78f+row*.08f,2.377f),new Vector3(1.24f,.02f,.025f),steel);
                foreach(int side in new[]{-1,1})
                {
                    B(root.transform,"Headlamp housing",new Vector3(side*.8f,.96f,2.34f),new Vector3(.35f,.4f,.06f),black);
                    for(int row=-1;row<=1;row++) B(root.transform,"Stepped round headlamp",new Vector3(side*.8f,.96f+row*.09f,2.38f),new Vector3(row==0?.27f:.19f,.09f,.035f),lamp);
                    B(root.transform,"Front indicator",new Vector3(side*1.015f,.96f,2.36f),new Vector3(.12f,.33f,.06f),amber);
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                Debug.Log("Transit van saved: "+root.GetComponentsInChildren<MeshRenderer>().Length+" destructible pieces; no per-piece colliders.");
            }
            finally { Object.DestroyImmediate(root); }
            var tuning=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>(TuningPath);
            if(tuning==null)
            {
                tuning=Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning"));
                AssetDatabase.CreateAsset(tuning,TuningPath);
            }
            tuning.displayName="Civilian Transit Van"; tuning.modelPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            tuning.collisionHalfWidth=1.4f; tuning.collisionHalfLength=2.65f;
            EditorUtility.SetDirty(tuning); AssetDatabase.SaveAssets();
            Render();
        }

        public static void ReplaceTruckSpawns()
        {
            var van=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>(TuningPath);
            var tunings=new System.Collections.Generic.HashSet<VoxelObstacleCarTuning>();
            foreach(string guid in AssetDatabase.FindAssets("t:VoxelTrackDefinition"))
            {
                var track=AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if(track.obstacleCarTuning!=null) tunings.Add(track.obstacleCarTuning);
            }
            foreach(string guid in AssetDatabase.FindAssets("t:VoxelObstacleCarTuning"))
                foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if(asset is VoxelObstacleCarTuning traffic) tunings.Add(traffic);
            foreach(var traffic in tunings)
            {
                        Undo.RecordObject(traffic,"Replace truck with civilian van");
                        traffic.semiTrailerEnemyTuning=van; traffic.semiImpactVoxelDamageSurfaceOffset=2.2f;
                        if(traffic.semiTrailerSpawnChance<=0) traffic.semiTrailerSpawnChance=.25f;
                        EditorUtility.SetDirty(traffic);
            }
            AssetDatabase.SaveAssets();
        }
        private static void Wheel(Transform parent,int side,float z)
        {
            var wheel=new GameObject("Obstacle Voxel Wheel").transform;
            wheel.SetParent(parent,false); wheel.localPosition=new Vector3(side*1.045f,.46f,z);
            int[] widths={3,5,7,7,7,5,3};
            for(int row=0;row<7;row++) B(wheel,"Tyre row",new Vector3(0,(row-3)*.13f,0),new Vector3(.29f,.13f,widths[row]*.13f),black);
            for(int row=-2;row<=2;row++) B(wheel,"Steel wheel row",new Vector3(side*.16f,row*.105f,0),new Vector3(.035f,.105f,Mathf.Abs(row)==2?.31f:.52f),steel);
            B(wheel,"Black hub",new Vector3(side*.19f,0,0),new Vector3(.04f,.19f,.19f),black);
        }
        private static Material Mat(string name,Color colour)
        {
            string path="Assets/Resources/CarMaterials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) {mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",colour);mat.SetFloat("_Smoothness",.18f);EditorUtility.SetDirty(mat);return mat;
        }
        private static void B(Transform parent,string name,Vector3 position,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
        [MenuItem("Tools/Voxel Racer/Render Civilian Transit Van")]
        public static void Render()
        {
            for(int side=0;side<2;side++)
            {
                var preview=new PreviewRenderUtility();
                try
                {
                    preview.AddSingleGO(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
                    preview.camera.transform.position=new Vector3(6,3.4f,side==0?7.8f:-7.8f);preview.camera.transform.LookAt(new Vector3(0,1,0));
                    preview.camera.fieldOfView=34;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.17f,.2f,.23f);
                    preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(40,side==0?25:155,0);
                    preview.lights[1].intensity=1.3f;preview.lights[1].transform.rotation=Quaternion.Euler(30,side==0?210:20,0);
                    preview.ambientColor=new Color(.45f,.45f,.45f);
                    preview.BeginStaticPreview(new Rect(0,0,1280,800));preview.Render(true);
                    var image=preview.EndStaticPreview();File.WriteAllBytes("Temp/CivilianTransitVan"+side+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
                finally {preview.Cleanup();}
            }
        }
    }
}
