using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelV6EngineBuilder
    {
        public const string PrefabPath="Assets/Prefabs/Upgrades/V6Engine.prefab";
        [MenuItem("Tools/Voxel Racer/Build V6 Engine Upgrade")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory("Assets/Prefabs/Upgrades");
            var car=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cars/SpyCar2PlayerCar.prefab");
            var standard=car.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Protected Engine");
            var engine=Object.Instantiate(standard.gameObject);engine.name="V6 Engine";
            try
            {
                if(engine.GetComponent<VoxelIndestructiblePart>()==null) engine.AddComponent<VoxelIndestructiblePart>();
                foreach(Transform child in engine.transform.Cast<Transform>().ToArray())
                    if(child.name.StartsWith("Valve Cover") || child.name.StartsWith("Exhaust Header") ||
                        child.name.StartsWith("Intake Runner") || child.name=="Cylinder Head" || child.name=="Oil Filler Cap")
                        Object.DestroyImmediate(child.gameObject);
                var cover=Mat("V6RedCovers",new Color(.5f,.045f,.025f));
                var silver=Mat("V6MachinedMetal",new Color(.53f,.57f,.6f));
                var dark=Mat("V6DarkMetal",new Color(.10f,.12f,.14f));
                foreach(string name in new[]{"Engine Block","Oil Sump"})
                {
                    var block=engine.transform.Find(name); var size=block.localScale;size.z*=.8f;block.localScale=size;
                }
                var left=engine.transform.Find("Engine Exhaust Assembly");left.name="Left Exhaust Assembly";
                var collector=left.Find("Header Collector");
                collector.localPosition=new Vector3(-.35f,.63f,1.12f);
                collector.localScale=new Vector3(.09f,.32f,.09f);
                var right=Object.Instantiate(left.gameObject,engine.transform,false).transform;right.name="Right Exhaust Assembly";
                foreach(var t in right.GetComponentsInChildren<Transform>(true))
                {
                    var p=t.localPosition;p.x=-p.x;t.localPosition=p;
                    var q=t.localRotation;t.localRotation=new Quaternion(q.x,-q.y,-q.z,q.w);
                }
                foreach(int side in new[]{-1,1})
                {
                    var bank=B(engine.transform,(side<0?"Left":"Right")+" cylinder bank",new Vector3(side*.18f,.755f,1.12f),new Vector3(.25f,.11f,.82f),dark);
                    bank.localRotation=Quaternion.Euler(0,0,-side*22f);
                    for(int i=0;i<3;i++)
                    {
                        float z=.86f+i*.26f;
                        var cap=B(engine.transform,(side<0?"Left":"Right")+" cylinder cover "+(i+1),new Vector3(side*.19f,.825f,z),new Vector3(.23f,.075f,.22f),cover);
                        cap.localRotation=Quaternion.Euler(0,0,-side*22f);
                        B(engine.transform,"Polished cover rib",new Vector3(side*.19f,.874f,z),new Vector3(.17f,.018f,.035f),silver);
                        // Each bank feeds its own three-input collector.
                        Pipe(side<0?left:right,"Header input "+(i+1),new Vector3(side*.265f,.73f,z),new Vector3(side*.35f,.63f,z),.065f,
                            collector.GetComponent<MeshRenderer>().sharedMaterial);
                    }
                }
                B(engine.transform,"V6 intake plenum",new Vector3(0,.84f,1.12f),new Vector3(.11f,.11f,.59f),silver);
                B(engine.transform,"Intake front neck",new Vector3(0,.8f,1.51f),new Vector3(.14f,.1f,.2f),dark);
                B(engine.transform,"Oil filler",new Vector3(.19f,.9f,1.37f),new Vector3(.07f,.04f,.07f),dark);
                PrefabUtility.SaveAsPrefabAsset(engine,PrefabPath);
            }
            finally {Object.DestroyImmediate(engine);}
            const string path="Assets/Resources/Upgrades/V6EngineUpgradeTuning.asset";
            var tuning=AssetDatabase.LoadAssetAtPath<VoxelEngineUpgradeTuning>(path);
            if(tuning==null) {tuning=ScriptableObject.CreateInstance<VoxelEngineUpgradeTuning>();AssetDatabase.CreateAsset(tuning,path);}
            tuning.enginePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);tuning.compatibleCarPrefab=car;
            EditorUtility.SetDirty(tuning);AssetDatabase.SaveAssets();
        }
        private static Material Mat(string name,Color colour)
        {
            string path="Assets/Resources/CarMaterials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) {mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",colour);mat.SetFloat("_Metallic",.45f);mat.SetFloat("_Smoothness",.4f);EditorUtility.SetDirty(mat);return mat;
        }
        private static Transform B(Transform parent,string name,Vector3 p,Vector3 size,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go.transform;
        }
        private static void Pipe(Transform parent,string name,Vector3 a,Vector3 b,float diameter,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=(a+b)*.5f;
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,a-b);go.transform.localScale=new Vector3(diameter,Vector3.Distance(a,b)*.5f,diameter);
            go.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
    }
}
