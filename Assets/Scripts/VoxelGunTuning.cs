using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Persistent firing and damage settings shared by every instance of one gun type.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Weapons/Gun Tuning", fileName = "VoxelGunTuning")]
    public sealed class VoxelGunTuning : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Basic Hood Gun";

        [Header("Shop")]
        [Tooltip("Prefab displayed and mounted when this gun is purchased as an upgrade.")]
        public GameObject visualPrefab;
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

        [Header("Projectile")]
        [Min(0.01f)] public float projectileSpeed = 70f;
        [Min(0.1f)] public float maximumRange = 100f;
        [Min(0f)] public float damagePerBullet = 1f;
        [Min(0f)] public float areaOfEffectRadius;

        public float SecondsPerShot => 1f / Mathf.Max(0.01f, shotsPerSecond);

#if UNITY_EDITOR
        private void OnValidate()
        {
            bulletsPerShot = Mathf.Max(1, bulletsPerShot);
            purchasePrice = Mathf.Max(0, purchasePrice);
            maximumPurchases = Mathf.Max(1, maximumPurchases);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
