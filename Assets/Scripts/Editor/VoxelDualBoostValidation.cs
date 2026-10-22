using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelDualBoostValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Output = "Temp/DualBoost/";
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);

        [MenuItem("Tools/Voxel Racer/Validate Dual Boost Upgrade")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Validate upgrade content in Edit Mode.");
            Directory.CreateDirectory(Output);
            using var saved = new VoxelGarageUpgradeDockValidation.SavedState();
            var random = UnityEngine.Random.state;
            var globals = new Dictionary<FieldInfo, object>();
            foreach (var type in new[] { typeof(VoxelMissionProgress), typeof(VoxelStartCountdown), typeof(VoxelPauseMenu), typeof(VoxelLoadingScreen) })
            {
                var field = type.GetField("<Active>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
                globals[field] = field.GetValue(null); field.SetValue(null, null);
            }
            var events = EventSystem.current;
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary Dual Boost Validation"); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var definition = Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
                var single = VoxelBoostUpgradeTuning.LoadUpgrade(); var dual = VoxelBoostUpgradeTuning.LoadDualUpgrade();
                Check(dual != null && dual != single && dual.bottleCount == 2 && dual.purchasePrice == 1000 && dual.Fits(definition), "Dual boost tuning, price or compatibility incorrect");
                Check(dual.bottlePrefab == single.bottlePrefab, "Dual upgrade must reuse the existing bottle model");
                VoxelCarRunState.BeginNewRun(definition);
                VoxelCurrencyState.Add(999);
                Check(!VoxelBoostUpgradeState.TryPurchase(dual, definition), "Unaffordable dual purchase allowed");
                VoxelCurrencyState.Add(1);
                Check(VoxelBoostUpgradeState.TryPurchase(dual, definition) && VoxelCurrencyState.Balance == 0, "Direct dual purchase requires the single bottle");
                Check(!VoxelBoostUpgradeState.TryPurchase(single, definition), "Dual upgrade allowed downgrade");
                VoxelCarRunState.BeginNewRun(definition);
                VoxelCurrencyState.Add(single.purchasePrice + dual.purchasePrice + 100);
                Check(VoxelBoostUpgradeState.TryPurchase(single, definition), "Single bottle purchase regressed");
                var carObject = new GameObject("Temporary Car"); carObject.transform.SetParent(root.transform, false);
                Object.Instantiate(definition.visualPrefab, carObject.transform);
                var car = carObject.AddComponent<VoxelCarController>(); car.enabled = false; car.ResetIntegrityBaseline();
                var damaged = car.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
                damaged.gameObject.SetActive(false); int total = car.TotalIntegrityVoxels, missing = car.MissingIntegrityVoxels;
                VoxelBoostUpgradeState.ApplyTo(car.transform, definition);
                var shop = root.AddComponent<VoxelRepairUpgradeSceneController>();
                typeof(VoxelRepairUpgradeSceneController).GetProperty("DisplayedCar").SetValue(shop, car);
                typeof(VoxelRepairUpgradeSceneController).GetField("definition", Private).SetValue(shop, definition);
                shop.repairTuning = VoxelRepairTuning.Load(); Call(shop, "BuildUi"); Call(shop, "RefreshUi");
                VoxelGarageUpgradeDockValidation.CheckCategories(shop);
                var button = shop.GetComponentsInChildren<Button>(true).Single(b => b.name == "Dual Boost Bottle Purchase Button");
                int cash = VoxelCurrencyState.Balance;
                Check(button.interactable, "Dual shop card unavailable after single purchase");
                button.onClick.Invoke();
                VerifyBottles(Field<VoxelGarageUpgradePlacement>(shop, "upgradePlacement").Car.transform, dual);
                Check(VoxelCurrencyState.Balance == cash && VoxelBoostUpgradeState.InstalledTuning == single, "Preview modified cash or ownership");
                Call(shop, "BeginUpgradePlacement", VoxelGarageUpgradeKind.Boost);
                Check(Field<VoxelGarageUpgradePlacement>(shop, "upgradePlacement").PendingCount(VoxelGarageUpgradeKind.Boost) == 0, "Both boost variants entered the basket");
                Call(shop, "CancelUpgradePlacement");
                Check(car.transform.Find(VoxelBoostUpgradeState.LeftInstanceName) == null && VoxelCurrencyState.Balance == cash, "Cancel installed or charged dual boost");
                button.onClick.Invoke(); Call(shop, "ConfirmUpgradePurchase");
                Check(VoxelBoostUpgradeState.InstalledTuning == dual && VoxelCurrencyState.Balance == cash - 1000, "Confirmed replacement failed");
                Check(!VoxelBoostUpgradeState.TryPurchase(dual, definition) && !VoxelBoostUpgradeState.TryPurchase(single, definition), "Repeat or downgrade purchase accepted");
                VoxelBoostUpgradeState.ApplyTo(car.transform, definition); VerifyBottles(car.transform, dual);
                Check(!damaged.gameObject.activeSelf && total == car.TotalIntegrityVoxels && missing == car.MissingIntegrityVoxels, "Upgrade changed body damage/integrity");
                var next = Object.Instantiate(definition.visualPrefab, root.transform);
                VoxelBoostUpgradeState.ApplyTo(next.transform, definition); VerifyBottles(next.transform, dual);
                Check(VoxelBoostUpgradeState.ResolveTuning(definition) == dual, "Next race lost dual settings");
                var entries = VoxelUpgradeFitCatalog.Discover().Where(e => e.Asset == dual).ToArray();
                Check(entries.Length == 3, "Dual fit preview needs both/left/right choices");
                foreach (var entry in entries)
                {
                    var fit = Object.Instantiate(definition.visualPrefab, root.transform);
                    entry.Build(fit.transform); entry.Build(fit.transform);
                    int bottles = fit.transform.Cast<Transform>().Count(t => t.name == VoxelBoostUpgradeState.InstanceName || t.name == VoxelBoostUpgradeState.LeftInstanceName);
                    Check(bottles == (entry.Label.Contains("Both") ? 2 : 1), "Fit preview duplicates or omits bottles");
                    Object.DestroyImmediate(fit);
                }
                foreach (var other in VoxelUpgradeFitCatalog.Assets<VoxelCarDefinition>().Where(c => c != definition))
                    Check(!dual.Fits(other), "Dual compatibility differs from existing single bottle");
                Render(next, "IsolatedLeft", new Vector3(-6, 2.5f, -5)); Render(next, "IsolatedRight", new Vector3(6, 2.5f, -5));
                var combined = Object.Instantiate(definition.visualPrefab, root.transform);
                var original = combined.GetComponentsInChildren<Renderer>(true).ToHashSet();
                foreach (var entry in VoxelUpgradeFitCatalog.Discover().Where(e => e.Fits(definition))) entry.Build(combined.transform);
                VerifyBottles(combined.transform, dual);
                Render(combined, "CombinedLeft", new Vector3(-6, 2.5f, -5)); Render(combined, "CombinedRight", new Vector3(6, 2.5f, -5));
                foreach (var renderer in original) if (renderer != null && renderer.GetComponentInParent<VoxelIndestructiblePart>() == null) renderer.enabled = false;
                Render(combined, "BodyHiddenLeft", new Vector3(-6, 2.5f, -5)); Render(combined, "BodyHiddenRight", new Vector3(6, 2.5f, -5));
                Render(combined, "BottleBracesLeft", new Vector3(-2.5f, 2.8f, -4.2f), new Vector3(0, .95f, -1.4f), 1.15f);
                Render(combined, "BottleBracesRight", new Vector3(2.5f, 2.8f, -4.2f), new Vector3(0, .95f, -1.4f), 1.15f);
                foreach (var renderer in original) if (renderer != null) renderer.enabled = false;
                Render(combined, "UpgradesOnlyLeft", new Vector3(-6, 2.5f, -5)); Render(combined, "UpgradesOnlyRight", new Vector3(6, 2.5f, -5));
                Check(VoxelCurrencyState.Balance == cash - 1000 && VoxelBoostUpgradeState.InstalledTuning == dual, "Fit preview changed purchases");
                ValidateCharges(root, car, dual);
                VoxelCarRunState.BeginNewRun(definition);
                Check(!VoxelBoostUpgradeState.IsPurchased && VoxelBoostUpgradeState.InstalledTuning == null && VoxelBoostUpgradeState.ResolveTuning(definition) == VoxelBoostTuning.Load(), "New run retained dual upgrade");
                File.WriteAllText(Output + "Validation.txt", "PASS: $1000 direct/replacement purchases, affordability, duplicate/downgrade prevention; preview/cancel/confirmation and basket exclusivity; original body damage/integrity; next-race tuning and new-run reset; identical mirrored bottle models; both/left/right fit discovery and isolated/combined/body-hidden/upgrades-only renders; independent charge consumption/recharge, overlap rejection, cooldown during the other burst, empty/recharged state, disabled controller cleanup and default single-charge regression. Temporary state restored.");
                Debug.Log(File.ReadAllText(Output + "Validation.txt"));
            }
            catch (Exception e) { File.WriteAllText(Output + "Validation.txt", "FAIL: " + e); throw; }
            finally
            {
                Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
                foreach (var pair in globals) pair.Key.SetValue(null, pair.Value);
                UnityEngine.Random.state = random;
                if (events == null && EventSystem.current != null) Object.DestroyImmediate(EventSystem.current.gameObject);
            }
        }

        private static void VerifyBottles(Transform car, VoxelBoostUpgradeTuning tuning)
        {
            foreach (bool right in new[] { false, true })
            {
                string name = right ? VoxelBoostUpgradeState.InstanceName : VoxelBoostUpgradeState.LeftInstanceName;
                var bottles = car.Cast<Transform>().Where(t => t.name == name).ToArray();
                Check(bottles.Length == 1, "Missing or duplicate bottle on " + name);
                var bottle = bottles[0]; var expected = tuning.mountPosition; if (!right) expected.x = -expected.x;
                Check(Vector3.Distance(bottle.localPosition, expected) < .0001f &&
                    Vector3.Dot(bottle.up, new Vector3(0, .5f, 1).normalized) > .999f, "Bottle lost mirrored rear-panel fit");
                Check(bottle.GetComponent<VoxelIndestructiblePart>() != null && bottle.GetComponentsInChildren<Collider>().Length == 0, "Bottle protection/physics changed");
                var cradle = bottle.Find("Protected Bottle Cradle");
                Check(cradle != null && (right ? cradle.localScale.x > 0 : cradle.localScale.x < 0), "Bottle cradle faces away from the chassis");
                foreach (string braceName in new[] { "Upper Cage Brace", "Lower Cage Brace" })
                {
                    var brace = cradle.Find(braceName);
                    Check(brace != null, "Missing bottle support brace");
                    var a = car.InverseTransformPoint(brace.TransformPoint(Vector3.forward * .5f));
                    var b = car.InverseTransformPoint(brace.TransformPoint(Vector3.back * .5f));
                    Check(Mathf.Min(Mathf.Abs(a.x), Mathf.Abs(b.x)) < Mathf.Abs(bottle.localPosition.x) - .1f,
                        "Bottle brace does not reach inwards towards the chassis");
                    if (right) continue;
                    var rightBrace = car.Find(VoxelBoostUpgradeState.InstanceName).Find("Protected Bottle Cradle/" + braceName);
                    foreach (float end in new[] { -.5f, .5f })
                    {
                        var leftEnd = car.InverseTransformPoint(brace.TransformPoint(Vector3.forward * end));
                        var rightEnd = car.InverseTransformPoint(rightBrace.TransformPoint(Vector3.forward * end));
                        rightEnd.x = -rightEnd.x;
                        Check(Vector3.Distance(leftEnd, rightEnd) < .0001f, "Left bottle brace is not mirrored onto its chassis anchor");
                    }
                }
            }
        }

        private static void ValidateCharges(GameObject root, VoxelCarController car, VoxelBoostUpgradeTuning dual)
        {
            var boost = root.AddComponent<VoxelBoostController>(); boost.Configure(car, dual);
            Check(boost.ChargeCount == 2 && boost.IsReady, "Dual charges not full at race start");
            float now = Time.time;
            Check(boost.TryActivateBoost() && boost.ActiveChargeIndex == 0 && !boost.IsReady, "First bottle did not activate");
            Call(boost, "AdvanceCharges", now + dual.boostLength * .5f, 0f);
            Check(Mathf.Abs(boost.GetChargePercent(0) - .5f) < .001f && boost.GetChargePercent(1) == 1, "Boost consumed both bottles");
            Check(!boost.TryActivateBoost() && boost.ActiveChargeIndex == 0, "Concurrent boost accepted");
            Call(boost, "AdvanceCharges", now + dual.boostLength, 0f);
            Check(!boost.IsBoosting && boost.GetChargePercent(0) == 0 && boost.IsReady, "Unused bottle not ready after first burst");
            Check(boost.TryActivateBoost() && boost.ActiveChargeIndex == 1, "Second bottle not independently available");
            Call(boost, "AdvanceCharges", now + dual.boostLength * .5f, dual.boostLength * .5f);
            Check(boost.GetChargePercent(0) > 0 && boost.GetChargePercent(1) > 0 && boost.IsBoosting, "First cooldown froze during second burst");
            Call(boost, "AdvanceCharges", now + dual.boostLength, 0f);
            Check(!boost.IsReady && !boost.TryActivateBoost(), "Partially recharged bottle activated");
            var display = root.AddComponent<VoxelBoostDisplay>(); Call(display, "BuildHud"); display.Configure(boost);
            Call(display, "RefreshPresentation", boost.ChargePercent, boost.ChargePercent, false);
            var rings = root.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith("Boost Charge Ring")).ToArray();
            Check(rings.Length == 2 && rings.All(i => i.gameObject.activeSelf) && rings[0].fillAmount != rings[1].fillAmount, "Dual HUD does not independently display charges");
            RenderHud(root, "IndependentCharges");
            Call(boost, "AdvanceCharges", now + dual.rechargeCooldownLength, dual.rechargeCooldownLength);
            Check(boost.GetChargePercent(0) == 1 && boost.GetChargePercent(1) == 1 && boost.IsReady, "Independent recharge did not finish");
            Check(boost.TryActivateBoost(), "Recharged boost unavailable"); boost.enabled = false;
            Call(boost, "OnDisable"); // Non-ExecuteAlways components do not receive Edit Mode callbacks.
            Check(!boost.IsBoosting && boost.ActiveChargeIndex == -1 && boost.RamDamageMultiplier == 1, "Disabled boost retained active effects");
            boost.enabled = true; boost.Configure(car, VoxelBoostTuning.Load());
            Check(boost.ChargeCount == 1 && boost.TryActivateBoost(), "Default single boost regressed");
            Call(boost, "AdvanceCharges", Time.time + boost.Tuning.boostLength, 0f);
            Check(!boost.IsReady && !boost.TryActivateBoost(), "Default boost gained a second charge");
            Call(display, "RefreshPresentation", 0f, 0f, false);
            Check(!Field<Image>(display, "secondBoostRing").gameObject.activeSelf, "Default boost retained split HUD");
        }

        private static void Render(GameObject source, string name, Vector3 position, Vector3? centre = null, float zoom = 3.1f)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(Object.Instantiate(source)); preview.camera.orthographic = true; preview.camera.orthographicSize = zoom;
                preview.camera.transform.position = position; preview.camera.transform.LookAt(centre ?? new Vector3(0, .6f, 0));
                preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 50; preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.17f, .20f, .23f); preview.ambientColor = Color.gray;
                preview.lights[0].intensity = 1.8f; preview.lights[0].transform.rotation = Quaternion.Euler(40, 150, 0);
                preview.lights[1].intensity = 1; preview.lights[1].transform.rotation = Quaternion.Euler(30, 210, 0);
                preview.BeginStaticPreview(new Rect(0, 0, 1200, 800)); preview.Render(true);
                var image = preview.EndStaticPreview(); File.WriteAllBytes(Output + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
        }

        private static void RenderHud(GameObject root, string name)
        {
            var cameraObject = new GameObject("Dual Boost HUD Preview Camera"); cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = root.scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(root.scene);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .04f, .065f);
            camera.cullingMask = 1 << 31;
            var canvas = root.transform.Find("Boost HUD").GetComponent<Canvas>();
            foreach (var child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            canvas.GetComponent<CanvasScaler>().enabled = false; canvas.scaleFactor = 1;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            canvas.GetComponent<CanvasGroup>().alpha = 1;
            var texture = new RenderTexture(1920, 1080, 24); var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = texture; Canvas.ForceUpdateCanvases(); camera.Render(); camera.Render();
                RenderTexture.active = texture; var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                File.WriteAllBytes(Output + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject); }
        }
    }
}
