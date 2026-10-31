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
    public static class VoxelExtendedArmorValidation
    {
        public const string Output = "Temp/ExtendedArmor";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Extended armour: " + message); }
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static VoxelArmorVoxel[] Pieces(Transform car, VoxelArmorSide side) => car.Find(VoxelArmorUpgradeState.UpgradeRootName + "/" +
            (side == VoxelArmorSide.Left ? VoxelArmorUpgradeState.LeftMountName : VoxelArmorUpgradeState.RightMountName)).GetComponentsInChildren<VoxelArmorVoxel>(true);

        [MenuItem("Tools/Voxel Racer/Validate and Render Extended Door Armour")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run from Edit Mode.");
            using var saved = new VoxelGarageUpgradeDockValidation.SavedState();
            var random = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewPreviewScene();
            VoxelRepairUpgradeSceneController shop = null;
            Directory.CreateDirectory(Output);
            try
            {
                var tuning = VoxelArmorTuning.LoadExtended(); var basic = VoxelArmorTuning.Load();
                var definition = AssetDatabase.LoadAssetAtPath<VoxelCarDefinition>("Assets/Resources/Cars/SpyCar2PlayerCar.asset");
                Check(tuning != null && tuning.panelPrefab != null && tuning.Fits(definition), "missing tuning/prefab or compatibility");
                Check(tuning.upgradeLevel > basic.upgradeLevel && tuning.voxelHitPoints == basic.voxelHitPoints, "variant level or per-voxel durability");
                var pieces = tuning.panelPrefab.GetComponentsInChildren<VoxelArmorVoxel>(true);
                Check(pieces.Length == 45 && tuning.panelPrefab.GetComponentsInChildren<MeshRenderer>(true).Length == 45, "expected 45 damage pieces and no decorative renderers");
                Check(tuning.panelPrefab.GetComponentsInChildren<Collider>(true).Length == 0, "unexpected colliders");
                float ratio = ProjectedArea(tuning.panelPrefab) / ProjectedArea(basic.panelPrefab);
                Check(Mathf.Abs(ratio - 1.5f) < .002f, "projected door coverage ratio is " + ratio);
                Color grey = pieces[0].GetComponent<MeshRenderer>().sharedMaterial.color;
                Check(Mathf.Abs(grey.r - grey.g) < .001f && Mathf.Abs(grey.g - grey.b) < .001f && grey.r >= .35f && grey.r <= .6f, "not mid grey");
                var fit = VoxelUpgradeFitCatalog.Discover().Where(e => e.Asset == tuning).ToArray();
                Check(fit.Length == 2 && fit.All(e => e.Fits(definition)), "both preview sides missing");
                Check(VoxelUpgradeDashboardCatalog.Discover().Count(e => e.Asset == tuning) == 1, "dashboard must show tuning once");
                var wrong = AssetDatabase.LoadAssetAtPath<VoxelCarDefinition>("Assets/Resources/Cars/FormulaCar.asset");
                Check(!tuning.Fits(wrong) && fit.All(e => !e.Fits(wrong)), "incompatible car accepted");
                VoxelCarRunState.BeginNewRun(definition);
                Check(!VoxelArmorUpgradeState.TryPurchase(tuning, definition, VoxelArmorSide.Left), "unaffordable purchase succeeded");
                VoxelCurrencyState.Add(10000);
                var car = NewCar(scene, definition);
                var body = car.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
                body.gameObject.SetActive(false); int baseCount = car.TotalIntegrityVoxels;
                Check(VoxelArmorUpgradeState.TryPurchase(basic, definition, VoxelArmorSide.Left), "base purchase failed");
                VoxelArmorUpgradeState.ApplyTo(car.transform, definition); car.ResetIntegrityBaseline();
                var oldPiece = Pieces(car.transform, VoxelArmorSide.Left)[0]; oldPiece.AbsorbDamage(1);
                VoxelCarRunState.Capture(car, definition);
                int cash = VoxelCurrencyState.Balance;
                var shopObject = new GameObject("Extended Armour Shop QA"); SceneManager.MoveGameObjectToScene(shopObject, scene);
                shop = shopObject.AddComponent<VoxelRepairUpgradeSceneController>();
                typeof(VoxelRepairUpgradeSceneController).GetProperty("DisplayedCar").SetValue(shop, car);
                typeof(VoxelRepairUpgradeSceneController).GetField("definition", Private).SetValue(shop, definition);
                shop.repairTuning = VoxelRepairTuning.Load(); Call(shop, "BuildUi"); Call(shop, "RefreshUi");
                VoxelGarageUpgradeDockValidation.CheckCategories(shop);
                Check(shop.GetComponentsInChildren<Button>(true).Single(b => b.name == "Extended Door Armor Purchase Button").interactable, "shop card unavailable");
                Call(shop, "BeginUpgradePlacement", VoxelGarageUpgradeKind.ExtendedArmour);
                Call(shop, "SelectUpgradePlacement", 0);
                Check(VoxelCurrencyState.Balance == cash && VoxelArmorUpgradeState.InstalledTuning(VoxelArmorSide.Left) == basic && oldPiece.NeedsRepair,
                    "preview spent money, replaced purchase or healed damage");
                Call(shop, "CancelUpgradePlacement");
                Check(oldPiece.NeedsRepair && !body.gameObject.activeSelf, "cancel healed armour/body");
                Call(shop, "BeginUpgradePlacement", VoxelGarageUpgradeKind.ExtendedArmour); Call(shop, "SelectUpgradePlacement", 0); Call(shop, "ConfirmUpgradePurchase");
                Check(VoxelCurrencyState.Balance == cash - tuning.panelPurchasePrice && VoxelArmorUpgradeState.InstalledTuning(VoxelArmorSide.Left) == tuning,
                    "confirmed replacement price/ownership failed");
                Check(Pieces(car.transform, VoxelArmorSide.Left).Length == 45 && car.TotalIntegrityVoxels == baseCount + 45 && !body.gameObject.activeSelf,
                    "replacement stacked armour or changed body damage");
                Check(!VoxelArmorUpgradeState.TryPurchase(tuning, definition, VoxelArmorSide.Left) && !VoxelArmorUpgradeState.TryPurchase(basic, definition, VoxelArmorSide.Left),
                    "duplicate/downgrade allowed");
                Check(VoxelArmorUpgradeState.TryPurchase(basic, definition, VoxelArmorSide.Right), "independent opposite door purchase failed");
                VoxelArmorUpgradeState.ApplyTo(car.transform, definition); car.ResetIntegrityBaseline();
                Check(Pieces(car.transform, VoxelArmorSide.Right).Length == 30 && car.TotalIntegrityVoxels == baseCount + 75, "mixed panel variants failed");
                var armour = Pieces(car.transform, VoxelArmorSide.Left);
                armour[0].AbsorbDamage(1); armour[1].AbsorbDamage(armour[1].MaximumHealth); armour[1].gameObject.SetActive(false);
                VoxelArmorUpgradeState.ApplyTo(car.transform, definition); car.ResetIntegrityBaseline();
                Check(armour[0].RemainingHealth == tuning.voxelHitPoints - 1 && !armour[1].gameObject.activeSelf, "repeat mount healed armour");
                VoxelCarRunState.Capture(car, definition);
                var next = NewCar(scene, definition); VoxelArmorUpgradeState.ApplyTo(next.transform, definition); next.ResetIntegrityBaseline(); VoxelCarRunState.Apply(next, definition);
                var restored = Pieces(next.transform, VoxelArmorSide.Left);
                Check(restored[0].RemainingHealth == armour[0].RemainingHealth && !restored[1].gameObject.activeSelf && next.MissingIntegrityVoxels == 2,
                    "next mission lost panel/body damage or partial HP");
                Check(next.RepairPercent(100) >= 2 && next.RepairableIntegrityVoxels == 0 && restored.All(p => p.RemainingHealth == p.MaximumHealth), "normal repair failed");
                // A repaired model-only copy provides comparable base/extended and combined renders.
                var model = Object.Instantiate(definition.visualPrefab); SceneManager.MoveGameObjectToScene(model, scene);
                var original = model.GetComponentsInChildren<Renderer>(true).ToArray();
                VoxelArmorUpgradeState.CreatePair(model.transform, basic); Render(model, "BasicRight", new Vector3(5, 2.1f, -4));
                VoxelArmorUpgradeState.CreatePair(model.transform, tuning);
                foreach (var entry in fit) entry.Build(model.transform);
                Check(model.GetComponentsInChildren<VoxelArmorVoxel>(true).Length == 90, "idempotent pair preview failed");
                Render(model, "MountedRight", new Vector3(5, 2.1f, -4)); Render(model, "MountedLeft", new Vector3(-5, 2.1f, -4));
                Render(model, "Closeup", new Vector3(4.5f, 1.8f, -1.7f), new Vector3(1.1f, .78f, -.17f), .87f);
                foreach (var entry in VoxelUpgradeFitCatalog.Discover().Where(e => e.Asset != tuning && e.Fits(definition))) entry.Build(model.transform);
                Check(model.GetComponentsInChildren<VoxelArmorVoxel>(true).Length == 90, "combined preview downgraded/stacked armour");
                Render(model, "CombinedRight", new Vector3(5, 2.1f, -4)); Render(model, "CombinedLeft", new Vector3(-5, 2.1f, -4));
                foreach (var r in original.Where(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null)) r.enabled = false;
                Render(model, "BodyHiddenRight", new Vector3(5, 2.1f, -4)); Render(model, "BodyHiddenLeft", new Vector3(-5, 2.1f, -4));
                foreach (var r in original) r.enabled = false;
                Render(model, "UpgradesOnlyRight", new Vector3(5, 2.1f, -4)); Render(model, "UpgradesOnlyLeft", new Vector3(-5, 2.1f, -4));
                Check(VoxelArmorUpgradeState.InstalledTuning(VoxelArmorSide.Left) == tuning && VoxelArmorUpgradeState.InstalledTuning(VoxelArmorSide.Right) == basic,
                    "fit preview changed installed variants");
                VoxelCarRunState.BeginNewRun(definition);
                Check(!VoxelArmorUpgradeState.IsPurchased && VoxelArmorUpgradeState.InstalledTuning(VoxelArmorSide.Left) == null, "new run kept armour");
                int triangles = pieces.Sum(p => p.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
                File.WriteAllText(Output + "/Validation.txt", "PASS: projected door area " + ratio.ToString("F4") + "x; mid grey; 45 pieces / " + triangles +
                    " triangles; same per-voxel HP; no colliders; separate tuning, both fit entries and one dashboard row; incompatible/unaffordable/duplicate/downgrade checks; actual shop preview/cancel/confirm and category/all-card placement; independent doors, base-to-extended replacement without stacking/body healing; idempotence; partial/missing armour/body persistence and full repair; new-run reset; both-side isolated/combined/body-hidden/upgrades-only native Unity renders. Saved state restored.");
                Debug.Log(File.ReadAllText(Output + "/Validation.txt"));
            }
            catch (Exception e) { File.WriteAllText(Output + "/Validation.txt", "FAIL: " + e); throw; }
            finally
            {
                if (shop != null) Call(shop, "CancelUpgradePlacement");
                EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Random.state = random;
            }
        }

        private static VoxelCarController NewCar(Scene scene, VoxelCarDefinition definition)
        {
            var root = new GameObject("Extended Armour Car QA"); SceneManager.MoveGameObjectToScene(root, scene);
            Object.Instantiate(definition.visualPrefab, root.transform);
            var car = root.AddComponent<VoxelCarController>(); car.enabled = false; car.debrisVoxelsPerDamagedVoxel = 0; car.ResetIntegrityBaseline(); return car;
        }

        private static float ProjectedArea(GameObject model)
        {
            var points = model.GetComponentsInChildren<MeshFilter>(true).SelectMany(f => f.sharedMesh.vertices.Select(v => {
                Vector3 p = model.transform.InverseTransformPoint(f.transform.TransformPoint(v)); return new Vector2(p.z, p.y);
            })).Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToArray();
            float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            var hull = new List<Vector2>();
            foreach (var p in points) { while (hull.Count > 1 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
            int lower = hull.Count;
            foreach (var p in points.Reverse().Skip(1)) { while (hull.Count > lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
            float area = 0; for (int i = 0; i < hull.Count - 1; i++) area += hull[i].x * hull[i + 1].y - hull[i + 1].x * hull[i].y;
            return Mathf.Abs(area) * .5f;
        }

        private static void Render(GameObject source, string name, Vector3 position, Vector3? target = null, float zoom = 2.35f)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                var copy = Object.Instantiate(source); foreach (var b in copy.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
                preview.AddSingleGO(copy); preview.camera.orthographic = true; preview.camera.orthographicSize = zoom;
                preview.camera.transform.position = position; preview.camera.transform.LookAt(target ?? new Vector3(0, .8f, 0));
                preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 50; preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.13f, .16f, .19f); preview.ambientColor = new Color(.45f, .45f, .45f);
                preview.lights[0].intensity = 1.6f; preview.lights[0].transform.rotation = Quaternion.Euler(35, -60, 0);
                preview.lights[1].intensity = 1.2f; preview.lights[1].transform.rotation = Quaternion.Euler(25, 120, 0);
                preview.BeginStaticPreview(new Rect(0, 0, 1200, 800)); preview.Render(true);
                var image = preview.EndStaticPreview(); try { File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG()); } finally { Object.DestroyImmediate(image); }
            }
            finally { preview.Cleanup(); }
        }
    }
}
