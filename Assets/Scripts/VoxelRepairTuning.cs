using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Calculates workshop repair costs from the active car's integrity voxels.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Repair Tuning", fileName = "VoxelRepairTuning")]
    public sealed class VoxelRepairTuning : ScriptableObject
    {
        /// <summary>
        /// Partial repairs use their rounded-up share of the full car.
        /// A full repair charges for each missing or partially damaged armour voxel.
        /// </summary>
        public int GetRepairCost(VoxelCarController car, float repairPercent, float costPerPixel = 2f)
        {
            if (car == null)
                return 0;

            costPerPixel = Mathf.Max(0, costPerPixel);
            if (repairPercent >= 100f)
                return Mathf.CeilToInt(costPerPixel * car.RepairableIntegrityVoxels);

            return Mathf.CeilToInt(costPerPixel * Mathf.CeilToInt(car.TotalIntegrityVoxels * Mathf.Clamp01(repairPercent / 100f)));
        }

        public static VoxelRepairTuning Load() => Resources.Load<VoxelRepairTuning>("VoxelRepairTuning");

#if UNITY_EDITOR
        private void OnValidate() => VoxelAssetSaveQueue.Request(this);
#endif
    }
}
