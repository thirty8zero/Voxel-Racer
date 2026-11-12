using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelGaragePlacementPlayValidation
    {
        private const string Pending = "VoxelRacer.GaragePlacementPlayCheck";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameObject root;
        private static VoxelRepairUpgradeSceneController shop;
        private static VoxelGarageUpgradeDockValidation.SavedState saved;
        private static Mouse mouse;
        private static Touchscreen touchscreen;
        private static Vector2 point;
        private static int phase, frame, balance;
        private static bool background;
        private static float started;
        private static Quaternion rotation;
        private static VoxelMainMenuController menu;
        private static GameObject temporaryMenu;
        private static Quaternion menuRotation;
        private static float savedTimeScale;
        private static int missionIndex, departureBalance;
        private static string missionScene;
        private static VoxelRepairUpgradeTuning sectionTuning;
        private static float carriedIntegrity;
        private static int carriedMissingVoxels;
        private static float swipeDistance;
        private static Button Button(string name)
        {
            var button=shop.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
            var card=button.GetComponent<VoxelGarageUpgradeCard>();
            if(card!=null && !button.gameObject.activeSelf)
                shop.GetComponentsInChildren<Button>(true).First(b=>b.name=="Category "+card.Category).onClick.Invoke();
            return button;
        }
        private static VoxelGarageUpgradePlacement Preview() => (VoxelGarageUpgradePlacement)
            typeof(VoxelRepairUpgradeSceneController).GetField("upgradePlacement", Private).GetValue(shop);
        private static void UseUpgradeAction(string card) => VoxelGarageUpgradeDockValidation.InspectUpgrade(Button(card)).onClick.Invoke();
        private static void Call(string method, params object[] args) =>
            typeof(VoxelRepairUpgradeSceneController).GetMethod(method, Private).Invoke(shop, args);
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }
        [MenuItem("Tools/Voxel Racer/Validate Garage Placement in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start from Edit Mode.");
            SessionState.SetBool(Pending + ".Background", Application.runInBackground);
            SessionState.SetBool(Pending + ".RestoreBackground", true);
            Application.runInBackground = true;
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending + ".RestoreBackground", false))
            {
                Application.runInBackground = SessionState.GetBool(Pending + ".Background", false);
                SessionState.SetBool(Pending + ".RestoreBackground", false);
            }
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick; SessionState.SetBool(Pending, false);
                Cleanup();
            }
        }
        private static void Setup()
        {
            background = SessionState.GetBool(Pending + ".Background", false); Application.runInBackground = true;
            saved = new VoxelGarageUpgradeDockValidation.SavedState();
            try
            {
                VoxelGunUpgradeState.BeginNewRun(); VoxelArmorUpgradeState.BeginNewRun(); VoxelMissileUpgradeState.BeginNewRun();
                VoxelPerformanceWheelUpgradeState.BeginNewRun(); VoxelWheelSpikeUpgradeState.BeginNewRun();
                VoxelBoostUpgradeState.BeginNewRun(); VoxelEngineUpgradeState.BeginNewRun(); VoxelPloughUpgradeState.BeginNewRun();
                VoxelCurrencyState.Reset(); VoxelCurrencyState.Add(100000);
                savedTimeScale = Time.timeScale;
                menu = Object.FindFirstObjectByType<VoxelMainMenuController>();
                if (menu == null)
                {
                    temporaryMenu = new GameObject("Temporary immediate menu rotation QA");
                    menu = temporaryMenu.AddComponent<VoxelMainMenuController>();
                }
                menuRotation = menu.transform.Find("Featured Car Displays").rotation;
                root = new GameObject("Temporary live garage placement QA");
                SeedIntegrityDamage();
                shop = root.AddComponent<VoxelRepairUpgradeSceneController>();
                sectionTuning = Object.Instantiate(shop.cameraTuning);
                sectionTuning.showUpgradeSectionTabs = true;
                shop.cameraTuning = sectionTuning;
                rotation = shop.DisplayedCar.transform.rotation;
                Time.timeScale = 0; // Menu and garage display motion must not inherit a paused race clock.
                mouse = InputSystem.AddDevice<Mouse>();
                touchscreen = InputSystem.AddDevice<Touchscreen>();
                Button("Upgrade Menu Button").onClick.Invoke();
                Canvas.ForceUpdateCanvases(); phase = -20; frame = Time.frameCount;
                swipeDistance = Mathf.Min(80f, Screen.width * .06f);
                started = Time.realtimeSinceStartup; balance = VoxelCurrencyState.Balance;
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        private static void SeedIntegrityDamage()
        {
            var definition = VoxelCarSelectionState.GetSelectedOrDefault();
            var source = new GameObject("Temporary race damage source");
            source.transform.SetParent(root.transform, false);
            Object.Instantiate(definition.visualPrefab, source.transform);
            var car = source.AddComponent<VoxelCarController>();
            car.enabled = false;
            car.ResetIntegrityBaseline();
            var body = car.GetComponentsInChildren<MeshRenderer>()
                .Where(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null).ToArray();
            foreach (var piece in body.Take(body.Length / 3)) piece.gameObject.SetActive(false);
            VoxelCarRunState.Capture(car, definition);
            carriedIntegrity = car.IntegrityPercent;
            carriedMissingVoxels = VoxelCarRunState.MissingVoxelCount;
            Check(carriedIntegrity < 90 && carriedMissingVoxels > 0, "QA source must have repairable race damage.");
            source.SetActive(false);
            Object.Destroy(source);
        }
        private static void CheckIntegrityDisplay()
        {
            var hud = shop.GetComponent<VoxelCarIntegrityDisplay>();
            var group = hud.GetComponentInChildren<CanvasGroup>();
            var ring = hud.GetComponentsInChildren<Image>().First(i => i.name == "Integrity Ring");
            var label = hud.GetComponentsInChildren<Text>().First(t => t.name == "Integrity Percentage");
            void Refresh() => typeof(VoxelCarIntegrityDisplay).GetMethod("Update", Private).Invoke(hud, null);
            void CheckVisible()
            {
                Check(!shop.DisplayedCar.enabled && group.alpha == 1 && !group.blocksRaycasts && !group.interactable,
                    "Garage dial hidden for stationary car or intercepting pause input.");
                Check(Mathf.Abs(ring.fillAmount - shop.DisplayedCar.IntegrityPercent / 100f) < .001f &&
                    label.text == Mathf.CeilToInt(shop.DisplayedCar.IntegrityPercent) + "%", "Garage dial did not refresh integrity.");
            }
            // First check follows real Update frames, with the gameplay clock paused.
            CheckVisible();
            Check(Mathf.Abs(shop.DisplayedCar.IntegrityPercent - carriedIntegrity) < .001f &&
                VoxelCarRunState.MissingVoxelCount == carriedMissingVoxels, "Race damage did not carry into garage.");
            Call("ShowGaragePanel", new object[] { null }); Refresh(); CheckVisible();
            Button("Repair Menu Button").onClick.Invoke(); Refresh(); CheckVisible();
            int cost = (int)typeof(VoxelRepairUpgradeSceneController).GetMethod("GetRepairCost", Private).Invoke(shop, new object[] { 10f });
            Call("TryRepair", 10f, cost); Refresh(); CheckVisible();
            Check(ring.fillAmount > carriedIntegrity / 100f && ring.fillAmount < 1, "Partial repair did not update garage dial.");
            cost = (int)typeof(VoxelRepairUpgradeSceneController).GetMethod("GetRepairCost", Private).Invoke(shop, new object[] { 100f });
            Call("TryRepair", 100f, cost); Refresh(); CheckVisible();
            Check(ring.fillAmount == 1 && label.text == "100%" && VoxelCarRunState.MissingVoxelCount == 0,
                "Full repair did not restore the dial and persisted integrity.");
            shop.DisplayedCar.gameObject.SetActive(false); Refresh();
            Check(group.alpha == 0, "Inactive garage car still shows a dial.");
            shop.DisplayedCar.gameObject.SetActive(true); Refresh(); CheckVisible();
            hud.enabled = false; Check(group.alpha == 0, "Disabled garage dial still visible.");
            hud.enabled = true; Refresh(); CheckVisible();
            var race = new GameObject("Temporary race integrity visibility QA");
            race.transform.SetParent(root.transform, false);
            var raceHud = race.AddComponent<VoxelCarIntegrityDisplay>(); raceHud.target = shop.DisplayedCar;
            typeof(VoxelCarIntegrityDisplay).GetMethod("Update", Private).Invoke(raceHud, null);
            Check(raceHud.GetComponentInChildren<CanvasGroup>().alpha == 0, "Race dial lost its disabled-controller gate.");
            race.SetActive(false); Object.Destroy(race);
            Button("Upgrade Menu Button").onClick.Invoke(); Refresh(); CheckVisible();
            balance = VoxelCurrencyState.Balance;
        }
        private static Vector2 Spot(int slot)
        {
            var camera = (Camera)typeof(VoxelRepairUpgradeSceneController).GetField("workshopCamera", Private).GetValue(shop);
            foreach (var renderer in Preview().Car.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.sharedMaterial == null || renderer.sharedMaterial.name != "Garage Light Green Placement") continue;
                Vector3 projected = camera.WorldToScreenPoint(renderer.bounds.center);
                if (projected.z <= 0 || projected.x < 0 || projected.x > Screen.width || projected.y < 0 || projected.y > Screen.height) continue;
                Vector2 position = projected;
                if ((bool)typeof(VoxelRepairUpgradeSceneController).GetMethod("BlocksCarRotation", Private).Invoke(shop, new object[] { position })) continue;
                if (Preview().TryPick(camera.ScreenPointToRay(position), out int found) && found == slot) return position;
            }
            throw new InvalidOperationException("No visible screen-space spot for slot " + slot);
        }
        private static Vector2 PendingSpot(VoxelGarageUpgradeKind kind, int slot)
        {
            var camera = (Camera)typeof(VoxelRepairUpgradeSceneController).GetField("workshopCamera", Private).GetValue(shop);
            foreach (var renderer in Preview().Car.GetComponentsInChildren<MeshRenderer>())
            {
                Vector3 projected = camera.WorldToScreenPoint(renderer.bounds.center);
                if (!renderer.enabled || projected.z <= 0 || projected.x < 0 || projected.x > Screen.width || projected.y < 0 || projected.y > Screen.height) continue;
                Vector2 position = projected;
                if ((bool)typeof(VoxelRepairUpgradeSceneController).GetMethod("BlocksCarRotation", Private).Invoke(shop, new object[] { position })) continue;
                if (Preview().TryPickPart(camera.ScreenPointToRay(position), out var part, out bool pending) && pending && part.Kind == kind && part.Slot == slot)
                    return position;
            }
            throw new InvalidOperationException("No visible pending model for " + kind + " slot " + slot);
        }
        private static void MouseEvent(bool held, Vector2 position) => InputSystem.QueueStateEvent(mouse,
            new MouseState { position = position, buttons = (ushort)(held ? 1 : 0) });
        private static Vector2 EmptySwipeSpot()
        {
            var camera = (Camera)typeof(VoxelRepairUpgradeSceneController).GetField("workshopCamera", Private).GetValue(shop);
            var renderers = (Preview()?.Car ?? shop.DisplayedCar.gameObject).GetComponentsInChildren<Renderer>();
            for (float y = .1f; y < .9f; y += .1f)
            for (float x = .1f; x < .85f; x += .1f)
            {
                var start = new Vector2(Screen.width * x, Screen.height * y);
                var end = start + Vector2.right * swipeDistance;
                bool Blocked(Vector2 p) => (bool)typeof(VoxelRepairUpgradeSceneController)
                    .GetMethod("BlocksCarRotation", Private).Invoke(shop, new object[] { p });
                if (Blocked(start) || Blocked(end)) continue;
                if (renderers.Any(r => r.enabled && r.gameObject.activeInHierarchy &&
                    (r.bounds.IntersectRay(camera.ScreenPointToRay(start)) || r.bounds.IntersectRay(camera.ScreenPointToRay(end))))) continue;
                return start;
            }
            throw new InvalidOperationException("No empty background swipe area available.");
        }
        private static void TouchEvent(UnityEngine.InputSystem.TouchPhase touchPhase) => InputSystem.QueueStateEvent(touchscreen,
            new TouchState { touchId = 1, phase = touchPhase, position = point });
        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            if (Time.realtimeSinceStartup - started > 30) { Finish("FAIL: input validation timed out."); return; }
            if (Time.frameCount < frame + 3) return;
            // Model taps need settled screen positions after navigating between garage panels.
            if (phase >= 0 && shop != null && (bool)typeof(VoxelRepairUpgradeSceneController).GetField("garageCameraTransitionActive", Private).GetValue(shop)) return;
            try
            {
                switch (phase)
                {
                    case -20:
                        CheckIntegrityDisplay();
                        VoxelGarageUpgradeDockValidation.CheckCategories(shop);
                        Check(Quaternion.Angle(rotation, shop.DisplayedCar.transform.rotation) > .01f,
                            "Garage entry still delays spinning in its first frames");
                        Check(Quaternion.Angle(menuRotation, menu.transform.Find("Featured Car Displays").rotation) > .01f,
                            "Main-menu entry does not spin immediately with a paused gameplay clock");
                        Time.timeScale = savedTimeScale;
                        shop.garageAutoRotationDegreesPerSecond = 0;
                        shop.DisplayedCar.transform.rotation = Quaternion.Euler(0, -15, 0);
                        break;
                    case -19:
                        Call("ShowGaragePanel", new object[] { null });
                        point = EmptySwipeSpot(); rotation = shop.DisplayedCar.transform.rotation;
                        TouchEvent(UnityEngine.InputSystem.TouchPhase.Began); break;
                    case -18:
                        point += Vector2.right * swipeDistance;
                        TouchEvent(UnityEngine.InputSystem.TouchPhase.Moved); break;
                    case -17:
                        Check(Mathf.DeltaAngle(rotation.eulerAngles.y, shop.DisplayedCar.transform.eulerAngles.y) < -1,
                            "Right swipe on empty garage background did not rotate the car.");
                        point -= Vector2.right * swipeDistance;
                        TouchEvent(UnityEngine.InputSystem.TouchPhase.Moved); break;
                    case -16:
                        Check(Quaternion.Angle(rotation, shop.DisplayedCar.transform.rotation) < .01f,
                            "Left background swipe did not reverse the car rotation.");
                        TouchEvent(UnityEngine.InputSystem.TouchPhase.Ended); break;
                    case -15:
                        Button("Repair Menu Button").onClick.Invoke();
                        point = EmptySwipeSpot(); rotation = shop.DisplayedCar.transform.rotation;
                        MouseEvent(true, point); break;
                    case -14: MouseEvent(true, point + Vector2.right * swipeDistance); break;
                    case -13:
                        Check(Quaternion.Angle(rotation, shop.DisplayedCar.transform.rotation) > 1,
                            "Mouse drag on background beside repair panel did not rotate the car.");
                        MouseEvent(false, point + Vector2.right * swipeDistance); break;
                    case -12:
                        var back = shop.transform.Find("Repair Upgrade UI/Repair Panel/Close Panel");
                        point = RectTransformUtility.WorldToScreenPoint(null, back.position);
                        rotation = shop.DisplayedCar.transform.rotation;
                        MouseEvent(true, point); break;
                    case -11: MouseEvent(true, point + Vector2.right * swipeDistance); break;
                    case -10: MouseEvent(false, point + Vector2.right * swipeDistance); break;
                    case -9:
                        Check(Quaternion.Angle(rotation, shop.DisplayedCar.transform.rotation) < .01f,
                            "Dragging on repair UI rotated the garage car.");
                        Call("ShowGaragePanel", new object[] { null });
                        Button("Upgrade Menu Button").onClick.Invoke();
                        shop.DisplayedCar.transform.rotation = Quaternion.Euler(0, -15, 0);
                        break;
                    case -8:
                        point=RectTransformUtility.WorldToScreenPoint(null,Button("Category WheelsAndTires").transform.position);
                        TouchEvent(UnityEngine.InputSystem.TouchPhase.Began);break;
                    case -7: TouchEvent(UnityEngine.InputSystem.TouchPhase.Ended);break;
                    case -6:
                        Check(shop.GetComponentsInChildren<VoxelGarageUpgradeCard>().Length==4 &&
                            shop.GetComponentsInChildren<VoxelGarageUpgradeCard>().All(c=>c.Category==VoxelGarageUpgradeCategory.WheelsAndTires),
                            "Touch input did not open Wheels & Tires category.");
                        point=RectTransformUtility.WorldToScreenPoint(null,Button("Category Engines").transform.position);
                        TouchEvent(UnityEngine.InputSystem.TouchPhase.Began);break;
                    case -5: TouchEvent(UnityEngine.InputSystem.TouchPhase.Ended);break;
                    case -4:
                        Check(shop.GetComponentsInChildren<VoxelGarageUpgradeCard>().Single().Category==VoxelGarageUpgradeCategory.Engines,
                            "Touch input did not open Engines category.");
                        point = RectTransformUtility.WorldToScreenPoint(null, Button("Long Gun Purchase Button").transform.position);
                        TouchEvent(UnityEngine.InputSystem.TouchPhase.Began); break;
                    case -3: TouchEvent(UnityEngine.InputSystem.TouchPhase.Ended); break;
                    case -2:
                        Check(Preview() == null && VoxelCurrencyState.Balance == balance &&
                            Button("Selected Upgrade Action").GetComponentInChildren<Text>().text.StartsWith("PLACE +"),
                            "Touch inspection started a preview or failed to show the place action");
                        point = RectTransformUtility.WorldToScreenPoint(null, Button("Selected Upgrade Action").transform.position);
                        TouchEvent(UnityEngine.InputSystem.TouchPhase.Began); break;
                    case -1: TouchEvent(UnityEngine.InputSystem.TouchPhase.Ended); break;
                    case 0:
                        Check(Preview()?.Kind == VoxelGarageUpgradeKind.Guns, "Touch on the detail action did not open gun placement");
                        Call("ApplyGaragePreviewViewport"); point = Spot(1); MouseEvent(true, point); break;
                    case 1: MouseEvent(false, point); break;
                    case 2:
                        Check(Preview() != null && Preview().SelectedSlot == 1 && VoxelCurrencyState.Balance == balance,
                            "Mouse tap did not select right ghost without spending");
                        Button("Cancel Upgrade Purchase").onClick.Invoke();
                        Check(Preview() == null && !VoxelGunUpgradeState.IsPurchased(1), "Cancel bought the preview");
                        UseUpgradeAction("Long Gun Purchase Button"); point = Spot(1);
                        TouchEvent(UnityEngine.InputSystem.TouchPhase.Began); break;
                    case 3: TouchEvent(UnityEngine.InputSystem.TouchPhase.Ended); break;
                    case 4:
                        Check(Preview() != null && Preview().SelectedSlot == 1 && VoxelCurrencyState.Balance == balance,
                            "Touch tap did not select right ghost without spending; selected="+Preview()?.SelectedSlot+", balance="+VoxelCurrencyState.Balance+", expected="+balance);
                        point = Spot(0); MouseEvent(true, point); break;
                    case 5: MouseEvent(false, point); break;
                    case 6:
                        Check(Preview().PendingCount(VoxelGarageUpgradeKind.Guns) == 2 && VoxelCurrencyState.Balance == balance,
                            "Open confirmation blocked selection of a second gun");
                        UseUpgradeAction("Wheel Spike Purchase Button");
                        UseUpgradeAction("Performance Wheel Purchase Button");
                        UseUpgradeAction("Gold Track Wheel Purchase Button");
                        Check(Preview().PendingCount(VoxelGarageUpgradeKind.GoldWheels) == 0,
                            "Conflicting wheel sets entered the same basket");
                        UseUpgradeAction("Boost Bottle Purchase Button");
                        Check(Preview().Selections.Count == 5, "Switching cards discarded the pending gun pair");
                        VoxelGarageUpgradeDockValidation.CheckCategories(shop);
                        point = EmptySwipeSpot();
                        rotation = shop.DisplayedCar.transform.rotation;
                        MouseEvent(true, point); break;
                    case 7: MouseEvent(true, point + new Vector2(80, 0)); break;
                    case 8: MouseEvent(false, point + new Vector2(80, 0)); break;
                    case 9:
                        Check(Preview().Selections.Count == 5 && Quaternion.Angle(rotation, shop.DisplayedCar.transform.rotation) > 1,
                            "Background drag did not rotate the car with the purchase panel open");
                        Check(Quaternion.Angle(Preview().Car.transform.rotation, shop.DisplayedCar.transform.rotation) < .01f,
                            "Pending parts did not follow manual rotation");
                        shop.garageAutoRotationDegreesPerSecond = 12;
                        typeof(VoxelRepairUpgradeSceneController).GetField("resumeAutoRotationAt", Private).SetValue(shop, Time.unscaledTime - 1);
                        rotation = shop.DisplayedCar.transform.rotation; break;
                    case 10:
                        Check(Quaternion.Angle(rotation, shop.DisplayedCar.transform.rotation) > .01f,
                            "Automatic spinning did not resume while previewing multiple upgrades");
                        Button("Confirm Upgrade Purchase").onClick.Invoke();
                        int cost = 2 * VoxelGunUpgradeState.LongGunTuning.purchasePrice + VoxelWheelSpikeTuning.Load().purchasePrice +
                            VoxelPerformanceWheelTuning.Load().purchasePrice + VoxelBoostUpgradeTuning.LoadUpgrade().purchasePrice;
                        Check(Preview() == null && VoxelGunUpgradeState.PurchasedLongGunCount == 2 && VoxelWheelSpikeUpgradeState.IsPurchased &&
                            VoxelPerformanceWheelUpgradeState.IsPurchased && VoxelBoostUpgradeState.IsPurchased && VoxelCurrencyState.Balance == balance - cost,
                            "Combined confirmation lost parts or charged the wrong total");
                        UseUpgradeAction("Left Door Armor Purchase Button");
                        Call("SelectUpgradePlacement", 1); shop.enabled = false;
                        Check(Preview() == null && !VoxelArmorUpgradeState.IsPurchased, "Disabling the shop did not cancel its pending parts");
                        shop.enabled = true;
                        Check(Button("Left Door Armor Purchase Button").GetComponent<VoxelGarageUpgradeCard>().State == VoxelGarageUpgradeState.Affordable,
                            "Re-enabling the shop left its canceled card marked pending");
                        UseUpgradeAction("Left Door Armor Purchase Button");
                        shop.transform.Find("Repair Upgrade UI/Car Upgrade Panel/Close Panel").GetComponent<Button>().onClick.Invoke();
                        Check(Preview() == null, "Closing upgrades left a preview");
                        shop.garageAutoRotationDegreesPerSecond = 0;
                        shop.DisplayedCar.transform.rotation = Quaternion.Euler(0, -15, 0);
                        Button("Upgrade Menu Button").onClick.Invoke();
                        UseUpgradeAction("Left Door Armor Purchase Button");
                        Call("SelectUpgradePlacement", 1); Call("SelectUpgradePlacement", 0);
                        UseUpgradeAction("Plough Purchase Button");
                        point = PendingSpot(VoxelGarageUpgradeKind.Armour, 1); MouseEvent(true, point); break;
                    case 11: MouseEvent(false, point); break;
                    case 12:
                        Check(Preview().PendingCount(VoxelGarageUpgradeKind.Armour) == 1 && Preview().IsPending(VoxelGarageUpgradeKind.Armour, 0) &&
                            !Preview().IsPending(VoxelGarageUpgradeKind.Armour, 1) && Preview().PendingCount(VoxelGarageUpgradeKind.Plough) == 1,
                            "Mouse tap on a fitted model removed the wrong type/side or failed across tabs");
                        UseUpgradeAction("Left Door Armor Purchase Button");
                        Check(Preview().PendingCount(VoxelGarageUpgradeKind.Armour) == 0 && Preview().PendingCount(VoxelGarageUpgradeKind.Plough) == 1,
                            "The armour detail action did not remove its remaining pending side");
                        UseUpgradeAction("Plough Purchase Button");
                        Check(Preview() == null && !shop.transform.Find("Repair Upgrade UI/Upgrade Purchase Confirmation").gameObject.activeSelf,
                            "Removing the last pending upgrade did not hide confirmation");
                        UseUpgradeAction("Left Missile Purchase Button"); Call("SelectUpgradePlacement", 1);
                        point = PendingSpot(VoxelGarageUpgradeKind.Missiles, 1);
                        TouchEvent(UnityEngine.InputSystem.TouchPhase.Began); break;
                    case 13: TouchEvent(UnityEngine.InputSystem.TouchPhase.Ended); break;
                    case 14:
                        Check(Preview() == null && !shop.transform.Find("Repair Upgrade UI/Upgrade Purchase Confirmation").gameObject.activeSelf &&
                            !VoxelArmorUpgradeState.IsPurchased && !VoxelPloughUpgradeState.IsPurchased && !VoxelMissileUpgradeState.IsPurchased(true),
                            "Touch removal of the final model retained confirmation or changed ownership");
                        int confirmedCost = 2 * VoxelGunUpgradeState.LongGunTuning.purchasePrice + VoxelWheelSpikeTuning.Load().purchasePrice +
                            VoxelPerformanceWheelTuning.Load().purchasePrice + VoxelBoostUpgradeTuning.LoadUpgrade().purchasePrice;
                        Check(VoxelCurrencyState.Balance == balance - confirmedCost, "Removing pending parts changed cash");
                        CheckGoldWheelReplacement();
                        UseUpgradeAction("Plough Purchase Button");
                        shop.NextRaceButton.onClick.Invoke();
                        var warning=shop.transform.Find("Repair Upgrade UI/Unfitted Parts Warning");
                        Check(warning.gameObject.activeSelf && Preview().PendingCount(VoxelGarageUpgradeKind.Plough)==1,"Starting with pending parts did not warn");
                        warning.GetComponentsInChildren<Button>().First(b=>b.name=="Go Back to Upgrades").onClick.Invoke();
                        Check(!warning.gameObject.activeSelf && Preview().PendingCount(VoxelGarageUpgradeKind.Plough)==1,"Go Back removed the pending preview");
                        missionIndex=VoxelTrackProgressState.NextTrackIndex; departureBalance=VoxelCurrencyState.Balance;
                        missionScene=VoxelTrackProgressState.NextTrack!=null?VoxelTrackProgressState.NextTrack.raceSceneName:shop.raceSceneName;
                        if(string.IsNullOrWhiteSpace(missionScene))missionScene=shop.raceSceneName;
                        shop.NextRaceButton.onClick.Invoke();
                        warning.GetComponentsInChildren<Button>().First(b=>b.name=="Start Mission Without Parts").onClick.Invoke();
                        break;
                    case 15:
                        if (VoxelLoadingScreen.IsLoading) return;
                        Check(VoxelMissionBriefing.IsShowing, "Starting without pending parts did not open the mission briefing");
                        VoxelMissionBriefing.Active.GetComponentsInChildren<Button>().First(b => b.name == "Start Mission").onClick.Invoke();
                        break;
                    case 16:
                        Check(SceneManager.GetActiveScene().name==missionScene && VoxelTrackProgressState.CurrentTrackIndex==missionIndex,"Start Mission did not advance and load the mission");
                        Check(!VoxelPloughUpgradeState.IsPurchased && VoxelCurrencyState.Balance==departureBalance,"Starting without pending parts bought them or spent cash");
                        Check(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).All(t=>t.name!="Temporary Upgrade Placement"),"Unpurchased preview leaked into the mission");
                        var raceCar = Object.FindObjectsByType<VoxelCarController>(FindObjectsSortMode.None)
                            .Single(car => car.gameObject.scene == SceneManager.GetActiveScene());
                        var gold = VoxelPerformanceWheelTuning.LoadGold();
                        VoxelGoldTrackWheelValidation.VerifyMounts(raceCar.gameObject, gold);
                        Check(Mathf.Approximately(raceCar.EffectiveAcceleration, raceCar.acceleration * (1 + gold.accelerationBonusPercent / 100)) &&
                            Mathf.Approximately(raceCar.EffectiveLaneChangeSpeed, raceCar.laneChangeSpeed * (1 + gold.laneChangeBonusPercent / 100)) &&
                            Mathf.Approximately(raceCar.EffectiveBrakingForce, raceCar.brakingForce * (1 + gold.brakingBonusPercent / 100)),
                            "The next race lost or stacked the installed wheel bonuses");
                        Finish("PASS (Play Mode): real touch category selection, upgrade inspection and separate Place action, per-category sorting, pending cart and wallet preserved across tabs; empty-background touch swipes right/left, mouse background drag beside repairs, repair UI drag blocking, background rotation with pending upgrades and confirmation open; garage integrity visible with disabled controller and paused clock, race damage carry-over, partial/full repair refresh and persistence, home/repair/upgrades, no pause input, inactive/disabled visibility and retained race gate; immediate menu/garage spin, real mouse/touch placement and removal, multiple selections, combined purchase, disable/close cleanup; Gold Track Wheels preview/cancel and exclusive Sports Wheel replacement with exact cost, preserved damage/spikes and installed gold wheels and tuning in the next race; starting with pending parts warns, Go Back retains the cart, the mission briefing launches the real race without spending or purchasing pending parts. Temporary input devices and cash/purchase/campaign/clock state restored."); return;
                }
                phase++; frame = Time.frameCount;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        private static void CheckGoldWheelReplacement()
        {
            var gold = VoxelPerformanceWheelTuning.LoadGold();
            var sports = VoxelPerformanceWheelTuning.Load();
            var original = shop.DisplayedCar;
            int wallet = VoxelCurrencyState.Balance;
            int missing = original.MissingIntegrityVoxels;
            var spikes = original.GetComponentsInChildren<VoxelWheelSpikeMount>();
            var outgoing = VoxelGoldTrackWheelValidation.VerifyMounts(original.gameObject, sports);
            UseUpgradeAction("Gold Track Wheel Purchase Button");
            Check(Preview() != null && Preview().PendingCount(VoxelGarageUpgradeKind.GoldWheels) == 1, "Gold wheel preview failed");
            VoxelGoldTrackWheelValidation.VerifyMounts(Preview().Car, gold);
            Check(VoxelPerformanceWheelUpgradeState.InstalledTuning == sports && VoxelCurrencyState.Balance == wallet,
                "Gold wheel preview changed ownership or balance");
            Button("Cancel Upgrade Purchase").onClick.Invoke();
            Check(Preview() == null && outgoing.SequenceEqual(VoxelGoldTrackWheelValidation.VerifyMounts(original.gameObject, sports)) &&
                VoxelCurrencyState.Balance == wallet, "Cancel replaced or charged for the wheel set");
            UseUpgradeAction("Gold Track Wheel Purchase Button");
            Button("Confirm Upgrade Purchase").onClick.Invoke();
            VoxelGoldTrackWheelValidation.VerifyMounts(original.gameObject, gold);
            Check(outgoing.All(wheel => !wheel.gameObject.activeSelf && wheel.transform.parent == null) &&
                original.MissingIntegrityVoxels == missing && spikes.SequenceEqual(original.GetComponentsInChildren<VoxelWheelSpikeMount>()) &&
                VoxelCurrencyState.Balance == wallet - gold.purchasePrice && VoxelPerformanceWheelUpgradeState.InstalledTuning == gold,
                "Confirmed gold wheel replacement retained an old wheel or changed damage, spikes or the wrong amount of cash");
            var definition = VoxelCarSelectionState.GetSelectedOrDefault();
            Check(!VoxelPerformanceWheelUpgradeState.TryPurchase(gold, definition) &&
                !VoxelPerformanceWheelUpgradeState.TryPurchase(sports, definition), "Duplicate or downgrade wheel purchase was allowed");
        }

        private static void Cleanup()
        {
            if (shop != null) Call("CancelUpgradePlacement");
            if (root != null) Object.Destroy(root); root = null; shop = null;
            if (sectionTuning != null) Object.Destroy(sectionTuning); sectionTuning = null;
            if (temporaryMenu != null) Object.Destroy(temporaryMenu); temporaryMenu = null; menu = null;
            if (mouse != null) InputSystem.RemoveDevice(mouse); mouse = null;
            if (touchscreen != null) InputSystem.RemoveDevice(touchscreen); touchscreen = null;
            saved?.Dispose(); saved = null; Application.runInBackground = background; Time.timeScale = savedTimeScale;
        }
        private static void Finish(string report)
        {
            EditorApplication.update -= Tick;
            Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/GaragePlacementPlayValidation.txt", report);
            Cleanup();
            if (report.StartsWith("PASS")) Debug.Log(report); else Debug.LogError(report);
            EditorApplication.isPlaying = false;
        }
    }
}
