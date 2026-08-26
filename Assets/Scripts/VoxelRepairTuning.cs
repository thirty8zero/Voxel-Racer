using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Persistent costs for workshop repair choices.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Repair Tuning", fileName = "VoxelRepairTuning")]
    public sealed class VoxelRepairTuning : ScriptableObject
    {
        [Min(0)] public int fullRepairCost;
        [Min(0)] public int repair10PercentCost;
        [Min(0)] public int repair25PercentCost;
        [Min(0)] public int repair50PercentCost;

        public static VoxelRepairTuning Load() => Resources.Load<VoxelRepairTuning>("VoxelRepairTuning");

#if UNITY_EDITOR
        private void OnValidate() => VoxelAssetSaveQueue.Request(this);
#endif
    }
}
