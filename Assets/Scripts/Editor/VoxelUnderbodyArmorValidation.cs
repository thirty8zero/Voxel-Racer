using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelUnderbodyArmorValidation
    {
        public const string Output = "Temp/UnderbodyArmor";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Underbody armour: " + message); }
        private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        internal static void Ready(VoxelCarController car) => typeof(VoxelCarController).GetField("nextDamageTime", Private).SetValue(car, -1f);

        [MenuItem("Tools/Voxel Racer/Validate and Render Underbody Mine Armour")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Start in Edit Mode.");
            using var saved = new VoxelGarageUpgradeDockValidation.SavedState();
            var random = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewPreviewScene();
            VoxelRepairUpgradeSceneController shop = null;
            Directory.CreateDirectory(Output);
            try
            {
                var tuning = VoxelUnderbodyArmorTuning.Load();
                var definition = Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
                Check(tuning != null && tuning.Fits(definition) && tuning.purchasePrice == 700 && Mathf.Approximately(tuning.mineDamageReductionPercent, 30), "missing model/tuning or requested price/protection");
                string beforeTuning = EditorJsonUtility.ToJson(tuning);
                var prefab = tuning.armorPrefab;
                var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                var mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
                Check(renderers.Length == 1 && renderers[0].sharedMaterials.Length == 3 && mesh.triangles.Length / 3 < 5000, "static mesh/material budget");
                Check(prefab.GetComponentsInChildren<Collider>(true).Length == 0 && prefab.GetComponent<VoxelIndestructiblePart>() != null, "kit must use protected collider-free hardware");
                Check(mesh.bounds.min.y > .10f && mesh.bounds.max.y < .281f && mesh.bounds.max.x < .8f && mesh.bounds.max.z < 2.51f && mesh.bounds.min.z > -2.51f, "kit protrudes outside underside envelope");
                foreach (float z in new[] { 1.80f, .64f, -.57f, -1.80f })
                {
                    Check(!Hit(mesh, new Ray(new Vector3(0, -1, z + .24f), Vector3.up)), "service opening is painted/closed");
                    Check(Hit(mesh, new Ray(new Vector3(.17f, -1, z), Vector3.up)), "solid plate surface is missing");
                }
                foreach (int side in new[] { -1, 1 })
                {
                    foreach (float z in new[] { -.57f, .4f }) Check(!Hit(mesh, new Ray(new Vector3(side * .4f, .11f, z), Vector3.up), .105f), "exhaust pipe intersects plate");
                    Check(!Hit(mesh, new Ray(new Vector3(side * .51f, .04f, -2.09f), Vector3.up), .32f), "rear muffler intersects plate");
                }
                var entries = VoxelUpgradeFitCatalog.Discover().Where(e => e.Asset == tuning).ToArray();
                Check(entries.Length == 1 && entries[0].Fits(definition) && !entries[0].Fits(Resources.Load<VoxelCarDefinition>("Cars/FormulaCar")), "fit discovery/compatibility");
                var dashboard = VoxelUpgradeDashboardCatalog.Discover().Single(e => e.Asset == tuning);
                Check(dashboard.Category == "Armour" && dashboard.Price == 700 && dashboard.Description.Contains("30"), "dashboard metadata");

                VoxelCarRunState.BeginNewRun(definition);
                VoxelCurrencyState.Add(699);
                Check(!VoxelUnderbodyArmorUpgradeState.TryPurchase(tuning, definition), "unaffordable purchase succeeded");
                VoxelCurrencyState.Add(301);
                var car = NewCar(scene, definition, false); int integrity = car.TotalIntegrityVoxels;
                var body = car.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
                body.gameObject.SetActive(false);
                var paths = car.GetComponentsInChildren<MeshRenderer>(true).ToDictionary(r => r, r => SiblingPath(car.transform, r.transform));
                var shopObject = new GameObject("Underbody Shop QA"); SceneManager.MoveGameObjectToScene(shopObject, scene);
                shop = shopObject.AddComponent<VoxelRepairUpgradeSceneController>();
                typeof(VoxelRepairUpgradeSceneController).GetProperty("DisplayedCar").SetValue(shop, car);
                typeof(VoxelRepairUpgradeSceneController).GetField("definition", Private).SetValue(shop, definition);
                shop.repairTuning = VoxelRepairTuning.Load(); Call(shop, "BuildUi"); Call(shop, "RefreshUi");
                VoxelGarageUpgradeDockValidation.CheckCategories(shop);
                Check(shop.GetComponentsInChildren<Button>(true).Single(b => b.name == "Underbody Armor Purchase Button").interactable, "shop card unavailable");
                Call(shop, "BeginUpgradePlacement", VoxelGarageUpgradeKind.UnderbodyArmour);
                Check(!VoxelUnderbodyArmorUpgradeState.IsPurchased && VoxelCurrencyState.Balance == 1000 && car.transform.Find(VoxelUnderbodyArmorUpgradeState.InstanceName) == null,
                    "preview purchased or edited the original car");
                Call(shop, "CancelUpgradePlacement"); Check(!body.gameObject.activeSelf && car.TotalIntegrityVoxels == integrity, "cancel healed body or changed integrity");
                Call(shop, "BeginUpgradePlacement", VoxelGarageUpgradeKind.UnderbodyArmour); Call(shop, "ConfirmUpgradePurchase");
                Check(VoxelUnderbodyArmorUpgradeState.IsPurchased && VoxelCurrencyState.Balance == 300 && car.TotalIntegrityVoxels == integrity && !body.gameObject.activeSelf,
                    "confirmed purchase price/ownership/body preservation");
                Check(paths.All(p => SiblingPath(car.transform, p.Key.transform) == p.Value), "mount shifted existing damage paths");
                Call(shop, "ConfirmUpgradePurchase"); Check(!VoxelUnderbodyArmorUpgradeState.TryPurchase(tuning, definition) && VoxelCurrencyState.Balance == 300, "duplicate charge");
                VoxelUnderbodyArmorUpgradeState.ApplyTo(car.transform, definition);
                Check(car.GetComponentsInChildren<VoxelUnderbodyArmorMount>(true).Length == 1, "repeat apply duplicated kit");
                VoxelCarRunState.Capture(car, definition);
                var next = NewCar(scene, definition); VoxelCarRunState.Apply(next, definition);
                Check(next.MissingIntegrityVoxels == 1 && next.TotalIntegrityVoxels == integrity && Mathf.Approximately(VoxelUnderbodyArmorUpgradeState.MineDamageMultiplier(next.transform), .7f), "next stage lost body damage/kit/effect");
                Check(next.RepairPercent(100) == 1 && next.RepairableIntegrityVoxels == 0, "protected kit changed normal repair count");
                var rails = next.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Left Frame Rail" || r.name == "Right Frame Rail").ToArray();
                var mountRoot = next.transform.Find(VoxelUnderbodyArmorUpgradeState.InstanceName);
                foreach (var anchor in mountRoot.GetComponentsInChildren<Transform>().Where(t => t.name.EndsWith("Frame Mount")))
                    Check(rails.Any(r => { var b = r.bounds; b.Expand(.002f); return b.Contains(anchor.position); }), "mount does not contact frame rail: " + anchor.name);

                ValidateDamage(scene, definition, tuning);
                var model = Object.Instantiate(definition.visualPrefab); SceneManager.MoveGameObjectToScene(model, scene);
                var original = model.GetComponentsInChildren<Renderer>(true).ToArray(); entries[0].Build(model.transform); entries[0].Build(model.transform);
                Check(model.GetComponentsInChildren<VoxelUnderbodyArmorMount>().Length == 1, "fit preview is not idempotent");
                Render(model, "MountedFront", new Vector3(5, 2.1f, 6)); Render(model, "MountedRear", new Vector3(-5, 2.1f, -6));
                Render(model, "LowFront", new Vector3(4, .3f, 6)); Render(model, "LowRear", new Vector3(-4, .3f, -6));
                Render(model, "Underside", new Vector3(4, -4, 5), new Vector3(0, .25f, 0), 2.8f);
                foreach (var entry in VoxelUpgradeFitCatalog.Discover().Where(e => e.Asset != tuning && e.Fits(definition))) entry.Build(model.transform);
                Render(model, "CombinedUnderside", new Vector3(-4, -4, -5), new Vector3(0, .25f, 0), 3.1f);
                foreach (var r in original.Where(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null)) r.enabled = false;
                Render(model, "BodyHiddenFront", new Vector3(4, -2.2f, 5)); Render(model, "BodyHiddenRear", new Vector3(-4, -2.2f, -5));
                foreach (var r in model.GetComponentsInChildren<Renderer>(true)) if (r.GetComponentInParent<VoxelUnderbodyArmorMount>() == null) r.enabled = false;
                Render(model, "KitOnly", new Vector3(3.2f, -4.5f, 3.5f), new Vector3(0, .18f, 0), 2.6f);
                VoxelCarRunState.BeginNewRun(definition); Check(!VoxelUnderbodyArmorUpgradeState.IsPurchased, "new run retained upgrade");
                Check(EditorJsonUtility.ToJson(tuning) == beforeTuning, "validation edited source tuning");
                File.WriteAllText(Output + "/Validation.txt", "PASS: $700 / 30% mine-only reduction; four real perforated plates, eight chassis contacts, low-profile bounds; " + mesh.triangles.Length / 3 +
                    " triangles / one renderer / three materials / no colliders or extra integrity; fit provider/dashboard/Odin tuning; actual shop preview/cancel/confirm, affordability/duplicate prevention and category/all-card inclusion; stable damage paths, repeat mounting, stage persistence/reset and normal body repair; all authored mine damage ranges, small-hit fractional carry/cooldown, unchanged non-mine damage and disabled/missing/unowned mounts; both-side normal/low/underside/combined/body-hidden/kit-only renders. Source tuning and run/RNG state restored.");
                Debug.Log(File.ReadAllText(Output + "/Validation.txt"));
            }
            catch (Exception e) { File.WriteAllText(Output + "/Validation.txt", "FAIL: " + e); throw; }
            finally { if (shop != null) Call(shop, "CancelUpgradePlacement"); EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Random.state = random; }
        }

        internal static VoxelCarController NewCar(Scene scene, VoxelCarDefinition definition, bool mounted = true)
        {
            var root = new GameObject("Underbody Armour Car QA"); SceneManager.MoveGameObjectToScene(root, scene); Object.Instantiate(definition.visualPrefab, root.transform);
            var car = root.AddComponent<VoxelCarController>(); car.enabled = false; car.debrisVoxelsPerDamagedVoxel = 0;
            if (mounted) VoxelUnderbodyArmorUpgradeState.ApplyTo(root.transform, definition); car.ResetIntegrityBaseline(); return car;
        }

        private static void ValidateDamage(Scene scene, VoxelCarDefinition definition, VoxelUnderbodyArmorTuning tuning)
        {
            foreach (var mine in VoxelUpgradeFitCatalog.Assets<VoxelMineLayerTuning>())
                foreach (int raw in new[] { mine.playerDamageVoxelsMin, mine.playerDamageVoxelsMax })
                {
                    var car = NewCar(scene, definition); car.damageVoxelsPerHit = raw; int before = car.RemainingIntegrityVoxels;
                    car.ApplyMineDamage(new Vector3(0, .5f, 0), Vector3.up);
                    Check(before - car.RemainingIntegrityVoxels == Mathf.FloorToInt(raw * .7f + .00001f), "mine range reduction failed: " + mine.name + " / " + raw);
                    Check(car.damageVoxelsPerHit == raw, "mine wrapper changed caller damage settings");
                }
            var small = NewCar(scene, definition); small.damageVoxelsPerHit = 1; int initial = small.RemainingIntegrityVoxels;
            for (int i = 0; i < 10; i++) { Ready(small); small.ApplyMineDamage(new Vector3(0, .5f, 0), Vector3.up); }
            Check(initial - small.RemainingIntegrityVoxels == 7, "ten one-point mines must cause seven damage");
            float carry = (float)typeof(VoxelCarController).GetField("mineDamageRemainder", Private).GetValue(small);
            small.ApplyMineDamage(Vector3.zero, Vector3.up);
            Check((float)typeof(VoxelCarController).GetField("mineDamageRemainder", Private).GetValue(small) == carry && initial - small.RemainingIntegrityVoxels == 7, "cooldown consumed protection carry/damage");
            foreach (string source in new[] { "Enemy bullets", "Potholes", "Explosion", "Enemy collision" })
            {
                var car = NewCar(scene, definition); car.damageVoxelsPerHit = 10; int before = car.RemainingIntegrityVoxels;
                car.ApplyDamage(new Vector3(0, .5f, 0), Vector3.up, source);
                Check(before - car.RemainingIntegrityVoxels == 10, "non-mine damage changed: " + source);
            }
            var disabled = NewCar(scene, definition); disabled.transform.Find(VoxelUnderbodyArmorUpgradeState.InstanceName).gameObject.SetActive(false);
            Check(VoxelUnderbodyArmorUpgradeState.MineDamageMultiplier(disabled.transform) == 1, "inactive kit protects");
            Check(VoxelUnderbodyArmorUpgradeState.MineDamageMultiplier(NewCar(scene, definition, false).transform) == 1, "missing kit protects");
            var clone = Object.Instantiate(tuning);
            try
            {
                foreach (float reduction in new[] { 0f, 100f })
                {
                    var car = NewCar(scene, definition); car.GetComponentInChildren<VoxelUnderbodyArmorMount>().tuning = clone;
                    clone.mineDamageReductionPercent = reduction; car.damageVoxelsPerHit = 10; int before = car.RemainingIntegrityVoxels;
                    car.ApplyMineDamage(new Vector3(0, .5f, 0), Vector3.up);
                    Check(before - car.RemainingIntegrityVoxels == (reduction == 0 ? 10 : 0), "configurable protection extreme failed");
                }
            }
            finally { Object.DestroyImmediate(clone); }
            VoxelUnderbodyArmorUpgradeState.BeginNewRun(); var preview = NewCar(scene, definition, false);
            VoxelUnderbodyArmorUpgradeState.CreateVisual(preview.transform, tuning);
            Check(VoxelUnderbodyArmorUpgradeState.MineDamageMultiplier(preview.transform) == 1, "unowned preview grants protection");
        }

        private static string SiblingPath(Transform root, Transform child)
        { var parts = new List<int>(); while (child != root) { parts.Add(child.GetSiblingIndex()); child = child.parent; } parts.Reverse(); return string.Join("/", parts); }

        private static bool Hit(Mesh mesh, Ray ray, float maximum = float.PositiveInfinity)
        {
            var vertices = mesh.vertices; var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], e1 = vertices[triangles[i + 1]] - a, e2 = vertices[triangles[i + 2]] - a;
                Vector3 p = Vector3.Cross(ray.direction, e2); float det = Vector3.Dot(e1, p); if (Mathf.Abs(det) < 1e-7f) continue;
                Vector3 t = ray.origin - a; float u = Vector3.Dot(t, p) / det; if (u < 0 || u > 1) continue;
                Vector3 q = Vector3.Cross(t, e1); float v = Vector3.Dot(ray.direction, q) / det;
                float distance = Vector3.Dot(e2, q) / det;
                if (v >= 0 && u + v <= 1 && distance >= 0 && distance <= maximum) return true;
            }
            return false;
        }

        private static void Render(GameObject source, string name, Vector3 position, Vector3? target = null, float zoom = 2.7f)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                var copy = Object.Instantiate(source); foreach (var b in copy.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
                preview.AddSingleGO(copy); preview.camera.orthographic = true; preview.camera.orthographicSize = zoom;
                preview.camera.transform.position = position; preview.camera.transform.LookAt(target ?? new Vector3(0, .6f, 0));
                preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 100; preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.12f, .15f, .19f); preview.ambientColor = new Color(.55f, .55f, .55f);
                preview.lights[0].intensity = 1.6f; preview.lights[0].transform.rotation = Quaternion.Euler(position.y < 0 ? -40 : 35, -45, 0);
                preview.lights[1].intensity = .9f; preview.lights[1].transform.rotation = Quaternion.Euler(position.y < 0 ? -25 : 30, 160, 0);
                preview.BeginStaticPreview(new Rect(0, 0, 1200, 800)); preview.Render(true);
                var image = preview.EndStaticPreview(); try { File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG()); } finally { Object.DestroyImmediate(image); }
            }
            finally { preview.Cleanup(); }
        }
    }
}
