using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer.Editor
{
    public static class VoxelMissionBriefingPlayValidation
    {
        private const string Pending = "VoxelRacer.MissionBriefingPlayQA";
        private static int phase, oldIndex, wallet, missing;
        private static int phaseFrame;
        private static double began, phaseAt;
        private static bool oldBackground;
        private static VoxelGarageUpgradeDockValidation.SavedState saved;
        private static UnityEngine.Random.State random;
        private static VoxelTrackDefinition nextTrack;
        private static Keyboard keyboard;
        private static string selectedCar;
        private static bool hadSelectedCar;
        private static bool ploughLifted, truckReachedRightLane, truckFellBehind;
        private static Transform[] truckWheels;
        private static Quaternion[] lastTruckWheelRotations;
        private static Vector3 lastTruckPosition;
        private static bool sawRampSparks, capturedRampSparks;
        private static float roadSampleDistance;
        private static VoxelTrackPose roadAtFinalCount;
        private static bool pickupBlocked, pickupPan, pickupRamp, pickupLoaded;
        private static bool pickupRearView, pickupBoardingFramed;
        private static bool resultsAtFront, enemyPulledAway, enemyDroppedBack;
        private static VoxelEnemyCar finishEnemyAhead;
        private static VoxelObstacleCar finishEnemyBehind;
        private static Transform[] pickupSteeringPivots;
        private static Vector3 pickupCamera, pickupTruck;
        private static VoxelCarrierTruck Truck => UnityEngine.Object.FindFirstObjectByType<VoxelCarrierTruck>();
        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }
        [MenuItem("Tools/Voxel Racer/Mission Briefing/Validate Transitions In Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "MainMenu")
                throw new InvalidOperationException("Start from MainMenu in idle Edit Mode.");
            Directory.CreateDirectory("Temp/MissionBriefing");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                phase = 0; began = phaseAt = EditorApplication.timeSinceStartup;
                saved = new VoxelGarageUpgradeDockValidation.SavedState(); random = UnityEngine.Random.state;
                oldBackground = Application.runInBackground; Application.runInBackground = true;
                hadSelectedCar = PlayerPrefs.HasKey(VoxelCarSelectionState.SelectedCarKey);
                selectedCar = PlayerPrefs.GetString(VoxelCarSelectionState.SelectedCarKey);
                keyboard = InputSystem.AddDevice<Keyboard>();
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode) Finish(false, "Interrupted before completion.");
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Next(int value) { phase = value; phaseAt = EditorApplication.timeSinceStartup; phaseFrame = Time.frameCount; }
        private static void Click(string name) => VoxelMissionBriefing.Active.GetComponentsInChildren<Button>().Single(b => b.name == name).onClick.Invoke();
        private static void PressSpace(int next)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            Next(next);
        }
        private static void ReleaseSpace() => InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        private static bool AfterPress => Time.frameCount >= phaseFrame + 2 && EditorApplication.timeSinceStartup - phaseAt >= .2;
        private static bool SpaceBrakes() => (bool)typeof(VoxelMenuUi).Assembly.GetType("VoxelRacer.VoxelEditorNavigation")
            .GetMethod("IsSpaceBrakeHeld", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .Invoke(null, new object[] { keyboard });
        private static void Load(string name)
        {
            int index = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + name + ".unity");
            Check(VoxelLoadingScreen.LoadScene(index), "Could not load " + name);
        }
        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            try
            {
                Check(EditorApplication.timeSinceStartup - began < 100, "Briefing transition test timed out at phase " + phase);
                if (VoxelLoadingScreen.IsLoading) return;
                switch (phase)
                {
                    case 0:
                        var menu = UnityEngine.Object.FindFirstObjectByType<VoxelMainMenuController>();
                        if (menu == null) return;
                        VoxelCarSelectionState.Select(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar"));
                        PressSpace(1); break;
                    case 1:
                        if (!AfterPress) return;
                        Check(SceneManager.GetActiveScene().name == "SampleScene" && VoxelMissionBriefing.IsShowing && Truck != null,
                            "Main-menu Start did not enter the carrier briefing, or held Space confirmed it.");
                        Check(VoxelMissionBriefing.Active.GetComponentsInChildren<RectTransform>()
                            .Any(rect => rect.name == "Briefing Panel" && Mathf.Approximately(rect.rect.width, 900)),
                            "The live briefing did not match the result-panel width.");
                        truckWheels = Truck.transform.Cast<Transform>().Where(child => child.name.EndsWith(" wheel", StringComparison.Ordinal)).ToArray();
                        Check(truckWheels.Length == 6, "The carrier did not have six independent wheel pivots.");
                        lastTruckWheelRotations = truckWheels.Select(wheel => wheel.localRotation).ToArray();
                        lastTruckPosition = Truck.transform.position;
                        sawRampSparks = capturedRampSparks = false;
                        VerifyRollingDetails(Truck);
                        ReleaseSpace(); Next(10); break;
                    case 10:
                        VerifyRollingDetails(Truck);
                        if (keyboard.spaceKey.isPressed || EditorApplication.timeSinceStartup - phaseAt < 2.7) return;
                        Check(Truck != null && VoxelStartCountdown.Active.target.transform.position.y > .1f,
                            "The test did not reach the moving unload.");
                        ScreenCapture.CaptureScreenshot("Temp/MissionBriefing/LiveMenuBriefing.png");
                        Next(30); break;
                    case 30:
                        if (Time.frameCount <= phaseFrame + 1) return;
                        truckReachedRightLane = truckFellBehind = false;
                        PressSpace(2); break;
                    case 2:
                        if (!AfterPress) return;
                        Check(SceneManager.GetActiveScene().name == "SampleScene" && !VoxelMissionBriefing.IsShowing && VoxelStartCountdown.Active != null,
                            "Race repeated or bypassed confirmed briefing.");
                        Check(!VoxelStartCountdown.Active.IsComplete && !VoxelStartCountdown.Active.IsTrafficSpawnWindowOpen, "Loading did not retain opening gate.");
                        Check(!VoxelStartCountdown.Active.HasReachedFinalCount && Truck != null &&
                            VoxelStartCountdown.Active.target.transform.position.y > .1f,
                            "Starting during unload interrupted the carrier or started the countdown early.");
                        ReleaseSpace();
                        Next(3); break;
                    case 3:
                        VerifyDeparture();
                        if (!VoxelStartCountdown.Active.HasReachedFinalCount) return;
                        var openingCar = VoxelStartCountdown.Active.target;
                        var camera = Camera.main.GetComponent<VoxelCameraFollow>();
                        var pose = openingCar.TrackPath.Evaluate(openingCar.TrackDistance);
                        var chase = openingCar.transform.position + pose.rotation * camera.tuning.chaseOffset;
                        Check(Vector3.Distance(camera.transform.position, chase) < .06f, "Camera did not reach normal chase at 1.");
                        Check(!(bool)typeof(EndlessVoxelRoad).GetField("straightOpening",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(openingCar.TrackPath),
                            "The planned road did not settle by 1.");
                        roadSampleDistance = openingCar.TrackDistance + 260f;
                        roadAtFinalCount = openingCar.TrackPath.Evaluate(roadSampleDistance);
                        Next(11); break;
                    case 11:
                        VerifyDeparture();
                        if (!VoxelStartCountdown.Active.IsComplete) return;
                        Check(Truck == null && truckReachedRightLane && truckFellBehind,
                            "The truck did not clear the ramp, merge right and fall back before driving began.");
                        var roadAtLaunch = VoxelStartCountdown.Active.target.TrackPath.Evaluate(roadSampleDistance);
                        Check(roadAtLaunch.position == roadAtFinalCount.position && roadAtLaunch.forward == roadAtFinalCount.forward,
                            "The visible route changed at HUNT.");
                        Check(sawRampSparks, "Both ramp corners did not maintain a dense spray during unloading.");
                        VoxelPauseMenu.Active.SetPaused(true);
                        Check(VoxelPauseMenu.IsPaused, "Could not open pause for Space validation.");
                        PressSpace(13); break;
                    case 13:
                        if (!AfterPress) return;
                        Check(!VoxelPauseMenu.IsPaused, "Space did not activate Resume.");
                        Check(!SpaceBrakes(), "Held Resume press leaked into the brake.");
                        ReleaseSpace(); Next(24); break;
                    case 24:
                        if (keyboard.spaceKey.isPressed || Time.frameCount <= phaseFrame) return;
                        PressSpace(25);
                        InputSystem.Update();
                        Check(SpaceBrakes(), "A fresh Space press during driving did not brake.");
                        ReleaseSpace(); break;
                    case 25:
                        if (keyboard.spaceKey.isPressed) return;
                        var car = VoxelStartCountdown.Active.target;
                        VoxelDeveloperActions.GiveAllUpgrades(car);
                        car.SetDrivingEnabled(false);
                        var pickupRoad = car.TrackPath;
                        typeof(VoxelCarController).GetField("currentLane", System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic).SetValue(car, pickupRoad.laneCount - 1);
                        car.SetTrack(pickupRoad, car.TrackDistance);
                        pickupSteeringPivots = car.GetComponentsInChildren<Transform>()
                            .Where(pivot => pivot.name == "Front Wheel Steering").ToArray();
                        Check(pickupSteeringPivots.Length == 2, "The player car did not have both front steering pivots.");
                        foreach (var pivot in pickupSteeringPivots)
                            pivot.localRotation = Quaternion.Euler(0f, car.frontWheelTurnDegrees * .6f, 0f);
                        new GameObject("Pickup lane blocker").AddComponent<VoxelObstacle>().Configure(car, pickupRoad, null,
                            car.TrackDistance + 18f, pickupRoad.GetLaneOffset(pickupRoad.laneCount - 2));
                        pickupBlocked = pickupPan = pickupRamp = pickupLoaded = false;
                        pickupRearView = pickupBoardingFramed = false;
                        resultsAtFront = enemyPulledAway = enemyDroppedBack = false;
                        SpawnFinishEnemies(car, pickupRoad);
                        var body = car.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
                        body.gameObject.SetActive(false); VoxelCurrencyState.Add(123);
                        VoxelCarRunState.Capture(car); wallet = VoxelCurrencyState.Balance; missing = VoxelCarRunState.MissingVoxelCount;
                        oldIndex = VoxelTrackProgressState.CurrentTrackIndex; nextTrack = VoxelTrackProgressState.NextTrack;
                        // Let VoxelRunFinish start the real success presentation.
                        typeof(VoxelMissionProgress).GetProperty("IsComplete").SetValue(VoxelMissionProgress.Active, true);
                        Next(14); break;
                    case 14:
                        VerifyCarrierPickup();
                        var results = UnityEngine.Object.FindFirstObjectByType<VoxelPostRaceContinue>();
                        if (!pickupLoaded || results == null || results.ContinueButton == null || keyboard.spaceKey.isPressed) return;
                        if (Vector3.Distance(Camera.main.transform.position, pickupCamera) <= 10f ||
                            Vector3.Distance(Camera.main.transform.position, Truck.transform.position) <
                            Vector3.Distance(pickupCamera, pickupTruck) + 8f) return;
                        Check(pickupBlocked && pickupPan && pickupRamp && pickupRearView && pickupBoardingFramed &&
                            resultsAtFront && enemyPulledAway && enemyDroppedBack && finishEnemyAhead == null && finishEnemyBehind == null &&
                            Vector3.Distance(Truck.transform.position, pickupTruck) > 15f,
                            "The carrier did not finish the blocked-lane pickup and leave the moving results camera.");
                        PressSpace(4); break;
                    case 4:
                        if (!AfterPress) return;
                        var shop = UnityEngine.Object.FindFirstObjectByType<VoxelRepairUpgradeSceneController>();
                        Check(shop != null && shop.DisplayedCar != null && VoxelCarRunState.MissingVoxelCount == missing, "Garage lost damage.");
                        ReleaseSpace(); Next(15); break;
                    case 15:
                        if (keyboard.spaceKey.isPressed) return;
                        PressSpace(5); break;
                    case 5:
                        if (!AfterPress) return;
                        Check(VoxelMissionBriefing.IsShowing && VoxelMissionBriefing.Active.Track == nextTrack, "Space in the garage did not brief the next level.");
                        Check(VoxelCurrencyState.Balance == wallet && VoxelCarRunState.MissingVoxelCount == missing,
                            "Garage briefing changed cash or damage before Start.");
                        Click("Back");
                        ReleaseSpace(); Next(12); break;
                    case 12:
                        if (keyboard.spaceKey.isPressed || !AfterPress) return;
                        var garage = UnityEngine.Object.FindFirstObjectByType<VoxelRepairUpgradeSceneController>();
                        Check(garage != null && VoxelTrackProgressState.CurrentTrackIndex == oldIndex, "Back did not restore the garage and mission index.");
                        garage.StartNextRace(); ploughLifted = false; Next(26); break;
                    case 26:
                        Check(VoxelMissionBriefing.IsShowing && VoxelStartCountdown.Active != null &&
                            !VoxelStartCountdown.Active.IsComplete && !VoxelStartCountdown.Active.IsTrafficSpawnWindowOpen,
                            "The garage carrier briefing did not keep the race gated.");
                        var upgraded = VoxelStartCountdown.Active.target;
                        Check(upgraded.GetComponent<VoxelCarrierIntro>() != null || upgraded.GetComponentInParent<VoxelCarrierIntro>() != null,
                            "The fully upgraded car did not fit the carrier.");
                        VerifyPlough(upgraded);
                        var road = upgraded.TrackPath;
                        for (int i = 0; i <= 9; i++)
                            Check(Mathf.Abs(road.Evaluate(upgraded.TrackDistance + i * 60).forward.x) < .001f,
                                "The staging road turned while the briefing was open.");
                        if (Truck != null || EditorApplication.timeSinceStartup - phaseAt < 12) return;
                        Check(ploughLifted && upgraded.TrackDistance > 100, "The truck did not unload the fitted plough and leave the player cruising.");
                        ScreenCapture.CaptureScreenshot("Temp/MissionBriefing/LiveGarageBriefing.png");
                        Next(31); break;
                    case 31:
                        if (Time.frameCount <= phaseFrame + 1) return;
                        PressSpace(6); break;
                    case 6:
                        if (!AfterPress) return;
                        Check(VoxelTrackProgressState.CurrentTrack == nextTrack && !VoxelMissionBriefing.IsShowing && VoxelStartCountdown.Active != null,
                            "Next race advanced incorrectly or repeated the briefing.");
                        Check(VoxelCurrencyState.Balance == wallet && VoxelStartCountdown.Active.target.MissingIntegrityVoxels == missing,
                            "Next race lost cash or damage.");
                        ReleaseSpace(); Next(27); break;
                    case 27:
                        if (!VoxelStartCountdown.Active.HasReachedFinalCount) return;
                        Check(Truck == null, "The carrier returned when Start was pressed after departure.");
                        Check(EditorApplication.timeSinceStartup - phaseAt < 2.8, "Late Start replayed an opening delay.");
                        Load("SampleScene"); Next(7); break;
                    case 7:
                        Check(VoxelMissionBriefing.IsShowing && VoxelMissionBriefing.Active.Track == nextTrack && !VoxelStartCountdown.Active.IsComplete &&
                            !VoxelStartCountdown.Active.IsTrafficSpawnWindowOpen && VoxelStartCountdown.CurrentGameplayHudAlpha == 0,
                            "Direct race entry did not hold driving/traffic/HUD behind its briefing.");
                        Next(8); break;
                    case 8:
                        if (EditorApplication.timeSinceStartup - phaseAt < .4) return;
                        var countdown = VoxelStartCountdown.Active;
                        Check(!countdown.IsComplete && !countdown.IsTrafficSpawnWindowOpen && countdown.target.CurrentSpeed > 0,
                            "Carrier briefing did not keep the moving car gated.");
                        var gun = countdown.target.GetComponentInChildren<VoxelGunMount>();
                        Check(gun == null || !gun.TryBeginShot(out _), "Briefing allowed weapons.");
                        VoxelPauseMenu.Active.SetPaused(true); Check(!VoxelPauseMenu.IsPaused, "Pause opened over briefing.");
                        Click("Back"); Next(9); break;
                    case 9:
                        Check(SceneManager.GetActiveScene().name == "MainMenu" && !VoxelMissionBriefing.IsShowing, "Direct-entry Back did not cleanly return to menu.");
                        Load("CarSelect"); Next(16); break;
                    case 16:
                        PressSpace(17); break;
                    case 17:
                        if (!AfterPress) return;
                        var carSelect = UnityEngine.Object.FindFirstObjectByType<VoxelCarSelectSceneController>();
                        Check(carSelect != null && carSelect.GetComponentsInChildren<Button>().Any(b => b.name == "Race Button"),
                            "Space did not select the displayed car, or the held press launched its race.");
                        ReleaseSpace(); Next(18); break;
                    case 18:
                        if (keyboard.spaceKey.isPressed) return;
                        PressSpace(19); break;
                    case 19:
                        if (!AfterPress) return;
                        Check(SceneManager.GetActiveScene().name == "SampleScene" && VoxelMissionBriefing.IsShowing,
                            "A second Space in CarSelect did not enter the race briefing.");
                        ReleaseSpace(); Click("Start Mission"); Next(20); break;
                    case 20:
                        if (keyboard.spaceKey.isPressed || !VoxelStartCountdown.Active.IsComplete) return;
                        VoxelPlayerDeathScreen.Active.ShowBossEscaped(); PressSpace(21); break;
                    case 21:
                        if (EditorApplication.timeSinceStartup - phaseAt < .8) return;
                        Check(VoxelPlayerDeathScreen.IsShowing && SceneManager.GetActiveScene().name == "SampleScene",
                            "Space bypassed the disabled Restart button during its reveal.");
                        ReleaseSpace(); Next(22); break;
                    case 22:
                        if (keyboard.spaceKey.isPressed) return;
                        PressSpace(23); break;
                    case 23:
                        if (!AfterPress) return;
                        Check(SceneManager.GetActiveScene().name == "MainMenu" && !VoxelMissionBriefing.IsShowing, "Space did not Restart, or carried through to Start.");
                        ReleaseSpace(); Next(28); break;
                    case 28:
                        if (keyboard.spaceKey.isPressed) return;
                        PressSpace(29); break;
                    case 29:
                        if (!AfterPress) return;
                        Check(SceneManager.GetActiveScene().name == "SampleScene" && !VoxelMissionBriefing.IsShowing &&
                            Truck == null && VoxelStartCountdown.Active != null && VoxelStartCountdown.Active.target.CurrentSpeed == 0,
                            "Retry did not use the normal stationary opening.");
                        Finish(true, "PASS (Play Mode): early and late Start, camera at 1, fully upgraded unload and plough clearance, rolling wheels and ramp sparks, results at the front three-quarter shot, intact enemies clear ahead and behind, blocked-lane pickup, rear-left orbit and ramp framing, moving road during departure, retry, garage Back, damage and cash continuity, and Space navigation."); break;
                }
            }
            catch (Exception error) { Finish(false, "FAIL at phase " + phase + ": " + error); }
        }

        private static void SpawnFinishEnemies(VoxelCarController car, EndlessVoxelRoad road)
        {
            var traffic = VoxelRacerBootstrap.ActiveTrack.obstacleCarTuning;
            var interceptor = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning");
            var radar = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/RadarInterceptorTuning");
            finishEnemyAhead = new GameObject("Finish interceptor ahead").AddComponent<VoxelEnemyCar>();
            finishEnemyAhead.Configure(car, traffic, interceptor, road, car.TrackDistance + 24f, car.CurrentLaneOffset);
            finishEnemyBehind = new GameObject("Finish radar behind").AddComponent<VoxelObstacleCar>();
            Check(finishEnemyBehind.Configure(car, traffic, true, road, car.TrackDistance - 12f, car.CurrentLaneOffset, 22f, radar),
                "Could not stage intact radar traffic behind the player.");
        }

        private static void VerifyCarrierPickup()
        {
            var finish = UnityEngine.Object.FindFirstObjectByType<VoxelRunFinish>();
            if (finish == null || !finish.HasFinished) return;
            var car = finish.target;
            var road = car.TrackPath;
            var truck = Truck;
            float originalLane = road.GetLaneOffset(road.laneCount - 1);
            float pickupLane = road.GetLaneOffset(road.laneCount - 2);
            if (finishEnemyAhead != null)
            {
                Check(!finishEnemyAhead.IsDrivingTraffic && Mathf.Approximately(finishEnemyAhead.HealthPercent, 1f),
                    "The intact interceptor was still active in traffic or took damage.");
                enemyPulledAway |= finishEnemyAhead.TrackDistance - car.TrackDistance > 31f;
            }
            if (finishEnemyBehind != null)
            {
                Check(!finishEnemyBehind.IsDrivingTraffic &&
                    Mathf.Approximately(finishEnemyBehind.CurrentHealth, finishEnemyBehind.EnemyTuning.vehicleHealth),
                    "The intact radar car was still active in traffic or took damage.");
                enemyDroppedBack |= finishEnemyBehind.TravelSpeed < 10f && car.TrackDistance - finishEnemyBehind.TrackDistance > 20f;
            }
            bool resultsShown = GameObject.Find("Mission Reward UI") != null;
            if (!finish.FinishCameraComplete)
                Check(!resultsShown, "The results appeared before the front orbit settled.");
            else if (!resultsAtFront)
            {
                Check(resultsShown && car.transform.InverseTransformPoint(Camera.main.transform.position).z > 2f,
                    "The results did not appear at the front three-quarter shot.");
                resultsAtFront = true;
            }
            if (resultsAtFront)
                Check(pickupSteeringPivots.All(pivot => Quaternion.Angle(pivot.localRotation, Quaternion.identity) < .05f),
                    "The front wheels stayed turned during the finish sequence.");
            if (truck == null)
            {
                pickupBlocked |= Mathf.Abs(car.CurrentLaneOffset - originalLane) < .01f &&
                    Mathf.Approximately(car.CurrentSpeed, car.EffectiveTopSpeed);
                return;
            }

            var recovery = typeof(VoxelRunFinish).GetField("recovery",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(finish);
            bool hasCamera = (bool)recovery.GetType().GetProperty("HasCamera",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(recovery);
            Vector3 carLocal = truck.transform.InverseTransformPoint(car.transform.position);
            Check(Mathf.Abs(car.CurrentLaneOffset - pickupLane) < .02f, "Pickup truck approached before the car moved left.");
            if (hasCamera && !pickupPan)
            {
                Check(Mathf.Abs(carLocal.z) < 3f && carLocal.x < -car.laneWidth * .65f,
                    "Camera did not begin following the pickup truck when it drew alongside.");
                pickupPan = true;
            }
            bool boarding = truck.IsOpen && carLocal.y > .35f;
            if (hasCamera && (!pickupRamp || boarding))
            {
                Camera camera = Camera.main;
                Vector3 carInFrame = camera.WorldToViewportPoint(car.transform.TransformPoint(Vector3.up * .8f));
                Check(carInFrame.z > 0f && carInFrame.x > .04f && carInFrame.x < .96f &&
                    carInFrame.y > .04f && carInFrame.y < .96f, "The player car left the shot during the pickup.");
                Vector3 eye = car.transform.InverseTransformPoint(camera.transform.position);
                bool besideResults = carInFrame.x > .59f && carInFrame.x < .89f && Mathf.Abs(carInFrame.y - .5f) < .10f;
                pickupRearView |= !truck.IsOpen && eye.x < -3f && eye.z < -4f && besideResults;
                pickupBoardingFramed |= boarding && besideResults;
            }
            pickupRamp |= boarding;
            if (pickupRamp && truck.IsClosed && !pickupLoaded)
            {
                Check(truck.IsClosed && carLocal.y >= truck.DeckHeight - .1f &&
                    carLocal.z > truck.BayRear && carLocal.z < truck.BayFront,
                    "The ramp shut before the car was fully in the bay.");
                pickupLoaded = true;
                pickupCamera = Camera.main.transform.position;
                pickupTruck = truck.transform.position;
            }
        }

        private static void VerifyDeparture()
        {
            var truck = Truck;
            if (truck == null)
                return;
            VerifyRollingDetails(truck);
            var player = VoxelStartCountdown.Active.target;
            Vector3 separation = truck.transform.position - player.transform.position;
            if (!capturedRampSparks && sawRampSparks && truck.IsOpen && truck.Opening && separation.z > 9.5f)
            {
                ScreenCapture.CaptureScreenshot("Temp/MissionBriefing/LiveRampSparks.png");
                capturedRampSparks = true;
            }
            if (separation.x > .15f)
                Check(truck.IsClosed, "The truck started its lane change with the ramp still open.");
            truckReachedRightLane |= Mathf.Abs(separation.x - player.laneWidth) < .15f;
            truckFellBehind |= separation.z < -6f;
        }

        private static void VerifyRollingDetails(VoxelCarrierTruck truck)
        {
            Check(truck != null, "Carrier disappeared during unload.");
            float distance = truck.transform.position.z - lastTruckPosition.z;
            for (int i = 0; i < truckWheels.Length; i++)
            {
                Transform wheel = truckWheels[i];
                float angle = distance / wheel.localPosition.y * Mathf.Rad2Deg;
                Quaternion expected = lastTruckWheelRotations[i] * Quaternion.AngleAxis(angle, Vector3.right);
                Check(Quaternion.Angle(wheel.localRotation, expected) < .1f, "Carrier wheel spin did not match distance travelled.");
                lastTruckWheelRotations[i] = wheel.localRotation;
            }
            lastTruckPosition = truck.transform.position;

            var sparks = truck.GetComponentsInChildren<ParticleSystem>();
            if (!truck.IsOpen || !truck.Opening)
            {
                Check(sparks.All(emitter => !emitter.isEmitting), "Ramp sparks emitted away from road contact.");
                return;
            }
            Check(sparks.Length == 2 && sparks.All(emitter => emitter.isEmitting &&
                emitter.main.simulationSpace == ParticleSystemSimulationSpace.World &&
                Mathf.Abs(emitter.transform.position.y) < .08f &&
                Vector3.Dot(emitter.transform.forward, truck.transform.forward) < -.8f),
                "The fully deployed ramp did not trail sparks from both road-contact points.");
            Check(Vector3.Distance(sparks[0].transform.position, sparks[1].transform.position) > 3f,
                "Ramp sparks did not originate at opposite corners.");
            if (sawRampSparks || !sparks.All(emitter => emitter.particleCount >= 32))
                return;

            var color = new MaterialPropertyBlock();
            foreach (var emitter in sparks)
            {
                Check(emitter.main.startSize.constantMax <= .055f && emitter.main.maxParticles == 96 &&
                    Mathf.Approximately(emitter.emission.rateOverTime.constant, 200f) &&
                    emitter.shape.angle >= 20f && Vector3.Dot(emitter.transform.forward, Vector3.up) > .5f,
                    "The ramp spray did not use small sparks aimed up in a wide cone.");
                emitter.GetComponent<ParticleSystemRenderer>().GetPropertyBlock(color);
                Color yellow = color.GetColor("_BaseColor");
                Check(yellow.r > 1f && yellow.g > 1f && yellow.b < .1f, "The ramp spray was not bright yellow.");
                Check(emitter.collision.enabled && emitter.collision.type == ParticleSystemCollisionType.Planes &&
                    emitter.collision.GetPlane(0) == truck.transform, "The ramp spray did not use the shared road plane.");
            }
            VerifyRampSparkBounce(sparks[0], truck.transform);
            sawRampSparks = true;
        }

        private static void VerifyRampSparkBounce(ParticleSystem source, Transform roadPlane)
        {
            var emitter = UnityEngine.Object.Instantiate(source);
            try
            {
                emitter.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var emission = emitter.emission;
                emission.enabled = false;
                emitter.Play(false);
                Vector3 origin = roadPlane.position;
                emitter.Emit(new ParticleSystem.EmitParams
                {
                    position = origin + Vector3.up * .2f,
                    velocity = -roadPlane.forward * 5f + Vector3.down * 8f,
                    startSize = .04f,
                    startLifetime = .4f
                }, 1);
                emitter.Simulate(.05f, false, false, false);
                var particle = new ParticleSystem.Particle[1];
                Check(emitter.GetParticles(particle) == 1 && particle[0].velocity.y > 0f &&
                    Vector3.Dot(particle[0].position - origin, -roadPlane.forward) > .1f,
                    "A ramp spark did not rebound and continue along the road.");
            }
            finally
            {
                UnityEngine.Object.Destroy(emitter.gameObject);
            }
        }

        private static void VerifyPlough(VoxelCarController car)
        {
            var plough = car.GetComponentInChildren<VoxelPloughMount>();
            Check(plough != null && plough.Tuning == VoxelPloughTuning.Load(), "The next race lost the equipped plough.");
            bool raised = Quaternion.Angle(plough.transform.localRotation, Quaternion.Euler(plough.Tuning.mountRotation)) > 2;
            ploughLifted |= raised;
            var truck = Truck;
            if (truck == null || !truck.IsOpen)
            {
                Check(!raised, "The plough did not restore its gameplay angle.");
                return;
            }
            foreach (MeshFilter filter in plough.GetComponentsInChildren<MeshFilter>())
            {
                var matrix = truck.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(vertex);
                    Check(point.y >= truck.SurfaceHeight(point.z) - .008f, "The lifted plough clipped the ramp.");
                }
            }
        }
        private static void Finish(bool success, string report)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false); EditorApplication.update -= Tick;
            VoxelMissionBriefing.Active?.Close(); saved?.Dispose(); saved = null;
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            if (hadSelectedCar) PlayerPrefs.SetString(VoxelCarSelectionState.SelectedCarKey, selectedCar);
            else PlayerPrefs.DeleteKey(VoxelCarSelectionState.SelectedCarKey);
            PlayerPrefs.Save();
            UnityEngine.Random.state = random; Application.runInBackground = oldBackground;
            Directory.CreateDirectory("Temp/MissionBriefing"); File.WriteAllText("Temp/MissionBriefing/PlayValidation.txt", report);
            if (success) Debug.Log(report); else Debug.LogError(report);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
