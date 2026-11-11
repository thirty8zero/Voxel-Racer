using UnityEngine;

namespace VoxelRacer
{
    public enum VoxelArmorSide
    {
        Right,
        Left
    }

    /// <summary>Tracks each door armour panel independently during the current run.</summary>
    public static class VoxelArmorUpgradeState
    {
        public const string UpgradeRootName = "Purchased Door Armor";
        public const string RightMountName = "Right Door Armor Mount";
        public const string LeftMountName = "Left Door Armor Mount";

        private const string PanelInstanceName = "Door Armor Panel";
        private static bool rightPurchased;
        private static bool leftPurchased;
        private static VoxelArmorTuning rightTuning, leftTuning;

        public static bool IsRightPurchased => rightPurchased;
        public static bool IsLeftPurchased => leftPurchased;
        public static bool IsFullyPurchased => rightPurchased && leftPurchased;

        // Compatibility for code that only needs to know whether any armour is installed.
        public static bool IsPurchased => rightPurchased || leftPurchased;

        public static void BeginNewRun()
        {
            rightPurchased = false;
            leftPurchased = false;
            rightTuning = leftTuning = null;
        }

        public static bool IsPurchasedFor(VoxelArmorSide side) => side == VoxelArmorSide.Right
            ? rightPurchased
            : leftPurchased;

        public static bool CanPurchase(VoxelArmorTuning tuning, VoxelCarDefinition car,
            VoxelArmorSide side)
        {
            var installed = InstalledTuning(side);
            return tuning != null && tuning.panelPrefab != null && tuning.Fits(car) &&
                (installed == null || tuning.upgradeLevel > installed.upgradeLevel);
        }

        public static VoxelArmorTuning InstalledTuning(VoxelArmorSide side) => !IsPurchasedFor(side) ? null :
            (side == VoxelArmorSide.Right ? rightTuning : leftTuning) ?? VoxelArmorTuning.Load();

        public static bool IsEquipped(VoxelArmorTuning tuning, VoxelArmorSide side) => tuning != null &&
            InstalledTuning(side) is VoxelArmorTuning installed && installed.upgradeLevel >= tuning.upgradeLevel;

        public static bool TryPurchase(VoxelArmorTuning tuning, VoxelCarDefinition car,
            VoxelArmorSide side)
        {
            if (!CanPurchase(tuning, car, side) || !VoxelCurrencyState.TrySpend(tuning.panelPurchasePrice))
                return false;

            SetInstalled(tuning, side);
            return true;
        }

        private static void SetInstalled(VoxelArmorTuning tuning, VoxelArmorSide side)
        {
            if (side == VoxelArmorSide.Right)
            {
                rightPurchased = true;
                rightTuning = tuning;
            }
            else
            {
                leftPurchased = true;
                leftTuning = tuning;
            }
        }

        public static void ApplyTo(Transform car, VoxelCarDefinition definition, bool preserveReplacementDamage = false)
        {
            if (car == null)
                return;

            Transform root = car.Find(UpgradeRootName);
            if (!IsPurchased)
            {
                if (root != null)
                    DestroyObject(root.gameObject);
                return;
            }

            root = EnsureUpgradeRoot(car);
            foreach (var side in new[] { VoxelArmorSide.Right, VoxelArmorSide.Left })
            {
                var tuning = InstalledTuning(side);
                EnsurePanel(root, tuning, side, tuning != null && tuning.Fits(definition) && tuning.panelPrefab != null,
                    preserveReplacementDamage);
            }
        }

        internal static void GrantForDevelopment(VoxelArmorTuning tuning, VoxelCarDefinition car)
        {
            if (CanPurchase(tuning, car, VoxelArmorSide.Right)) SetInstalled(tuning, VoxelArmorSide.Right);
            if (CanPurchase(tuning, car, VoxelArmorSide.Left)) SetInstalled(tuning, VoxelArmorSide.Left);
        }

        /// <summary>Also used by the fit preview; creates both panels without changing purchases or currency.</summary>
        public static Transform CreatePair(Transform car, VoxelArmorTuning tuning)
            => CreatePanels(car, tuning, true, true);

        public static void CreatePanel(Transform car, VoxelArmorTuning tuning, VoxelArmorSide side)
        {
            if (car == null || tuning == null || tuning.panelPrefab == null) return;
            EnsurePanel(EnsureUpgradeRoot(car), tuning, side, true);
        }

        /// <summary>Creates selected panels for an isolated preview without changing ownership.</summary>
        public static Transform CreatePanels(Transform car, VoxelArmorTuning tuning, bool right, bool left)
        {
            if (car == null || tuning == null || tuning.panelPrefab == null)
                return null;

            Transform root = EnsureUpgradeRoot(car);
            EnsurePanel(root, tuning, VoxelArmorSide.Right, right);
            EnsurePanel(root, tuning, VoxelArmorSide.Left, left);
            return root;
        }

        private static Transform EnsureUpgradeRoot(Transform car)
        {
            Transform root = car.Find(UpgradeRootName);
            if (root == null)
            {
                root = new GameObject(UpgradeRootName).transform;
                root.SetParent(car, false);
            }

            // The generated gun root occupies index 1. Keeping armour after it
            // gives every car build the same paths as upgrades are added later.
            Transform gunRoot = car.Find("Purchased Gun Upgrades");
            int desiredIndex = gunRoot != null ? gunRoot.GetSiblingIndex() + 1 : 1;
            root.SetSiblingIndex(Mathf.Clamp(desiredIndex, 0, car.childCount - 1));

            EnsureMount(root, VoxelArmorSide.Right);
            EnsureMount(root, VoxelArmorSide.Left);
            return root;
        }

        private static Transform EnsureMount(Transform root, VoxelArmorSide side)
        {
            string name = side == VoxelArmorSide.Right ? RightMountName : LeftMountName;
            Transform mount = root.Find(name);
            if (mount == null)
            {
                mount = new GameObject(name).transform;
                mount.SetParent(root, false);
            }

            // Reserve both child indices so adding the opposite side later can
            // never move the path of an already damaged panel.
            mount.SetSiblingIndex(side == VoxelArmorSide.Right ? 0 : 1);
            return mount;
        }

        private static void EnsurePanel(Transform root, VoxelArmorTuning tuning,
            VoxelArmorSide side, bool installed, bool preserveReplacementDamage = false)
        {
            Transform mount = EnsureMount(root, side);
            Transform panel = mount.Find(PanelInstanceName);
            if (!installed)
            {
                if (panel != null)
                    DestroyObject(panel.gameObject);
                return;
            }

            if (panel != null)
            {
                var existing = panel.GetComponentInChildren<VoxelArmorVoxel>(true)?.tuning;
                if (existing == tuning || existing != null && existing.upgradeLevel > tuning.upgradeLevel) return;
            }

            GameObject instance = Object.Instantiate(tuning.panelPrefab, mount);
            instance.name = PanelInstanceName;
            instance.transform.localPosition = side == VoxelArmorSide.Right
                ? tuning.rightMountPosition
                : new Vector3(-tuning.rightMountPosition.x, tuning.rightMountPosition.y, tuning.rightMountPosition.z);
            instance.transform.localRotation = Quaternion.Euler(0f,
                side == VoxelArmorSide.Right ? 0f : 180f, 0f);
            var replacement = instance.GetComponentsInChildren<VoxelArmorVoxel>(true);
            foreach (VoxelArmorVoxel voxel in replacement)
            {
                voxel.tuning = tuning;
                voxel.RepairToFull();
            }

            if (panel == null) return;
            if (preserveReplacementDamage) TransferDamage(panel, replacement);
            // Detach immediately so deferred Play Mode destruction cannot affect integrity or sibling paths.
            panel.gameObject.SetActive(false);
            panel.SetParent(null, true);
            DestroyObject(panel.gameObject);
        }

        private static void TransferDamage(Transform previous, VoxelArmorVoxel[] replacement)
        {
            // Different tiers may shift the grid. Keep each damaged cell on the closest
            // unused cell of the replacement panel, and keep any extra coverage intact.
            foreach (var source in previous.GetComponentsInChildren<VoxelArmorVoxel>(true))
            {
                if (source.gameObject.activeSelf && !source.NeedsRepair) continue;
                int closest = -1;
                float distance = float.PositiveInfinity;
                for (int i = 0; i < replacement.Length; i++)
                {
                    var candidate = replacement[i];
                    if (candidate == null) continue;
                    float separation = (source.transform.position - candidate.transform.position).sqrMagnitude;
                    if (separation >= distance) continue;
                    closest = i;
                    distance = separation;
                }
                if (closest < 0) break;
                var target = replacement[closest];
                target.RestoreHealth(source.RemainingHealth);
                target.gameObject.SetActive(source.gameObject.activeSelf);
                replacement[closest] = null;
            }
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
