using UnityEngine;

namespace VoxelRacer
{
    public static class VoxelPerformanceWheelUpgradeState
    {
        public const string InstanceName = "Performance Wheel Visual";
        public const float OutwardOffset = 0.07f;
        public static VoxelPerformanceWheelTuning InstalledTuning { get; private set; }
        public static bool IsPurchased => InstalledTuning != null;
        public static void BeginNewRun() => InstalledTuning = null;

        public static bool CanPurchase(VoxelPerformanceWheelTuning tuning, VoxelCarDefinition car) =>
            tuning != null && tuning.Fits(car) &&
            (InstalledTuning == null || InstalledTuning.upgradeLevel < tuning.upgradeLevel);

        public static bool TryPurchase(VoxelPerformanceWheelTuning tuning, VoxelCarDefinition car)
        {
            if (!CanPurchase(tuning, car) || !VoxelCurrencyState.TrySpend(tuning.purchasePrice)) return false;
            InstalledTuning = tuning;
            return true;
        }

        public static void ApplyTo(Transform car, VoxelCarDefinition definition)
        {
            var tuning = InstalledTuning;
            if (tuning == null || !tuning.Fits(definition) || car == null) return;
            CreateVisuals(car, tuning);
            car.GetComponent<VoxelCarController>()?.SetWheelPerformance(tuning.accelerationBonusPercent, tuning.laneChangeBonusPercent, tuning.brakingBonusPercent);
        }

        internal static void GrantForDevelopment(VoxelPerformanceWheelTuning tuning, VoxelCarDefinition car)
        {
            if (CanPurchase(tuning, car)) InstalledTuning = tuning;
        }

        /// <summary>Retains wheel transforms, steering, protected chassis and existing spike children.</summary>
        public static void CreateVisuals(Transform car, VoxelPerformanceWheelTuning tuning, int side = 0)
        {
            if (car == null || tuning == null || tuning.wheelPrefab == null) return;
            foreach (var wheel in car.GetComponentsInChildren<Transform>(true))
            {
                if (wheel == null || wheel.name != "Voxel Wheel") continue;
                int wheelSide = car.InverseTransformPoint(wheel.position).x >= 0 ? 1 : -1;
                if (side != 0 && side != wheelSide) continue;

                var previous = wheel.Find(InstanceName);
                if (previous != null)
                {
                    var mounted = previous.GetComponent<VoxelPerformanceWheelMount>()?.tuning;
                    if (mounted == tuning || (mounted != null && mounted.upgradeLevel > tuning.upgradeLevel)) continue;
                }

                // Hide only the original wheel surfaces. Keep all objects and sibling paths
                // intact so body damage and independently purchased spikes survive.
                if (previous == null)
                {
                    foreach (Transform child in wheel)
                    {
                        if (child.name == VoxelWheelSpikeUpgradeState.SpikeInstanceName) continue;
                        foreach (var renderer in child.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                    }
                }
                else
                {
                    previous.gameObject.SetActive(false);
                    previous.SetParent(null, true);
                    if (Application.isPlaying) Object.Destroy(previous.gameObject);
                    else Object.DestroyImmediate(previous.gameObject);
                }
                var visual = Object.Instantiate(tuning.wheelPrefab, wheel);
                visual.name = InstanceName;
                visual.AddComponent<VoxelPerformanceWheelMount>().tuning = tuning;
                visual.transform.localPosition = Vector3.right * (wheelSide * OutwardOffset);
                visual.transform.localRotation = wheelSide > 0 ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
                visual.transform.localScale = Vector3.one;
                var spike = wheel.Find(VoxelWheelSpikeUpgradeState.SpikeInstanceName);
                if (spike != null) spike.localPosition = visual.transform.localPosition;
            }
        }
    }
}
