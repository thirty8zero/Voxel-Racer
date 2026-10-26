using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    internal static class VoxelCivilianTrafficSetAuthoring
    {
        // Discover each shared asset once, including sets not currently assigned to a track.
        internal static void RegisterVehicle(VoxelEnemyVehicleTuning vehicle)
        {
            if (vehicle == null) return;
            foreach (string guid in AssetDatabase.FindAssets("t:VoxelCivilianTrafficSet"))
            {
                var set = AssetDatabase.LoadAssetAtPath<VoxelCivilianTrafficSet>(AssetDatabase.GUIDToAssetPath(guid));
                if (set == null || set.civilianVehiclePool?.Contains(vehicle) == true) continue;
                Undo.RecordObject(set, "Add civilian vehicle to traffic set");
                set.civilianVehiclePool = (set.civilianVehiclePool ?? Array.Empty<VoxelEnemyVehicleTuning>()).Concat(new[] { vehicle }).ToArray();
                EditorUtility.SetDirty(set);
            }
        }
    }

    /// <summary>Give validation/preview code a private set so changing a cloned traffic tuning cannot edit a shared asset.</summary>
    internal sealed class VoxelCivilianTrafficSetScope : IDisposable
    {
        private readonly VoxelObstacleCarTuning tuning;
        private readonly VoxelCivilianTrafficSet original;
        private VoxelCivilianTrafficSet copy;

        internal VoxelCivilianTrafficSetScope(VoxelObstacleCarTuning owner)
        {
            tuning = owner; original = owner.civilianTrafficSet;
            copy = original != null ? Object.Instantiate(original) : ScriptableObject.CreateInstance<VoxelCivilianTrafficSet>();
            copy.hideFlags = HideFlags.HideAndDontSave;
            owner.civilianTrafficSet = copy;
        }

        public void Dispose()
        {
            if (copy == null) return;
            if (tuning != null) tuning.civilianTrafficSet = original;
            Object.DestroyImmediate(copy); copy = null;
        }
    }
}
