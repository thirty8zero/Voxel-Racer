using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Tracks and mounts the one-per-run set of four non-damageable wheel spikes.</summary>
    public static class VoxelWheelSpikeUpgradeState
    {
        public const string SpikeInstanceName = "Wheel Spike";
        private static bool purchased;
        private static VoxelWheelSpikeTuning installedTuning;

        public static bool IsPurchased => purchased;
        public static VoxelWheelSpikeTuning InstalledTuning => purchased ? installedTuning != null ? installedTuning : VoxelWheelSpikeTuning.Load() : null;
        public static bool IsEquipped(VoxelWheelSpikeTuning tuning) => tuning != null && InstalledTuning != null &&
            InstalledTuning.upgradeLevel >= tuning.upgradeLevel;
        public static float SideRamDamageBonusPercent
        {
            get
            {
                VoxelWheelSpikeTuning tuning = InstalledTuning;
                return purchased && tuning != null ? Mathf.Max(0f, tuning.sideRamDamageBonusPercent) : 0f;
            }
        }

        public static float CalculateRamDamage(float baseDamage, bool rearImpact) =>
            baseDamage * (rearImpact ? 1f : 1f + SideRamDamageBonusPercent / 100f);

        public static void BeginNewRun() { purchased = false; installedTuning = null; }

        public static bool CanPurchase(VoxelWheelSpikeTuning tuning) =>
            tuning != null && tuning.spikePrefab != null && !IsEquipped(tuning);

        public static bool TryPurchase(VoxelWheelSpikeTuning tuning)
        {
            if (!CanPurchase(tuning) || !VoxelCurrencyState.TrySpend(tuning.purchasePrice))
                return false;

            purchased = true;
            installedTuning = tuning;
            return true;
        }

        public static void ApplyTo(Transform car, VoxelWheelSpikeTuning tuning = null)
        {
            if (car == null)
                return;

            tuning = InstalledTuning;
            foreach (Transform wheel in car.GetComponentsInChildren<Transform>(true))
            {
                if (wheel == null || wheel.name != "Voxel Wheel")
                    continue;

                Transform spike = wheel.Find(SpikeInstanceName);
                if (!purchased || tuning == null || tuning.spikePrefab == null)
                {
                    if (spike != null)
                        DestroyObject(spike.gameObject);
                    continue;
                }

                if (spike != null && spike.GetComponent<VoxelWheelSpikeMount>()?.tuning == tuning)
                    continue;

                CreateVisual(car, wheel, tuning);
            }
        }

        /// <summary>Shared mounting geometry without changing purchases or currency.</summary>
        public static GameObject CreateVisual(Transform car, Transform wheel, VoxelWheelSpikeTuning tuning)
        {
                if (car == null || wheel == null || tuning == null || tuning.spikePrefab == null) return null;
                Transform previous = wheel.Find(SpikeInstanceName);
                if (previous != null)
                {
                    if (previous.GetComponent<VoxelWheelSpikeMount>()?.tuning == tuning) return previous.gameObject;
                    // Detach immediately so deferred Play Mode destruction cannot leave duplicate mounts.
                    previous.gameObject.SetActive(false); previous.SetParent(null, true);
                    DestroyObject(previous.gameObject);
                }
                GameObject instance = Object.Instantiate(tuning.spikePrefab, wheel);
                instance.name = SpikeInstanceName;
                instance.AddComponent<VoxelWheelSpikeMount>().tuning = tuning;
                var upgradedWheel = wheel.Find(VoxelPerformanceWheelUpgradeState.InstanceName);
                instance.transform.localPosition = upgradedWheel != null ? upgradedWheel.localPosition : Vector3.zero;
                // The shared model points along +X. Mirror it on the left side so
                // every spike projects out from the car rather than through its wheel.
                bool isRightWheel = car.InverseTransformPoint(wheel.position).x >= 0f;
                instance.transform.localRotation = isRightWheel
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 180f, 0f);
                instance.transform.localScale = Vector3.one;
                return instance;
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
