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
        [Tooltip("Authored body width used to scale this model to 1.8 lane widths. Attached hoppers do not contribute to this measurement.")]
        [Min(.1f)] public float referenceBodyWidth = 2.23f;
        [Tooltip("Body collision half-length before lane-based model scaling. Attached hopper contacts are tested separately.")]
        [Min(.1f)] public float bodyCollisionHalfLength = 2.65f;
        [Tooltip("Seconds for the boss model to grow from zero to full size. Zero disables the entrance animation.")]
        [Min(0)] public float entranceDuration = .75f;
        [Tooltip("Extra track distance beyond the normal spawn gap where the boss begins its entrance. It moves toward the normal gap while scaling up.")]
        [Min(0f)] public float entranceApproachDistance = 100f;

        [Header("Introduction Poster")]
        [Tooltip("Optional independent layered introduction. Takes priority over the single-poster settings below when it contains valid panels.")]
        public VoxelBossIntroductionSequence introductionSequence;
        [Tooltip("Optional reveal after the radar objective and natural traffic clearance, before spawning this boss. Empty skips the poster.")]
        public Texture2D introductionPoster;
        [Min(.01f)] public float introductionSlideInDuration = .55f;
        [Tooltip("Seconds fully on screen, including the brief landing shake. Mission countdown and player input are held throughout the reveal.")]
        [Min(0)] public float introductionHoldDuration = 3f;
        [Min(.01f)] public float introductionSlideOutDuration = .45f;
        [Min(0)] public float introductionShakeDuration = .2f;
        [Tooltip("Landing shake strength in 1080p reference pixels. Zero disables shake.")]
        [Min(0)] public float introductionShakePixels = 10f;

        public bool HasIntroduction => (introductionSequence != null && introductionSequence.HasPanels) || introductionPoster != null;
        public float IntroductionAssembledAt => introductionSequence != null && introductionSequence.HasPanels
            ? introductionSequence.AssembledAt : Mathf.Max(.01f, introductionSlideInDuration);
        public float IntroductionDuration => introductionSequence != null && introductionSequence.HasPanels
            ? introductionSequence.Duration : IntroductionAssembledAt + Mathf.Max(0, introductionHoldDuration) + Mathf.Max(.01f, introductionSlideOutDuration);

        [Header("Health & Ram Damage")]
        [Min(1)] public float health = 2000;
        [Min(.01f)] public float voxelHealth = 8;
        [Min(0)] public int playerCollisionDamageMin = 35;
        [Min(0)] public int playerCollisionDamageMax = 50;
        [Tooltip("Base boss health damage dealt by each player ram. Side rams can receive an additional wheel-spike upgrade bonus.")]
        [Min(0)] public float playerRamDamage = 20;
        [Tooltip("Maximum body pieces detached by one player ram, from any direction. Does not change health damage or the final destruction explosion.")]
        [Min(0)] public int maximumVoxelsRemovedPerRam = 80;

        [Header("Gun Damage")]
        [Tooltip("Multiplier for health lost to bullets. Individual voxel damage still uses the bullet's original damage and Voxel Health above.")]
        [Min(0)] public float gunHealthDamageMultiplier = 1f;
        [Min(0)] public float rearSurfaceHitRandomness = 3f;

        [Header("Ram Voxel Damage")]
        [Tooltip("Boss-only body pieces requested per ram, before boost/plough bonuses and Maximum Voxels Removed Per Ram. Independent of track traffic damage.")]
        [Min(0)] public int ramDamageVoxelsMin = 100;
        [Min(0)] public int ramDamageVoxelsMax = 200;

        [Header("Missile Damage")]
        [Tooltip("Use an exact requested number of nearby destructible pieces per qualifying missile hit instead of only carving pieces inside the blast sphere. Health is still damaged once. Protected structure and active attack rigs remain protected.")]
        public bool useFixedMissileVoxelDamage;
        [Min(0)] public int missileVoxelsRemoved = 50;
        [Tooltip("Multiplier for health lost per missile. Zero disables missile damage to this boss.")]
        [Min(0)] public float missileHealthDamageMultiplier = 1f;
        [Tooltip("Scales this boss's blast-carving radius only. Fixed-count hits still need direct contact or overlapping blast geometry.")]
        [Min(0)] public float missileBlastRadiusMultiplier = 1f;
        [Min(0)] public int maximumMissileDebris = 24;

        [Header("Destruction")]
        [Tooltip("Destructible body pieces removed when this boss dies, also limited by Maximum Destruction Removal Fraction. Protected structure is excluded; spikes detach separately.")]
        [Min(0)] public int onDestroyVoxelsRemoved = 200;
        [Tooltip("Portion of the remaining destructible body that can be removed by death. 0.65 means 65%; use 1 to allow the full requested count.")]
        [Range(0, 1)] public float maximumDestructionRemovalFraction = .65f;
        [Tooltip("Physical flying pieces spawned from the removed bodywork. Raising voxel removal does not require raising this budget.")]
        [Min(0)] public int maximumDestructionDebris = 200;
        [Min(.1f)] public float destructionExplosionScale = 2f;
        [Tooltip("Use an absolute explosion size instead of multiplying the shared effect prefab's global scale. Off preserves the previous appearance.")]
        public bool useWorldScaleForDestructionExplosion;
        [Min(.1f)] public float destroyedLifetime = 4f;

        [Header("Weapon Debris")]
        public VoxelBossDebrisSettings weaponDebris = new();
        [Header("Ram Debris")]
        public VoxelBossDebrisSettings ramDebris = new() { upwardForce = 30, spreadForce = 15 };
        [Header("Destruction Debris")]
        public VoxelBossDebrisSettings destructionDebris = new();

        [Header("Spike Destruction")]
        public bool detachSpikesOnDestruction = true;
        [Min(0)] public float spikeLaunchForce = 20f;
        [Min(0)] public float spikeUpwardForce = 12f;
        [Min(0)] public float spikeSideForce = 6f;
        [Min(0)] public float spikeSpreadForce = 3f;
        [Min(0)] public float spikeSpinSpeed = 120f;
        [Range(0, 1)] public float spikeVelocityInheritance = .35f;
        [Min(.1f)] public float spikeDebrisLifetime = 6f;
        [Min(0)] public float spikeFireSmokeDuration = 2f;
        [Min(.01f)] public float spikeFireSmokeScale = 1f;
        [Tooltip("Absolute world scale of each spike's one-shot burst, independent of the shared explosion prefab's global size.")]
        [Min(.1f)] public float spikeExplosionScale = .8f;

        [Header("Mission Completion")]
        public bool overrideCompletionSlowMotion = true;
        [Tooltip("Real-time seconds of slow motion after defeating this boss. Zero disables the effect.")]
        [Min(0)] public float completionSlowMotionDuration = 1.5f;
        [Tooltip("Game speed multiplier relative to the clock before completion. 0.25 is quarter speed; 1 disables slowdown.")]
        [Range(.01f, 1)] public float completionSlowMotionTimeScale = .25f;
        public bool overrideFinishStopDuration;
        [Tooltip("Scaled game seconds for the player car to coast to a stop. Disable the override to retain braking-based timing.")]
        [Min(.01f)] public float finishStopDuration = 3f;

        [Header("Destruction Camera")]
        public bool focusCameraOnDestruction;
        [Tooltip("Real-time seconds spent viewing the defeated boss before the normal player finish camera begins.")]
        [Min(0)] public float destructionCameraDuration = 3f;
        [Min(0)] public float destructionCameraBlendDuration = .5f;
        public Vector3 destructionCameraOffset = new(16, 10, -20);
        [Min(0)] public float destructionCameraLookHeight = 4f;
        [Range(10, 90)] public float destructionCameraFieldOfView = 50f;

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

        /// <summary>Build independent runtime tuning without inheriting any ordinary enemy asset.</summary>
        public VoxelEnemyVehicleTuning CreateRuntimeVehicleTuning()
        {
            var tuning = CreateInstance<VoxelEnemyVehicleTuning>();
            tuning.name = bossName + " Runtime Tuning";
            tuning.displayName = bossName; tuning.modelPrefab = bossPrefab;
            tuning.vehicleHealth = Mathf.Max(1, health); tuning.voxelHealth = Mathf.Max(.01f, voxelHealth);
            tuning.playerDamageVoxelsMin = playerCollisionDamageMin; tuning.playerDamageVoxelsMax = playerCollisionDamageMax;
            tuning.playerRamDamage = playerRamDamage; tuning.rearSurfaceHitRandomness = rearSurfaceHitRandomness;
            tuning.laneChangeChance = 0; tuning.destructionScore = 0;
            tuning.weaponDebrisScale = weaponDebris.scale; tuning.weaponDebrisForwardForceMin = weaponDebris.forwardForceMin;
            tuning.weaponDebrisForwardForceMax = weaponDebris.forwardForceMax; tuning.weaponDebrisUpwardForce = weaponDebris.upwardForce;
            tuning.weaponDebrisSpreadForce = weaponDebris.spreadForce; tuning.weaponDebrisLifetime = weaponDebris.lifetime;
            tuning.ramDebrisScale = ramDebris.scale; tuning.ramDebrisForwardForceMin = ramDebris.forwardForceMin;
            tuning.ramDebrisForwardForceMax = ramDebris.forwardForceMax; tuning.ramDebrisUpwardForce = ramDebris.upwardForce;
            tuning.ramDebrisSpreadForce = ramDebris.spreadForce; tuning.ramDebrisLifetime = ramDebris.lifetime;
            tuning.explosionDebrisScale = destructionDebris.scale; tuning.explosionForwardForceMin = destructionDebris.forwardForceMin;
            tuning.explosionForwardForceMax = destructionDebris.forwardForceMax; tuning.explosionUpwardForce = destructionDebris.upwardForce;
            tuning.explosionSpreadForce = destructionDebris.spreadForce; tuning.explosionDebrisLifetime = destructionDebris.lifetime;
            tuning.explosionEffectScale = destructionExplosionScale; tuning.destroyedLifetime = destroyedLifetime;
            tuning.explosionVoxelCount = maximumDestructionDebris;
            tuning.maximumExplosionVoxelRemovalPercent = maximumDestructionRemovalFraction;
            return tuning;
        }

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

    [System.Serializable]
    public sealed class VoxelBossDebrisSettings
    {
        [Min(.01f)] public float scale = .8f;
        [Min(0)] public float forwardForceMin = 20f;
        [Min(0)] public float forwardForceMax = 30f;
        [Min(0)] public float upwardForce = 20f;
        [Min(0)] public float spreadForce = 10f;
        [Min(.1f)] public float lifetime = 4f;
    }
}
