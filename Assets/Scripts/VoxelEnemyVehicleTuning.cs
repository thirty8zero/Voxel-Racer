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

        [Header("Collision")]
        [Tooltip("Lane-space collision width used for a player ram.")]
        [Min(0.1f)] public float collisionHalfWidth = 1.35f;
        [Tooltip("Track-space collision depth used for a player ram. Increase this if vehicle models visibly overlap before impact.")]
        [Min(0.1f)] public float collisionHalfLength = 3.8f;

        [Header("Player Impact")]
        [Tooltip("Random range of player-car voxels damaged when this enemy is rammed.")]
        [Min(1)] public int playerDamageVoxelsMin = 10;
        [Min(1)] public int playerDamageVoxelsMax = 12;

        [Header("Weapon Damage")]
        [Tooltip("Variation applied when choosing a voxel on the enemy's rear surface. Higher values create a less uniform damage pattern.")]
        [Min(0f)] public float rearSurfaceHitRandomness = 1.5f;

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
            playerDamageVoxelsMin = Mathf.Max(1, playerDamageVoxelsMin);
            playerDamageVoxelsMax = Mathf.Max(playerDamageVoxelsMin, playerDamageVoxelsMax);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
