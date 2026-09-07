using UnityEngine;

namespace VoxelRacer
{
    public static class VoxelPerformanceWheelUpgradeState
    {
        public const string InstanceName = "Performance Wheel Visual";
        public static bool IsPurchased { get; private set; }
        public static void BeginNewRun() => IsPurchased = false;
        public static bool TryPurchase(VoxelPerformanceWheelTuning tuning, VoxelCarDefinition car)
        {
            if (IsPurchased || tuning == null || !tuning.Fits(car) || !VoxelCurrencyState.TrySpend(tuning.purchasePrice)) return false;
            IsPurchased = true;
            return true;
        }

        public static void ApplyTo(Transform car, VoxelCarDefinition definition)
        {
            var tuning = VoxelPerformanceWheelTuning.Load();
            if (!IsPurchased || tuning == null || !tuning.Fits(definition) || car == null) return;
            CreateVisuals(car, tuning);
            car.GetComponent<VoxelCarController>()?.SetWheelPerformance(tuning.accelerationBonusPercent, tuning.laneChangeBonusPercent, tuning.brakingBonusPercent);
        }

        /// <summary>Retains wheel transforms, steering, protected chassis and existing spike children.</summary>
        public static void CreateVisuals(Transform car, VoxelPerformanceWheelTuning tuning)
        {
            foreach (var wheel in car.GetComponentsInChildren<Transform>(true))
            {
                if (wheel.name != "Voxel Wheel" || wheel.Find(InstanceName) != null) continue;
                // Hide only the original wheel surfaces. Keep all objects and sibling paths
                // intact so body damage and independently purchased spikes survive.
                foreach (Transform child in wheel)
                {
                    if (child.name == VoxelWheelSpikeUpgradeState.SpikeInstanceName) continue;
                    foreach (var renderer in child.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                }
                var visual = Object.Instantiate(tuning.wheelPrefab, wheel);
                visual.name = InstanceName;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = car.InverseTransformPoint(wheel.position).x >= 0
                    ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
                visual.transform.localScale = Vector3.one;
            }
        }
    }
}
