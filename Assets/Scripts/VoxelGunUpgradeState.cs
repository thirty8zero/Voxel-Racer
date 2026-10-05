using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Tracks weapon upgrades purchased during the current multi-stage run.</summary>
    public static class VoxelGunUpgradeState
    {
        private const string UpgradeRootName = "Purchased Gun Upgrades";
        private const string LongGunTuningPath = "Weapons/LongBarrelHoodGunTuning";
        private static int purchasedLongGunCount;
        private static bool firstLongGunIsRight;
        public const int SlotCount = 2;

        public static int PurchasedLongGunCount => purchasedLongGunCount;
        public static VoxelGunTuning LongGunTuning => Resources.Load<VoxelGunTuning>(LongGunTuningPath);

        public static void BeginNewRun() { purchasedLongGunCount = 0; firstLongGunIsRight = false; }

        public static bool IsPurchased(int slot) => slot >= 0 && slot < SlotCount &&
            (purchasedLongGunCount >= SlotCount || purchasedLongGunCount == 1 && slot == (firstLongGunIsRight ? 1 : 0));

        public static bool CanPurchase(VoxelGunTuning tuning)
        {
            return tuning != null && tuning.visualPrefab != null &&
                purchasedLongGunCount < Mathf.Clamp(tuning.maximumPurchases, 1, SlotCount);
        }

        public static bool CanPurchase(VoxelGunTuning tuning, int slot) =>
            slot >= 0 && slot < SlotCount && CanPurchase(tuning) && !IsPurchased(slot);

        public static bool TryPurchase(VoxelGunTuning tuning)
            => TryPurchase(tuning, IsPurchased(0) ? 1 : 0);

        public static bool TryPurchase(VoxelGunTuning tuning, int slot)
        {
            if (!CanPurchase(tuning, slot) || !VoxelCurrencyState.TrySpend(tuning.purchasePrice))
                return false;

            if (purchasedLongGunCount == 0) firstLongGunIsRight = slot == 1;
            purchasedLongGunCount++;
            return true;
        }

        /// <summary>Installs the purchased pair symmetrically beside the starter hood gun.</summary>
        public static void ApplyTo(Transform carRoot, VoxelGunTuning tuning)
        {
            if (carRoot == null)
                return;

            Transform existing = carRoot.Find(UpgradeRootName);
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
                // Keep sibling paths stable immediately, even before deferred destruction.
                existing.SetParent(null, true);
                if (Application.isPlaying)
                    Object.Destroy(existing.gameObject);
                else
                    Object.DestroyImmediate(existing.gameObject);
            }

            if (purchasedLongGunCount <= 0 || tuning == null || tuning.visualPrefab == null)
                return;

            Transform upgrades = new GameObject(UpgradeRootName).transform;
            upgrades.SetParent(carRoot, false);
            upgrades.SetSiblingIndex(1);
            for (int index = 0; index < SlotCount; index++)
            {
                if (IsPurchased(index)) CreateVisual(upgrades, tuning, index);
            }
        }

        /// <summary>Shared by gameplay and the isolated upgrade fit preview. Does not change ownership.</summary>
        public static GameObject CreateVisual(Transform parent, VoxelGunTuning tuning, int index)
        {
            GameObject gun = Object.Instantiate(tuning.visualPrefab, parent);
            gun.name = tuning.displayName + " " + (index + 1);
            gun.transform.localPosition = new Vector3(index == 0 ? -0.58f : 0.58f, 1.06f, 1.08f);
            gun.transform.localRotation = Quaternion.identity;
            return gun;
        }
    }
}
