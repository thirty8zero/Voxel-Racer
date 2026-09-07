using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelUpgradeFitValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Upgrade Fit Preview")]
        public static void Run()
        {
            string report = "Temp/UpgradeFitValidation.txt";
            Directory.CreateDirectory("Temp");
            int cash = VoxelCurrencyState.Balance;
            bool right = VoxelArmorUpgradeState.IsRightPurchased, left = VoxelArmorUpgradeState.IsLeftPurchased;
            bool spikes = VoxelWheelSpikeUpgradeState.IsPurchased;
            int guns = VoxelGunUpgradeState.PurchasedLongGunCount, missing = VoxelCarRunState.MissingVoxelCount;
            var car = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Cars/SpyCar2PlayerCar.prefab");
            try
            {
                var definition = AssetDatabase.LoadAssetAtPath<VoxelCarDefinition>("Assets/Resources/Cars/SpyCar2PlayerCar.asset");
                var entries = VoxelUpgradeFitCatalog.Discover();
                Check(entries.Any(e => e.Asset is VoxelArmorTuning), "Armour undiscovered");
                Check(entries.Any(e => e.Asset is VoxelGunTuning), "Guns undiscovered");
                Check(entries.Any(e => e.Asset is VoxelWheelSpikeTuning), "Spikes undiscovered");
                foreach (var entry in entries.Where(e => e.Fits(definition))) entry.Build(car.transform);
                Check(car.GetComponentsInChildren<VoxelArmorVoxel>().Length == 60, "Both armour panels must coexist");
                var spikeRoots = car.GetComponentsInChildren<Transform>().Where(t => t.name == VoxelWheelSpikeUpgradeState.SpikeInstanceName).ToArray();
                Check(spikeRoots.Length == 4, "Expected four spike mounts");
                foreach (var spike in spikeRoots)
                    Check(Vector3.Dot(spike.right, spike.position.x > 0 ? Vector3.right : Vector3.left) > .99f, "Spike points inward");
                Check(cash == VoxelCurrencyState.Balance && right == VoxelArmorUpgradeState.IsRightPurchased &&
                    left == VoxelArmorUpgradeState.IsLeftPurchased && spikes == VoxelWheelSpikeUpgradeState.IsPurchased &&
                    guns == VoxelGunUpgradeState.PurchasedLongGunCount && missing == VoxelCarRunState.MissingVoxelCount,
                    "Preview modified run state");
                File.WriteAllText(report, "PASS: automatic discovery, combined armour/guns/spikes, 60 armour voxels, four outward spikes, unchanged cash/ownership/damage. Entries: " + entries.Count);
                VoxelUpgradeFitPreview.OpenDefault();
                Debug.Log(File.ReadAllText(report));
            }
            catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex); throw; }
            finally { PrefabUtility.UnloadPrefabContents(car); }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
