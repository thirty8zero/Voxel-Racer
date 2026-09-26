using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelBossVoxelDetailBuilder
    {
        private const string Marker = "Boss voxel detail 2x";

        [MenuItem("Tools/Voxel Racer/Update Boss Voxel Detail")]
        public static void UpdatePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(VoxelRedVanBossBuilder.PrefabPath);
            try
            {
                Apply(root);
                VoxelBossEyesBuilder.AddEyes(root);
                PrefabUtility.SaveAsPrefabAsset(root, VoxelRedVanBossBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            VoxelVanditoBossAppearanceBuilder.UpdateSideLettering();
            AssetDatabase.SaveAssets();
        }

        // Authoring-time subdivision only. Two closed half-cubes replace each
        // destructible cube, keeping its silhouette, parent rig and materials.
        // Artwork is rebuilt afterwards at the new panel boundaries.
        public static void Apply(GameObject root)
        {
            if (root.transform.Find(Marker) != null) return;
            var pieces = root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.GetComponentInParent<VoxelIndestructiblePart>(true) == null).ToArray();
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            foreach (var piece in pieces)
            {
                var filter = piece.GetComponent<MeshFilter>();
                if (piece.transform.childCount != 0 || filter == null ||
                    (filter.sharedMesh != cube && piece.name != "Rear door glass" && piece.name != "Van body voxel"))
                    throw new InvalidOperationException("Unsupported boss subdivision piece: " + piece.name);
            }
            foreach (var piece in pieces)
            {
                var original = piece.transform;
                Vector3 size = original.localScale;
                int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
                Vector3 shift = Vector3.zero;
                shift[axis] = size[axis] * .25f;
                shift = original.localRotation * shift;
                Vector3 centre = original.localPosition;
                var half = Object.Instantiate(original.gameObject, original.parent, false).transform;
                half.name = original.name;
                size[axis] *= .5f;
                original.localScale = half.localScale = size;
                original.localPosition = centre - shift;
                half.localPosition = centre + shift;
                foreach (var part in new[] { original, half })
                {
                    part.GetComponent<MeshFilter>().sharedMesh = cube;
                    var renderer = part.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials = new[] { renderer.sharedMaterial };
                }
            }
            new GameObject(Marker).transform.SetParent(root.transform, false);
        }
    }
}
