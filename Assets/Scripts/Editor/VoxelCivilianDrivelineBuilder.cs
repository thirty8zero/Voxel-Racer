using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelCivilianDrivelineBuilder
    {
        public const string StructureName = "Indestructible Civilian Driveline";
        public static readonly string[] PrefabPaths =
        {
            VoxelCivilianHatchbackBuilder.PrefabPath,
            VoxelCivilianVanBuilder.PrefabPath,
            VoxelCivilianBeetleBuilder.PrefabPath,
            VoxelCivilianKombiBuilder.PrefabPath,
            VoxelCivilianJettaBuilder.PrefabPath,
            VoxelCivilianMercedesWagonBuilder.PrefabPath
        };

        [MenuItem("Tools/Voxel Racer/Update Civilian Axles and Drivelines")]
        public static void UpdatePrefabs()
        {
            foreach (string path in PrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { AddStructure(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log("Added protected axles and drivelines to all civilian prefabs (three pieces each).");
        }

        public static void AddStructure(GameObject root)
        {
            var wheels = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Obstacle Voxel Wheel")
                .Select(t => root.transform.InverseTransformPoint(t.position)).OrderBy(p => p.z).ToArray();
            if (wheels.Length != 4) throw new InvalidOperationException("Civilian driveline requires four wheel roots: " + root.name);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CarMaterials/CivilianHatchTrim.mat");
            if (material == null) throw new InvalidOperationException("Civilian driveline trim material is missing.");
            var existing = root.transform.Find(StructureName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var structure = new GameObject(StructureName);
            structure.transform.SetParent(root.transform, false);
            structure.AddComponent<VoxelIndestructiblePart>();

            Vector3 Axle(string name, Vector3 a, Vector3 b)
            {
                Vector3 centre = (a + b) * .5f;
                // Ends sit just inside the tyres; axles stay on the car, outside the spinning wheel roots.
                Box(structure.transform, name, centre, new Vector3(Mathf.Abs(a.x - b.x) - .20f, .14f, .16f), material);
                return centre;
            }
            Vector3 rear = Axle("Rear Axle", wheels[0], wheels[1]);
            Vector3 front = Axle("Front Axle", wheels[2], wheels[3]);
            var shaft = Box(structure.transform, "Driveline", (rear + front) * .5f,
                new Vector3(.14f, .14f, Vector3.Distance(rear, front) + .16f), material);
            shaft.transform.localRotation = Quaternion.LookRotation(front - rear);
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
