using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelPerformanceWheelValidation
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        [MenuItem("Tools/Voxel Racer/Validate Performance Wheels")]
        public static void Run()
        {
            var ownership = typeof(VoxelPerformanceWheelUpgradeState).GetField("<IsPurchased>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            bool owned = VoxelPerformanceWheelUpgradeState.IsPurchased;
            int cash = VoxelCurrencyState.Balance;
            var prefab = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cars/SpyCar2PlayerCar.prefab"));
            var root = new GameObject("Temporary Wheel Validation");
            try
            {
                prefab.transform.SetParent(root.transform, false);
                var definition = AssetDatabase.LoadAssetAtPath<VoxelCarDefinition>("Assets/Resources/Cars/SpyCar2PlayerCar.asset");
                var tuning = VoxelPerformanceWheelTuning.Load();
                Check(tuning != null && tuning.Fits(definition), "Missing or incompatible tuning");
                var controller = root.AddComponent<VoxelCarController>(); controller.enabled = false;
                controller.ResetIntegrityBaseline();
                int total = controller.TotalIntegrityVoxels;
                var damaged = prefab.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
                damaged.gameObject.SetActive(false);
                VoxelPerformanceWheelUpgradeState.BeginNewRun();
                VoxelCurrencyState.Reset();
                Check(!VoxelPerformanceWheelUpgradeState.TryPurchase(tuning, definition), "Unaffordable purchase succeeded");
                VoxelCurrencyState.Add(tuning.purchasePrice * 2);
                Check(VoxelPerformanceWheelUpgradeState.TryPurchase(tuning, definition), "Purchase failed");
                Check(!VoxelPerformanceWheelUpgradeState.TryPurchase(tuning, definition) && VoxelCurrencyState.Balance == tuning.purchasePrice, "Duplicate purchase charged cash");
                VoxelPerformanceWheelUpgradeState.ApplyTo(root.transform, definition);
                VoxelPerformanceWheelUpgradeState.ApplyTo(root.transform, definition);
                Check(root.GetComponentsInChildren<Transform>().Count(t => t.name == VoxelPerformanceWheelUpgradeState.InstanceName) == 4, "Repeated apply duplicated wheels");
                Check(Mathf.Approximately(controller.EffectiveAcceleration, controller.acceleration * (1 + tuning.accelerationBonusPercent / 100)), "Acceleration bonus incorrect or stacked");
                Check(Mathf.Approximately(controller.EffectiveLaneChangeSpeed, controller.laneChangeSpeed * (1 + tuning.laneChangeBonusPercent / 100)), "Lane bonus incorrect");
                Check(Mathf.Approximately(controller.EffectiveBrakingForce, controller.brakingForce * (1 + tuning.brakingBonusPercent / 100)), "Braking bonus incorrect or stacked");
                controller.SetWheelPerformance(0, 0, 0);
                Check(Mathf.Approximately(controller.EffectiveBrakingForce, controller.brakingForce), "Zero braking bonus changed baseline");
                controller.SetWheelPerformance(0, 0, -20);
                Check(Mathf.Approximately(controller.EffectiveBrakingForce, controller.brakingForce), "Negative braking bonus reduced baseline");
                VoxelPerformanceWheelUpgradeState.ApplyTo(root.transform, definition);
                Check(controller.TotalIntegrityVoxels == total && controller.MissingIntegrityVoxels == 1 && !damaged.gameObject.activeSelf, "Wheel purchase changed body integrity/damage");
                foreach (var wheel in root.GetComponentsInChildren<Transform>().Where(t => t.name == "Voxel Wheel").ToArray())
                    VoxelWheelSpikeUpgradeState.CreateVisual(root.transform, wheel, VoxelWheelSpikeTuning.Load());
                Check(root.GetComponentsInChildren<Transform>().Count(t => t.name == "Wheel Spike") == 4, "Spikes failed to mount after replacement");
                Check(VoxelUpgradeFitCatalog.Discover().Any(e => e.Asset == tuning && e.Fits(definition)), "Fit preview entry missing");
                var next = new GameObject("Next Stage Car");
                next.transform.SetParent(root.transform, false);
                UnityEngine.Object.Instantiate(definition.visualPrefab, next.transform);
                var nextController = next.AddComponent<VoxelCarController>(); nextController.enabled = false;
                foreach (var wheel in next.GetComponentsInChildren<Transform>().Where(t => t.name == "Voxel Wheel").ToArray())
                    VoxelWheelSpikeUpgradeState.CreateVisual(next.transform, wheel, VoxelWheelSpikeTuning.Load());
                VoxelPerformanceWheelUpgradeState.ApplyTo(next.transform, definition);
                Check(next.GetComponentsInChildren<Transform>().Count(t => t.name == "Wheel Spike") == 4 &&
                    next.GetComponentsInChildren<Transform>().Count(t => t.name == VoxelPerformanceWheelUpgradeState.InstanceName) == 4 &&
                    Mathf.Approximately(nextController.EffectiveAcceleration, controller.EffectiveAcceleration), "Next stage or spikes-first install failed");
                VoxelPerformanceWheelUpgradeState.BeginNewRun();
                Check(!VoxelPerformanceWheelUpgradeState.IsPurchased, "New run retained purchase");
                Directory.CreateDirectory("Temp");
                File.WriteAllText("Temp/PerformanceWheelValidation.txt", "PASS: purchase/duplicate/affordability, four replacements, non-stacking acceleration and lane stats, preserved body damage/integrity, spike compatibility, fit preview discovery, new-run reset.");
            }
            catch (Exception e) { Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/PerformanceWheelValidation.txt", "FAIL: " + e); throw; }
            finally
            {
                ownership.SetValue(null, owned);
                VoxelCurrencyState.Reset(); VoxelCurrencyState.Add(cash);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
