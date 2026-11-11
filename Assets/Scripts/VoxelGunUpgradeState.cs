using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Tracks the selected weapon in each hood slot for the current run.</summary>
    public static class VoxelGunUpgradeState
    {
        private const string UpgradeRootName = "Purchased Gun Upgrades";
        public const int SlotCount = 2;
        private static VoxelGunTuning[] installed = new VoxelGunTuning[SlotCount];

        public static VoxelGunTuning LongGunTuning => Resources.Load<VoxelGunTuning>("Weapons/LongBarrelHoodGunTuning");
        public static VoxelGunTuning MinigunTuning => Resources.Load<VoxelGunTuning>("Weapons/MinigunTuning");
        public static int PurchasedLongGunCount => Count(LongGunTuning);
        public static void BeginNewRun() => installed = new VoxelGunTuning[SlotCount];
        public static VoxelGunTuning InstalledTuning(int slot) => slot >= 0 && slot < SlotCount ? installed[slot] : null;
        public static bool IsPurchased(int slot) => InstalledTuning(slot) != null;

        public static int Count(VoxelGunTuning tuning)
        {
            if (tuning == null) return 0;
            int count = 0;
            foreach (var gun in installed)
                if (gun == tuning) count++;
            return count;
        }

        public static int Capacity(VoxelGunTuning tuning) => tuning == null ? 1 : Mathf.Clamp(tuning.maximumPurchases, 1, SlotCount);

        public static bool CanPurchase(VoxelGunTuning tuning, VoxelCarDefinition definition = null)
            => CanPurchase(tuning, 0, definition) || CanPurchase(tuning, 1, definition);

        public static bool CanPurchase(VoxelGunTuning tuning, int slot, VoxelCarDefinition definition = null)
        {
            if (slot < 0 || slot >= SlotCount || tuning == null || tuning.visualPrefab == null ||
                tuning.projectileKind != VoxelProjectileKind.Bullet || Count(tuning) >= Capacity(tuning))
                return false;
            if (tuning.compatibleCarPrefab != null && !tuning.Fits(definition ?? VoxelCarSelectionState.GetSelectedOrDefault()))
                return false;
            var current = installed[slot];
            return current == null || current.upgradeLevel < tuning.upgradeLevel;
        }

        public static bool TryPurchase(VoxelGunTuning tuning)
            => TryPurchase(tuning, CanPurchase(tuning, 0) ? 0 : 1);

        public static bool TryPurchase(VoxelGunTuning tuning, int slot, VoxelCarDefinition definition = null)
        {
            if (!CanPurchase(tuning, slot, definition) || !VoxelCurrencyState.TrySpend(tuning.purchasePrice))
                return false;

            SetSlot(slot, tuning);
            return true;
        }

        private static void SetSlot(int slot, VoxelGunTuning tuning)
        {
            var updated = (VoxelGunTuning[])installed.Clone();
            updated[slot] = tuning;
            installed = updated;
        }

        internal static void GrantForDevelopment(VoxelGunTuning tuning, VoxelCarDefinition definition)
        {
            for (int slot = 0; slot < SlotCount; slot++)
                if (CanPurchase(tuning, slot, definition)) SetSlot(slot, tuning);
        }

        public static void ApplyTo(Transform carRoot)
        {
            if (carRoot == null) return;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                var tuning = installed[slot];
                if (tuning != null && tuning.visualPrefab != null)
                    SetVisual(carRoot, tuning, slot, false);
                else
                    RemoveVisual(FindVisual(carRoot.Find(UpgradeRootName), slot));
            }
        }

        private static VoxelGunMount FindVisual(Transform parent, int slot)
        {
            if (parent != null)
                foreach (Transform child in parent)
                    if (child.TryGetComponent(out VoxelGunMount mount) && mount.upgradeSlot == slot)
                        return mount;
            return null;
        }

        private static void RemoveVisual(VoxelGunMount mount)
        {
            if (mount == null) return;
            mount.gameObject.SetActive(false);
            mount.transform.SetParent(null, true);
            if (Application.isPlaying) Object.Destroy(mount.gameObject);
            else Object.DestroyImmediate(mount.gameObject);
        }

        /// <summary>Fits into the shared hood hardpoints without changing ownership.</summary>
        public static GameObject CreateVisual(Transform carRoot, VoxelGunTuning tuning, int slot)
            => SetVisual(carRoot, tuning, slot, true);

        private static GameObject SetVisual(Transform carRoot, VoxelGunTuning tuning, int slot, bool keepHigherLevel)
        {
            Transform upgrades = carRoot.Find(UpgradeRootName);
            if (upgrades == null)
            {
                upgrades = new GameObject(UpgradeRootName).transform;
                upgrades.SetParent(carRoot, false);
                upgrades.SetSiblingIndex(1);
            }
            var current = FindVisual(upgrades, slot);
            if (current != null && (current.tuning == tuning ||
                (keepHigherLevel && current.tuning != null && current.tuning.upgradeLevel > tuning.upgradeLevel)))
                return current.gameObject;

            int sibling = current != null ? current.transform.GetSiblingIndex() : slot;
            RemoveVisual(current);
            GameObject gun = Object.Instantiate(tuning.visualPrefab, upgrades);
            gun.name = tuning.displayName + " " + (slot + 1);
            gun.transform.localPosition = new Vector3(slot == 0 ? -.58f : .58f, 1.06f, 1.08f);
            gun.transform.localRotation = Quaternion.identity;
            gun.transform.SetSiblingIndex(sibling);
            var mount = gun.GetComponent<VoxelGunMount>();
            mount.upgradeSlot = slot;
            return gun;
        }
    }
}
