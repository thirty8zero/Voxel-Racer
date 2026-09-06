using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Calculates workshop repair costs from the active car's integrity voxels.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Repair Tuning", fileName = "VoxelRepairTuning")]
    public sealed class VoxelRepairTuning : ScriptableObject
    {
        /// <summary>
        /// Partial repairs cost their percentage of the full car's voxel count.
        /// A full repair costs one unit per missing or partially damaged armour voxel.
        /// </summary>
        public int GetRepairCost(VoxelCarController car, float repairPercent)
        {
            if (car == null)
                return 0;

            if (repairPercent >= 100f)
                return car.RepairableIntegrityVoxels;

            return Mathf.CeilToInt(car.TotalIntegrityVoxels * Mathf.Clamp01(repairPercent / 100f));
        }

        public static VoxelRepairTuning Load() => Resources.Load<VoxelRepairTuning>("VoxelRepairTuning");

#if UNITY_EDITOR
        private void OnValidate() => VoxelAssetSaveQueue.Request(this);
#endif
    }
}
