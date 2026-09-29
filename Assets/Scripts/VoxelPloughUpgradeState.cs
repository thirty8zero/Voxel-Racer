using UnityEngine;

namespace VoxelRacer
{
    public static class VoxelPloughUpgradeState
    {
        public const string InstanceName = "Front Plough";
        private static bool purchased;
        public static bool IsPurchased => purchased;
        public static void BeginNewRun() => purchased = false;
        public static bool CanPurchase(VoxelPloughTuning tuning, VoxelCarDefinition car) =>
            !purchased && tuning != null && tuning.Fits(car);
        public static bool TryPurchase(VoxelPloughTuning tuning, VoxelCarDefinition car)
        {
            if (!CanPurchase(tuning, car) || !VoxelCurrencyState.TrySpend(tuning.purchasePrice)) return false;
            purchased = true;
            return true;
        }
        public static void ApplyTo(Transform car, VoxelCarDefinition definition)
        {
            var tuning = VoxelPloughTuning.Load();
            if (car != null && purchased && tuning != null && tuning.Fits(definition)) CreateVisual(car, tuning);
        }
        // Shared by runtime and the isolated fit preview. No run-state changes.
        public static GameObject CreateVisual(Transform car, VoxelPloughTuning tuning)
        {
            if (car == null || tuning == null || tuning.ploughPrefab == null) return null;
            var existing = car.Find(InstanceName);
            if (existing != null) return existing.gameObject;
            var instance = Object.Instantiate(tuning.ploughPrefab, car);
            instance.name = InstanceName;
            instance.transform.localPosition = tuning.mountPosition;
            instance.transform.localRotation = Quaternion.Euler(tuning.mountRotation);
            instance.transform.localScale = tuning.mountScale;
            return instance;
        }
        public static bool IsFrontContact(Transform car, Vector3 playerToContact)
        {
            if (!purchased || car == null) return false;
            var mount = car.Find(InstanceName);
            if (mount == null || !mount.gameObject.activeInHierarchy) return false;
            var local = car.InverseTransformDirection(playerToContact);
            return local.z > .001f && local.z > Mathf.Abs(local.x);
        }
        public static float ImpactDamage(float damage, Transform car, Vector3 playerToContact)
        {
            var tuning = VoxelPloughTuning.Load();
            return damage * (tuning != null && IsFrontContact(car, playerToContact)
                ? 1f + Mathf.Max(0, tuning.impactDamageBonusPercent) / 100f : 1f);
        }
        public static float PlayerDamage(float damage, Transform car, Vector3 playerToContact)
        {
            var tuning = VoxelPloughTuning.Load();
            return damage * (tuning != null && IsFrontContact(car, playerToContact)
                ? 1f - Mathf.Clamp01(tuning.playerDamageReductionPercent / 100f) : 1f);
        }
    }
}
