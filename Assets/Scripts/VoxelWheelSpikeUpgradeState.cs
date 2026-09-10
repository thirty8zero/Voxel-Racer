using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Tracks and mounts the one-per-run set of four non-damageable wheel spikes.</summary>
    public static class VoxelWheelSpikeUpgradeState
    {
        public const string SpikeInstanceName = "Wheel Spike";
        private static bool purchased;

        public static bool IsPurchased => purchased;
        public static float SideRamDamageBonus
        {
            get
            {
                VoxelWheelSpikeTuning tuning = VoxelWheelSpikeTuning.Load();
                return purchased && tuning != null ? Mathf.Max(0f, tuning.sideRamDamageBonus) : 0f;
            }
        }

        public static void BeginNewRun() => purchased = false;

        public static bool CanPurchase(VoxelWheelSpikeTuning tuning) =>
            !purchased && tuning != null && tuning.spikePrefab != null;

        public static bool TryPurchase(VoxelWheelSpikeTuning tuning)
        {
            if (!CanPurchase(tuning) || !VoxelCurrencyState.TrySpend(tuning.purchasePrice))
                return false;

            purchased = true;
            return true;
        }

        public static void ApplyTo(Transform car, VoxelWheelSpikeTuning tuning = null)
        {
            if (car == null)
                return;

            tuning ??= VoxelWheelSpikeTuning.Load();
            foreach (Transform wheel in car.GetComponentsInChildren<Transform>(true))
            {
                if (wheel.name != "Voxel Wheel")
                    continue;

                Transform spike = wheel.Find(SpikeInstanceName);
                if (!purchased || tuning == null || tuning.spikePrefab == null)
                {
                    if (spike != null)
                        DestroyObject(spike.gameObject);
                    continue;
                }

                if (spike != null)
                    continue;

                CreateVisual(car, wheel, tuning);
            }
        }

        /// <summary>Shared mounting geometry without changing purchases or currency.</summary>
        public static GameObject CreateVisual(Transform car, Transform wheel, VoxelWheelSpikeTuning tuning)
        {
                GameObject instance = Object.Instantiate(tuning.spikePrefab, wheel);
                instance.name = SpikeInstanceName;
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
