using UnityEngine;

namespace VoxelRacer
{
    public enum VoxelProjectileKind { Bullet, Missile }
    /// <summary>Persistent firing and damage settings shared by every instance of one gun type.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Weapons/Gun Tuning", fileName = "VoxelGunTuning")]
    public sealed class VoxelGunTuning : ScriptableObject
    {
        private const float DpsReferenceFrameRate = 60f;

        [Header("Identity")]
        public string displayName = "Basic Hood Gun";
        [TextArea(2, 5), Tooltip("Overview shown in the upgrade dashboard and garage. Leave blank to use the default copy.")]
        public string description;

        [Header("Shop")]
        [Tooltip("Prefab displayed and mounted when this gun is purchased as an upgrade.")]
        public GameObject visualPrefab;
        [Tooltip("Optional restriction for fitted hood guns.")]
        public GameObject compatibleCarPrefab;
        [Tooltip("Higher levels can replace a lower-level gun in the same hood slot.")]
        [Min(1)] public int upgradeLevel = 1;
        [Tooltip("Currency required for each copy purchased in the repair workshop.")]
        [Min(0)] public int purchasePrice = 50;
        [Tooltip("Maximum number of copies of this gun that can be fitted during one run.")]
        [Min(1)] public int maximumPurchases = 2;

        [Header("Firing")]
        [Min(0.01f)] public float shotsPerSecond = 4f;
        [Tooltip("Projectiles emitted whenever this weapon fires.")]
        [Min(1)] public int bulletsPerShot = 1;
        [Tooltip("Available bullets at the beginning of a stage. Set to zero for unlimited ammunition.")]
        [Min(0)] public int ammunitionPerStage;
        [Min(0f)] public float spreadDegrees;

        [Header("Rotary Gun")]
        [Tooltip("Barrel cluster rotation speed in degrees per second while firing.")]
        [Min(0f)] public float barrelSpinDegreesPerSecond;
        [Tooltip("Maximum visible tracer length in metres. Zero uses the standard projectile visual.")]
        [Min(0f)] public float tracerLength;

        [Header("Projectile")]
        [Min(0.01f)] public float projectileSpeed = 70f;
        [Min(0.1f)] public float maximumRange = 100f;
        [Min(0f)] public float damagePerBullet = 1f;
        [Min(0f)] public float areaOfEffectRadius;

        [Header("Missile Flight")]
        public VoxelProjectileKind projectileKind;
        public GameObject missilePrefab;
        [Min(.1f)] public float missileDescentDistance = 14f;
        [Min(.1f)] public float missileCruiseHeight = .65f;
        [Tooltip("Distance inside the launch-side lane boundary, in metres.")]
        [Min(0f)] public float missileLaneEdgeInset = .70f;
        [Tooltip("Follow road bends after settling. Disabled: continue straight along the heading at the end of the descent.")]
        public bool missileFollowRoad;

        [Header("Missile Cooldown")]
        [Tooltip("Use a cooldown in seconds for missiles instead of Shots Per Second.")]
        public bool useMissileCooldown;
        [Tooltip("Time to recharge after each missile firing event. Zero allows firing every frame while held.")]
        [Min(0f)] public float missileCooldownSeconds = 5f;

        public float SecondsPerShot => projectileKind == VoxelProjectileKind.Missile && useMissileCooldown
            ? Mathf.Max(0f, missileCooldownSeconds) : 1f / Mathf.Max(0.01f, shotsPerSecond);

        public int ProjectilesPerShot => Mathf.Max(1, bulletsPerShot);

        /// <summary>Nominal damage per weapon against one target while ammunition lasts and all projectiles hit.</summary>
        public float DamagePerSecond
        {
            get
            {
                if (ammunitionPerStage > 0 && ammunitionPerStage < ProjectilesPerShot) return 0f;
                float interval = SecondsPerShot;
                float shotsPerSecond = interval > 0f ? 1f / interval : DpsReferenceFrameRate;
                return Mathf.Max(0f, damagePerBullet) * ProjectilesPerShot * shotsPerSecond;
            }
        }

        public string DamagePerSecondLabel => $"{DamagePerSecond:0.##} DPS" +
            (SecondsPerShot <= 0f ? $" @ {DpsReferenceFrameRate:0} FPS" : "");

        public bool Fits(VoxelCarDefinition car) => compatibleCarPrefab == null || car?.visualPrefab == compatibleCarPrefab;
        public bool IsRotary => visualPrefab != null && visualPrefab.TryGetComponent(out VoxelGunMount mount) && mount.HasRotor;

#if UNITY_EDITOR
        private void OnValidate()
        {
            bulletsPerShot = Mathf.Max(1, bulletsPerShot);
            purchasePrice = Mathf.Max(0, purchasePrice);
            maximumPurchases = Mathf.Max(1, maximumPurchases);
            upgradeLevel = Mathf.Max(1, upgradeLevel);
            barrelSpinDegreesPerSecond = Mathf.Max(0f, barrelSpinDegreesPerSecond);
            tracerLength = Mathf.Max(0f, tracerLength);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
