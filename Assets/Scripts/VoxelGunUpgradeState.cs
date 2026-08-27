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
                if (Application.isPlaying)
                    Object.Destroy(existing.gameObject);
                else
                    Object.DestroyImmediate(existing.gameObject);
            }

            if (purchasedLongGunCount <= 0 || tuning == null || tuning.visualPrefab == null)
                return;

            Transform upgrades = new GameObject(UpgradeRootName).transform;
            upgrades.SetParent(carRoot, false);
            for (int index = 0; index < purchasedLongGunCount; index++)
            {
                GameObject gun = Object.Instantiate(tuning.visualPrefab, upgrades);
                gun.name = tuning.displayName + " " + (index + 1);
                gun.transform.localPosition = new Vector3(index == 0 ? -0.72f : 0.72f, 1.06f, 1.08f);
                gun.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
