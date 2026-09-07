using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Tracks weapon upgrades purchased during the current multi-stage run.</summary>
    public static class VoxelGunUpgradeState
    {
        private const string UpgradeRootName = "Purchased Gun Upgrades";
        private const string LongGunTuningPath = "Weapons/LongBarrelHoodGunTuning";
        private static int purchasedLongGunCount;

        public static int PurchasedLongGunCount => purchasedLongGunCount;
        public static VoxelGunTuning LongGunTuning => Resources.Load<VoxelGunTuning>(LongGunTuningPath);

        public static void BeginNewRun() => purchasedLongGunCount = 0;

        public static bool CanPurchase(VoxelGunTuning tuning)
        {
            return tuning != null && tuning.visualPrefab != null &&
                purchasedLongGunCount < Mathf.Max(1, tuning.maximumPurchases);
        }

        public static bool TryPurchase(VoxelGunTuning tuning)
        {
            if (!CanPurchase(tuning) || !VoxelCurrencyState.TrySpend(tuning.purchasePrice))
                return false;

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
            for (int index = 0; index < purchasedLongGunCount; index++)
            {
                CreateVisual(upgrades, tuning, index);
            }
        }

        /// <summary>Shared by gameplay and the isolated upgrade fit preview. Does not change ownership.</summary>
        public static GameObject CreateVisual(Transform parent, VoxelGunTuning tuning, int index)
        {
            GameObject gun = Object.Instantiate(tuning.visualPrefab, parent);
            gun.name = tuning.displayName + " " + (index + 1);
            gun.transform.localPosition = new Vector3(index == 0 ? -0.72f : 0.72f, 1.06f, 1.08f);
            gun.transform.localRotation = Quaternion.identity;
            return gun;
        }
    }
}
