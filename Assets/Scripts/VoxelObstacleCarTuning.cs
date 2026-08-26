using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Persistent tuning shared by the generated traffic-car obstacles.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Obstacle Car Tuning", fileName = "VoxelObstacleCarTuning")]
    public sealed class VoxelObstacleCarTuning : ScriptableObject
    {
        [Header("Spawning")]
        [Min(1f)] public float spawnDistanceAhead = 65f;
        [Range(0f, 1f)] public float obstacleCarSpawnChance = 0.5f;
        [Range(0f, 1f)] public float oppositeDirectionChance = 0.5f;

        [Header("Paint Colours")]
        public Color[] paintColours =
        {
            new Color(0.80f, 0.07f, 0.10f),
            new Color(0.98f, 0.55f, 0.06f),
            new Color(0.12f, 0.70f, 0.32f),
            new Color(0.55f, 0.18f, 0.82f)
        };

        [Header("Traffic Models")]
        [Range(0f, 1f)] public float semiTrailerSpawnChance = 0.35f;

        [Header("Traffic Speed")]
        [Min(0f)] public float sameDirectionSpeedMin = 10f;
        [Min(0f)] public float sameDirectionSpeedMax = 18f;
        [Min(0f)] public float oppositeDirectionSpeedMin = 22f;
        [Min(0f)] public float oppositeDirectionSpeedMax = 32f;
        [Min(0f)] public float wheelSpinDegreesPerUnit = 125f;

        [Header("Impact")]
        [Min(1)] public int playerDamageVoxelsMin = 10;
        [Min(1)] public int playerDamageVoxelsMax = 12;
        [Min(1)] public int obstacleDamageVoxelsMin = 100;
        [Min(1)] public int obstacleDamageVoxelsMax = 150;
        [Min(0f)] public float impactVoxelDamageSurfaceOffset = 1.7f;
        [Min(0f)] public float semiImpactVoxelDamageSurfaceOffset = 4.4f;
        [Min(0f)] public float collisionCooldown = 0.35f;
        [Min(0f)] public float launchForce = 15f;
        [Min(0f)] public float launchUpwardForce = 6f;
        [Min(0f)] public float destroyedLifetime = 2.5f;

        [Header("Obstacle Voxel Debris")]
        [Min(0)] public int debrisVoxelsPerDamagedVoxel = 2;
        [Min(0f)] public float explosionSpawnOffset = 0.45f;
        [Min(0f)] public float explosionUpwardBias = 0.75f;
        [Min(0f)] public float explosionForwardForceMin = 7f;
        [Min(0f)] public float explosionForwardForceMax = 10f;
        [Min(0f)] public float explosionUpwardForce = 2.5f;
        [Min(0f)] public float explosionSpreadForce = 1.5f;

        public static VoxelObstacleCarTuning Load() => Resources.Load<VoxelObstacleCarTuning>("VoxelObstacleCarTuning");

#if UNITY_EDITOR
        private void OnValidate() => VoxelAssetSaveQueue.Request(this);
#endif
    }
}
