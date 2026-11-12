using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

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
            // Try higher gun tiers first so a one-per-run tuning can still leave a slot for the next tier.
            foreach (var gun in Ranked<VoxelGunTuning>("Weapons",
                t => t.visualPrefab != null && t.projectileKind == VoxelProjectileKind.Bullet && t.Fits(definition), t => t.upgradeLevel))
                VoxelGunUpgradeState.GrantForDevelopment(gun, definition);
            VoxelArmorUpgradeState.GrantForDevelopment(Ranked<VoxelArmorTuning>("Armor",
                t => t.panelPrefab != null && t.Fits(definition), t => t.upgradeLevel).FirstOrDefault(), definition);
            VoxelPerformanceWheelUpgradeState.GrantForDevelopment(Ranked<VoxelPerformanceWheelTuning>("Upgrades",
                t => t.Fits(definition), t => t.upgradeLevel).FirstOrDefault(), definition);
            VoxelEngineUpgradeState.GrantForDevelopment(VoxelEngineUpgradeTuning.Load(), definition);
            VoxelPloughUpgradeState.GrantForDevelopment(Ranked<VoxelPloughTuning>("Upgrades",
                t => t.Fits(definition), t => t.upgradeLevel).FirstOrDefault(), definition);
            VoxelUnderbodyArmorUpgradeState.GrantForDevelopment(VoxelUnderbodyArmorTuning.Load(), definition);
            VoxelMissileUpgradeState.GrantForDevelopment(VoxelMissileLauncherTuning.Load(), definition);

            VoxelWheelSpikeUpgradeState.GrantForDevelopment(Ranked<VoxelWheelSpikeTuning>("Upgrades",
                t => t.Fits(definition), t => t.upgradeLevel).FirstOrDefault(), definition);
            var previousBoost = VoxelBoostUpgradeState.InstalledTuning;
            VoxelBoostUpgradeState.GrantForDevelopment(Ranked<VoxelBoostUpgradeTuning>("Boost",
                t => t.Fits(definition), t => t.bottleCount).FirstOrDefault(), definition);

            VoxelGunUpgradeState.ApplyTo(car.transform);
            VoxelArmorUpgradeState.ApplyTo(car.transform, definition, preserveReplacementDamage: true);
            VoxelPerformanceWheelUpgradeState.ApplyTo(car.transform, definition);
            VoxelWheelSpikeUpgradeState.ApplyTo(car.transform);
            VoxelBoostUpgradeState.ApplyTo(car.transform, definition);
            VoxelEngineUpgradeState.ApplyTo(car.transform, definition);
            VoxelPloughUpgradeState.ApplyTo(car.transform, definition);
            VoxelMissileUpgradeState.ApplyTo(car.transform, definition);
            VoxelUnderbodyArmorUpgradeState.ApplyTo(car.transform, definition);
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

        private static IEnumerable<T> Ranked<T>(string path, Func<T, bool> fits, Func<T, int> tier) where T : ScriptableObject =>
            Resources.LoadAll<T>(path).Where(fits).OrderByDescending(tier).ThenBy(t => t.name, StringComparer.Ordinal);
    }
}
