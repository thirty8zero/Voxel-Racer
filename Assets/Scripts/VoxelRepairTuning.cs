using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Calculates workshop repair costs from the active car's integrity voxels.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Repair Tuning", fileName = "VoxelRepairTuning")]
    public sealed class VoxelRepairTuning : ScriptableObject
    {
        private const int CostPerVoxel = 2;

        /// <summary>
        /// Partial repairs cost two units per voxel in their rounded-up share of the full car.
        /// A full repair costs two units per missing or partially damaged armour voxel.
        /// </summary>
        public int GetRepairCost(VoxelCarController car, float repairPercent)
        {
            if (car == null)
                return 0;

            if (repairPercent >= 100f)
                return CostPerVoxel * car.RepairableIntegrityVoxels;

            return CostPerVoxel * Mathf.CeilToInt(car.TotalIntegrityVoxels * Mathf.Clamp01(repairPercent / 100f));
        }

        public static VoxelRepairTuning Load() => Resources.Load<VoxelRepairTuning>("VoxelRepairTuning");

#if UNITY_EDITOR
        private void OnValidate() => VoxelAssetSaveQueue.Request(this);
#endif
    }
}
