using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Persistent scoring rules for one combat mission.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Mission Tuning", fileName = "VoxelMissionTuning")]
    public sealed class VoxelMissionTuning : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Mission";

        [Header("Completion")]
        [Min(1)] public int requiredPoints = 100;

        [Header("Rewards")]
        [Tooltip("Currency awarded whenever this mission is completed.")]
        [Min(0)] public int completionCurrencyAward = 100;

        [Header("Time Bonus")]
        [Tooltip("Seconds available to earn the time-completion bonus. The mission can still be completed after this reaches zero.")]
        [Min(1f)] public float timeLimitSeconds = 120f;
        [Tooltip("Multiplier applied to the completion award if the mission is completed before the countdown reaches zero. For example, 1.25 pays 125%, 1.5 pays 150%, and 2 pays 200%.")]
        [Min(1f)] public float timeBonusCurrencyMultiplier = 1.5f;

        [Header("Enemy Score")]
        [Min(0)] public int enemyVoxelDamagePoints = 1;
        [Min(0)] public int enemyVehicleDestroyedPoints = 25;

        [Header("Civilian Penalties")]
        [Tooltip("Use a negative value to reduce mission progress.")]
        public int civilianVoxelDamagePoints = -2;
        [Tooltip("Use a negative value to reduce mission progress.")]
        public int civilianVehicleDestroyedPoints = -30;

        public static VoxelMissionTuning Load() => Resources.Load<VoxelMissionTuning>("Missions/DefaultMissionTuning");

#if UNITY_EDITOR
        private void OnValidate()
        {
            requiredPoints = Mathf.Max(1, requiredPoints);
            completionCurrencyAward = Mathf.Max(0, completionCurrencyAward);
            timeLimitSeconds = Mathf.Max(1f, timeLimitSeconds);
            timeBonusCurrencyMultiplier = Mathf.Max(1f, timeBonusCurrencyMultiplier);
            enemyVoxelDamagePoints = Mathf.Max(0, enemyVoxelDamagePoints);
            enemyVehicleDestroyedPoints = Mathf.Max(0, enemyVehicleDestroyedPoints);
            civilianVoxelDamagePoints = Mathf.Min(0, civilianVoxelDamagePoints);
            civilianVehicleDestroyedPoints = Mathf.Min(0, civilianVehicleDestroyedPoints);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
