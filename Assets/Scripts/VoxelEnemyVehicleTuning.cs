using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Durability and combat identity shared by every instance of one enemy vehicle type.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Enemies/Vehicle Tuning", fileName = "VoxelEnemyVehicleTuning")]
    public sealed class VoxelEnemyVehicleTuning : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Enemy Vehicle";

        [Header("Durability")]
        [Tooltip("Damage each body voxel can absorb before it is removed.")]
        [Min(0.01f)] public float voxelHealth = 3f;
        [Tooltip("Total damage required to destroy the vehicle.")]
        [Min(0.01f)] public float vehicleHealth = 30f;

        [Header("Movement")]
        [Tooltip("Random range for the multiplier applied to the player's maximum speed when this enemy spawns. The selected speed remains fixed for that enemy.")]
        [Min(0f)] public float minimumSpawnSpeedMultiplier = 0.98f;
        [Min(0f)] public float maximumSpawnSpeedMultiplier = 0.98f;
        [Tooltip("Distance ahead of the player at which the enemy switches from its spawn speed to its approach speed.")]
        [Min(0f)] public float approachSpeedDistance = 100f;
        [Min(0f)] public float minimumApproachSpeedMultiplier = 0.98f;
        [Min(0f)] public float maximumApproachSpeedMultiplier = 0.98f;
        [Tooltip("Distance ahead of the player at which the enemy switches to its engage speed. Use this later for close-range attack behaviours.")]
        [Min(0f)] public float engageSpeedDistance = 0f;
        [Min(0f)] public float minimumEngageSpeedMultiplier = 0.98f;
        [Min(0f)] public float maximumEngageSpeedMultiplier = 0.98f;

        [Header("Evasive Lane Change")]
        [Tooltip("Fraction of the vehicle's health that must be lost before its lane-change chance is rolled. 0.5 means 50% damage taken.")]
        [Range(0f, 1f)] public float laneChangeDamagePercent = 0.5f;
        [Tooltip("Chance to attempt one evasive lane change once the damage threshold is reached.")]
        [Range(0f, 1f)] public float laneChangeChance = 0.5f;
        [Tooltip("Sideways movement speed while changing into a safe adjacent lane.")]
        [Min(0.1f)] public float laneChangeSpeed = 4.5f;
        [Tooltip("Chance of receiving a temporary speed burst once an evasive lane change has found a safe destination.")]
        [Range(0f, 1f)] public float laneChangeSpeedBoostChance = 0.5f;
        [Tooltip("Extra fraction of the enemy's current phase speed during the lane-change burst. 0.2 means 20% faster.")]
        [Min(0f)] public float laneChangeSpeedBoostMultiplier = 0.2f;
        [Tooltip("How long the lane-change speed burst lasts before the enemy resumes its normal speed.")]
        [Min(0f)] public float laneChangeSpeedBoostDuration = 1f;

        [Header("Collision")]
        [Tooltip("Lane-space collision width used for a player ram.")]
        [Min(0.1f)] public float collisionHalfWidth = 1.35f;
        [Tooltip("Track-space collision depth used for a player ram. Increase this if vehicle models visibly overlap before impact.")]
        [Min(0.1f)] public float collisionHalfLength = 3.8f;

        [Header("Player Impact")]
        [Tooltip("Random range of player-car voxels damaged when this enemy is rammed.")]
        [Min(1)] public int playerDamageVoxelsMin = 10;
        [Min(1)] public int playerDamageVoxelsMax = 12;
        [Tooltip("Damage this enemy takes from one player-car ram. The enemy only explodes once its health reaches zero.")]
        [Min(0f)] public float playerRamDamage = 20f;

        [Header("Player Ram Response")]
        [Tooltip("How long a surviving enemy matches the player's speed after being rammed.")]
        [Min(0f)] public float playerRamSpeedMatchDuration = 2f;
        [Min(0f)] public float rearRamEnemyForwardPushDistance = 1.25f;
        [Min(0.01f)] public float rearRamEnemyForwardPushDuration = 0.18f;
        public VoxelEasingType rearRamEnemyForwardPushEasing = VoxelEasingType.EaseOutCubic;
        [Min(0f)] public float playerRearRamRecoilDistance = 0.8f;
        [Min(0.01f)] public float playerRearRamRecoilDuration = 0.25f;
        public VoxelEasingType playerRearRamRecoilEasing = VoxelEasingType.EaseOutCubic;
        [Min(0f)] public float sideRamEnemyLaneShiftDistance = 0.9f;
        [Min(0.01f)] public float sideRamEnemyLaneShiftDuration = 0.3f;
        public VoxelEasingType sideRamEnemyLaneShiftEasing = VoxelEasingType.EaseOutCubic;
        [Min(0f)] public float playerSideRamBounceDistance = 1.2f;
        [Min(0.01f)] public float playerSideRamBounceDuration = 0.32f;
        public VoxelEasingType playerSideRamBounceEasing = VoxelEasingType.EaseOutCubic;

        [Header("Weapon Damage")]
        [Tooltip("Variation applied when choosing a voxel on the enemy's rear surface. Higher values create a less uniform damage pattern.")]
        [Min(0f)] public float rearSurfaceHitRandomness = 1.5f;

        [Header("Weapon Damage Debris")]
        [Tooltip("Scale of voxels removed by weapon fire.")]
        [Min(0.01f)] public float weaponDebrisScale = 0.65f;
        [Min(0f)] public float weaponDebrisForwardForceMin = 7f;
        [Min(0f)] public float weaponDebrisForwardForceMax = 10f;
        [Min(0f)] public float weaponDebrisUpwardForce = 2.5f;
        [Min(0f)] public float weaponDebrisSpreadForce = 1.5f;
        [Min(0.1f)] public float weaponDebrisLifetime = 1.5f;

        [Header("Player Ram Debris")]
        [Tooltip("Scale of body voxels removed when the player rams this vehicle.")]
        [Min(0.01f)] public float ramDebrisScale = 0.65f;
        [Min(0f)] public float ramDebrisForwardForceMin = 7f;
        [Min(0f)] public float ramDebrisForwardForceMax = 10f;
        [Min(0f)] public float ramDebrisUpwardForce = 2.5f;
        [Min(0f)] public float ramDebrisSpreadForce = 1.5f;
        [Min(0.1f)] public float ramDebrisLifetime = 1.5f;

        [Header("Health Bar")]
        [Min(0.1f)] public float healthBarWidth = 2.4f;
        [Min(0.02f)] public float healthBarHeight = 0.22f;
        [Min(0f)] public float healthBarHeightOffset = 2.8f;
        public Color healthBarFullColour = new Color(0.92f, 0.05f, 0.04f);
        public Color healthBarEmptyColour = new Color(0.36f, 0.01f, 0.01f);
        [Range(0f, 1f)] public float criticalHealthPercent = 0.2f;
        [Min(0f)] public float criticalPulseSpeed = 7f;
        [Range(0f, 0.5f)] public float criticalPulseScale = 0.16f;

        [Header("Explosion")]
        [Tooltip("Size multiplier applied to the shared VoxelDestructionExplosion effect.")]
        [Min(0.1f)] public float explosionEffectScale = 1f;
        [Min(1)] public int explosionVoxelCount = 40;
        [Tooltip("Maximum portion of the active body that can detach during a weapon explosion, preserving a visible flying wreck.")]
        [Range(0f, 0.95f)] public float maximumExplosionVoxelRemovalPercent = 0.65f;
        [Min(0f)] public float explosionForwardForceMin = 7f;
        [Min(0f)] public float explosionForwardForceMax = 10f;
        [Min(0f)] public float explosionUpwardForce = 2.5f;
        [Min(0f)] public float explosionSpreadForce = 1.5f;
        [Min(0.01f)] public float explosionDebrisScale = 0.65f;
        [Min(0.1f)] public float explosionDebrisLifetime = 1.5f;
        [Min(0f)] public float destroyedLifetime = 2.5f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            voxelHealth = Mathf.Max(0.01f, voxelHealth);
            vehicleHealth = Mathf.Max(0.01f, vehicleHealth);
            maximumSpawnSpeedMultiplier = Mathf.Max(minimumSpawnSpeedMultiplier, maximumSpawnSpeedMultiplier);
            approachSpeedDistance = Mathf.Max(engageSpeedDistance, approachSpeedDistance);
            maximumApproachSpeedMultiplier = Mathf.Max(minimumApproachSpeedMultiplier, maximumApproachSpeedMultiplier);
            maximumEngageSpeedMultiplier = Mathf.Max(minimumEngageSpeedMultiplier, maximumEngageSpeedMultiplier);
            laneChangeSpeedBoostChance = Mathf.Clamp01(laneChangeSpeedBoostChance);
            laneChangeSpeedBoostMultiplier = Mathf.Max(0f, laneChangeSpeedBoostMultiplier);
            laneChangeSpeedBoostDuration = Mathf.Max(0f, laneChangeSpeedBoostDuration);
            playerDamageVoxelsMin = Mathf.Max(1, playerDamageVoxelsMin);
            playerDamageVoxelsMax = Mathf.Max(playerDamageVoxelsMin, playerDamageVoxelsMax);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
