using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Keeps the selected car's missing voxels while moving between race and workshop scenes.</summary>
    public static class VoxelCarRunState
    {
        private static readonly HashSet<string> missingVoxelPaths = new();
        private static string carDefinitionName = string.Empty;

        public static int MissingVoxelCount => missingVoxelPaths.Count;

        public static void BeginNewRun(VoxelCarDefinition definition)
        {
            carDefinitionName = definition != null ? definition.name : string.Empty;
            missingVoxelPaths.Clear();
        }

        public static void Capture(VoxelCarController controller, VoxelCarDefinition definition = null)
        {
            if (controller == null)
                return;

            definition ??= VoxelCarSelectionState.GetSelectedOrDefault();
            carDefinitionName = definition != null ? definition.name : string.Empty;
            missingVoxelPaths.Clear();

            foreach (MeshRenderer renderer in controller.GetComponentsInChildren<MeshRenderer>(true))
            {
                Transform voxel = renderer.transform;
                if (voxel == controller.transform || voxel.GetComponentInParent<VoxelIndestructiblePart>() != null)
                    continue;
                if (!voxel.gameObject.activeSelf)
                    missingVoxelPaths.Add(GetSiblingPath(controller.transform, voxel));
            }
        }

        public static void Apply(VoxelCarController controller, VoxelCarDefinition definition = null)
        {
            if (controller == null || missingVoxelPaths.Count == 0)
                return;

            definition ??= VoxelCarSelectionState.GetSelectedOrDefault();
            if (definition == null || definition.name != carDefinitionName)
                return;

            foreach (string path in missingVoxelPaths)
            {
                Transform voxel = FindBySiblingPath(controller.transform, path);
                if (voxel != null)
                    voxel.gameObject.SetActive(false);
            }
        }

        private static string GetSiblingPath(Transform root, Transform child)
        {
            var indices = new List<int>();
            Transform current = child;
            while (current != null && current != root)
            {
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }
            indices.Reverse();
            return string.Join("/", indices);
        }

        private static Transform FindBySiblingPath(Transform root, string path)
        {
            Transform current = root;
            string[] indices = path.Split('/');
            foreach (string value in indices)
            {
                if (!int.TryParse(value, out int index) || index < 0 || index >= current.childCount)
                    return null;
                current = current.GetChild(index);
            }
            return current;
        }
    }
}
