using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Optional advanced driving module; identity, model and durability remain on the enemy tuning.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Enemies/Psycho Bug Behaviour", fileName = "PsychoBugBehaviour")]
    public sealed class VoxelPsychoBugTuning : ScriptableObject
    {
        [Header("Movement")]
        [Min(.05f)] public float decisionInterval = .2f;
        [Min(0)] public float acceleration = 18f;
        [Min(0)] public float braking = 26f;
        [Min(0)] public float maximumSpeedMultiplier = 1.35f;
        [Tooltip("Independent cruising speed as a fraction of the player's normal top speed. Player braking does not lower this speed; traffic can still make the Bug brake.")]
        [Min(0)] public float cruiseSpeedMultiplier = .95f;
        [Tooltip("Minimum forward driving/positioning speed in m/s. Obstacles can still force braking or a stop. Set to zero to allow parking alongside a stopped player; optional reverse positioning remains available.")]
        [Min(0)] public float minimumDrivingSpeed = 6f;
        [Min(0)] public float positioningSpeed = 28f;
        [Min(.01f)] public float positioningResponse = 1.8f;
        [Tooltip("Damps relative approach speed so returning to the player does not overshoot and repeatedly miss attack alignment.")]
        [Min(0)] public float positioningDamping = .6f;
        [Min(.1f)] public float laneChangeSpeed = 7f;
        [Tooltip("Relative positioning normally uses braking. Enable actual reverse motion only if desired.")]
        public bool allowReverse;
        [Min(0)] public float maximumReverseSpeed = 5f;

        [Header("Engagement Range")]
        [Tooltip("Allows the Bug time to catch up after a player escape before ordinary distance cleanup removes it.")]
        [Min(0)] public float maximumDistanceBehind = 75f;
        [Tooltip("Never shorter than the track's spawn lead plus its normal margin.")]
        [Min(0)] public float maximumDistanceAhead = 200f;

        [Header("Traffic Awareness")]
        public bool weaveThroughTraffic = true;
        [Range(0, 1)] public float weaveChance = .55f;
        [Min(0)] public float trafficClearance = 2f;
        [Min(.1f)] public float trafficPredictionSeconds = 1.2f;
        [Min(.1f)] public float trafficLookAhead = 26f;
        [Min(.01f)] public float trafficScanInterval = .15f;

        [Header("Shooting Windows")]
        [Tooltip("Regularly holds the player's lane ahead, with no dodging or attacks during the exposure.")]
        public bool shootingWindows = true;
        [Min(6)] public float shootingDistanceAhead = 14f;
        [Min(0)] public float minimumShootingDuration = 1f;
        [Min(0)] public float maximumShootingDuration = 1.5f;
        [Min(0)] public float maximumTimeBetweenShootingWindows = 12f;

        [Header("Side Ram")]
        public bool sideRamEnabled = true;
        [Range(0, 1)] public float sideRamChance = 1f;
        [Tooltip("When cruising or returning to a shooting position, becoming level in the adjacent lane starts attack alignment without a random chance roll. Still respects cooldown, recovery, exposure and the attacker limit.")]
        public bool ramOnPassingAlignment = true;
        [Tooltip("Minimum speed in m/s for lining up or committing a ram. A slower player cancels alignment; a Bug slowed below this during warning/slam abandons the attack and returns to independent cruising. Zero disables this guard.")]
        [Min(0)] public float minimumRamSpeed = 6f;
        [Min(.1f)] public float alignmentTolerance = .8f;
        [Tooltip("Maximum speed difference before the warning begins, preventing the Bug from overshooting a braking player during its wind-up.")]
        [Min(.1f)] public float ramSpeedTolerance = 1f;
        [Min(0)] public float alignmentHoldDuration = .15f;
        [Min(.1f)] public float positioningTimeout = 14f;
        [Min(.05f)] public float warningDuration = 1f;
        [Min(0)] public float pullAwayDistance = .18f;
        [Min(.1f)] public float slamSpeed = 14f;
        [Min(.05f)] public float slamTimeout = .65f;
        [Min(0)] public float minimumAttackCooldown = 1f;
        [Min(0)] public float maximumAttackCooldown = 2f;
        [Min(0)] public int sideRamDamageMin = 6;
        [Min(0)] public int sideRamDamageMax = 10;
        [Tooltip("Player collision envelope, added to this enemy's physical half width/length for swept contact.")]
        [Min(.1f)] public float playerCollisionHalfWidth = 1.3f;
        [Min(.1f)] public float playerCollisionHalfLength = 2.5f;
        [Min(1)] public int maximumSimultaneousRams = 1;

        [Header("Recovery & Interruption")]
        [Min(0)] public float recoveryDuration = .6f;
        [Min(0)] public float recoveryDistanceAhead = 12f;
        public bool playerRamInterrupts = true;
        [Min(0)] public float staggerDuration = 1.25f;
        [Range(0, 1)] public float feintChance = .05f;

        [Header("Damage Evasion")]
        public bool damageEvasion = true;
        [Range(0, 1)] public float damageEvasionChance = .35f;
        [Min(0)] public float damageReactionDelay = .5f;
        [Min(0)] public float damageEvasionCooldown = 4f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            maximumShootingDuration = Mathf.Max(minimumShootingDuration, maximumShootingDuration);
            maximumAttackCooldown = Mathf.Max(minimumAttackCooldown, maximumAttackCooldown);
            sideRamDamageMax = Mathf.Max(sideRamDamageMin, sideRamDamageMax);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
