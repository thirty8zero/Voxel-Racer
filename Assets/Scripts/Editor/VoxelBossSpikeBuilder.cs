using System.Linq;
using UnityEditor;
using UnityEngine;
namespace VoxelRacer.Editor
{
    public static class VoxelBossSpikeBuilder
    {
        public static void UpdatePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(VoxelRedVanBossBuilder.PrefabPath);
            try { AddRig(root); PrefabUtility.SaveAsPrefabAsset(root, VoxelRedVanBossBuilder.PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        public static void AddRig(GameObject root)
        {
            var rig = root.GetComponent<VoxelBossSpikeRig>();
            if (rig != null) return; // Preserve authored pivots when rebuilding just the structure.
            rig = root.AddComponent<VoxelBossSpikeRig>();
            Transform Pivot(string name, Vector3 position)
            {
                var t = new GameObject(name).transform; t.SetParent(root.transform, false); t.localPosition = position; return t;
            }
            rig.leftDoor = Pivot("Left Rear Door Hinge", new Vector3(-1.04f, 0, -2.3f));
            rig.rightDoor = Pivot("Right Rear Door Hinge", new Vector3(1.04f, 0, -2.3f));
            string[] doorNames = { "Rear door voxel", "Rear door glass", "Rear door centre seam", "Rear door handle" };
            foreach (var piece in root.transform.Cast<Transform>().Where(t => doorNames.Contains(t.name)).ToArray())
                piece.SetParent(piece.localPosition.x < 0 ? rig.leftDoor : rig.rightDoor, true);
            rig.spikes = Pivot("Retractable Triple Spikes", Vector3.zero);
            rig.spikes.gameObject.AddComponent<VoxelIndestructiblePart>();
            var steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CarMaterials/TransitSteel.mat");
            var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Bosses/VanditoBossChassis.mat");
            void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>()); go.transform.SetParent(parent, false);
                go.transform.localPosition = position; go.transform.localScale = scale;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            var support = Pivot("Spike Floor Guides", Vector3.zero); support.gameObject.AddComponent<VoxelIndestructiblePart>();
            const string meshPath = "Assets/Resources/Bosses/BossSpikeTip.asset";
            var tipMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (tipMesh == null)
            {
                tipMesh = new Mesh { name = "Four sided spike tip" };
                tipMesh.vertices = new[] { new Vector3(-.11f,-.11f,0), new Vector3(.11f,-.11f,0), new Vector3(.11f,.11f,0), new Vector3(-.11f,.11f,0), new Vector3(0,0,-.55f) };
                tipMesh.triangles = new[] { 0,1,2,0,2,3, 0,4,1,1,4,2,2,4,3,3,4,0 }; tipMesh.RecalculateNormals(); tipMesh.RecalculateBounds();
                AssetDatabase.CreateAsset(tipMesh, meshPath);
            }
            foreach (float x in new[] { -.65f, 0f, .65f })
            {
                Box(support,"Floor slide rail",new Vector3(x,.76f,-1.25f),new Vector3(.48f,.08f,1.95f),dark);
                for (int step=0; step<3; step++)
                    Box(rig.spikes,"Stepped steel spike",new Vector3(x,.99f,-.575f-step*.35f),new Vector3(.42f-step*.10f,.42f-step*.10f,.35f),steel);
                var tip = new GameObject("Sharp spike tip"); tip.transform.SetParent(rig.spikes, false); tip.transform.localPosition = new Vector3(x,.99f,-1.45f);
                tip.AddComponent<MeshFilter>().sharedMesh = tipMesh; tip.AddComponent<MeshRenderer>().sharedMaterial = steel;
            }
            rig.SetPose(0,0);
        }
    }
}
