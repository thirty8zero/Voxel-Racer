using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelPsychoBugBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Cars/PsychoBugEnemy.prefab";
        public const string TuningPath = "Assets/Resources/EnemyVehicles/PsychoBugEnemyTuning.asset";
        public const string BehaviourPath = "Assets/Resources/EnemyVehicles/PsychoBugBehaviour.asset";
        private const string ArtFolder = "Assets/Resources/EnemyVehicles/PsychoBugArt";

        [MenuItem("Tools/Voxel Racer/Build Psycho Bug Enemy")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(VoxelCivilianBeetleBuilder.PrefabPath);
            if (source == null) throw new InvalidOperationException("Civilian Beetle prefab is required.");
            Directory.CreateDirectory(ArtFolder);
            var paint = Material("BlackPaint", new Color(.026f,.031f,.036f), .2f, .36f);
            var steel = Material("SpikeSteel", new Color(.42f,.46f,.50f), .72f, .42f);
            var rim = Material("GunmetalRim", new Color(.13f,.15f,.17f), .6f, .35f);
            var rubber = Material("RoughRubber", new Color(.012f,.014f,.018f), 0, .12f);
            var glass = Material("SmokedGlass", new Color(.045f,.072f,.085f), .3f, .65f);
            var red = Material("HubRed", new Color(.48f,.025f,.025f), .35f, .4f);
            Mesh spike = SaveMesh("Spike", Pyramid());
            Mesh tyre = SaveMesh("OctagonalTyre", Drum(.395f,.16f));
            Mesh hub = SaveMesh("OctagonalHub", Drum(.27f,.025f));
            var root = Object.Instantiate(source); root.name = "Psycho Bug Enemy";
            try
            {
                var paintMarker = root.GetComponent<VoxelTrafficPaint>();
                var originalPaint = paintMarker != null ? paintMarker.bodyMaterial : null;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    if (renderer.sharedMaterial == originalPaint) renderer.sharedMaterial = paint;
                    else if (renderer.sharedMaterial != null && renderer.sharedMaterial.name.Contains("Glass")) renderer.sharedMaterial = glass;
                    else if (renderer.sharedMaterial != null && renderer.sharedMaterial.name.Contains("Rim")) renderer.sharedMaterial = rim;
                }
                if (paintMarker != null) Object.DestroyImmediate(paintMarker);
                foreach (var wheel in root.GetComponentsInChildren<Transform>().Where(t => t.name == "Obstacle Voxel Wheel").ToArray())
                {
                    int side = wheel.localPosition.x < 0 ? -1 : 1;
                    foreach (Transform child in wheel.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                    MeshPart(wheel, "Heavy octagonal tyre", Vector3.zero, Vector3.one, Quaternion.identity, tyre, rubber);
                    MeshPart(wheel, "Gunmetal wheel hub", new Vector3(side*.18f,0,0), Vector3.one, Quaternion.identity, hub, rim);
                    for (int tread = 0; tread < 12; tread++)
                    {
                        float angle = tread * 30f * Mathf.Deg2Rad;
                        var block = Block(wheel, "Raised tyre tread", new Vector3(0,Mathf.Cos(angle)*.402f,Mathf.Sin(angle)*.402f), new Vector3(.34f,.055f,.14f), rubber);
                        block.localRotation = Quaternion.Euler(tread*30f,0,0);
                    }
                    for(int spoke=0;spoke<6;spoke++)
                    {
                        var block = Block(wheel,"Steel wheel spoke",new Vector3(side*.212f,0,0),new Vector3(.045f,.065f,.45f),steel);
                        block.localRotation=Quaternion.Euler(spoke*30,0,0);
                    }
                    Block(wheel,"Red spike collar",new Vector3(side*.25f,0,0),new Vector3(.085f,.20f,.20f),red);
                    MeshPart(wheel,"Wheel spike",new Vector3(side*.29f,0,0),new Vector3(.19f,.19f,.32f),
                        Quaternion.LookRotation(Vector3.right*side),spike,steel);
                }
                var doorBodies = root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial==paint &&
                    (r.name=="Body voxel" || r.name=="Door shoulder voxel")).ToArray();
                foreach(int side in new[]{-1,1})
                foreach(float height in new[]{.64f,.84f})
                foreach(float depth in new[]{-.55f,-.18f,.19f})
                {
                    Vector3 mount=new(side*.85f,height,depth);
                    var anchor=doorBodies.Where(r=>Mathf.Sign(r.transform.position.x)==side)
                        .OrderBy(r=>(r.transform.position-mount).sqrMagnitude).First();
                    var plate=Block(root.transform,"Door spike mounting plate",mount,new Vector3(.06f,.17f,.22f),rim);
                    var point=MeshPart(root.transform,"Door spike",mount+Vector3.right*side*.035f,new Vector3(.14f,.14f,.29f),
                        Quaternion.LookRotation(Vector3.right*side),spike,steel);
                    // Both pieces disappear with their source door voxel; no floating invulnerable spikes.
                    plate.SetParent(anchor.transform,true);point.SetParent(anchor.transform,true);
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }

            var behaviour=AssetDatabase.LoadAssetAtPath<VoxelPsychoBugTuning>(BehaviourPath);
            if(behaviour==null) { behaviour=ScriptableObject.CreateInstance<VoxelPsychoBugTuning>();AssetDatabase.CreateAsset(behaviour,BehaviourPath); }
            var tuning=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>(TuningPath);
            if(tuning==null)
            {
                tuning=Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning"));
                tuning.name="PsychoBugEnemyTuning";tuning.displayName="Psycho Bug Enemy";
                tuning.mineLayer=null;tuning.vehicleHealth=90;tuning.voxelHealth=1.5f;tuning.destructionScore=40;
                tuning.collisionHalfWidth=1.5f;tuning.collisionHalfLength=2.3f;
                tuning.playerDamageVoxelsMin=4;tuning.playerDamageVoxelsMax=6;
                tuning.minimumSpawnSpeedMultiplier=tuning.maximumSpawnSpeedMultiplier=.98f;
                tuning.playerSideRamBounceDistance=.55f;tuning.sideRamEnemyLaneShiftDistance=.35f;
                AssetDatabase.CreateAsset(tuning,TuningPath);
            }
            tuning.modelPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);tuning.psychoBug=behaviour;EditorUtility.SetDirty(tuning);
            foreach(string guid in AssetDatabase.FindAssets("t:VoxelObstacleCarTuning").Concat(AssetDatabase.FindAssets("t:VoxelTrackDefinition")).Distinct())
            foreach(var traffic in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<VoxelObstacleCarTuning>())
            {
                var pool=traffic.enemyVehiclePool??Array.Empty<VoxelEnemyVehicleTuning>();
                if(pool.Contains(tuning)) continue;
                traffic.enemyVehiclePool=pool.Concat(new[]{tuning}).ToArray();EditorUtility.SetDirty(traffic);
            }
            AssetDatabase.SaveAssets(); Debug.Log("Psycho Bug Enemy built and registered; civilian Beetle and existing tuning preserved.");
            Render();
        }
        private static Material Material(string name,Color color,float metallic,float smoothness)
        {
            string path=ArtFolder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null)return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Psycho Bug "+name,color=color};
            material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smoothness);AssetDatabase.CreateAsset(material,path);return material;
        }
        private static Mesh SaveMesh(string name,Mesh generated)
        {
            string path=ArtFolder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){generated.name=name;AssetDatabase.CreateAsset(generated,path);return generated;}
            mesh.Clear();mesh.vertices=generated.vertices;mesh.triangles=generated.triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);Object.DestroyImmediate(generated);return mesh;
        }
        private static Mesh Pyramid()
        {
            var mesh=new Mesh();var corners=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
            var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
            for(int face=0;face<4;face++)
            {int first=vertices.Count;vertices.Add(corners[face]);vertices.Add(corners[(face+1)%4]);vertices.Add(Vector3.forward);triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);}
            int start=vertices.Count;vertices.AddRange(corners);triangles.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static Mesh Drum(float radius,float halfWidth)
        {
            var mesh=new Mesh();var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
            for(int side=-1;side<=1;side+=2)
            {
                int start=vertices.Count;vertices.Add(new Vector3(side*halfWidth,0,0));
                for(int i=0;i<8;i++){float a=i*Mathf.PI/4;vertices.Add(new Vector3(side*halfWidth,Mathf.Sin(a)*radius,Mathf.Cos(a)*radius));}
                for(int i=0;i<8;i++){triangles.Add(start);triangles.Add(start+1+(side>0?i:(i+1)%8));triangles.Add(start+1+(side>0?(i+1)%8:i));}
            }
            for(int i=0;i<8;i++)
            {
                int next=(i+1)%8,first=vertices.Count;vertices.Add(vertices[1+i]);vertices.Add(vertices[1+next]);vertices.Add(vertices[10+next]);vertices.Add(vertices[10+i]);
                triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
            }
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static Transform Block(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
        private static Transform MeshPart(Transform parent,string name,Vector3 position,Vector3 size,Quaternion rotation,Mesh mesh,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=size;go.transform.localRotation=rotation;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
        [MenuItem("Tools/Voxel Racer/Render Psycho Bug Enemy")]
        public static void Render()
        {
            Directory.CreateDirectory("Temp/PsychoBug");
            var views=new[]{new Vector3(5,2.8f,6.8f),new Vector3(5,2.8f,-6.8f),new Vector3(-8,1.1f,0),new Vector3(8,1.1f,0),new Vector3(0,8,.001f)};
            var names=new[]{"Front","Rear","Left","Right","Top"};
            for(int i=0;i<views.Length;i++)
            {
                var preview=new PreviewRenderUtility();
                try
                {
                    preview.AddSingleGO(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
                    preview.camera.transform.position=views[i];preview.camera.transform.LookAt(new Vector3(0,.8f,0));preview.camera.fieldOfView=34;
                    preview.camera.orthographic=i>=2;preview.camera.orthographicSize=i==4?2.6f:1.7f;
                    preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.19f,.22f,.25f);
                    preview.lights[0].intensity=2;preview.lights[0].transform.rotation=Quaternion.Euler(40,25,0);
                    preview.lights[1].intensity=1.5f;preview.lights[1].transform.rotation=Quaternion.Euler(30,210,0);preview.ambientColor=new Color(.5f,.5f,.5f);
                    preview.BeginStaticPreview(new Rect(0,0,1100,700));preview.Render(true);var image=preview.EndStaticPreview();
                    File.WriteAllBytes("Temp/PsychoBug/"+names[i]+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
                finally{preview.Cleanup();}
            }
        }
    }
}
