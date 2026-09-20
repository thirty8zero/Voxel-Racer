using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Enemies/Mine Layer", fileName = "MineLayerAttackTuning")]
    public sealed class VoxelMineLayerTuning : ScriptableObject
    {
        [Tooltip("Fraction of enemy spawns replaced by mine layers.")]
        [Range(0, 1)] public float enemySelectionChance = .25f;
        [Range(0, 1)] public float dropChance = .5f;
        [Min(.1f)] public float dropInterval = 3f;
        public GameObject minePrefab;
        [Header("Lane Change After Dropping a Mine")]
        [Tooltip("Chance per successfully dropped mine to request a safe adjacent lane. 0 disables this behaviour; 1 always requests it.")]
        [Range(0, 1)] public float postDropLaneChangeChance = .5f;
        [Tooltip("Seconds after dropping the mine before attempting the lane change. If blocked, waits for a safe lane. Uses the vehicle's existing lane-change speed and burst settings.")]
        [Min(0)] public float postDropLaneChangeDelay = 1f;
        [Header("Mine Behaviour")]
        [Min(0)] public float armingDelay = .5f;
        [Min(1)] public float lifetime = 25f;
        [Min(1)] public int playerDamageVoxelsMin = 60;
        [Min(1)] public int playerDamageVoxelsMax = 80;
        [Min(.1f)] public float collisionHalfWidth = 1.475f;
        [Min(.1f)] public float collisionHalfLength = 2.325f;
        [Min(.1f)] public float explosionScale = 1.3f;
    }
}
