using UnityEngine;

namespace VoxelRacer
{
    public static class VoxelBoostUpgradeState
    {
        public const string InstanceName = "Purchased Boost Bottle";
        public const string LeftInstanceName = "Purchased Left Boost Bottle";
        public static bool IsPurchased { get; private set; }
        private static VoxelBoostUpgradeTuning installedTuning;
        public static VoxelBoostUpgradeTuning InstalledTuning => IsPurchased ? installedTuning != null ? installedTuning : VoxelBoostUpgradeTuning.LoadUpgrade() : null;
        public static bool IsEquipped(VoxelBoostUpgradeTuning tuning) => tuning != null && InstalledTuning != null &&
            InstalledTuning.bottleCount >= tuning.bottleCount;
        public static bool CanPurchase(VoxelBoostUpgradeTuning tuning, VoxelCarDefinition car) =>
            tuning != null && tuning.Fits(car) && !IsEquipped(tuning);
        public static void BeginNewRun() { IsPurchased = false; installedTuning = null; }
        public static bool TryPurchase(VoxelBoostUpgradeTuning tuning, VoxelCarDefinition car)
        {
            if (!CanPurchase(tuning, car) || !VoxelCurrencyState.TrySpend(tuning.purchasePrice)) return false;
            IsPurchased = true;
            installedTuning = tuning;
            return true;
        }
        public static VoxelBoostTuning ResolveTuning(VoxelCarDefinition car)
        {
            var tuning = InstalledTuning;
            return IsPurchased && tuning != null && tuning.Fits(car) ? tuning : VoxelBoostTuning.Load();
        }

        internal static void GrantForDevelopment(VoxelBoostUpgradeTuning tuning, VoxelCarDefinition car)
        {
            if (!CanPurchase(tuning, car)) return;
            IsPurchased = true;
            installedTuning = tuning;
        }
        public static void ApplyTo(Transform car, VoxelCarDefinition definition)
        {
            var tuning = InstalledTuning;
            if (IsPurchased && tuning != null && tuning.Fits(definition)) CreateVisual(car, tuning);
        }
        /// <summary>Shared preview/runtime mounting; does not change ownership or existing damage paths.</summary>
        public static GameObject CreateVisual(Transform car, VoxelBoostUpgradeTuning tuning)
        {
            var right = CreateBottle(car, tuning, true);
            if (tuning != null && tuning.bottleCount > 1) CreateBottle(car, tuning, false);
            return right;
        }

        public static GameObject CreateBottle(Transform car, VoxelBoostUpgradeTuning tuning, bool right)
        {
            if (car == null || tuning == null || tuning.bottlePrefab == null) return null;
            string instanceName = right ? InstanceName : LeftInstanceName;
            var existing = car.Find(instanceName);
            if (existing != null)
            {
                SetSupportSide(existing, right);
                return existing.gameObject;
            }
            var bottle = Object.Instantiate(tuning.bottlePrefab, car);
            bottle.name = instanceName;
            var position = tuning.mountPosition;
            var angles = tuning.mountEulerAngles;
            if (!right) { position.x = -position.x; angles.y = -angles.y; angles.z = -angles.z; }
            bottle.transform.localPosition = position;
            bottle.transform.localRotation = Quaternion.Euler(angles);
            SetSupportSide(bottle.transform, right);
            return bottle;
        }

        private static void SetSupportSide(Transform bottle, bool right)
        {
            // The authored right-hand cradle reaches towards -X into the chassis.
            // Mirror the support alone on the left, preserving bottle and label orientation.
            var cradle = bottle.Find("Protected Bottle Cradle");
            if (cradle == null) return;
            var scale = cradle.localScale;
            scale.x = Mathf.Abs(scale.x) * (right ? 1f : -1f);
            cradle.localScale = scale;
        }
    }
}
