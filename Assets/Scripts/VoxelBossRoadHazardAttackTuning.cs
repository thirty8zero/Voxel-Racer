using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Boss-specific roadside turret and oil-slick spawn settings.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Boss Attacks/Road Hazards", fileName = "BossRoadHazardAttack")]
    public sealed class VoxelBossRoadHazardAttackTuning : VoxelBossAttackDefinition
    {
        [Header("Roadside Turrets")]
        public VoxelRoadsideTurretTuning turretTuning;
        [Min(.1f)] public float turretSpawnCheckInterval = 5f;
        [Range(0f, 1f)] public float turretSpawnChance = .4f;
        [Min(10f)] public float turretSpawnDistanceAhead = 100f;
        [Min(1)] public int maximumActiveTurrets = 1;

        [Header("Oil Slicks")]
        public VoxelStaticObstacleSpawnEntry[] oilSlickSpawns;
        [Min(.1f)] public float oilSlickSpawnCheckInterval = 5f;
        [Range(0f, 1f)] public float oilSlickSpawnChance = .35f;
        [Min(10f)] public float oilSlickSpawnDistanceAhead = 100f;
        [Min(1)] public int maximumActiveOilSlicks = 2;
        [Min(0f)] public float oilSlickClearance = 12f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            turretSpawnCheckInterval = Mathf.Max(.1f, turretSpawnCheckInterval);
            turretSpawnDistanceAhead = Mathf.Max(10f, turretSpawnDistanceAhead);
            maximumActiveTurrets = Mathf.Max(1, maximumActiveTurrets);
            oilSlickSpawnCheckInterval = Mathf.Max(.1f, oilSlickSpawnCheckInterval);
            oilSlickSpawnDistanceAhead = Mathf.Max(10f, oilSlickSpawnDistanceAhead);
            maximumActiveOilSlicks = Mathf.Max(1, maximumActiveOilSlicks);
            oilSlickClearance = Mathf.Max(0f, oilSlickClearance);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
