using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelStarWheelSpikeBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Upgrades/StarWheelSpike.prefab";
        public const string TuningPath = "Assets/Resources/Upgrades/StarWheelSpikeTuning.asset";
        public const string MeshPath = "Assets/Prefabs/Upgrades/StarWheelSpikeMesh.asset";

        [MenuItem("Tools/Voxel Racer/Build Star Wheel Spike Upgrade")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Build star wheel spikes in Edit Mode.");
            var basic = VoxelWheelSpikeTuning.Load();
            if (basic == null || basic.spikePrefab == null) throw new System.InvalidOperationException("Missing original spike materials/hub.");
            var hub = basic.spikePrefab.transform.Find("Armoured Hub");
            var steel = basic.spikePrefab.transform.Find("Long Piercing Spike").GetComponent<MeshRenderer>().sharedMaterial;
            var root = new GameObject("Star Wheel Spike");
            var spikeMesh = MakeSpikes();
            var hubMesh = new Mesh();
            var combined = new Mesh();
            try
            {
                root.AddComponent<VoxelIndestructiblePart>();
                hubMesh.CombineMeshes(new[]{new CombineInstance {
                    mesh=hub.GetComponent<MeshFilter>().sharedMesh,
                    transform=Matrix4x4.TRS(hub.localPosition,hub.localRotation,hub.localScale) }},true,true);
                combined.CombineMeshes(new[]{new CombineInstance {mesh=hubMesh,transform=Matrix4x4.identity},
                    new CombineInstance {mesh=spikeMesh,transform=Matrix4x4.identity}},false,true);
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                if (saved == null) { saved = new Mesh {name="Star Wheel Spike Mesh"}; AssetDatabase.CreateAsset(saved,MeshPath); }
                saved.Clear(); saved.vertices=combined.vertices; saved.normals=combined.normals;
                saved.subMeshCount=combined.subMeshCount;
                for(int i=0;i<combined.subMeshCount;i++) saved.SetTriangles(combined.GetTriangles(i),i);
                saved.RecalculateBounds(); EditorUtility.SetDirty(saved);
                root.AddComponent<MeshFilter>().sharedMesh=saved;
                root.AddComponent<MeshRenderer>().sharedMaterials=new[]{hub.GetComponent<MeshRenderer>().sharedMaterial,steel};
                var prefab = PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                var tuning = AssetDatabase.LoadAssetAtPath<VoxelWheelSpikeTuning>(TuningPath);
                if(tuning==null)
                {
                    tuning=ScriptableObject.CreateInstance<VoxelWheelSpikeTuning>();
                    tuning.displayName="STAR WHEEL SPIKES"; tuning.purchasePrice=400;
                    tuning.sideRamDamageBonusPercent=50; tuning.upgradeLevel=2;
                    AssetDatabase.CreateAsset(tuning,TuningPath);
                }
                tuning.spikePrefab=prefab; EditorUtility.SetDirty(tuning);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(spikeMesh);
                Object.DestroyImmediate(hubMesh); Object.DestroyImmediate(combined);
            }
        }

        private static Mesh MakeSpikes()
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            // +X faces out of the wheel. Five tips lie on a pentagonal ring in the hub's YZ plane.
            AddSpike(Vector3.zero,.042f,.14f,0,.19f);
            for(int i=0;i<5;i++)
            {
                float angle=i*Mathf.PI*2/5;
                AddSpike(new Vector3(0,Mathf.Cos(angle)*.15f,Mathf.Sin(angle)*.15f),.064f,.34f,angle);
            }
            var mesh=new Mesh {name="Six Star Spikes"};
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;

            void AddSpike(Vector3 centre,float halfWidth,float length,float angle,float baseX=.10f)
            {
                centre.x=baseX;
                var rotation=Quaternion.AngleAxis(angle*Mathf.Rad2Deg,Vector3.right);
                var corners=new[]{new Vector3(0,-halfWidth,-halfWidth),new Vector3(0,halfWidth,-halfWidth),
                    new Vector3(0,halfWidth,halfWidth),new Vector3(0,-halfWidth,halfWidth)};
                for(int j=0;j<4;j++) Triangle(centre+rotation*corners[j],centre+rotation*corners[(j+1)%4],centre+Vector3.right*length);
                Triangle(centre+rotation*corners[0],centre+rotation*corners[2],centre+rotation*corners[1]);
                Triangle(centre+rotation*corners[0],centre+rotation*corners[3],centre+rotation*corners[2]);
            }
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {
                int start=vertices.Count; vertices.Add(a);vertices.Add(b);vertices.Add(c);
                triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
            }
        }
    }
}
