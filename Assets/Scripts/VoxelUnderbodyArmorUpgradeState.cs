using UnityEngine;

namespace VoxelRacer
{
    public static class VoxelUnderbodyArmorUpgradeState
    {
        public const string InstanceName = "Underbody Mine Armour";
        private static bool purchased;
        public static bool IsPurchased => purchased;
        public static void BeginNewRun() => purchased = false;
        public static bool CanPurchase(VoxelUnderbodyArmorTuning tuning, VoxelCarDefinition car) => !purchased && tuning != null && tuning.Fits(car);
        public static bool TryPurchase(VoxelUnderbodyArmorTuning tuning, VoxelCarDefinition car)
        {
            if (!CanPurchase(tuning, car) || !VoxelCurrencyState.TrySpend(tuning.purchasePrice)) return false;
            purchased = true; return true;
        }
        public static void ApplyTo(Transform car, VoxelCarDefinition definition)
        {
            var tuning = VoxelUnderbodyArmorTuning.Load();
            if (car != null && purchased && tuning != null && tuning.Fits(definition)) CreateVisual(car, tuning);
        }
        internal static void GrantForDevelopment(VoxelUnderbodyArmorTuning tuning, VoxelCarDefinition car)
        { if (tuning != null && tuning.Fits(car)) purchased = true; }

        /// <summary>Shared visual-only mount. Appends a protected kit without changing damage paths or run state.</summary>
        public static GameObject CreateVisual(Transform car, VoxelUnderbodyArmorTuning tuning)
        {
            if (car == null || tuning == null || tuning.armorPrefab == null) return null;
            var existing = car.Find(InstanceName); if (existing != null) return existing.gameObject;
            var instance = Object.Instantiate(tuning.armorPrefab, car); instance.name = InstanceName;
            instance.transform.localPosition = tuning.mountPosition;
            instance.transform.localRotation = Quaternion.Euler(tuning.mountRotation);
            instance.transform.localScale = tuning.mountScale;
            var mount = instance.GetComponent<VoxelUnderbodyArmorMount>() ?? instance.AddComponent<VoxelUnderbodyArmorMount>();
            mount.tuning = tuning;
            return instance;
        }
        public static float MineDamageMultiplier(Transform car)
        {
            if (!purchased || car == null) return 1;
            var mount = car.Find(InstanceName);
            var tuning = mount != null ? mount.GetComponent<VoxelUnderbodyArmorMount>()?.tuning : null;
            return mount != null && mount.gameObject.activeInHierarchy && tuning != null
                ? 1f - Mathf.Clamp01(tuning.mineDamageReductionPercent / 100f) : 1f;
        }
    }
}
