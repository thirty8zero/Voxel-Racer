using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelUpgradeDashboardValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Upgrade Dashboard")]
        public static void Run()
        {
            var report = new List<string>();
            var entries = VoxelUpgradeDashboardCatalog.Discover();
            var wheels = entries.Where(e => e.Asset is VoxelPerformanceWheelTuning || e.Asset is VoxelWheelSpikeTuning).ToArray();
            Check(wheels.Any(e => e.Asset is VoxelPerformanceWheelTuning) && wheels.Any(e => e.Asset is VoxelWheelSpikeTuning) &&
                wheels.All(e => e.Category == "Wheels") && !VoxelUpgradeDashboardCatalog.Categories(entries).Contains("Wheel spikes"),
                "Wheels must include performance wheels and all wheel-spike tunings");
            report.Add("PASS Wheels category: " + wheels.Length + " wheel/spike upgrades share filtering and category sort");
            var assets = entries.SelectMany(e => new[] { e.Asset, e.PriceAsset }).Where(a => a != null).Distinct().ToArray();
            var before = assets.ToDictionary(a => a, a => EditorJsonUtility.ToJson(a));
            int fields = 0;
            foreach (var asset in assets)
            {
                var view = new VoxelUpgradeDashboard.TuningView(asset);
                using (view.Serialized)
                {
                    view.Prepare();
                    var paths = view.Groups.SelectMany(g => g.Fields).Select(f => f.Path).ToArray();
                    var expected = new List<string>();
                    var iterator = view.Serialized.GetIterator();
                    bool children = true;
                    while (iterator.NextVisible(children))
                    {
                        children = false;
                        if (iterator.propertyPath != "m_Script") expected.Add(iterator.propertyPath);
                    }
                    Check(paths.Length == paths.Distinct().Count() && expected.OrderBy(p => p).SequenceEqual(paths.OrderBy(p => p)), asset.name + ": missing/duplicate dashboard fields");
                    fields += paths.Length;
                    report.Add("PASS field coverage: " + asset.name + " (" + paths.Length + ")");
                }
            }
            var clone = UnityEngine.Object.Instantiate(assets.OfType<VoxelPerformanceWheelTuning>().First());
            try
            {
                var view = new VoxelUpgradeDashboard.TuningView(clone);
                using (view.Serialized)
                {
                    float original = clone.accelerationBonusPercent;
                    view.Serialized.FindProperty("accelerationBonusPercent").floatValue = original + 3;
                    Check(view.Serialized.ApplyModifiedProperties() && clone.accelerationBonusPercent == original + 3, "Serialized performance edit failed");
                    Undo.FlushUndoRecordObjects();
                    Undo.PerformUndo();
                    Check(clone.accelerationBonusPercent == original, "Undo failed");
                    Undo.PerformRedo();
                    Check(clone.accelerationBonusPercent == original + 3, "Redo failed");
                    report.Add("PASS dashboard serialized performance edit / Undo / Redo on temporary tuning");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
            var gun = ScriptableObject.CreateInstance<VoxelGunTuning>();
            try
            {
                using var serialized = new SerializedObject(gun);
                Check(!VoxelOdinTuningLayout.UpgradeFieldVisible(serialized, "missilePrefab"), "Bullet exposed missile-only field");
                gun.projectileKind = VoxelProjectileKind.Missile;
                gun.useMissileCooldown = true;
                serialized.Update();
                Check(VoxelOdinTuningLayout.UpgradeFieldVisible(serialized, "missilePrefab") &&
                    VoxelOdinTuningLayout.UpgradeFieldVisible(serialized, "missileCooldownSeconds") &&
                    !VoxelOdinTuningLayout.UpgradeFieldVisible(serialized, "shotsPerSecond"), "Missile cooldown visibility failed");
                gun.useMissileCooldown = false;
                serialized.Update();
                Check(!VoxelOdinTuningLayout.UpgradeFieldVisible(serialized, "missileCooldownSeconds") &&
                    VoxelOdinTuningLayout.UpgradeFieldVisible(serialized, "shotsPerSecond"), "Missile firing visibility failed");
                report.Add("PASS bullet/missile/cooldown conditional fields");
            }
            finally { UnityEngine.Object.DestroyImmediate(gun); }
            foreach (VoxelUpgradeDashboardSort order in Enum.GetValues(typeof(VoxelUpgradeDashboardSort)))
            {
                var sorted = VoxelUpgradeDashboardCatalog.Sort(entries, order).ToArray();
                var categories = VoxelUpgradeDashboardCatalog.Categories(entries);
                for (int i = 1; i < sorted.Length; i++)
                {
                    int comparison = order == VoxelUpgradeDashboardSort.PriceAscending || order == VoxelUpgradeDashboardSort.PriceDescending
                        ? Nullable.Compare(sorted[i - 1].Price, sorted[i].Price)
                        : StringComparer.OrdinalIgnoreCase.Compare(sorted[i - 1].Name, sorted[i].Name);
                    if (order == VoxelUpgradeDashboardSort.Category)
                    {
                        int categoryComparison = Array.IndexOf(categories, sorted[i - 1].Category).CompareTo(Array.IndexOf(categories, sorted[i].Category));
                        if (categoryComparison != 0) comparison = categoryComparison;
                    }
                    bool descending = order == VoxelUpgradeDashboardSort.NameDescending || order == VoxelUpgradeDashboardSort.PriceDescending;
                    Check(descending ? comparison >= 0 : comparison <= 0, "Incorrect " + order);
                }
                report.Add("PASS sort: " + order);
            }
            Check(assets.All(a => before[a] == EditorJsonUtility.ToJson(a)), "Original upgrade values changed");
            report.Add("PASS original upgrade serialization unchanged");
            report.Add("TOTAL: " + entries.Count + " upgrades; " + fields + " serialized dashboard fields.");
            Directory.CreateDirectory("Temp/UpgradeDashboard");
            File.WriteAllLines("Temp/UpgradeDashboard/InteractionValidation.txt", report);
            Debug.Log(string.Join("\n", report));
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
