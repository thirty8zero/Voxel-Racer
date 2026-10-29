using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Free run-local developer actions; uses the same ownership and mounting as the garage.</summary>
    public static class VoxelDeveloperActions
    {
        public static void RestoreHealth(VoxelCarController car)
        {
            if (car == null || car.IsDestroyed) return;
            // Developer repair is immediate, including isolated pieces that the garage's
            // connected-voxel repair intentionally cannot reach in a single operation.
            foreach (var renderer in car.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.transform == car.transform || renderer.GetComponentInParent<VoxelIndestructiblePart>() != null)
                    continue;
                renderer.gameObject.SetActive(true);
                renderer.GetComponent<VoxelArmorVoxel>()?.RepairToFull();
            }
            car.GetComponent<VoxelVehicleDamageEffects>()?.SetDamageFraction(0);
            VoxelCarRunState.Capture(car);
        }

        public static void GiveAllUpgrades(VoxelCarController car)
        {
            if (car == null || car.IsDestroyed) return;
            var definition = VoxelCarSelectionState.GetSelectedOrDefault();
            var gun = VoxelGunUpgradeState.LongGunTuning;
            int previousGuns = VoxelGunUpgradeState.PurchasedLongGunCount;
            VoxelGunUpgradeState.GrantForDevelopment(gun);
            VoxelArmorUpgradeState.GrantForDevelopment(VoxelArmorTuning.Load(), definition);
            VoxelPerformanceWheelUpgradeState.GrantForDevelopment(VoxelPerformanceWheelTuning.Load(), definition);
            VoxelEngineUpgradeState.GrantForDevelopment(VoxelEngineUpgradeTuning.Load(), definition);
            VoxelPloughUpgradeState.GrantForDevelopment(VoxelPloughTuning.Load(), definition);
            VoxelMissileUpgradeState.GrantForDevelopment(VoxelMissileLauncherTuning.Load(), definition);

            // Use each family's replacement rank, rather than stacking variants or
            // assuming the most expensive tuning is the strongest one.
            VoxelWheelSpikeTuning bestSpikes = null;
            foreach (var tuning in Resources.LoadAll<VoxelWheelSpikeTuning>("Upgrades"))
                if (tuning.Fits(definition) && (bestSpikes == null || tuning.upgradeLevel > bestSpikes.upgradeLevel))
                    bestSpikes = tuning;
            VoxelWheelSpikeUpgradeState.GrantForDevelopment(bestSpikes, definition);
            VoxelBoostUpgradeTuning bestBoost = null;
            foreach (var tuning in Resources.LoadAll<VoxelBoostUpgradeTuning>("Boost"))
                if (tuning.Fits(definition) && (bestBoost == null || tuning.bottleCount > bestBoost.bottleCount))
                    bestBoost = tuning;
            var previousBoost = VoxelBoostUpgradeState.InstalledTuning;
            VoxelBoostUpgradeState.GrantForDevelopment(bestBoost, definition);

            // Avoid rebuilding existing guns (and resetting their cooldown/ammunition)
            // on repeated clicks. All other mounts already reuse their current visuals.
            if (previousGuns != VoxelGunUpgradeState.PurchasedLongGunCount)
                VoxelGunUpgradeState.ApplyTo(car.transform, gun);
            VoxelArmorUpgradeState.ApplyTo(car.transform, definition);
            VoxelPerformanceWheelUpgradeState.ApplyTo(car.transform, definition);
            VoxelWheelSpikeUpgradeState.ApplyTo(car.transform);
            VoxelBoostUpgradeState.ApplyTo(car.transform, definition);
            VoxelEngineUpgradeState.ApplyTo(car.transform, definition);
            VoxelPloughUpgradeState.ApplyTo(car.transform, definition);
            VoxelMissileUpgradeState.ApplyTo(car.transform, definition);
            car.ResetIntegrityBaseline();
            if (previousBoost != VoxelBoostUpgradeState.InstalledTuning)
            {
                var boost = car.BoostController;
                if (boost != null && boost.Target == car)
                    boost.Configure(car, VoxelBoostUpgradeState.ResolveTuning(definition));
            }
            // Missile HUD caches mounts, so refresh it after live installations.
            foreach (var controls in Object.FindObjectsByType<VoxelMobileControls>(FindObjectsSortMode.None))
                controls.Configure(car);
            VoxelCarRunState.Capture(car, definition);
        }
    }
}
