using UnityEngine;

namespace VoxelRacer
{
    public static class VoxelBoostUpgradeState
    {
        public const string InstanceName = "Purchased Boost Bottle";
        public static bool IsPurchased { get; private set; }
        public static void BeginNewRun() => IsPurchased = false;
        public static bool TryPurchase(VoxelBoostUpgradeTuning tuning, VoxelCarDefinition car)
        {
            if (IsPurchased || tuning == null || !tuning.Fits(car) || !VoxelCurrencyState.TrySpend(tuning.purchasePrice)) return false;
            IsPurchased = true;
            return true;
        }
        public static VoxelBoostTuning ResolveTuning(VoxelCarDefinition car)
        {
            var tuning = VoxelBoostUpgradeTuning.LoadUpgrade();
            return IsPurchased && tuning != null && tuning.Fits(car) ? tuning : VoxelBoostTuning.Load();
        }
        public static void ApplyTo(Transform car, VoxelCarDefinition definition)
        {
            var tuning = VoxelBoostUpgradeTuning.LoadUpgrade();
            if (IsPurchased && tuning != null && tuning.Fits(definition)) CreateVisual(car, tuning);
        }
        /// <summary>Shared preview/runtime mounting; does not change ownership or existing damage paths.</summary>
        public static GameObject CreateVisual(Transform car, VoxelBoostUpgradeTuning tuning)
        {
            if (car == null || tuning == null || tuning.bottlePrefab == null) return null;
            var existing = car.Find(InstanceName);
            if (existing != null) return existing.gameObject;
            var bottle = Object.Instantiate(tuning.bottlePrefab, car);
            bottle.name = InstanceName;
            bottle.transform.localPosition = tuning.mountPosition;
            bottle.transform.localRotation = Quaternion.Euler(tuning.mountEulerAngles);
            return bottle;
        }
    }
}
