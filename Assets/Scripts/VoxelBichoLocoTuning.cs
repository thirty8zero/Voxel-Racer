using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Boss Attacks/El Bicho Loco", fileName = "BichoLocoAttackTuning")]
    public sealed class VoxelBichoLocoTuning : VoxelBossAttackDefinition
    {
        [Header("Hopper Health")]
        [Tooltip("Combined share of the boss's total health reserved for all four hoppers. Divided equally; the remainder belongs to the shielded body.")]
        [Range(1, 95)] public float combinedHopperHealthPercent = 40;
        [Min(.1f)] public float healthBarWidth = 2.5f;
        [Min(.05f)] public float healthBarHeight = .22f;
        [Tooltip("Height above each hopper roof, in world metres.")]
        [Min(0)] public float healthBarClearance = .65f;

        [Header("Sequential Mine Drops")]
        public VoxelMineLayerTuning mineTuning;
        [Tooltip("Scaled seconds between mines. If the lamps are in their off interval when due, wait for the next lit hopper. No catch-up burst after a long frame.")]
        [Min(.1f)] public float mineDropInterval = 2.5f;
        [Tooltip("Scaled seconds per lamp step. Surviving hoppers cycle left to right; destroyed slots are skipped.")]
        [Min(.05f)] public float lightStepDuration = .45f;
        [Tooltip("Fraction of each step the selected lamp is lit; the remainder is off.")]
        [Range(.05f, 1)] public float lightOnFraction = .65f;
        [Min(0)] public float mineRearClearance = .3f;

        [Header("Approach And Retreat")]
        [Tooltip("How many metres per second the boss normally loses on the moving player.")]
        [Min(.1f)] public float approachClosingSpeed = 3f;
        [Tooltip("Minimum preferred gap between vehicle centres; physical rear-body clearance also applies.")]
        [Min(5)] public float minimumApproachDistance = 28f;
        [Min(1)] public float retreatDistance = 160f;
        [Min(.1f)] public float minimumRetreatInterval = 28f;
        [Min(.1f)] public float maximumRetreatInterval = 38f;
        [Tooltip("Speed advantage over the player during a retreat, in metres per second.")]
        [Min(.1f)] public float retreatSpeedAdvantage = 18f;

        [Header("Body Phase")]
        [Tooltip("After the last hopper is destroyed, use Psycho Bug hunting, shooting windows, evasions and side rams instead of the normal approach/160m retreat cycle. Can be switched during Play Mode.")]
        public bool usePsychoBehaviourAfterHoppers;
        [Tooltip("Independent Psycho Bug behaviour for this boss's exposed body. Empty uses the ordinary Psycho Bug behaviour as a read-only fallback. The boss keeps its size, health, damage/destruction settings and mission identity.")]
        public VoxelPsychoBugTuning bodyPhaseBehaviour;

        [Header("Hopper Destruction")]
        [Tooltip("Absolute world scale of each hopper explosion, independent of the shared effect prefab size.")]
        [Min(.1f)] public float explosionScale = 2.5f;
        [Min(0)] public int maximumDebris = 28;
        [Min(0)] public float debrisOutwardSpeed = 10f;
        [Min(0)] public float debrisUpwardSpeed = 9f;
        [Min(.1f)] public float debrisLifetime = 4f;
    }
}
