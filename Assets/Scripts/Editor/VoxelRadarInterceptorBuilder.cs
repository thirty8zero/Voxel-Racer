using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelRadarInterceptorBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Cars/RadarInterceptor.prefab";
        public const string TuningPath = "Assets/Resources/EnemyVehicles/RadarInterceptorTuning.asset";

        [MenuItem("Tools/Voxel Racer/Build Radar Interceptor")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>("Assets/Resources/EnemyVehicles/BlackInterceptorTuning.asset");
            var root = Object.Instantiate(source.modelPrefab);
            try
            {
                root.name = "Radar Interceptor";
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/InterceptorChrome.mat");
                if (material == null)
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                        if (renderer.sharedMaterial != null && renderer.sharedMaterial.name == "InterceptorChrome") { material = renderer.sharedMaterial; break; }
                var mount = new GameObject("Radar mount").transform;
                mount.SetParent(root.transform, false);
                mount.localPosition = new Vector3(0, 1.37f, -.25f);
                Block(mount, "Pedestal", new Vector3(0,.12f,0), new Vector3(.18f,.24f,.18f), material);
                var spin = new GameObject("Spinning radar dish").transform;
                spin.SetParent(mount, false);
                spin.localPosition = new Vector3(0,.33f,0);
                spin.gameObject.AddComponent<VoxelRadarDish>();
                // A small stepped concave reflector, tilted upwards, built in the vehicle's voxel style.
                var bowl = new GameObject("Dish reflector").transform;
                bowl.SetParent(spin, false);
                bowl.localRotation = Quaternion.Euler(-25,0,0);
                for (int x=-3;x<=3;x++) for(int y=-2;y<=2;y++)
                {
                    if (Mathf.Abs(x)==3 && Mathf.Abs(y)==2) continue;
                    float depth=(x*x+y*y)*.014f;
                    Block(bowl,"Reflector voxel",new Vector3(x*.11f,y*.11f,depth),new Vector3(.11f,.11f,.055f),material);
                }
                Block(bowl,"Receiver arm",new Vector3(0,0,.17f),new Vector3(.045f,.045f,.34f),material);
                Block(bowl,"Receiver",new Vector3(0,0,.35f),new Vector3(.10f,.10f,.10f),material);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var tuning = AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>(TuningPath);
                if(tuning==null)
                {
                    tuning = Object.Instantiate(source);
                    tuning.name = "RadarInterceptorTuning";
                    AssetDatabase.CreateAsset(tuning,TuningPath);
                }
                tuning.displayName="Radar Interceptor";
                tuning.modelPrefab=prefab;
                tuning.vehicleHealth=5;
                tuning.voxelHealth=.25f;
                tuning.mineLayer=null;
                EditorUtility.SetDirty(tuning);
                AssetDatabase.SaveAssets();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Block(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var block=GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name=name;block.transform.SetParent(parent,false);
            block.transform.localPosition=position;block.transform.localScale=scale;
            Object.DestroyImmediate(block.GetComponent<Collider>());
            block.GetComponent<MeshRenderer>().sharedMaterial=material;
        }

        [MenuItem("Tools/Voxel Racer/Render Radar Interceptor")]
        public static void Render()
        {
            var preview=new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
                preview.camera.transform.position=new Vector3(6.1f,3.7f,7.3f);
                preview.camera.transform.LookAt(new Vector3(0,1,0));
                preview.camera.fieldOfView=34;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.17f,.20f,.23f);
                preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(40,25,0);
                preview.lights[1].intensity=1.3f;preview.lights[1].transform.rotation=Quaternion.Euler(30,210,0);
                preview.ambientColor=new Color(.45f,.45f,.45f);
                preview.BeginStaticPreview(new Rect(0,0,1280,800));preview.Render(true);
                var image=preview.EndStaticPreview();
                File.WriteAllBytes("Temp/RadarInterceptor.png",image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
        }
    }
}
