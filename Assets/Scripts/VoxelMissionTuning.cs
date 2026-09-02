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

        [Header("Static Obstacle Score")]
        [Tooltip("Points awarded when the player detonates a fuel-drum group with weapons.")]
        [Min(0)] public int fuelDrumDestroyedPoints = 15;
        [Tooltip("How long the on-screen score popup remains visible after a fuel-drum group is destroyed.")]
        [Min(0.1f)] public float fuelDrumDestroyedPopupDuration = 2f;

        [Header("Roadside Turret Spawning")]
        [Tooltip("Leave empty to disable roadside gun turrets for this mission.")]
        public VoxelRoadsideTurretTuning roadsideTurretTuning;
        [Tooltip("Seconds between opportunities to place a roadside turret.")]
        [Min(0.1f)] public float roadsideTurretSpawnCheckInterval = 6f;
        [Tooltip("Chance that a turret is created at each spawn check.")]
        [Range(0f, 1f)] public float roadsideTurretSpawnChance = 0.35f;
        [Tooltip("Distance ahead of the player where a successful turret spawn is placed.")]
        [Min(10f)] public float roadsideTurretSpawnDistanceAhead = 100f;
        [Tooltip("Maximum number of roadside turrets that may be active at once.")]
        [Min(1)] public int maximumActiveRoadsideTurrets = 1;

        [Header("Civilian Near Miss Score")]
        [Tooltip("Maximum clear space between the player and a civilian vehicle that counts as a near miss.")]
        [Min(0.01f)] public float civilianNearMissDistance = 1f;
        [Min(1)] public int civilianNearMissMinPoints = 1;
        [Min(1)] public int civilianNearMissMaxPoints = 10;
        [Tooltip("Each percentage step closer than the trigger distance adds one point, up to the maximum score.")]
        [Range(1f, 100f)] public float civilianNearMissScoreStepPercent = 10f;
        [Tooltip("Extra clear space required after a pass before the near-miss award is made.")]
        [Min(0f)] public float civilianNearMissPassClearance = 0.5f;
        [Tooltip("Approximate player half-width used to measure the visible gap between vehicles.")]
        [Min(0f)] public float civilianNearMissPlayerHalfWidth = 1.3f;
        [Tooltip("Approximate player half-length used to measure the visible gap between vehicles.")]
        [Min(0f)] public float civilianNearMissPlayerHalfLength = 2.3f;
        [Tooltip("How long the near-miss title and score remain visible on screen.")]
        [Min(0.1f)] public float civilianNearMissPopupDuration = 2f;

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
            fuelDrumDestroyedPoints = Mathf.Max(0, fuelDrumDestroyedPoints);
            fuelDrumDestroyedPopupDuration = Mathf.Max(0.1f, fuelDrumDestroyedPopupDuration);
            roadsideTurretSpawnCheckInterval = Mathf.Max(0.1f, roadsideTurretSpawnCheckInterval);
            roadsideTurretSpawnDistanceAhead = Mathf.Max(10f, roadsideTurretSpawnDistanceAhead);
            maximumActiveRoadsideTurrets = Mathf.Max(1, maximumActiveRoadsideTurrets);
            civilianNearMissDistance = Mathf.Max(0.01f, civilianNearMissDistance);
            civilianNearMissMinPoints = Mathf.Max(1, civilianNearMissMinPoints);
            civilianNearMissMaxPoints = Mathf.Max(civilianNearMissMinPoints, civilianNearMissMaxPoints);
            civilianNearMissScoreStepPercent = Mathf.Clamp(civilianNearMissScoreStepPercent, 1f, 100f);
            civilianNearMissPassClearance = Mathf.Max(0f, civilianNearMissPassClearance);
            civilianNearMissPlayerHalfWidth = Mathf.Max(0f, civilianNearMissPlayerHalfWidth);
            civilianNearMissPlayerHalfLength = Mathf.Max(0f, civilianNearMissPlayerHalfLength);
            civilianNearMissPopupDuration = Mathf.Max(0.1f, civilianNearMissPopupDuration);
            civilianVoxelDamagePoints = Mathf.Min(0, civilianVoxelDamagePoints);
            civilianVehicleDestroyedPoints = Mathf.Min(0, civilianVehicleDestroyedPoints);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
