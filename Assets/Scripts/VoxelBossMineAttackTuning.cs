using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Boss Attacks/Mine Layer Attack", fileName = "BossMineAttackTuning")]
    public sealed class VoxelBossMineAttackTuning : VoxelBossAttackDefinition
    {
        [Header("Mine Layer")]
        [Tooltip("Mine model, hit area, damage and explosion settings.")]
        public VoxelMineLayerTuning mineTuning;
        [Range(0, 1)] public float dropChance = .6f;
        [Min(.1f)] public float dropInterval = 3;
        [Min(0)] public float armingDelay = .5f;
        [Min(1)] public float lifetime = 25;
        [Tooltip("Fallback damage and explosion values used only when Mine Tuning is empty.")]
        [Min(1)] public int fallbackDamageMin = 60;
        [Min(1)] public int fallbackDamageMax = 80;
        [Min(.1f)] public float fallbackExplosionScale = 2;
    }
}
