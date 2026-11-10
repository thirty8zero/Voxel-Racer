using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Performance Wheels", fileName = "PerformanceWheelTuning")]
    public sealed class VoxelPerformanceWheelTuning : ScriptableObject
    {
        public string displayName = "PERFORMANCE WHEELS";
        public GameObject wheelPrefab;
        public GameObject compatibleCarPrefab;
        [Min(0)] public int purchasePrice = 250;
        [Tooltip("Higher levels can replace a lower-level wheel set.")]
        [Min(1)] public int upgradeLevel = 1;
        [Min(0)] public float accelerationBonusPercent = 20;
        [Min(0)] public float laneChangeBonusPercent = 15;
        [Tooltip("Percentage increase in braking deceleration while these tyres are installed. Zero keeps standard braking.")]
        [Min(0)] public float brakingBonusPercent = 20;
        public bool Fits(VoxelCarDefinition car) => car != null && car.visualPrefab != null &&
            wheelPrefab != null && car.visualPrefab == compatibleCarPrefab;
        public static VoxelPerformanceWheelTuning Load() =>
            Resources.Load<VoxelPerformanceWheelTuning>("Upgrades/PerformanceWheelTuning");
        public static VoxelPerformanceWheelTuning LoadGold() =>
            Resources.Load<VoxelPerformanceWheelTuning>("Upgrades/GoldTrackWheelTuning");
    }
}
