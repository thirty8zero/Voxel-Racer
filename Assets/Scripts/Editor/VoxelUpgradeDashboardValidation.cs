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
                    var paths = view.Groups.SelectMany(g => g.Fields).Select(f => f.Path)
                        .Concat(view.Description != null ? new[] { view.Description.propertyPath } : Array.Empty<string>()).ToArray();
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
            ValidateDescriptions(entries);
            report.Add("PASS default text in the editor, custom descriptions, reset and Undo/Redo for every upgrade, including launcher-owned descriptions");
            ValidateDamagePerSecond(entries);
            report.Add("PASS live DPS for built-in guns, fitted guns and linked missile weapons, including ammo and cooldown edge cases");
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
                    bool priceOrder = order == VoxelUpgradeDashboardSort.PriceAscending || order == VoxelUpgradeDashboardSort.PriceDescending;
                    if (priceOrder && !sorted[i - 1].Price.HasValue)
                        Check(!sorted[i].Price.HasValue, "Included or unconfigured prices should sort last");
                    if (priceOrder && !sorted[i].Price.HasValue) continue;
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

        private static void ValidateDescriptions(IEnumerable<VoxelUpgradeDashboardEntry> entries)
        {
            const string text = "Custom upgrade overview\nSecond line";
            foreach (var original in entries)
            {
                var asset = UnityEngine.Object.Instantiate(original.Asset);
                try
                {
                    var entry = new VoxelUpgradeDashboardEntry(asset, original.Fits);
                    var view = new VoxelUpgradeDashboard.TuningView(asset);
                    using (view.Serialized)
                    {
                        Check(view.Description?.propertyType == SerializedPropertyType.String,
                            $"{entry.Name} is missing its editable description");
                        string previous = view.Description.stringValue;
                        Check(view.DescriptionText(entry.DefaultDescription) ==
                            (string.IsNullOrWhiteSpace(previous) ? entry.DefaultDescription : previous),
                            $"{entry.Name} did not show its existing copy or generated default in the editor");
                        Check(view.SetDescription(text) && view.DescriptionText(entry.DefaultDescription) == text &&
                            entry.Description.StartsWith(text + "\n") &&
                            entry.Matches("Second line"), $"{entry.Name} did not show its edited description");
                        if (entry.WeaponTuning != null)
                            Check(entry.TuningSummary.Contains(entry.WeaponTuning.DamagePerSecondLabel), "Custom copy hid live DPS");
                        Undo.IncrementCurrentGroup();
                        Check(view.SetDescription(string.Empty) && view.DescriptionText(entry.DefaultDescription) == entry.DefaultDescription &&
                            view.DescriptionText(entry.DefaultDescription, showEmptyEdit: true) == string.Empty &&
                            entry.Description == entry.DefaultDescription, "Reset did not restore the generated description");
                        Undo.FlushUndoRecordObjects();
                        Undo.PerformUndo();
                        view.Serialized.Update();
                        Check(view.DescriptionText(entry.DefaultDescription) == text, "Undo reset did not restore the custom copy");
                        Undo.PerformRedo();
                        view.Serialized.Update();
                        Check(view.DescriptionText(entry.DefaultDescription) == entry.DefaultDescription, "Redo reset did not restore the default");
                        Undo.IncrementCurrentGroup();
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(asset); }
            }
        }

        private static void ValidateDamagePerSecond(IReadOnlyList<VoxelUpgradeDashboardEntry> entries)
        {
            var starter = Resources.Load<VoxelGunTuning>("Weapons/BasicHoodGunTuning");
            var definition = Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
            Check(starter != null && entries.Count(e => e.Asset == starter && e.IsIncluded && !e.Price.HasValue &&
                e.FitsCar(definition)) == 1, "The installed starter gun is missing from the dashboard");
            foreach (var entry in entries.Where(e => e.WeaponTuning != null))
                Check(entry.Description.Contains(entry.WeaponTuning.DamagePerSecondLabel), $"{entry.Name} is missing DPS");

            var weapon = ScriptableObject.CreateInstance<VoxelGunTuning>();
            var launcher = ScriptableObject.CreateInstance<VoxelMissileLauncherTuning>();
            try
            {
                var entry = new VoxelUpgradeDashboardEntry(weapon, Array.Empty<VoxelUpgradeFitEntry>());
                weapon.damagePerBullet = 2.5f;
                weapon.bulletsPerShot = 3;
                weapon.shotsPerSecond = 4f;
                Check(weapon.DamagePerSecond == 30f && entry.Description.Contains("30 DPS"), "Projectile damage, count and rate were not combined");
                using (var serialized = new SerializedObject(weapon))
                {
                    serialized.FindProperty("shotsPerSecond").floatValue = 8f;
                    serialized.ApplyModifiedProperties();
                    Check(weapon.DamagePerSecond == 60f && entry.Description.Contains("60 DPS"), "Serialized fire-rate edit did not refresh DPS");
                }
                weapon.ammunitionPerStage = 2;
                Check(weapon.DamagePerSecond == 0f, "An ammo supply smaller than one shot should yield zero DPS");
                weapon.ammunitionPerStage = 3;
                Check(weapon.DamagePerSecond == 60f, "DPS should describe firing while ammunition lasts");
                launcher.weapon = weapon;
                var missileEntry = new VoxelUpgradeDashboardEntry(launcher, Array.Empty<VoxelUpgradeFitEntry>());
                weapon.projectileKind = VoxelProjectileKind.Missile;
                weapon.useMissileCooldown = true;
                weapon.missileCooldownSeconds = 2.5f;
                Check(Mathf.Approximately(weapon.DamagePerSecond, 3f) && missileEntry.Description.Contains("3 DPS"),
                    "The linked missile's cooldown was not used");
                weapon.missileCooldownSeconds = 0f;
                Check(weapon.DamagePerSecond == 450f && missileEntry.Description.Contains("450 DPS @ 60 FPS"),
                    "Zero cooldown should report the labeled frame-based estimate");
                weapon.useMissileCooldown = false;
                Check(weapon.DamagePerSecond == 60f, "Disabling missile cooldown should restore the firing rate");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(launcher);
                UnityEngine.Object.DestroyImmediate(weapon);
            }
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
