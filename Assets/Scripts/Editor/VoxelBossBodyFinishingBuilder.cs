using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelBossBodyFinishingBuilder
    {
        private const string GroupName = "Boss mirror infill and wheel guards";

        [MenuItem("Tools/Voxel Racer/Update Boss Mirror Infill and Wheel Guards")]
        public static void UpdatePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(VoxelRedVanBossBuilder.PrefabPath);
            try
            {
                Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, VoxelRedVanBossBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject root)
        {
            var old = root.transform.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject(GroupName).transform;
            group.SetParent(root.transform, false);
            var paint = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Bosses/VanditoBossPaint.mat");
            foreach (int side in new[] { -1, 1 })
            {
                // Close the open cheek between the bonnet, cab side and stepped windscreen.
                for (int row = 0; row < 3; row++)
                    Block(group, "Mirror front infill voxel", new Vector3(side * 1.005f, 1.185f + row * .13f, 1.365f),
                        new Vector3(.13f, .13f, row < 2 ? .39f : .16f), paint);
                foreach (float wheelZ in new[] { -1.43f, 1.43f })
                    for (int step = 0; step <= 12; step++)
                    {
                        float angle = step * Mathf.PI / 12f;
                        // Raised, stepped semicircular lip; clearance includes the tyre's rotating corners.
                        Block(group, "Wheel guard voxel", new Vector3(side * 1.14f,
                            .46f + Mathf.Sin(angle) * .63f, wheelZ + Mathf.Cos(angle) * .63f),
                            new Vector3(.10f, .16f, .16f), paint);
                    }
            }
        }

        private static void Block(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = size;
            Object.DestroyImmediate(block.GetComponent<Collider>());
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
