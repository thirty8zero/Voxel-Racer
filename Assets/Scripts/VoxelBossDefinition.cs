using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Reusable identity, movement, durability and attack loadout for a boss vehicle.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Boss Definition", fileName = "VoxelBossDefinition")]
    public sealed class VoxelBossDefinition : ScriptableObject
    {
        [Header("Identity & Durability")]
        public string bossName = "RED MENACE";
        public GameObject bossPrefab;
        [Tooltip("Seconds for the boss model to grow from zero to full size. Zero disables the entrance animation.")]
        [Min(0)] public float entranceDuration = .75f;
        [Min(1)] public float health = 2000;
        [Min(.01f)] public float voxelHealth = 8;
        [Min(0)] public int playerCollisionDamageMin = 35;
        [Min(0)] public int playerCollisionDamageMax = 50;
        [Tooltip("Base boss health damage dealt by each player ram. Side rams can receive an additional wheel-spike upgrade bonus.")]
        [Min(0)] public float playerRamDamage = 20;
        [Tooltip("Maximum body pieces detached by one player ram, from any direction. Does not change health damage or the final destruction explosion.")]
        [Min(0)] public int maximumVoxelsRemovedPerRam = 80;

        [Header("Movement")]
        [Min(12)] public float minimumDistanceAhead = 50;
        [Min(12)] public float maximumDistanceAhead = 120;

        [Header("Escape")]
        [Min(1)] public float escapeWarningDistance = 150;
        [Min(2)] public float escapeFailureDistance = 200;
        [Tooltip("Fraction of the player's unboosted top speed. Boss never stops with the player.")]
        [Range(.1f, 1f)] public float minimumCruiseSpeedFraction = .6f;
        [Tooltip("Beyond this gap, hold a fixed speed independent of player boost.")]
        [Min(1)] public float catchUpDistance = 100;
        [Min(1)] public float catchUpResumeDistance = 70;
        [Tooltip("Fixed catch-up speed relative to unboosted top speed. Near 1 keeps the chase moving; boost closes the gap faster.")]
        [Range(.1f, 1f)] public float catchUpSpeedFraction = .95f;
        public float WarningDistance => Mathf.Clamp(escapeWarningDistance, 1, Mathf.Max(2, escapeFailureDistance) - 1);
        public float FailureDistance => Mathf.Max(2, escapeFailureDistance);

        [Header("Manoeuvres")]
        [Tooltip("Maximum speed difference during attack manoeuvres, in metres per second. Catch-up mode ignores this bonus.")]
        [Min(.1f)] public float distanceAdjustmentSpeed = 16;
        [Tooltip("Forward acceleration in metres per second squared.")]
        [Min(.1f)] public float acceleration = 24;
        [Tooltip("Braking deceleration in metres per second squared.")]
        [Min(.1f)] public float braking = 40;
        [Tooltip("Minimum pause after completing a lane change, in seconds.")]
        [Min(.1f)] public float minimumLaneChangeInterval = 1;
        [Tooltip("Maximum pause after completing a lane change, in seconds.")]
        [Min(.1f)] public float maximumLaneChangeInterval = 2;
        [Tooltip("Sideways speed in metres per second.")]
        [Min(.1f)] public float laneChangeSpeed = 6;

        [Header("Attack Loadout")]
        [Tooltip("Reusable attack modules that this boss uses. Remove an entry to disable that attack.")]
        public VoxelBossAttackDefinition[] attacks;

        [Header("Damage Effects")]
        [Range(0, 1)] public float smokeDamageThreshold = .25f;
        [Range(0, 1)] public float fireDamageThreshold = .5f;

        public T GetAttack<T>() where T : VoxelBossAttackDefinition
        {
            if (attacks == null) return null;
            foreach (var attack in attacks)
                if (attack is T typedAttack) return typedAttack;
            return null;
        }

        public void SetAttack(VoxelBossAttackDefinition attack)
        {
            if (attack == null) return;
            var current = attacks ?? System.Array.Empty<VoxelBossAttackDefinition>();
            for (int index = 0; index < current.Length; index++)
            {
                if (current[index] != null && current[index].GetType() == attack.GetType())
                {
                    current[index] = attack;
                    attacks = current;
                    return;
                }
            }
            attacks = new System.Collections.Generic.List<VoxelBossAttackDefinition>(current) { attack }.ToArray();
        }
    }
}
