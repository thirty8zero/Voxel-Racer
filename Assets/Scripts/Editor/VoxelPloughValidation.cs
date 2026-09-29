using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelPloughValidation
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

        [MenuItem("Tools/Voxel Racer/Validate Plough Upgrade")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
            int cash = VoxelCurrencyState.Balance;
            bool owned = VoxelPloughUpgradeState.IsPurchased;
            var randomState = UnityEngine.Random.state;
            var missing = (HashSet<string>)typeof(VoxelCarRunState).GetField("missingVoxelPaths", Statics).GetValue(null);
            var armor = (Dictionary<string, int>)typeof(VoxelCarRunState).GetField("armorHealth", Statics).GetValue(null);
            var savedMissing = missing.ToArray(); var savedArmor = armor.ToArray();
            var nameField = typeof(VoxelCarRunState).GetField("carDefinitionName", Statics); var savedName = nameField.GetValue(null);
            var oldEvent = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var root = new GameObject("Temporary plough validation");
            var incompatible = ScriptableObject.CreateInstance<VoxelCarDefinition>();
            try
            {
                var tuning = VoxelPloughTuning.Load();
                var definition = AssetDatabase.LoadAssetAtPath<VoxelCarDefinition>("Assets/Resources/Cars/SpyCar2PlayerCar.asset");
                Check(tuning != null && tuning.Fits(definition) && !tuning.Fits(incompatible) && !tuning.Fits(null), "Compatibility failed");
                var car = MakeCar(root.transform, definition);
                int integrity = car.TotalIntegrityVoxels;
                car.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null).gameObject.SetActive(false);
                VoxelPloughUpgradeState.BeginNewRun(); VoxelCurrencyState.Reset();
                if (tuning.purchasePrice > 0) Check(!VoxelPloughUpgradeState.TryPurchase(tuning, definition), "Unaffordable purchase allowed");
                var shop = root.AddComponent<VoxelRepairUpgradeSceneController>();
                typeof(VoxelRepairUpgradeSceneController).GetField("definition", Fields).SetValue(shop, definition);
                typeof(VoxelRepairUpgradeSceneController).GetProperty("DisplayedCar").SetValue(shop, car);
                typeof(VoxelRepairUpgradeSceneController).GetMethod("BuildUi", Fields).Invoke(shop, null);
                VoxelCurrencyState.Add(tuning.purchasePrice + 123);
                typeof(VoxelRepairUpgradeSceneController).GetMethod("RefreshUi", Fields).Invoke(shop, null);
                var button = root.GetComponentsInChildren<Button>(true).First(b => b.name == "Plough Purchase Button");
                Check(button.interactable, "Plough shop disabled"); button.onClick.Invoke();
                Check(VoxelPloughUpgradeState.IsPurchased && VoxelCurrencyState.Balance == 123 && !button.interactable, "Shop purchase failed");
                Check(!VoxelPloughUpgradeState.TryPurchase(tuning, definition), "Duplicate purchase allowed");
                VoxelPloughUpgradeState.ApplyTo(car.transform, definition);
                Check(car.transform.Cast<Transform>().Count(t => t.name == VoxelPloughUpgradeState.InstanceName) == 1, "Duplicate mount");
                Check(car.TotalIntegrityVoxels == integrity && car.MissingIntegrityVoxels == 1, "Installing plough changed integrity");
                var next = MakeCar(root.transform, definition);
                VoxelPloughUpgradeState.ApplyTo(next.transform, definition); VoxelCarRunState.Apply(next, definition);
                Check(next.MissingIntegrityVoxels == 1, "Scene transition lost body damage");
                VerifyDamage(car, tuning);
                VerifyEnemyCollisions(root.transform, definition, tuning);
                var entries = VoxelUpgradeFitCatalog.Discover();
                var entry = entries.Single(e => e.Asset == tuning);
                var preview = Object.Instantiate(definition.visualPrefab, root.transform);
                var baseRenderers = preview.GetComponentsInChildren<Renderer>(true);
                entry.Build(preview.transform);
                Check(preview.transform.Find(VoxelPloughUpgradeState.InstanceName) != null, "Preview mount missing");
                Check(preview.transform.Find(VoxelPloughUpgradeState.InstanceName).GetComponentsInChildren<Collider>().Length == 0, "Plough should use existing swept collision system");
                Directory.CreateDirectory("Temp/Plough");
                Render(preview, "FrontLeft", false); Render(preview, "FrontRight", true);
                foreach (var other in entries.Where(e => e.Asset != tuning && e.Fits(definition))) other.Build(preview.transform);
                Render(preview, "Combined", false);
                foreach (var r in baseRenderers) if (r.GetComponentInParent<VoxelIndestructiblePart>() == null) r.enabled = false;
                Render(preview, "BodyHidden", true);
                foreach (var r in baseRenderers) r.enabled = false;
                Render(preview, "UpgradesOnly", false);
                Check(VoxelCurrencyState.Balance == 123 && VoxelPloughUpgradeState.IsPurchased, "Preview changed purchases");
                VoxelPloughUpgradeState.BeginNewRun();
                Check(!VoxelPloughUpgradeState.IsPurchased && Mathf.Approximately(VoxelPloughUpgradeState.ImpactDamage(20, car.transform, Vector3.forward), 20), "New run did not reset combat bonus");
                File.WriteAllText("Temp/Plough/Validation.txt", "PASS: shop purchase/affordability/duplicate prevention; compatibility; damage persistence; front damage and protection; side/rear exclusions; fractional protection; preview discovery; combined fit and both sides/body-hidden/upgrades-only renders; new-run reset.");
                Debug.Log(File.ReadAllText("Temp/Plough/Validation.txt"));
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(incompatible);
                typeof(VoxelPloughUpgradeState).GetField("purchased", Statics).SetValue(null, owned);
                VoxelCurrencyState.Reset(); VoxelCurrencyState.Add(cash);
                missing.Clear(); foreach (var p in savedMissing) missing.Add(p);
                armor.Clear(); foreach (var p in savedArmor) armor.Add(p.Key, p.Value); nameField.SetValue(null, savedName);
                UnityEngine.Random.state = randomState;
                if (oldEvent == null) { var e = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(); if (e != null) Object.DestroyImmediate(e.gameObject); }
            }
        }

        private static VoxelCarController MakeCar(Transform root, VoxelCarDefinition definition)
        {
            var holder = new GameObject("Test car"); holder.transform.SetParent(root, false);
            Object.Instantiate(definition.visualPrefab, holder.transform);
            var car = holder.AddComponent<VoxelCarController>(); car.enabled = false;
            car.SetTuning(definition.tuning); car.debrisVoxelsPerDamagedVoxel = 0;
            car.ResetIntegrityBaseline(); return car;
        }
        private static void VerifyDamage(VoxelCarController car, VoxelPloughTuning tuning)
        {
            foreach (float damage in new[] { .5f, 20f, 100f })
            {
                Check(Mathf.Approximately(VoxelPloughUpgradeState.ImpactDamage(damage, car.transform, Vector3.forward), damage * (1 + tuning.impactDamageBonusPercent / 100)), "Front damage scaling failed");
                foreach (var direction in new[] { Vector3.back, Vector3.left, Vector3.right })
                {
                    Check(Mathf.Approximately(VoxelPloughUpgradeState.ImpactDamage(damage, car.transform, direction), damage), "Non-front damage boosted");
                    Check(Mathf.Approximately(VoxelPloughUpgradeState.PlayerDamage(damage, car.transform, direction), damage), "Non-front damage reduced");
                }
            }
            int before = car.RemainingIntegrityVoxels;
            car.damageVoxelsPerHit = 1;
            for (int i = 0; i < 20; i++)
            {
                typeof(VoxelCarController).GetField("nextDamageTime", Fields).SetValue(car, -1f);
                car.ApplyCollisionDamage(new Vector3(0, .5f, 2), Vector3.forward, "Plough validation");
            }
            int expected = Mathf.FloorToInt(20 * (1f - tuning.playerDamageReductionPercent / 100f) + .0001f);
            Check(before - car.RemainingIntegrityVoxels == expected, "Fractional voxel protection incorrect");
            Check(car.damageVoxelsPerHit == 1, "Damage setting leaked");
        }

        private static void VerifyEnemyCollisions(Transform root, VoxelCarDefinition definition, VoxelPloughTuning plough)
        {
            var traffic = ScriptableObject.CreateInstance<VoxelObstacleCarTuning>();
            traffic.obstacleDamageVoxelsMin = traffic.obstacleDamageVoxelsMax = 0;
            var boss = Resources.Load<VoxelBossDefinition>("Bosses/RedVanBoss");
            try
            {
                foreach (bool isBoss in new[] { false, true })
                {
                    var car = MakeCar(root, definition);
                    VoxelPloughUpgradeState.ApplyTo(car.transform, definition);
                    var go = new GameObject("Test collision enemy"); go.transform.SetParent(root, false);
                    go.transform.position = Vector3.forward * 4;
                    var body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.transform.SetParent(go.transform, false);
                    var enemy = go.AddComponent<VoxelEnemyCar>(); enemy.enabled = false;
                    var tuning = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>();
                    try
                    {
                        tuning.vehicleHealth = 10000;
                        tuning.playerRamDamage = isBoss ? boss.playerRamDamage : Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning").playerRamDamage;
                        tuning.playerDamageVoxelsMin = tuning.playerDamageVoxelsMax = 4;
                        typeof(VoxelEnemyCar).GetField("target", Fields).SetValue(enemy, car);
                        typeof(VoxelEnemyCar).GetField("trafficTuning", Fields).SetValue(enemy, traffic);
                        typeof(VoxelEnemyCar).GetField("bossSettings", Fields).SetValue(enemy, isBoss ? boss : null);
                        typeof(VoxelEnemyCar).GetProperty("Tuning").SetValue(enemy, tuning);
                        typeof(VoxelEnemyCar).GetProperty("CurrentHealth").SetValue(enemy, tuning.vehicleHealth);
                        int before = car.RemainingIntegrityVoxels;
                        typeof(VoxelEnemyCar).GetMethod("RamByPlayer", Fields).Invoke(enemy, new object[] { (Vector3?)Vector3.forward });
                        float expected = tuning.playerRamDamage * (1 + plough.impactDamageBonusPercent / 100);
                        Check(Mathf.Abs((tuning.vehicleHealth - enemy.CurrentHealth) - expected) < .01f, "Enemy/boss ram did not apply plough bonus");
                        Check(before - car.RemainingIntegrityVoxels == Mathf.FloorToInt(4 * (1 - plough.playerDamageReductionPercent / 100)), "Enemy collision did not apply protection");
                    }
                    finally { Object.DestroyImmediate(tuning); }
                }
            }
            finally { Object.DestroyImmediate(traffic); }
        }

        private static void Render(GameObject source, string name, bool right)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(Object.Instantiate(source));
                preview.camera.transform.position = new Vector3(right ? 6 : -6, 3.7f, 8);
                preview.camera.transform.LookAt(new Vector3(0, .5f, .35f)); preview.camera.fieldOfView = 37;
                preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 50;
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.17f, .20f, .23f);
                preview.ambientColor = new Color(.5f, .5f, .5f);
                preview.lights[0].intensity = 1.8f; preview.lights[0].transform.rotation = Quaternion.Euler(40, 150, 0);
                preview.lights[1].intensity = 1; preview.lights[1].transform.rotation = Quaternion.Euler(30, 210, 0);
                preview.BeginStaticPreview(new Rect(0, 0, 1280, 800)); preview.Render(true);
                var image = preview.EndStaticPreview(); File.WriteAllBytes("Temp/Plough/" + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
        }
    }
}
