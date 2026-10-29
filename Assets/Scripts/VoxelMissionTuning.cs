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

        [Header("Crate Rewards")]
        [Tooltip("Mission-specific reward settings for each crate source. An empty list awards no crate prizes. Prize weights are relative to the other eligible prize types.")]
        public VoxelDestructionRewards.Entry[] crateRewards = System.Array.Empty<VoxelDestructionRewards.Entry>();

        [Header("Repairs")]
        [Tooltip("Currency per repaired pixel in the garage after completing this mission. Zero makes repairs free.")]
        [Min(0)] public float repairCostPerPixel = 2f;

        [Header("Time Bonus")]
        [Tooltip("Finish before this countdown expires for +0.01x per second left on the clock. After expiry, the live multiplier decays by 0.1x each second, down to 1x.")]
        [Min(1f)] public float timeLimitSeconds = 120f;
        [Tooltip("Starting live multiplier each mission, with a 1x minimum. Completion pays base cash times the final multiplier, including any remaining-time bonus.")]
        [Min(1f)] public float timeBonusCurrencyMultiplier = 1f;
        [Header("Live Time Multiplier")]
        [Tooltip("Cap on live multiplier gains. The remaining-time bonus is added on completion and can exceed this cap.")]
        [Min(1f)] public float maximumTimeMultiplier = 5f;
        [Tooltip("Multiplier awarded for every 10 enemy voxels destroyed. Partial groups carry across enemies within the mission; feedback appears immediately when a group completes.")]
        [Min(0f)] public float enemyVoxelMultiplier = .01f;
        [Min(0f)] public float enemyDestroyedMultiplier = .25f;
        [Tooltip("Multiplier awarded once per destroyed fuel-drum group, regardless of how many visual drums it contains.")]
        [Min(0f)] public float barrelMultiplier = .1f;
        [Min(0f)] public float nearMissMultiplier = .1f;
        [Min(0f)] public float civilianVoxelMultiplierPenalty = .1f;
        [Min(0f)] public float civilianDestroyedMultiplierPenalty = 1f;
        [Min(1f)] public float bonusWarningSeconds = 10f;

        [Header("Popup Feedback")]
        [Tooltip("Hold beneath the mission percentage panel before non-voxel score numbers (ram damage, destruction and near misses) fly into the score tally. A new non-voxel number sends the previous held number off early. Applies to either sign.")]
        [InspectorName("Non Voxel Damage Popup Hold Seconds (s)")]
        [Min(0f)] public float damagePopupHoldSeconds = .12f;
        [Tooltip("Hold before individual voxel-hit damage/score numbers fly into the score tally. Applies to either sign and preserves travel speed.")]
        [Min(0f)] public float voxelDamagePopupHoldSeconds = .12f;
        [Tooltip("Hold before other multiplier popups fly to the counter: crate prizes, barrels, near misses and kill-chain bonuses. Voxel damage and destroyed vehicles use the separate settings below.")]
        [InspectorName("Other Multiplier Popup Hold Seconds (s)")]
        [Min(0f)] public float multiplierPopupHoldSeconds = .17f;
        [Tooltip("Hold before voxel multiplier popups fly to the counter, for both positive enemy-voxel rewards and negative civilian-voxel penalties. Enemy rewards still occur per 10 voxels; civilian penalties appear after their quiet period.")]
        [Min(0f)] public float voxelMultiplierPopupHoldSeconds = .17f;
        [Tooltip("Hold before destroyed-vehicle multiplier popups fly to the counter, for both positive enemy destruction rewards and negative civilian destruction penalties.")]
        [Min(0f)] public float destroyedVehicleMultiplierPopupHoldSeconds = .17f;
        [Tooltip("Seconds without civilian voxel damage before its accumulated multiplier penalty popup appears. The voxel multiplier hold follows this delay.")]
        [Min(0f)] public float civilianVoxelPopupQuietSeconds = 1f;

        [Header("Enemy Score")]
        [Min(0)] public int enemyVoxelDamagePoints = 1;
        // Legacy fallback for callers without an enemy identity; gameplay uses the enemy vehicle tuning.
        [HideInInspector, Min(0)] public int enemyVehicleDestroyedPoints = 25;

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
        [Min(10f)] public float roadsideTurretSpawnDistanceAhead = 250f;
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

        [Header("Mission Completion")]
        [Tooltip("Real-time seconds of slow motion after mission completion. Zero disables the effect. A boss can override this on its definition.")]
        [Min(0)] public float completionSlowMotionDuration = 1.5f;
        [Range(.01f, 1)] public float completionSlowMotionTimeScale = .25f;

        public static VoxelMissionTuning Load() => Resources.Load<VoxelMissionTuning>("Missions/DefaultMissionTuning");

#if UNITY_EDITOR
        private void OnValidate()
        {
            requiredPoints = Mathf.Max(1, requiredPoints);
            completionCurrencyAward = Mathf.Max(0, completionCurrencyAward);
            VoxelDestructionRewards.SanitizeEntries(crateRewards);
            repairCostPerPixel = Mathf.Max(0, repairCostPerPixel);
            timeLimitSeconds = Mathf.Max(1f, timeLimitSeconds);
            maximumTimeMultiplier = Mathf.Max(1f, maximumTimeMultiplier);
            timeBonusCurrencyMultiplier = Mathf.Clamp(timeBonusCurrencyMultiplier, 1f, maximumTimeMultiplier);
            damagePopupHoldSeconds = Mathf.Max(0f, damagePopupHoldSeconds);
            voxelDamagePopupHoldSeconds = Mathf.Max(0f, voxelDamagePopupHoldSeconds);
            multiplierPopupHoldSeconds = Mathf.Max(0f, multiplierPopupHoldSeconds);
            voxelMultiplierPopupHoldSeconds = Mathf.Max(0f, voxelMultiplierPopupHoldSeconds);
            destroyedVehicleMultiplierPopupHoldSeconds = Mathf.Max(0f, destroyedVehicleMultiplierPopupHoldSeconds);
            civilianVoxelPopupQuietSeconds = Mathf.Max(0f, civilianVoxelPopupQuietSeconds);
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
