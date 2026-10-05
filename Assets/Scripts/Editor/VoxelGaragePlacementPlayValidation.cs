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
        private static Button Button(string name) => shop.GetComponentsInChildren<Button>(true).First(b => b.name == name);
        private static VoxelGarageUpgradePlacement Preview() => (VoxelGarageUpgradePlacement)
            typeof(VoxelRepairUpgradeSceneController).GetField("upgradePlacement", Private).GetValue(shop);
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
                shop = root.AddComponent<VoxelRepairUpgradeSceneController>();
                rotation = shop.DisplayedCar.transform.rotation;
                Time.timeScale = 0; // Menu and garage display motion must not inherit a paused race clock.
                mouse = InputSystem.AddDevice<Mouse>();
                touchscreen = InputSystem.AddDevice<Touchscreen>();
                Button("Upgrade Menu Button").onClick.Invoke();
                Canvas.ForceUpdateCanvases(); phase = -1; frame = Time.frameCount;
                started = Time.realtimeSinceStartup; balance = VoxelCurrencyState.Balance;
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
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
        private static void TouchEvent(UnityEngine.InputSystem.TouchPhase touchPhase) => InputSystem.QueueStateEvent(touchscreen,
            new TouchState { touchId = 1, phase = touchPhase, position = point });
        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            if (Time.realtimeSinceStartup - started > 30) { Finish("FAIL: input validation timed out."); return; }
            if (Time.frameCount < frame + 3) return;
            try
            {
                switch (phase)
                {
                    case -1:
                        Check(Quaternion.Angle(rotation, shop.DisplayedCar.transform.rotation) > .01f,
                            "Garage entry still delays spinning in its first frames");
                        Check(Quaternion.Angle(menuRotation, menu.transform.Find("Featured Car Displays").rotation) > .01f,
                            "Main-menu entry does not spin immediately with a paused gameplay clock");
                        Time.timeScale = savedTimeScale;
                        shop.garageAutoRotationDegreesPerSecond = 0;
                        shop.DisplayedCar.transform.rotation = Quaternion.Euler(0, -15, 0);
                        break;
                    case 0:
                        Button("Long Gun Purchase Button").onClick.Invoke();
                        Call("ApplyGaragePreviewViewport"); point = Spot(1); MouseEvent(true, point); break;
                    case 1: MouseEvent(false, point); break;
                    case 2:
                        Check(Preview() != null && Preview().SelectedSlot == 1 && VoxelCurrencyState.Balance == balance,
                            "Mouse tap did not select right ghost without spending");
                        Button("Cancel Upgrade Purchase").onClick.Invoke();
                        Check(Preview() == null && !VoxelGunUpgradeState.IsPurchased(1), "Cancel bought the preview");
                        Button("Long Gun Purchase Button").onClick.Invoke(); point = Spot(1);
                        TouchEvent(UnityEngine.InputSystem.TouchPhase.Began); break;
                    case 3: TouchEvent(UnityEngine.InputSystem.TouchPhase.Ended); break;
                    case 4:
                        Check(Preview() != null && Preview().SelectedSlot == 1 && VoxelCurrencyState.Balance == balance,
                            "Touch tap did not select right ghost without spending");
                        point = Spot(0); MouseEvent(true, point); break;
                    case 5: MouseEvent(false, point); break;
                    case 6:
                        Check(Preview().PendingCount(VoxelGarageUpgradeKind.Guns) == 2 && VoxelCurrencyState.Balance == balance,
                            "Open confirmation blocked selection of a second gun");
                        Button("Wheel Spike Purchase Button").onClick.Invoke();
                        Button("Performance Wheel Purchase Button").onClick.Invoke();
                        Button("Boost Bottle Purchase Button").onClick.Invoke();
                        Check(Preview().Selections.Count == 5, "Switching cards discarded the pending gun pair");
                        rotation = shop.DisplayedCar.transform.rotation;
                        MouseEvent(true, point); break;
                    case 7: MouseEvent(true, point + new Vector2(80, 0)); break;
                    case 8: MouseEvent(false, point + new Vector2(80, 0)); break;
                    case 9:
                        Check(Preview().Selections.Count == 5 && Quaternion.Angle(rotation, shop.DisplayedCar.transform.rotation) > 1,
                            "Manual drag did not rotate the car with the purchase panel open");
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
                        Button("Left Door Armor Purchase Button").onClick.Invoke();
                        Call("SelectUpgradePlacement", 1); shop.enabled = false;
                        Check(Preview() == null && !VoxelArmorUpgradeState.IsPurchased, "Disabling the shop did not cancel its pending parts");
                        shop.enabled = true;
                        Check(Button("Left Door Armor Purchase Button").GetComponent<VoxelGarageUpgradeCard>().State == VoxelGarageUpgradeState.Affordable,
                            "Re-enabling the shop left its canceled card marked pending");
                        Button("Left Door Armor Purchase Button").onClick.Invoke();
                        shop.transform.Find("Repair Upgrade UI/Car Upgrade Panel/Close Panel").GetComponent<Button>().onClick.Invoke();
                        Check(Preview() == null, "Closing upgrades left a preview");
                        shop.garageAutoRotationDegreesPerSecond = 0;
                        shop.DisplayedCar.transform.rotation = Quaternion.Euler(0, -15, 0);
                        Button("Upgrade Menu Button").onClick.Invoke();
                        Button("Left Door Armor Purchase Button").onClick.Invoke();
                        Call("SelectUpgradePlacement", 1); Call("SelectUpgradePlacement", 0);
                        Button("Plough Purchase Button").onClick.Invoke();
                        point = PendingSpot(VoxelGarageUpgradeKind.Armour, 1); MouseEvent(true, point); break;
                    case 11: MouseEvent(false, point); break;
                    case 12:
                        Check(Preview().PendingCount(VoxelGarageUpgradeKind.Armour) == 1 && Preview().IsPending(VoxelGarageUpgradeKind.Armour, 0) &&
                            !Preview().IsPending(VoxelGarageUpgradeKind.Armour, 1) && Preview().PendingCount(VoxelGarageUpgradeKind.Plough) == 1,
                            "Mouse tap on a fitted model removed the wrong type/side or failed across tabs");
                        Button("Left Door Armor Purchase Button").onClick.Invoke();
                        Check(Preview().PendingCount(VoxelGarageUpgradeKind.Armour) == 0 && Preview().PendingCount(VoxelGarageUpgradeKind.Plough) == 1,
                            "Retapping an armour tab did not remove its remaining pending side");
                        Button("Plough Purchase Button").onClick.Invoke();
                        Check(Preview() == null && !shop.transform.Find("Repair Upgrade UI/Upgrade Purchase Confirmation").gameObject.activeSelf,
                            "Retapping the last pending tab did not hide confirmation");
                        Button("Left Missile Purchase Button").onClick.Invoke(); Call("SelectUpgradePlacement", 1);
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
                        Button("Plough Purchase Button").onClick.Invoke();
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
                        Check(SceneManager.GetActiveScene().name==missionScene && VoxelTrackProgressState.CurrentTrackIndex==missionIndex,"Start Mission did not advance and load the mission");
                        Check(!VoxelPloughUpgradeState.IsPurchased && VoxelCurrencyState.Balance==departureBalance,"Starting without pending parts bought them or spent cash");
                        Check(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).All(t=>t.name!="Temporary Upgrade Placement"),"Unpurchased preview leaked into the mission");
                        Finish("PASS (Play Mode): immediate menu/garage spin, real mouse/touch placement and removal, multiple selections, combined purchase, disable/close cleanup; starting with pending parts warns, Go Back retains the cart, Start Mission advances/loads the real race without spending or purchasing pending parts. Temporary input devices and cash/purchase/campaign/clock state restored."); return;
                }
                phase++; frame = Time.frameCount;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        private static void Cleanup()
        {
            if (shop != null) Call("CancelUpgradePlacement");
            if (root != null) Object.Destroy(root); root = null; shop = null;
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
