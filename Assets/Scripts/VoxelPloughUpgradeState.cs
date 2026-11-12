using UnityEngine;

namespace VoxelRacer
{
    public static class VoxelPloughUpgradeState
    {
        public const string InstanceName = "Front Plough";
        public static VoxelPloughTuning InstalledTuning { get; private set; }
        public static bool IsPurchased => InstalledTuning != null;
        public static void BeginNewRun() => InstalledTuning = null;
        public static bool IsEquipped(VoxelPloughTuning tuning) => tuning != null &&
            InstalledTuning != null && InstalledTuning.upgradeLevel >= tuning.upgradeLevel;
        public static bool CanPurchase(VoxelPloughTuning tuning, VoxelCarDefinition car) =>
            tuning != null && tuning.Fits(car) && !IsEquipped(tuning);
        public static bool TryPurchase(VoxelPloughTuning tuning, VoxelCarDefinition car)
        {
            if (!CanPurchase(tuning, car) || !VoxelCurrencyState.TrySpend(tuning.purchasePrice)) return false;
            InstalledTuning = tuning;
            return true;
        }
        public static void ApplyTo(Transform car, VoxelCarDefinition definition)
        {
            var tuning = InstalledTuning;
            if (car != null && tuning != null && tuning.Fits(definition)) CreateVisual(car, tuning);
        }
        internal static void GrantForDevelopment(VoxelPloughTuning tuning, VoxelCarDefinition car)
        {
            if (CanPurchase(tuning, car)) InstalledTuning = tuning;
        }
        // Shared by runtime and the isolated fit preview. No run-state changes.
        public static GameObject CreateVisual(Transform car, VoxelPloughTuning tuning)
        {
            if (car == null || tuning == null || tuning.ploughPrefab == null) return null;
            var existing = car.Find(InstanceName);
            if (existing != null)
            {
                var fitted = existing.GetComponent<VoxelPloughMount>()?.Tuning;
                if (fitted != null && fitted.upgradeLevel >= tuning.upgradeLevel) return existing.gameObject;
            }
            var instance = Object.Instantiate(tuning.ploughPrefab, car);
            instance.name = InstanceName;
            instance.transform.localPosition = tuning.mountPosition;
            instance.transform.localRotation = Quaternion.Euler(tuning.mountRotation);
            instance.transform.localScale = tuning.mountScale;
            var mount = instance.GetComponent<VoxelPloughMount>() ?? instance.AddComponent<VoxelPloughMount>();
            mount.Configure(tuning);
            if (existing != null)
            {
                // Preserve sibling-index paths for damageable parts next to the front mount.
                instance.transform.SetSiblingIndex(existing.GetSiblingIndex());
                existing.gameObject.SetActive(false);
                existing.SetParent(null, false);
                if (Application.isPlaying) Object.Destroy(existing.gameObject);
                else Object.DestroyImmediate(existing.gameObject);
            }
            return instance;
        }
        public static bool HasMountedPlough(Transform car)
        {
            if (InstalledTuning == null || car == null) return false;
            var mount = car.Find(InstanceName);
            return mount != null && mount.gameObject.activeInHierarchy &&
                mount.TryGetComponent(out VoxelPloughMount fitted) && fitted.isActiveAndEnabled && fitted.Tuning == InstalledTuning;
        }
        public static bool IsFrontContact(Transform car, Vector3 playerToContact)
        {
            if (!HasMountedPlough(car)) return false;
            var local = car.InverseTransformDirection(playerToContact);
            return local.z > .001f && local.z > Mathf.Abs(local.x);
        }
        public static float ImpactDamage(float damage, Transform car, Vector3 playerToContact)
        {
            var tuning = InstalledTuning;
            return damage * (tuning != null && IsFrontContact(car, playerToContact)
                ? 1f + Mathf.Max(0, tuning.impactDamageBonusPercent) / 100f : 1f);
        }
        public static float PlayerDamage(float damage, Transform car, Vector3 playerToContact)
        {
            var tuning = InstalledTuning;
            return damage * (tuning != null && IsFrontContact(car, playerToContact)
                ? 1f - Mathf.Clamp01(tuning.playerDamageReductionPercent / 100f) : 1f);
        }
    }
}
