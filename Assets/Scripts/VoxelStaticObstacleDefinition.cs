using UnityEngine;

namespace VoxelRacer
{
    public enum VoxelStaticObstacleType
    {
        VoxelBox,
        Pothole,
        FuelDrums
    }

    /// <summary>Reusable behaviour tuning for one selectable static road obstacle type.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Static Obstacles/Definition", fileName = "VoxelStaticObstacle")]
    public sealed class VoxelStaticObstacleDefinition : ScriptableObject
    {
        public string displayName = "Static Obstacle";
        public VoxelStaticObstacleType obstacleType;

        [Header("Player Impact")]
        [Min(1)] public int playerDamageVoxelsMin = 8;
        [Min(1)] public int playerDamageVoxelsMax = 12;

        [Header("Weapon Damage")]
        [Tooltip("Projectile hits required to destroy a box or detonate one fuel drum.")]
        [Min(1)] public int hitPoints = 3;
        [Tooltip("Randomness used when choosing a voxel from the rearmost surface struck by a bullet. Higher values make damage less row-by-row.")]
        [Min(0f)] public float rearSurfaceHitRandomness = 0.8f;
        [Min(0.01f)] public float weaponDebrisScale = 0.65f;
        [Min(0.05f)] public float weaponDebrisLifetime = 1.1f;
        [Min(0f)] public float weaponDebrisForwardForce = 6f;
        [Min(0f)] public float weaponDebrisUpwardForce = 2.2f;
        [Min(0f)] public float weaponDebrisSpreadForce = 1.2f;

        [Header("Explosion")]
        [Tooltip("Size multiplier applied to the shared VoxelDestructionExplosion effect.")]
        [Min(0.1f)] public float explosionEffectScale = 1f;
        [Tooltip("Maximum remaining voxels detached by box explosions. Fuel drums always detach every remaining voxel.")]
        [Min(1)] public int explosionDebrisCount = 18;
        [Min(0.01f)] public float explosionDebrisScale = 0.7f;
        [Min(0.05f)] public float explosionDebrisLifetime = 1.5f;
        [Min(0f)] public float explosionForwardForce = 8f;
        [Min(0f)] public float explosionUpwardForce = 4f;
        [Min(0f)] public float explosionSpreadForce = 3f;
        [Min(0.1f)] public float destroyedLifetime = 1.8f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            playerDamageVoxelsMax = Mathf.Max(playerDamageVoxelsMin, playerDamageVoxelsMax);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }

    [System.Serializable]
    public sealed class VoxelStaticObstacleSpawnEntry
    {
        public VoxelStaticObstacleDefinition obstacle;
        [Min(0f)] public float spawnWeight = 1f;
    }
}
