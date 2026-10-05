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
    public static class VoxelGaragePlacementValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static VoxelGarageUpgradePlacement Placement(VoxelRepairUpgradeSceneController shop) =>
            (VoxelGarageUpgradePlacement)typeof(VoxelRepairUpgradeSceneController).GetField("upgradePlacement", Private).GetValue(shop);
        private static void Reset()
        {
            VoxelGunUpgradeState.BeginNewRun(); VoxelArmorUpgradeState.BeginNewRun(); VoxelMissileUpgradeState.BeginNewRun();
            VoxelPerformanceWheelUpgradeState.BeginNewRun(); VoxelWheelSpikeUpgradeState.BeginNewRun();
            VoxelBoostUpgradeState.BeginNewRun(); VoxelEngineUpgradeState.BeginNewRun(); VoxelPloughUpgradeState.BeginNewRun();
            VoxelCurrencyState.Reset(); VoxelCurrencyState.Add(100000);
        }

        [MenuItem("Tools/Voxel Racer/Validate Garage Upgrade Placement")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run from Edit Mode.");
            using var saved = new VoxelGarageUpgradeDockValidation.SavedState();
            var random = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewPreviewScene();
            var definition = AssetDatabase.LoadAssetAtPath<VoxelCarDefinition>("Assets/Resources/Cars/SpyCar2PlayerCar.asset");
            var oldEvents = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var carObjects = new List<GameObject>();
            try
            {
                CheckSceneOwnership(definition);
                foreach (VoxelGarageUpgradeKind kind in Enum.GetValues(typeof(VoxelGarageUpgradeKind)))
                {
                    Reset();
                    var root = new GameObject("Garage placement QA " + kind);
                    SceneManager.MoveGameObjectToScene(root, scene); carObjects.Add(root);
                    var carRoot = new GameObject("Temporary Car"); carRoot.transform.SetParent(root.transform, false);
                    Object.Instantiate(definition.visualPrefab, carRoot.transform);
                    var car = carRoot.AddComponent<VoxelCarController>(); car.enabled = false; car.ResetIntegrityBaseline();
                    var damaged = car.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
                    damaged.gameObject.SetActive(false);
                    int integrity = car.RemainingIntegrityVoxels, missing = car.RepairableIntegrityVoxels;
                    var rendering = car.GetComponentsInChildren<Renderer>(true).ToDictionary(r => r, r => r.enabled);
                    var shop = root.AddComponent<VoxelRepairUpgradeSceneController>();
                    typeof(VoxelRepairUpgradeSceneController).GetProperty("DisplayedCar").SetValue(shop, car);
                    typeof(VoxelRepairUpgradeSceneController).GetField("definition", Private).SetValue(shop, definition);
                    shop.repairTuning = VoxelRepairTuning.Load();
                    Call(shop, "BuildUi"); Call(shop, "RefreshUi");
                    int cash = VoxelCurrencyState.Balance;
                    Call(shop, "BeginUpgradePlacement", kind);
                    var preview = Placement(shop);
                    Check(preview != null && preview.Car != null, kind + " has no preview");
                    Check(preview.Car.scene == scene, kind + " preview escaped its car's scene");
                    Check(car.RemainingIntegrityVoxels == integrity && car.RepairableIntegrityVoxels == missing,
                        kind + " preview changed displayed integrity/damage");
                    Check(preview.Car.GetComponentsInChildren<MonoBehaviour>(true).All(b => !b.enabled), "Preview gameplay behaviour enabled");
                    Check(preview.Car.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Preview collider enabled");
                    Check(VoxelCurrencyState.Balance == cash, "Preview charged cash");
                    if (preview.RequiresPlacement)
                    {
                        Check(preview.AvailableSpotCount == 2 && preview.SelectedSlot == -1, kind + " is missing its green choices");
                        var green = preview.Car.GetComponentsInChildren<MeshRenderer>()
                            .Where(r => r.sharedMaterial != null && r.sharedMaterial.name == "Garage Light Green Placement").ToArray();
                        Check(green.Length > 0 && green.Select(r => r.sharedMaterial).Distinct().Count() == 1, "Ghosts do not share one green material");
                        var block = new MaterialPropertyBlock(); green[0].GetPropertyBlock(block);
                        Color tint = block.GetColor("_BaseColor"); Check(tint.g > tint.r && tint.a > 0 && tint.a < 1, "Ghosts are not transparent green");
                        CheckPick(preview, kind, 0); CheckPick(preview, kind, 1);
                        Call(shop, "SelectUpgradePlacement", 1);
                        Check(Placement(shop).SelectedSlot == 1, "Right-side selection lost");
                        Check(Placement(shop).IsPending(kind, 1) && Placement(shop).AvailableSpotCount == 1,
                            "Selecting the right slot must retain the affordable left ghost");
                        CheckPick(Placement(shop), kind, 0);
                    }
                    else Check(preview.SelectedSlot == 0, kind + " should go straight to confirmation");
                    var pendingMeshes = (Dictionary<MeshRenderer, VoxelGarageUpgradeSelection>)typeof(VoxelGarageUpgradePlacement)
                        .GetField("pendingModels", Private).GetValue(preview);
                    Check(pendingMeshes.Count > 0 && pendingMeshes.Values.All(p => p.Kind == kind && p.Slot == preview.SelectedSlot),
                        kind + " pending model cannot be identified for removal");
                    Check(shop.GetComponentsInChildren<Button>().Any(b => b.name == "Confirm Upgrade Purchase"), "Confirmation missing");
                    Call(shop, "CancelUpgradePlacement");
                    Check(Placement(shop) == null && VoxelCurrencyState.Balance == cash, "Cancel charged cash or kept preview");
                    Check(rendering.All(p => p.Key.enabled == p.Value) && !damaged.gameObject.activeSelf && car.RemainingIntegrityVoxels == integrity,
                        kind + " cancel did not restore original rendering and damage");

                    // Losing funds after selecting must invalidate the confirmation.
                    Call(shop, "BeginUpgradePlacement", kind);
                    if (Placement(shop).RequiresPlacement) Call(shop, "SelectUpgradePlacement", 1);
                    VoxelCurrencyState.Reset();
                    Call(shop, "ConfirmUpgradePurchase");
                    Check(Placement(shop) == null && VoxelCurrencyState.Balance == 0, "Stale confirmation bypassed affordability");
                    VoxelCurrencyState.Add(cash);
                    Call(shop, "BeginUpgradePlacement", kind);
                    if (Placement(shop).RequiresPlacement) Call(shop, "SelectUpgradePlacement", 1);
                    var offerArgs = new object[] { kind, 0, "" };
                    bool offer = (bool)typeof(VoxelRepairUpgradeSceneController).GetMethod("UpgradeOffer", Private).Invoke(shop, offerArgs);
                    Check(offer, "Upgrade unexpectedly unavailable"); int price = (int)offerArgs[1];
                    var confirm = shop.GetComponentsInChildren<Button>().First(b => b.name == "Confirm Upgrade Purchase");
                    confirm.onClick.Invoke();
                    Check(Placement(shop) == null && VoxelCurrencyState.Balance == cash - price, kind + " confirmation charged incorrectly");
                    confirm.onClick.Invoke(); Check(VoxelCurrencyState.Balance == cash - price, "Double confirmation charged twice");
                    Check(!damaged.gameObject.activeSelf, "Purchase repaired body damage");
                    if (kind == VoxelGarageUpgradeKind.Armour)
                        Check(VoxelArmorUpgradeState.IsRightPurchased && !VoxelArmorUpgradeState.IsLeftPurchased, "Armour side incorrect");
                    if (kind == VoxelGarageUpgradeKind.Missiles)
                        Check(VoxelMissileUpgradeState.IsPurchased(true) && !VoxelMissileUpgradeState.IsPurchased(false), "Missile side incorrect");
                    if (kind == VoxelGarageUpgradeKind.Guns)
                    {
                        Check(VoxelGunUpgradeState.IsPurchased(1) && !VoxelGunUpgradeState.IsPurchased(0), "Gun side incorrect");
                        VoxelGunUpgradeState.ApplyTo(car.transform, VoxelGunUpgradeState.LongGunTuning);
                        var mounts = car.transform.Find("Purchased Gun Upgrades");
                        Check(mounts.childCount == 1 && mounts.GetChild(0).localPosition.x > 0, "Rebuilt car lost right-first gun placement");
                    }
                    if (preview.RequiresPlacement)
                    {
                        Call(shop, "BeginUpgradePlacement", kind);
                        Check(Placement(shop).AvailableSpotCount == 1, "Occupied slot offered again");
                        Call(shop, "SelectUpgradePlacement", 0); confirm.onClick.Invoke();
                        Check(VoxelCurrencyState.Balance == cash - 2 * price, "Second slot purchase incorrect");
                    }
                    else
                    {
                        Call(shop, "BeginUpgradePlacement", kind); Check(Placement(shop) == null, "Owned single upgrade offered again");
                    }
                    Call(shop, "CancelUpgradePlacement");
                }
                CheckCart(scene, definition, carObjects);
                Directory.CreateDirectory("Temp");
                string report = "PASS (Edit Mode): all eight upgrade types; shared transparent green ghosts and mesh picking; remaining free side stays selectable; eleven pending parts across eight types, exact combined-cost confirmation, budget reservation and single-gun capacity; grouped summary and pending cards; replacement wheels fit previously selected spikes; cancel restores rendering and damage; stale funds/prices reject the entire cart; integrity stable; double confirmation guarded; right-first guns survive rebuilding; preview scene ownership and scene-close cleanup. Cash, purchases, damage, campaign and random state restored.";
                File.WriteAllText("Temp/GaragePlacementValidation.txt", report); Debug.Log(report);
                File.AppendAllText("Temp/GaragePlacementValidation.txt", "\nPASS: repeated settings overlay open/close preserves all eleven pending parts, preview instance, summary/quote, confirmation, scroll, cash and damage; underlying home/repair/upgrade screens retained.");
            }
            catch (Exception e)
            {
                Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/GaragePlacementValidation.txt", "FAIL: " + e); throw;
            }
            finally
            {
                foreach (var root in carObjects)
                {
                    if (root == null) continue;
                    var shop = root.GetComponent<VoxelRepairUpgradeSceneController>();
                    if (shop != null) Call(shop, "CancelUpgradePlacement");
                }
                EditorSceneManager.ClosePreviewScene(scene);
                if (oldEvents == null)
                {
                    var created = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
                    if (created != null) Object.DestroyImmediate(created.gameObject);
                }
                UnityEngine.Random.state = random;
            }
        }

        private static int Price(VoxelGarageUpgradeKind kind, VoxelRepairUpgradeSceneController shop)
        {
            var args = new object[] { kind, 0, "" };
            Check((bool)typeof(VoxelRepairUpgradeSceneController).GetMethod("UpgradeOffer", Private).Invoke(shop, args), "Missing cart offer " + kind);
            return (int)args[1];
        }

        private static void AddAll(VoxelRepairUpgradeSceneController shop)
        {
            // Select spikes before wheels to exercise replacement-dependent mounting.
            foreach (var kind in new[] { VoxelGarageUpgradeKind.Spikes, VoxelGarageUpgradeKind.Guns, VoxelGarageUpgradeKind.Armour,
                VoxelGarageUpgradeKind.Missiles, VoxelGarageUpgradeKind.Wheels, VoxelGarageUpgradeKind.Boost, VoxelGarageUpgradeKind.Engine, VoxelGarageUpgradeKind.Plough })
            {
                Call(shop, "BeginUpgradePlacement", kind);
                if (Placement(shop).RequiresPlacement)
                { Call(shop, "SelectUpgradePlacement", 1); Call(shop, "SelectUpgradePlacement", 0); }
            }
        }

        private static void CheckSettingsOverlay(VoxelRepairUpgradeSceneController shop, VoxelCarController car, int missing)
        {
            var canvas = shop.transform.Find("Repair Upgrade UI");
            var overlay = canvas.Find("Garage Settings Overlay");
            var upgradePanel = canvas.Find("Car Upgrade Panel").gameObject;
            var confirmation = (GameObject)typeof(VoxelRepairUpgradeSceneController).GetField("purchaseModal", Private).GetValue(shop);
            var preview = Placement(shop);
            var previewCar = preview.Car;
            var selections = preview.Selections.ToArray();
            var summary = (Text)typeof(VoxelRepairUpgradeSceneController).GetField("purchaseDescription", Private).GetValue(shop);
            string summaryText = summary.text;
            int cash = VoxelCurrencyState.Balance;
            var scroll = upgradePanel.GetComponentInChildren<ScrollRect>();
            Vector2 scrollPosition = scroll.content.anchoredPosition;
            var camera = (Camera)typeof(VoxelRepairUpgradeSceneController).GetField("workshopCamera", Private).GetValue(shop);
            Matrix4x4 projection = camera != null ? camera.projectionMatrix : Matrix4x4.identity;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                canvas.Find("Settings Button").GetComponent<Button>().onClick.Invoke();
                Check(overlay.gameObject.activeSelf && upgradePanel.activeSelf && confirmation.activeSelf,
                    "Settings must overlay the open upgrade screen and confirmation");
                Check(overlay.GetSiblingIndex() == canvas.childCount - 1 && overlay.GetComponent<Image>().raycastTarget &&
                    ((RectTransform)overlay).anchorMin == Vector2.zero && ((RectTransform)overlay).anchorMax == Vector2.one,
                    "Settings must cover the screen and intercept background touches");
                Call(shop, "UpdateUpgradePlacementInput"); Call(shop, "UpdateCarRotationInput");
                Check(Placement(shop) == preview && preview.Car == previewCar && preview.Selections.SequenceEqual(selections) &&
                    summary.text == summaryText && scroll.content.anchoredPosition == scrollPosition &&
                    VoxelCurrencyState.Balance == cash && car.RepairableIntegrityVoxels == missing,
                    "Opening settings changed cart, preview, quote, scroll, cash or damage");
                if (camera != null) Check(camera.projectionMatrix == projection, "Settings changed camera framing");
                if (repeat == 0) overlay.Find("Garage Settings/Close Panel").GetComponent<Button>().onClick.Invoke();
                else Call(shop, "HideGarageSettings"); // Escape shares this close path.
                Check(!overlay.gameObject.activeSelf && upgradePanel.activeSelf && confirmation.activeSelf && Placement(shop) == preview,
                    "Closing settings reset the underlying upgrade screen");
            }
            foreach (string screen in new[] { "Repair Panel", "Garage Home" })
            {
                // A separate temporary panel state leaves the real cart untouched.
                bool homeActive = canvas.Find("Garage Home").gameObject.activeSelf;
                bool repairActive = canvas.Find("Repair Panel").gameObject.activeSelf;
                upgradePanel.SetActive(false); canvas.Find(screen).gameObject.SetActive(true);
                canvas.Find("Settings Button").GetComponent<Button>().onClick.Invoke();
                overlay.Find("Garage Settings/Close Panel").GetComponent<Button>().onClick.Invoke();
                Check(canvas.Find(screen).gameObject.activeSelf && Placement(shop) == preview, "Settings did not preserve " + screen);
                canvas.Find("Garage Home").gameObject.SetActive(homeActive);
                canvas.Find("Repair Panel").gameObject.SetActive(repairActive); upgradePanel.SetActive(true);
            }
        }

        private static void CheckCart(Scene scene, VoxelCarDefinition definition, List<GameObject> roots)
        {
            Reset();
            var root = new GameObject("Multiple upgrade purchase QA"); roots.Add(root); SceneManager.MoveGameObjectToScene(root, scene);
            var carRoot = new GameObject("Cart car"); carRoot.transform.SetParent(root.transform, false);
            Object.Instantiate(definition.visualPrefab, carRoot.transform);
            var car = carRoot.AddComponent<VoxelCarController>(); car.enabled = false; car.ResetIntegrityBaseline();
            var damaged = car.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
            damaged.gameObject.SetActive(false); int missing = car.RepairableIntegrityVoxels;
            var visibility = car.GetComponentsInChildren<Renderer>(true).ToDictionary(r => r, r => r.enabled);
            var shop = root.AddComponent<VoxelRepairUpgradeSceneController>();
            typeof(VoxelRepairUpgradeSceneController).GetProperty("DisplayedCar").SetValue(shop, car);
            typeof(VoxelRepairUpgradeSceneController).GetField("definition", Private).SetValue(shop, definition);
            shop.repairTuning = VoxelRepairTuning.Load(); Call(shop, "BuildUi"); Call(shop, "RefreshUi");
            root.GetComponentsInChildren<Button>(true).First(b => b.name == "Upgrade Menu Button").onClick.Invoke();
            int total = 0;
            foreach (VoxelGarageUpgradeKind kind in Enum.GetValues(typeof(VoxelGarageUpgradeKind)))
                total += Price(kind, shop) * (kind == VoxelGarageUpgradeKind.Armour || kind == VoxelGarageUpgradeKind.Guns || kind == VoxelGarageUpgradeKind.Missiles ? 2 : 1);

            AddAll(shop);
            Check(Placement(shop).Selections.Count == 11 && VoxelCurrencyState.Balance == 100000, "Adding multiple types spent cash or discarded pending parts");
            CheckSettingsOverlay(shop, car, missing);
            int trackIndex=VoxelTrackProgressState.CurrentTrackIndex;
            shop.NextRaceButton.onClick.Invoke();
            var warning=root.transform.Find("Repair Upgrade UI/Unfitted Parts Warning").gameObject;
            Check(warning.activeSelf && Placement(shop).Selections.Count==11 && VoxelCurrencyState.Balance==100000 &&
                VoxelTrackProgressState.CurrentTrackIndex==trackIndex,"Starting with pending parts must warn without altering the cart, cash or mission");
            Check(warning.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("haven't been fitted yet")),"Warning does not explain unfitted parts");
            warning.GetComponentsInChildren<Button>().First(b=>b.name=="Go Back to Upgrades").onClick.Invoke();
            Check(!warning.activeSelf && Placement(shop).Selections.Count==11 &&
                root.transform.Find("Repair Upgrade UI/Upgrade Purchase Confirmation").gameObject.activeSelf,"Go Back discarded the cart");
            VoxelGarageUpgradeDockValidation.CheckPurchaseGrowth(shop);
            Check(root.GetComponentsInChildren<VoxelGarageUpgradeCard>().All(c => c.State == VoxelGarageUpgradeState.Pending), "Pending cards are not marked separately from ownership");
            var label = root.GetComponentsInChildren<Text>().First(t => t.name == "Purchase Description");
            Check(label.text.Contains("LEFT + RIGHT") && label.text.Contains("DOOR ARMOUR") && label.text.Contains("MISSILE") && label.text.Contains("ENGINE"), "Cart summary omitted selected parts or sides");
            Check(root.GetComponentsInChildren<Button>().First(b => b.name == "Confirm Upgrade Purchase").GetComponentInChildren<Text>().text.Contains(total.ToString("N0")), "Cart confirmation does not show the combined cost");
            foreach (var spike in Placement(shop).Car.GetComponentsInChildren<Transform>().Where(t => t.name == VoxelWheelSpikeUpgradeState.SpikeInstanceName))
                Check(Mathf.Abs(spike.localPosition.x - spike.parent.Find(VoxelPerformanceWheelUpgradeState.InstanceName).localPosition.x) < .001f, "Preview spikes do not fit pending replacement wheels");

            int gunPrice = Price(VoxelGarageUpgradeKind.Guns, shop);
            Call(shop, "RemovePendingUpgrade", VoxelGarageUpgradeKind.Guns, 1);
            Check(Placement(shop).Selections.Count == 10 && Placement(shop).IsPending(VoxelGarageUpgradeKind.Guns, 0) &&
                !Placement(shop).IsPending(VoxelGarageUpgradeKind.Guns, 1), "Removing one gun discarded its opposite pending side");
            Check(root.GetComponentsInChildren<Button>().First(b => b.name == "Confirm Upgrade Purchase").GetComponentInChildren<Text>().text.Contains((total - gunPrice).ToString("N0")),
                "Removing a slot did not reduce the combined quote");
            root.GetComponentsInChildren<Button>().First(b => b.name == "Left Door Armor Purchase Button").onClick.Invoke();
            Check(Placement(shop).PendingCount(VoxelGarageUpgradeKind.Armour) == 0 && Placement(shop).Selections.Count == 8 &&
                !label.text.Contains("DOOR ARMOUR"), "Tapping an armour tab did not remove both pending sides from the car and summary");
            root.GetComponentsInChildren<Button>().First(b => b.name == "Performance Wheel Purchase Button").onClick.Invoke();
            Check(Placement(shop).PendingCount(VoxelGarageUpgradeKind.Wheels) == 0 && Placement(shop).PendingCount(VoxelGarageUpgradeKind.Spikes) == 1,
                "Removing pending tires removed the independent spike set");
            foreach (var spike in Placement(shop).Car.GetComponentsInChildren<Transform>().Where(t => t.name == VoxelWheelSpikeUpgradeState.SpikeInstanceName))
                Check(Mathf.Abs(spike.localPosition.x) < .001f, "Removing tires did not move pending spikes back to the original wheels");
            Call(shop, "BeginUpgradePlacement", VoxelGarageUpgradeKind.Wheels);
            Check(Placement(shop).PendingCount(VoxelGarageUpgradeKind.Wheels) == 1, "Removed replacement tires could not be reselected");
            Call(shop, "CancelUpgradePlacement");
            Check(VoxelCurrencyState.Balance == 100000 && visibility.All(v => v.Key.enabled == v.Value) && car.RepairableIntegrityVoxels == missing,
                "Cart cancellation changed cash, visibility or damage");

            // Reserve the total rather than allowing the same cash to buy two slots.
            VoxelCurrencyState.Reset(); VoxelCurrencyState.Add(gunPrice);
            Call(shop, "BeginUpgradePlacement", VoxelGarageUpgradeKind.Guns); Call(shop, "SelectUpgradePlacement", 1);
            var gunButton = root.GetComponentsInChildren<Button>().First(b => b.name == "Long Gun Purchase Button");
            Check(Placement(shop).AvailableSpotCount == 0 && gunButton.interactable && gunButton.GetComponent<VoxelGarageUpgradeCard>().State == VoxelGarageUpgradeState.Unaffordable,
                "A pending card must allow removal while its reserved cash prevents another purchase");
            Call(shop, "SelectUpgradePlacement", 0);
            Check(Placement(shop).Selections.Count == 1, "Unaffordable second slot entered the cart");
            gunButton.onClick.Invoke();
            Check(Placement(shop) == null && !root.transform.Find("Repair Upgrade UI/Upgrade Purchase Confirmation").gameObject.activeSelf &&
                VoxelCurrencyState.Balance == gunPrice, "Removing the final pending part failed to hide confirmation or release its budget");

            Reset();
            var gunTuning = VoxelGunUpgradeState.LongGunTuning; int oldMaximum = gunTuning.maximumPurchases;
            try
            {
                gunTuning.maximumPurchases = 1;
                Call(shop, "BeginUpgradePlacement", VoxelGarageUpgradeKind.Guns); Call(shop, "SelectUpgradePlacement", 1);
                Check(Placement(shop).AvailableSpotCount == 0 && Placement(shop).Selections.Count == 1,
                    "Pending gun ignored the tuning's single-purchase limit");
                Call(shop, "BeginUpgradePlacement", VoxelGarageUpgradeKind.Guns);
                Check(Placement(shop) == null, "Tapping a full pending gun card did not remove it");
            }
            finally { Call(shop, "CancelUpgradePlacement"); gunTuning.maximumPurchases = oldMaximum; }

            Reset(); AddAll(shop); VoxelCurrencyState.Reset(); Call(shop, "ConfirmUpgradePurchase");
            Check(Placement(shop) == null && !VoxelArmorUpgradeState.IsPurchased && VoxelGunUpgradeState.PurchasedLongGunCount == 0 && !VoxelPerformanceWheelUpgradeState.IsPurchased,
                "Insufficient total funds partially purchased the cart");
            Reset(); AddAll(shop);
            var tuning = VoxelEngineUpgradeTuning.Load(); int oldPrice = tuning.purchasePrice;
            try { tuning.purchasePrice++; Call(shop, "ConfirmUpgradePurchase"); }
            finally { tuning.purchasePrice = oldPrice; }
            Check(VoxelCurrencyState.Balance == 100000 && !VoxelWheelSpikeUpgradeState.IsPurchased && !VoxelArmorUpgradeState.IsPurchased && !VoxelEngineUpgradeState.IsPurchased,
                "A changed quoted price partially purchased the cart");

            Reset(); VoxelCurrencyState.Reset(); VoxelCurrencyState.Add(total); AddAll(shop);
            Call(shop, "ConfirmUpgradePurchase");
            Check(Placement(shop) == null && VoxelCurrencyState.Balance == 0 && VoxelArmorUpgradeState.IsFullyPurchased && VoxelGunUpgradeState.PurchasedLongGunCount == 2 &&
                VoxelMissileUpgradeState.IsPurchased(false) && VoxelMissileUpgradeState.IsPurchased(true) && VoxelPerformanceWheelUpgradeState.IsPurchased &&
                VoxelWheelSpikeUpgradeState.IsPurchased && VoxelEngineUpgradeState.IsPurchased && VoxelBoostUpgradeState.IsPurchased && VoxelPloughUpgradeState.IsPurchased,
                "Combined confirmation did not purchase every pending part at its exact total");
            Call(shop, "ConfirmUpgradePurchase");
            Check(VoxelCurrencyState.Balance == 0 && !damaged.gameObject.activeSelf && car.RepairableIntegrityVoxels == missing, "Cart commit repaired damage or charged twice");
            using (var ownedPreview = new VoxelGarageUpgradePlacement(car.gameObject, VoxelGarageUpgradeKind.Guns))
            {
                Check(ownedPreview.AvailableSpotCount == 0, "Purchased gun slots became selectable again");
                var ownedGun = ownedPreview.Car.transform.Find("Purchased Gun Upgrades").GetComponentInChildren<MeshRenderer>();
                Check(!ownedPreview.TryPickPart(new Ray(ownedGun.bounds.center + Vector3.up * 10, Vector3.down), out _, out _),
                    "A purchased gun can be removed by tapping its model");
            }
            Call(shop, "RemovePendingUpgrade", VoxelGarageUpgradeKind.Guns, 1);
            Check(VoxelGunUpgradeState.PurchasedLongGunCount == 2 && VoxelCurrencyState.Balance == 0,
                "Pending removal modified already purchased parts");
        }

        private static void CheckSceneOwnership(VoxelCarDefinition definition)
        {
            Reset();
            var scene = EditorSceneManager.NewPreviewScene();
            VoxelGarageUpgradePlacement preview = null;
            GameObject previewCar = null;
            try
            {
                var car = new GameObject("Unparented garage scene ownership QA");
                SceneManager.MoveGameObjectToScene(car, scene);
                Object.Instantiate(definition.visualPrefab, car.transform);
                preview = new VoxelGarageUpgradePlacement(car, VoxelGarageUpgradeKind.Armour);
                Check(preview.Car.scene == scene, "Unparented preview leaked into the active scene");
                preview.Select(1);
                previewCar = preview.Car;
                Check(previewCar.scene == scene, "Selected preview leaked into the active scene");
                Check((previewCar.hideFlags & (HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild)) ==
                    (HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild), "Temporary preview can be saved into a scene");
            }
            finally
            {
                // Simulate scene teardown before explicit cancellation, including QA failure.
                EditorSceneManager.ClosePreviewScene(scene);
                preview?.Dispose();
            }
            Check(previewCar == null, "Closing the car's scene left its preview behind");
        }

        private static void CheckPick(VoxelGarageUpgradePlacement preview, VoxelGarageUpgradeKind kind, int slot)
        {
            // Use rays at real mounted meshes, from above for hood guns and outside for sides.
            var renderers = preview.Car.GetComponentsInChildren<MeshRenderer>().Where(r => r.sharedMaterial != null && r.sharedMaterial.name == "Garage Light Green Placement" &&
                (preview.Car.transform.InverseTransformPoint(r.bounds.center).x > 0) == (slot == 1)).ToArray();
            Vector3 direction = kind == VoxelGarageUpgradeKind.Guns ? Vector3.down : (slot == 1 ? Vector3.left : Vector3.right);
            bool hit = renderers.Any(r => preview.TryPick(new Ray(r.bounds.center - direction * 10f, direction), out int picked) && picked == slot);
            Check(hit, kind + " slot " + slot + " cannot be picked from its visible side");
        }
    }
}
