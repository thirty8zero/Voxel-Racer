using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer.Editor
{
    public static class VoxelBoostUpgradeValidation
    {
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        [MenuItem("Tools/Voxel Racer/Validate Boost Bottle Upgrade")]
        public static void Run()
        {
            int cash = VoxelCurrencyState.Balance;
            bool purchased = VoxelBoostUpgradeState.IsPurchased;
            const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.NonPublic;
            var missing = (HashSet<string>)typeof(VoxelCarRunState).GetField("missingVoxelPaths", staticFlags).GetValue(null);
            var health = (Dictionary<string, int>)typeof(VoxelCarRunState).GetField("armorHealth", staticFlags).GetValue(null);
            var savedMissing = missing.ToArray(); var savedHealth = health.ToArray();
            var nameField = typeof(VoxelCarRunState).GetField("carDefinitionName", staticFlags);
            var savedName = nameField.GetValue(null);
            var oldEvent = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var root = new GameObject("Temporary Boost Upgrade Validation");
            try
            {
                var definition = AssetDatabase.LoadAssetAtPath<VoxelCarDefinition>("Assets/Resources/Cars/SpyCar2PlayerCar.asset");
                var tuning = VoxelBoostUpgradeTuning.LoadUpgrade();
                Check(tuning != null && tuning.Fits(definition), "Missing boost asset/model");
                foreach (var field in typeof(VoxelBoostTuning).GetFields(BindingFlags.Public | BindingFlags.Instance))
                    Check(typeof(VoxelBoostUpgradeTuning).GetField(field.Name) != null, "Missing boost option: " + field.Name);
                VoxelBoostUpgradeState.BeginNewRun(); VoxelCurrencyState.Reset();
                Check(VoxelBoostUpgradeState.ResolveTuning(definition) == VoxelBoostTuning.Load(), "Default boost changed before purchase");
                Check(!VoxelBoostUpgradeState.TryPurchase(tuning, definition), "Unaffordable purchase allowed");
                var carRoot = new GameObject("Car"); carRoot.transform.SetParent(root.transform, false);
                UnityEngine.Object.Instantiate(definition.visualPrefab, carRoot.transform);
                var car = carRoot.AddComponent<VoxelCarController>(); car.enabled = false; car.ResetIntegrityBaseline();
                int total = car.TotalIntegrityVoxels;
                var damaged = car.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
                damaged.gameObject.SetActive(false);
                var shop = root.AddComponent<VoxelRepairUpgradeSceneController>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(VoxelRepairUpgradeSceneController).GetField("definition", flags).SetValue(shop, definition);
                typeof(VoxelRepairUpgradeSceneController).GetProperty("DisplayedCar").SetValue(shop, car);
                typeof(VoxelRepairUpgradeSceneController).GetMethod("BuildUi", flags).Invoke(shop, null);
                VoxelCurrencyState.Add(tuning.purchasePrice * 2);
                typeof(VoxelRepairUpgradeSceneController).GetMethod("RefreshUi", flags).Invoke(shop, null);
                var button = root.GetComponentsInChildren<Button>(true).First(b => b.name == "Boost Bottle Purchase Button");
                Check(button.interactable, "Shop did not enable affordable upgrade");
                button.onClick.Invoke();
                Check(VoxelBoostUpgradeState.IsPurchased && !button.interactable && VoxelCurrencyState.Balance == tuning.purchasePrice, "Shop purchase failed");
                Check(!VoxelBoostUpgradeState.TryPurchase(tuning, definition) && VoxelCurrencyState.Balance == tuning.purchasePrice, "Duplicate charged cash");
                VoxelBoostUpgradeState.ApplyTo(car.transform, definition);
                Check(car.transform.Cast<Transform>().Count(t => t.name == VoxelBoostUpgradeState.InstanceName) == 1, "Duplicate bottle");
                Check(car.TotalIntegrityVoxels == total && car.MissingIntegrityVoxels == 1, "Purchase altered body damage/integrity");
                Check(VoxelBoostUpgradeState.ResolveTuning(definition) == tuning, "Purchased settings not selected");
                var bottle = car.transform.Find(VoxelBoostUpgradeState.InstanceName);
                Check(bottle.localPosition.x > 0 && Vector3.Dot(bottle.up, new Vector3(0, .5f, 1).normalized) > .999f, "Bottle does not follow right panel slope");
                var scroll = root.GetComponentInChildren<ScrollRect>(true);
                Check(scroll != null && scroll.content.rect.height > scroll.viewport.rect.height, "Upgrade list is not scrollable");
                Check(VoxelUpgradeFitCatalog.Discover().Any(e => e.Asset == tuning && e.Fits(definition)), "Fit preview missing bottle");
                VoxelBoostUpgradeState.BeginNewRun();
                Check(!VoxelBoostUpgradeState.IsPurchased && VoxelBoostUpgradeState.ResolveTuning(definition) == VoxelBoostTuning.Load(), "New run did not restore default");
                Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/BoostUpgradeValidation.txt", "PASS: all inherited boost settings; shop affordability, purchase and duplicate prevention; mounted slope; preserved integrity/damage; tuning replacement and new-run reset; scrollable shop; fit preview discovery.");
            }
            catch (Exception e) { Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/BoostUpgradeValidation.txt", "FAIL: " + e); throw; }
            finally
            {
                typeof(VoxelBoostUpgradeState).GetField("<IsPurchased>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, purchased);
                VoxelCurrencyState.Reset(); VoxelCurrencyState.Add(cash);
                missing.Clear(); foreach (var path in savedMissing) missing.Add(path);
                health.Clear(); foreach (var entry in savedHealth) health.Add(entry.Key, entry.Value);
                nameField.SetValue(null, savedName);
                UnityEngine.Object.DestroyImmediate(root);
                if (oldEvent == null) { var created = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(); if (created != null) UnityEngine.Object.DestroyImmediate(created.gameObject); }
            }
        }
    }
}
