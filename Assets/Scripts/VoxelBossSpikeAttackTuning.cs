using UnityEngine;
using UnityEngine.Serialization;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Boss Attacks/Spike Brake Attack", fileName = "BossSpikeAttackTuning")]
    public sealed class VoxelBossSpikeAttackTuning : VoxelBossAttackDefinition
    {
        [Header("Attack")]
        [Range(0, 1)] public float chance = .45f;
        [Min(1)] public float checkInterval = 12;
        [Tooltip("Doors open, then spikes extend, before braking begins.")]
        [Min(.5f)] public float warningDuration = 2;
        [Min(.1f)] public float holdDuration = 2;
        [Tooltip("Seconds the impact throws sparks while the cars separate.")]
        [FormerlySerializedAs("sideRamContactDuration")]
        [Range(.08f, .35f)] public float sideRamSparkDuration = .22f;
        [Tooltip("Seconds of lateral rebound while Vandito starts pulling away.")]
        [Range(.12f, .5f)] public float sideRamReboundDuration = .30f;
        [Min(1)] public float braking = 180;
        [Tooltip("How quickly the attack closes its remaining gap.")]
        [Min(.1f)] public float closingResponse = 6;
        [Tooltip("Maximum closing speed relative to the player, in m/s.")]
        [Min(1)] public float closingSpeed = 60;
        [Tooltip("Distance ahead to reach when the player dodges. Zero brings the boss alongside the player.")]
        [Min(0)] public float missDistance;
        [Tooltip("Player lane-center separation, as a fraction of one lane width, that counts as a successful dodge. Body clearance is checked separately.")]
        [Range(.5f, 1.2f)] public float dodgeLaneFraction = .85f;
        [Min(1)] public float acceleration = 100;
        [Min(1)] public float pullAwaySpeed = 55;
        [Min(20)] public float retreatDistance = 110;
        [Tooltip("Maximum approach duration before the boss abandons the slam and retreats.")]
        [Min(1)] public float approachTimeout = 8;
        [Min(.1f)] public float retractDuration = 1;
        [Tooltip("Player damage in voxel units, applied once per spike hit.")]
        [Min(0)] public int damageMin = 70;
        [Min(0)] public int damageMax = 100;
    }
}
