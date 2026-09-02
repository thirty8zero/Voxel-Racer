using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Persistent tuning shared by the generated traffic-car obstacles.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Obstacle Car Tuning", fileName = "VoxelObstacleCarTuning")]
    public sealed class VoxelObstacleCarTuning : ScriptableObject
    {
        [Min(1f)] public float spawnDistanceAhead = 65f;
        [Range(0f, 1f)] public float obstacleCarSpawnChance = 0.5f;
        [Range(0f, 1f)] public float oppositeDirectionChance = 0.5f;
        [Tooltip("Chance that a spawned traffic vehicle becomes the black enemy interceptor.")]
        [Range(0f, 1f)] public float enemyCarSpawnChance = 0.25f;
        [Tooltip("Number of objects created across different free lanes at each spawn opportunity.")]
        [Min(1)] public int minimumObjectsPerWave = 2;
        [Min(1)] public int maximumObjectsPerWave = 3;
        [Tooltip("Maximum speed difference allowed when civilian vehicles share a lane. New vehicles match the existing lane speed.")]
        [Min(0f)] public float sameLaneCivilianSpeedTolerance = 1f;
        [Tooltip("Extra distance inserted between objects created in the same wave. This prevents simultaneous hazards from forming an unavoidable wall.")]
        [Min(0f)] public float minimumWaveObjectDistanceOffset = 12f;
        [Min(0f)] public float maximumWaveObjectDistanceOffset = 20f;

        [Tooltip("Only obstacles in this list can spawn on this track. Spawn Weight is relative to the other entries.")]
        public VoxelStaticObstacleSpawnEntry[] staticObstacleSpawns;

        // Retained solely for migration from the original pothole implementation.
        [HideInInspector] [Range(0f, 1f)] public float potholeSpawnChance = 0.35f;
        [HideInInspector] [Min(1)] public int potholePlayerDamageVoxelsMin = 10;
        [HideInInspector] [Min(1)] public int potholePlayerDamageVoxelsMax = 14;

        public Color[] paintColours =
        {
            new Color(0.80f, 0.07f, 0.10f),
            new Color(0.98f, 0.55f, 0.06f),
            new Color(0.12f, 0.70f, 0.32f),
            new Color(0.55f, 0.18f, 0.82f)
        };

        [Range(0f, 1f)] public float semiTrailerSpawnChance = 0.35f;
        [Tooltip("Combat durability used by the regular traffic car.")]
        public VoxelEnemyVehicleTuning trafficCarEnemyTuning;
        [Tooltip("Combat durability used by the semi-trailer.")]
        public VoxelEnemyVehicleTuning semiTrailerEnemyTuning;

        [Tooltip("Civilian vehicles choose all phase speeds from the player's maximum speed. These legacy absolute speed values are retained for existing assets but are no longer used.")]
        [HideInInspector] [Min(0f)] public float sameDirectionSpeedMin = 10f;
        [HideInInspector] [Min(0f)] public float sameDirectionSpeedMax = 18f;
        [HideInInspector] [Min(0f)] public float oppositeDirectionSpeedMin = 22f;
        [HideInInspector] [Min(0f)] public float oppositeDirectionSpeedMax = 32f;

        [Tooltip("Distance ahead of the player at which civilian vehicles switch to approach speed.")]
        [Min(0f)] public float approachSpeedDistance = 100f;
        [Tooltip("Distance ahead of the player at which civilian vehicles switch to engage speed.")]
        [Min(0f)] public float engageSpeedDistance = 0f;

        [Min(0f)] public float sameDirectionSpawnSpeedMultiplierMin = 0.31f;
        [Min(0f)] public float sameDirectionSpawnSpeedMultiplierMax = 0.56f;
        [Min(0f)] public float sameDirectionApproachSpeedMultiplierMin = 0.31f;
        [Min(0f)] public float sameDirectionApproachSpeedMultiplierMax = 0.56f;
        [Min(0f)] public float sameDirectionEngageSpeedMultiplierMin = 0.31f;
        [Min(0f)] public float sameDirectionEngageSpeedMultiplierMax = 0.56f;

        [Min(0f)] public float oncomingSpawnSpeedMultiplierMin = 0.69f;
        [Min(0f)] public float oncomingSpawnSpeedMultiplierMax = 1f;
        [Min(0f)] public float oncomingApproachSpeedMultiplierMin = 0.69f;
        [Min(0f)] public float oncomingApproachSpeedMultiplierMax = 1f;
        [Min(0f)] public float oncomingEngageSpeedMultiplierMin = 0.69f;
        [Min(0f)] public float oncomingEngageSpeedMultiplierMax = 1f;

        [Min(0f)] public float wheelSpinDegreesPerUnit = 125f;

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

        // Debris is now tuned per individual vehicle in VoxelEnemyVehicleTuning.
        // Keep the old serialized fields only so existing track assets migrate safely.
        [HideInInspector] [Min(0)] public int debrisVoxelsPerDamagedVoxel = 2;
        [HideInInspector] [Min(0f)] public float explosionSpawnOffset = 0.45f;
        [HideInInspector] [Min(0f)] public float explosionUpwardBias = 0.75f;
        [HideInInspector] [Min(0f)] public float explosionForwardForceMin = 7f;
        [HideInInspector] [Min(0f)] public float explosionForwardForceMax = 10f;
        [HideInInspector] [Min(0f)] public float explosionUpwardForce = 2.5f;
        [HideInInspector] [Min(0f)] public float explosionSpreadForce = 1.5f;

        public static VoxelObstacleCarTuning Load() => Resources.Load<VoxelObstacleCarTuning>("FallbackTrafficTuning");

#if UNITY_EDITOR
        private void OnValidate()
        {
            approachSpeedDistance = Mathf.Max(engageSpeedDistance, approachSpeedDistance);
            sameDirectionSpawnSpeedMultiplierMax = Mathf.Max(sameDirectionSpawnSpeedMultiplierMin, sameDirectionSpawnSpeedMultiplierMax);
            sameDirectionApproachSpeedMultiplierMax = Mathf.Max(sameDirectionApproachSpeedMultiplierMin, sameDirectionApproachSpeedMultiplierMax);
            sameDirectionEngageSpeedMultiplierMax = Mathf.Max(sameDirectionEngageSpeedMultiplierMin, sameDirectionEngageSpeedMultiplierMax);
            oncomingSpawnSpeedMultiplierMax = Mathf.Max(oncomingSpawnSpeedMultiplierMin, oncomingSpawnSpeedMultiplierMax);
            oncomingApproachSpeedMultiplierMax = Mathf.Max(oncomingApproachSpeedMultiplierMin, oncomingApproachSpeedMultiplierMax);
            oncomingEngageSpeedMultiplierMax = Mathf.Max(oncomingEngageSpeedMultiplierMin, oncomingEngageSpeedMultiplierMax);
            maximumWaveObjectDistanceOffset = Mathf.Max(minimumWaveObjectDistanceOffset, maximumWaveObjectDistanceOffset);
            potholePlayerDamageVoxelsMax = Mathf.Max(potholePlayerDamageVoxelsMin, potholePlayerDamageVoxelsMax);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
