using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Shared prefab/runtime support for traffic, enemies and procedural fallbacks.</summary>
    public static class VoxelVehicleDriveline
    {
        public const string StructureName = "Indestructible Vehicle Driveline";
        public const string WheelName = "Obstacle Voxel Wheel";
        private static Mesh cube;
        private static Material material;

        public static Transform FindStructure(Transform model)
        {
            if (model == null) return null;
            // Keep the already-authored civilian and boss structures instead of stacking bars.
            var structure = model.Find(StructureName) ?? model.Find("Indestructible Civilian Driveline") ?? model.Find("Indestructible Boss Chassis");
            if (structure != null) return structure;
            var front = model.Find("Front Axle"); var rear = model.Find("Rear Axle"); var shaft = model.Find("Driveline");
            return front != null && rear != null && shaft != null && front.GetComponent<VoxelIndestructiblePart>() != null &&
                rear.GetComponent<VoxelIndestructiblePart>() != null && shaft.GetComponent<VoxelIndestructiblePart>() != null ? model : null;
        }

        public static Transform Ensure(GameObject model)
        {
            if (model == null) return null;
            var existing = FindStructure(model.transform);
            if (existing != null) return existing;
            var wheels = new List<Vector3>();
            foreach (var part in model.GetComponentsInChildren<Transform>(true))
                if (part.name == WheelName) wheels.Add(model.transform.InverseTransformPoint(part.position));
            if (wheels.Count < 4 || wheels.Count % 2 != 0) return null;
            wheels.Sort((a, b) => a.z.CompareTo(b.z));
            if (cube == null) cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (material == null) material = Resources.Load<Material>("CarMaterials/CivilianHatchTrim");
            if (cube == null || material == null) return null;
            var structure = new GameObject(StructureName).transform;
            structure.SetParent(model.transform, false);
            structure.gameObject.AddComponent<VoxelIndestructiblePart>();
            var centres = new List<Vector3>();
            for (int i = 0; i < wheels.Count; i += 2)
            {
                Vector3 a = wheels[i], b = wheels[i + 1], centre = (a + b) * .5f;
                string name = i == 0 ? "Rear Axle" : i == wheels.Count - 2 ? "Front Axle" : "Middle Axle " + i / 2;
                var axle = Box(name, centre, new Vector3(Mathf.Max(.1f, Vector3.Distance(a, b) - .20f), .14f, .16f));
                axle.localRotation = Quaternion.FromToRotation(Vector3.right, b - a);
                centres.Add(centre);
            }
            Vector3 rear = centres[0], front = centres[centres.Count - 1];
            var shaft = Box("Driveline", (rear + front) * .5f, new Vector3(.14f, .14f, Vector3.Distance(rear, front) + .16f));
            shaft.localRotation = Quaternion.LookRotation(front - rear);
            return structure;

            Transform Box(string name, Vector3 position, Vector3 size)
            {
                var part = new GameObject(name).transform;
                part.SetParent(structure, false); part.localPosition = position; part.localScale = size;
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = cube;
                part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                return part;
            }
        }
    }
}
