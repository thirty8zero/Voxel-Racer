using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    /// <summary>Implement for every new upgrade family. Providers and tuning assets are discovered automatically.</summary>
    public interface IVoxelUpgradeFitProvider
    {
        IEnumerable<VoxelUpgradeFitEntry> Discover();
    }

    public sealed class VoxelUpgradeFitEntry
    {
        public ScriptableObject Asset;
        public string Label;
        public Func<VoxelCarDefinition, bool> Fits;
        // Build only into the supplied temporary car. Never modify run state or assets.
        public Action<Transform> Build;
        public bool Selected;
        public string Key => AssetDatabase.GetAssetPath(Asset) + ":" + Label;
    }

    public static class VoxelUpgradeFitCatalog
    {
        public static IEnumerable<T> Assets<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(g =>
                AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).Where(a => a != null);

        public static List<VoxelUpgradeFitEntry> Discover() => TypeCache
            .GetTypesDerivedFrom<IVoxelUpgradeFitProvider>()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .SelectMany(t => ((IVoxelUpgradeFitProvider)Activator.CreateInstance(t)).Discover())
            .OrderBy(e => e.Label).ThenBy(e => AssetDatabase.GetAssetPath(e.Asset)).ToList();
    }

    public sealed class VoxelArmorFitProvider : IVoxelUpgradeFitProvider
    {
        public IEnumerable<VoxelUpgradeFitEntry> Discover()
        {
            foreach (var tuning in VoxelUpgradeFitCatalog.Assets<VoxelArmorTuning>())
                for (int i = 0; i < 2; i++)
                {
                    bool right = i == 0;
                    yield return new VoxelUpgradeFitEntry {
                        Asset = tuning, Label = tuning.displayName + (right ? " — Right door" : " — Left door"),
                        Fits = c => tuning.panelPrefab != null && tuning.Fits(c),
                        Build = car => VoxelArmorUpgradeState.CreatePanel(car, tuning,
                            right ? VoxelArmorSide.Right : VoxelArmorSide.Left)
                    };
                }
        }
    }

    public sealed class VoxelGunFitProvider : IVoxelUpgradeFitProvider
    {
        public IEnumerable<VoxelUpgradeFitEntry> Discover()
        {
            foreach (var tuning in VoxelUpgradeFitCatalog.Assets<VoxelGunTuning>().Where(t => t.visualPrefab != null))
                for (int i = 0; i < Mathf.Max(1, tuning.maximumPurchases); i++)
                {
                    int slot = i;
                    yield return new VoxelUpgradeFitEntry {
                        Asset = tuning, Label = tuning.displayName + " — Slot " + (slot + 1),
                        Fits = c => c != null && c.visualPrefab != null,
                        Build = car => VoxelGunUpgradeState.CreateVisual(car, tuning, slot)
                    };
                }
        }
    }

    public sealed class VoxelBoostBottleFitProvider : IVoxelUpgradeFitProvider
    {
        public IEnumerable<VoxelUpgradeFitEntry> Discover()
        {
            foreach (var tuning in VoxelUpgradeFitCatalog.Assets<VoxelBoostUpgradeTuning>())
                yield return new VoxelUpgradeFitEntry {
                    Asset = tuning, Label = tuning.displayName + " — Right rear panel", Fits = tuning.Fits,
                    Build = car => VoxelBoostUpgradeState.CreateVisual(car, tuning)
                };
        }
    }

    public sealed class VoxelPerformanceWheelFitProvider : IVoxelUpgradeFitProvider
    {
        public IEnumerable<VoxelUpgradeFitEntry> Discover()
        {
            foreach (var tuning in VoxelUpgradeFitCatalog.Assets<VoxelPerformanceWheelTuning>())
                yield return new VoxelUpgradeFitEntry {
                    Asset = tuning, Label = tuning.displayName + " — Set of four", Fits = tuning.Fits,
                    Build = car => VoxelPerformanceWheelUpgradeState.CreateVisuals(car, tuning)
                };
        }
    }

    public sealed class VoxelWheelSpikeFitProvider : IVoxelUpgradeFitProvider
    {
        public IEnumerable<VoxelUpgradeFitEntry> Discover()
        {
            foreach (var tuning in VoxelUpgradeFitCatalog.Assets<VoxelWheelSpikeTuning>())
                yield return new VoxelUpgradeFitEntry {
                    Asset = tuning, Label = tuning.displayName + " — All wheels",
                    Fits = c => tuning.spikePrefab != null && c != null && c.visualPrefab != null &&
                        c.visualPrefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Voxel Wheel"),
                    Build = car => {
                        foreach (var wheel in car.GetComponentsInChildren<Transform>(true))
                            if (wheel.name == "Voxel Wheel") VoxelWheelSpikeUpgradeState.CreateVisual(car, wheel, tuning);
                    }
                };
        }
    }
}
