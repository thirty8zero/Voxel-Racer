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
                    transform=Matrix4x4.TRS(hub.localPosition,hub.localRotation,hub.localScale) },
                    new CombineInstance {mesh=spikeMesh,subMeshIndex=0,transform=Matrix4x4.identity}},true,true);
                combined.CombineMeshes(new[]{new CombineInstance {mesh=hubMesh,transform=Matrix4x4.identity},
                    new CombineInstance {mesh=spikeMesh,subMeshIndex=1,transform=Matrix4x4.identity}},false,true);
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                if (saved == null) { saved = new Mesh {name="Star Wheel Spike Mesh"}; AssetDatabase.CreateAsset(saved,MeshPath); }
                // Halve depth along the outward axis while keeping the wheel mounting
                // face fixed. The YZ profile, spike widths and ring spacing stay intact.
                var vertices=combined.vertices;
                float mountingFace=combined.bounds.min.x;
                for(int i=0;i<vertices.Length;i++)
                    vertices[i].x=mountingFace+(vertices[i].x-mountingFace)*.5f;
                saved.Clear(); saved.vertices=vertices;
                saved.subMeshCount=combined.subMeshCount;
                for(int i=0;i<combined.subMeshCount;i++) saved.SetTriangles(combined.GetTriangles(i),i);
                saved.RecalculateNormals(); saved.RecalculateBounds(); EditorUtility.SetDirty(saved);
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
            var vertices=new List<Vector3>();
            var dark=new List<int>(); var steel=new List<int>();
            // +X faces out of the wheel. Five tips lie on a pentagonal ring in the hub's YZ plane.
            AddSpike(Vector3.zero,.042f,.19f,.38f,0);
            for(int i=0;i<5;i++)
            {
                float angle=i*Mathf.PI*2/5;
                var outerCentre=new Vector3(0,Mathf.Cos(angle)*.21f,Mathf.Sin(angle)*.21f);
                // Splayed spacers seat into the hub, then lift the wider-spaced sockets outwards.
                // Moving the entire blade preserves its length above the new raised mounting point.
                var spacerBack=Ring(outerCentre*(.12f/.21f),.15f,.053f,angle);
                var spacerFront=Ring(outerCentre,.332f,.046f,angle);
                var socketSeat=Ring(outerCentre,.332f,.064f*1.20f,angle);
                Join(spacerBack,spacerFront,dark); Join(spacerFront,socketSeat,dark);
                for(int j=1;j<spacerBack.Length-1;j++)Triangle(spacerBack[0],spacerBack[j+1],spacerBack[j],dark);
                AddSpike(outerCentre,.064f,.34f,.76f,angle);

                // Small square fasteners alternate with the large spikes on the hub face.
                float boltAngle=angle+Mathf.PI/5;
                var boltCentre=new Vector3(0,Mathf.Cos(boltAngle)*.089f,Mathf.Sin(boltAngle)*.089f);
                var washer=Ring(boltCentre,.201f,.020f,boltAngle);
                var boltBase=Ring(boltCentre,.213f,.014f,boltAngle);
                var boltFace=Ring(boltCentre,.224f,.011f,boltAngle);
                Join(washer,boltBase,dark); Join(boltBase,boltFace,steel); Cap(boltFace,steel);
            }
            var mesh=new Mesh {name="Detailed Six Star Spikes"};
            mesh.SetVertices(vertices); mesh.subMeshCount=2;
            mesh.SetTriangles(dark,0); mesh.SetTriangles(steel,1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;

            void AddSpike(Vector3 centre,float halfWidth,float baseX,float tipX,float angle)
            {
                // Stepped gunmetal socket and a bright beveled lip support each blade.
                var socketBack=Ring(centre,baseX-.008f,halfWidth*1.20f,angle);
                var socketShoulder=Ring(centre,baseX+.038f,halfWidth*1.20f,angle);
                var socketFront=Ring(centre,baseX+.055f,halfWidth*1.06f,angle);
                var bladeBase=Ring(centre,baseX+.065f,halfWidth*.94f,angle);
                Join(socketBack,socketShoulder,dark); Join(socketShoulder,socketFront,dark);
                Join(socketFront,bladeBase,steel);
                var tip=new Vector3(tipX,centre.y,centre.z);
                for(int j=0;j<bladeBase.Length;j++)
                    Triangle(bladeBase[j],bladeBase[(j+1)%bladeBase.Length],tip,steel);
            }
            Vector3[] Ring(Vector3 centre,float x,float width,float angle)
            {
                // Trim the four corners rather than smoothing: eight flat facets keep the voxel style.
                float inset=width*.23f;
                var ring=new[]{new Vector3(0,-width+inset,-width),new Vector3(0,width-inset,-width),
                    new Vector3(0,width,-width+inset),new Vector3(0,width,width-inset),
                    new Vector3(0,width-inset,width),new Vector3(0,-width+inset,width),
                    new Vector3(0,-width,width-inset),new Vector3(0,-width,-width+inset)};
                var rotation=Quaternion.AngleAxis(angle*Mathf.Rad2Deg,Vector3.right);
                centre.x=x;
                for(int j=0;j<ring.Length;j++)ring[j]=centre+rotation*ring[j];
                return ring;
            }
            void Join(Vector3[] back,Vector3[] front,List<int> triangles)
            {
                for(int j=0;j<back.Length;j++)
                {
                    int next=(j+1)%back.Length;
                    Triangle(back[j],back[next],front[next],triangles);
                    Triangle(back[j],front[next],front[j],triangles);
                }
            }
            void Cap(Vector3[] ring,List<int> triangles)
            {
                for(int j=1;j<ring.Length-1;j++)Triangle(ring[0],ring[j],ring[j+1],triangles);
            }
            void Triangle(Vector3 a,Vector3 b,Vector3 c,List<int> triangles)
            {
                int start=vertices.Count; vertices.Add(a);vertices.Add(b);vertices.Add(c);
                triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
            }
        }
    }
}
