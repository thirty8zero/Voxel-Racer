using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelBossStructureBuilder
    {
        public const string StructureName = "Indestructible Boss Chassis";
        [MenuItem("Tools/Voxel Racer/Update Vandito Boss Structure")]
        public static void UpdatePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(VoxelRedVanBossBuilder.PrefabPath);
            try { AddStructure(root); PrefabUtility.SaveAsPrefabAsset(root, VoxelRedVanBossBuilder.PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void AddStructure(GameObject root)
        {
            var existing = root.transform.Find(StructureName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var structure = new GameObject(StructureName);
            structure.transform.SetParent(root.transform, false);
            structure.AddComponent<VoxelIndestructiblePart>();
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Bosses/VanditoBossChassis.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(.23f, .26f, .28f));
                material.SetFloat("_Smoothness", .22f);
                AssetDatabase.CreateAsset(material, "Assets/Resources/Bosses/VanditoBossChassis.mat");
            }
            // Van-local dimensions; the encounter scales the complete model to span two lanes.
            // Axles reach into the inner tyre faces at x +/- .90, with centres at y .46.
            var pieces = new List<CombineInstance>();
            void Box(Vector3 position, Vector3 size)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pieces.Add(new CombineInstance { mesh = cube.GetComponent<MeshFilter>().sharedMesh,
                    transform = Matrix4x4.TRS(position, Quaternion.identity, size) });
                Object.DestroyImmediate(cube);
            }
            foreach (float z in new[] { -1.43f, 1.43f })
            {
                Box(new Vector3(0, .46f, z), new Vector3(2.0f, .16f, .18f));
                Box(new Vector3(0, .46f, z), new Vector3(.34f, .28f, .32f));
            }
            Box(new Vector3(0, .46f, 0), new Vector3(.14f, .14f, 2.86f)); // prop shaft
            Box(new Vector3(0, .48f, .88f), new Vector3(.36f, .28f, .58f)); // gearbox
            foreach (float x in new[] { -.72f, .72f })
                Box(new Vector3(x, .54f, -.02f), new Vector3(.14f, .16f, 4.48f));
            foreach (float z in new[] { -2.1f, -.3f, 2.05f })
                Box(new Vector3(0, .55f, z), new Vector3(1.55f, .14f, .13f));
            Box(new Vector3(0, .67f, -.03f), new Vector3(1.94f, .10f, 4.42f)); // interior floor
            Box(new Vector3(0, 1.37f, .12f), new Vector3(1.94f, 1.30f, .10f)); // behind cab
            foreach (float x in new[] { -.83f, 0f, .83f })
                Box(new Vector3(x, 1.37f, .045f), new Vector3(.065f, 1.30f, .055f));
            Box(new Vector3(0, 2.025f, .10f), new Vector3(1.94f, .07f, .15f));
            var mesh = new Mesh { name = "Vandito Boss Chassis Mesh" };
            mesh.CombineMeshes(pieces.ToArray(), true, true);
            const string path = "Assets/Resources/Bosses/VanditoBossChassis.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
            structure.AddComponent<MeshFilter>().sharedMesh = saved;
            structure.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
