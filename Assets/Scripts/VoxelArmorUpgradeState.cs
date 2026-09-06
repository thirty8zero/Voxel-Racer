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

        public static bool IsRightPurchased => rightPurchased;
        public static bool IsLeftPurchased => leftPurchased;
        public static bool IsFullyPurchased => rightPurchased && leftPurchased;

        // Compatibility for code that only needs to know whether any armour is installed.
        public static bool IsPurchased => rightPurchased || leftPurchased;

        public static void BeginNewRun()
        {
            rightPurchased = false;
            leftPurchased = false;
        }

        public static bool IsPurchasedFor(VoxelArmorSide side) => side == VoxelArmorSide.Right
            ? rightPurchased
            : leftPurchased;

        public static bool CanPurchase(VoxelArmorTuning tuning, VoxelCarDefinition car,
            VoxelArmorSide side)
        {
            return !IsPurchasedFor(side) && tuning != null && tuning.panelPrefab != null && tuning.Fits(car);
        }

        public static bool TryPurchase(VoxelArmorTuning tuning, VoxelCarDefinition car,
            VoxelArmorSide side)
        {
            if (!CanPurchase(tuning, car, side) || !VoxelCurrencyState.TrySpend(tuning.panelPurchasePrice))
                return false;

            if (side == VoxelArmorSide.Right)
                rightPurchased = true;
            else
                leftPurchased = true;
            return true;
        }

        public static void ApplyTo(Transform car, VoxelCarDefinition definition)
        {
            VoxelArmorTuning tuning = VoxelArmorTuning.Load();
            if (car == null || tuning == null || !tuning.Fits(definition) || tuning.panelPrefab == null)
                return;

            Transform root = car.Find(UpgradeRootName);
            if (!IsPurchased)
            {
                if (root != null)
                    DestroyObject(root.gameObject);
                return;
            }

            root = EnsureUpgradeRoot(car);
            EnsurePanel(root, tuning, VoxelArmorSide.Right, rightPurchased);
            EnsurePanel(root, tuning, VoxelArmorSide.Left, leftPurchased);
        }

        /// <summary>Also used by the fit preview; creates both panels without changing purchases or currency.</summary>
        public static Transform CreatePair(Transform car, VoxelArmorTuning tuning)
        {
            if (car == null || tuning == null || tuning.panelPrefab == null)
                return null;

            Transform root = EnsureUpgradeRoot(car);
            EnsurePanel(root, tuning, VoxelArmorSide.Right, true);
            EnsurePanel(root, tuning, VoxelArmorSide.Left, true);
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
            VoxelArmorSide side, bool installed)
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
                return;

            GameObject instance = Object.Instantiate(tuning.panelPrefab, mount);
            instance.name = PanelInstanceName;
            instance.transform.localPosition = side == VoxelArmorSide.Right
                ? tuning.rightMountPosition
                : new Vector3(-tuning.rightMountPosition.x, tuning.rightMountPosition.y, tuning.rightMountPosition.z);
            instance.transform.localRotation = Quaternion.Euler(0f,
                side == VoxelArmorSide.Right ? 0f : 180f, 0f);
            foreach (VoxelArmorVoxel voxel in instance.GetComponentsInChildren<VoxelArmorVoxel>(true))
            {
                voxel.tuning = tuning;
                voxel.RepairToFull();
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
