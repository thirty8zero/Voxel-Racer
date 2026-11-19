using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer.Editor
{
    public static class VoxelMissionBriefingPlayValidation
    {
        private const string Pending = "VoxelRacer.MissionBriefingPlayQA";
        private const string VolumePreference = "VoxelRacer.MasterVolume";
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
        private static float bayRevealedAt, startupBrakeOnAt, startupBrakeOffAt;
        private static float ignitionAt, idleAt, rearTouchdownAt, idleGainAtTouchdown, idleFadeStartedAt, idleFadeEndedAt;
        private static float touchdownLowRate, touchdownCruiseRate;
        private static AudioSource starterAudio, idleAudio, lowAudio, highAudio, maxRpmAudio, coastingAudio;
        private static AudioSource mediumOnAudio, mediumOffAudio;
        private static ShiftObservation rollingUpshift, upshift, downshift;
        private static AudioSource[] drivingAudio;
        private static bool heardRoadAudio, heardStationaryStartup, heardStationaryIdle, heardCoasting;
        private static bool heardTouchdownMidrange;
        private static float lowPitch, cruisePitch;
        private static float cruiseCoastingVolume;
        private static bool heardLaneLift, pickupEngineShutdown;
        private static float savedVolume;
        private static int skipPass, skipWallet, skipMissing;
        private static float skipRequestedAt, skipTimer, skipPauseScale, skipPauseAlpha, skipPauseCountdown;
        private static double skipPausedAt;
        private static bool skipSawBlack, skipSawRoad, skipPauseTested, skipFadeCleared;
        private static float[] skipRoadSamples;
        private static VoxelTrackPose[] skipStraightRoad, skipPlannedRoad, skipVisibleRoad;
        private static readonly FieldInfo OpeningFade = typeof(VoxelStartCountdown).GetField("openingFade",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool IsUnloading(VoxelCarrierIntro intro) => !(bool)typeof(VoxelCarrierIntro)
            .GetField("grounded", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(intro);
        private static bool SkipFadeActive(VoxelStartCountdown countdown) => (bool)typeof(VoxelStartCountdown)
            .GetField("carrierSkipFade", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(countdown);
        private static float CountdownAge(VoxelStartCountdown countdown) => (float)typeof(VoxelStartCountdown)
            .GetProperty("CountdownElapsed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(countdown);
        private static bool hadVolumePreference;
        private static readonly PropertyInfo BayRevealed = typeof(VoxelCarrierTruck).GetProperty("HasRevealedBay",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo IntroRearAxle = typeof(VoxelCarrierIntro).GetField("rearAxle",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly HashSet<int> exhaustPuffs = new(), touchdownPuffs = new();
        private static readonly HashSet<int> puffsWithParticles = new();
        private static float roadSampleDistance;
        private static VoxelTrackPose roadAtFinalCount;
        private static bool pickupBlocked, pickupPan, pickupRamp, pickupLoaded;
        private static bool pickupWaitedForTraffic;
        private static bool pickupLoadingTrafficAccelerated, pickupPassingTrafficAccelerated, pickupPassingTrafficFaded;
        private static bool pickupSawSlowMotion, pickupEasedSpeed, pickupLowerRevs;
        private static float pickupInitialLaneOffset, pickupCruiseSpeed, previousPickupSpeed, pickupInitialPitch;
        private static bool pickupRearView, pickupBoardingFramed, pickupBraked;
        private static bool resultsAtFront, enemyPulledAway, enemyDroppedBack;
        private static VoxelEnemyCar finishEnemyAhead;
        private static VoxelObstacleCar finishEnemyBehind;
        private static VoxelObstacle pickupLaneBlocker;
        private static VoxelObstacleCar pickupLoadingTraffic, pickupPassingTraffic, pickupOncoming;
        private static Transform[] pickupSteeringPivots, pickupWheels;
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
                phase = skipPass = 0; began = phaseAt = EditorApplication.timeSinceStartup;
                skipPausedAt = -1;
                saved = new VoxelGarageUpgradeDockValidation.SavedState(); random = UnityEngine.Random.state;
                oldBackground = Application.runInBackground; Application.runInBackground = true;
                hadVolumePreference = PlayerPrefs.HasKey(VolumePreference);
                savedVolume = PlayerPrefs.GetFloat(VolumePreference, 1f);
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
        private static void TapRightSide(VoxelMainMenuController menu)
        {
            var events = EventSystem.current;
            var hits = new List<RaycastResult>();
            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left };
            var area = menu.transform.Find("Main Menu UI/Start Tap Area").gameObject;
            foreach (var point in new[] { new Vector2(.51f, .02f), new Vector2(.98f, .98f), new Vector2(.75f, .55f) })
            {
                pointer.position = Vector2.Scale(point, new Vector2(Screen.width, Screen.height));
                events.RaycastAll(pointer, hits);
                Check(hits.Count > 0 && hits[0].gameObject == area, "Right-side tap missed the expanded START area.");
            }
            var rightTarget = hits[0].gameObject;
            pointer.position = new Vector2(Screen.width * .49f, Screen.height * .55f);
            events.RaycastAll(pointer, hits);
            Check(hits.All(hit => hit.gameObject != area), "The expanded START tap area covers the left side.");
            pointer.position = new Vector2(Screen.width * .75f, Screen.height * .55f);
            ExecuteEvents.Execute(rightTarget, pointer, ExecuteEvents.pointerClickHandler);
        }
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
                Check(EditorApplication.timeSinceStartup - began < 120, "Briefing transition test timed out at phase " + phase);
                if (VoxelLoadingScreen.IsLoading) return;
                switch (phase)
                {
                    case 0:
                        var menu = UnityEngine.Object.FindFirstObjectByType<VoxelMainMenuController>();
                        if (menu == null) return;
                        VoxelCarSelectionState.Select(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar"));
                        TapRightSide(menu); Next(1); break;
                    case 1:
                        if (!AfterPress) return;
                        Check(SceneManager.GetActiveScene().name == "SampleScene" && VoxelMissionBriefing.IsShowing && Truck != null,
                            "Tapping the right side of the main menu did not enter the carrier briefing.");
                        Check(VoxelMissionBriefing.Active.GetComponentsInChildren<RectTransform>()
                            .Any(rect => rect.name == "Briefing Panel" && Mathf.Approximately(rect.rect.width, 900)),
                            "The live briefing did not match the result-panel width.");
                        truckWheels = Truck.transform.Cast<Transform>().Where(child => child.name.EndsWith(" wheel", StringComparison.Ordinal)).ToArray();
                        Check(truckWheels.Length == 6, "The carrier did not have six independent wheel pivots.");
                        lastTruckWheelRotations = truckWheels.Select(wheel => wheel.localRotation).ToArray();
                        lastTruckPosition = Truck.transform.position;
                        sawRampSparks = capturedRampSparks = false;
                        bayRevealedAt = startupBrakeOnAt = startupBrakeOffAt = -1f;
                        ignitionAt = idleAt = rearTouchdownAt = idleFadeStartedAt = idleFadeEndedAt = -1f;
                        touchdownLowRate = touchdownCruiseRate = 0f;
                        heardTouchdownMidrange = false;
                        BindEngineAudio(VoxelStartCountdown.Active.target);
                        heardRoadAudio = false;
                        exhaustPuffs.Clear();
                        touchdownPuffs.Clear();
                        puffsWithParticles.Clear();
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
                        VerifyUnloadingSmoke(Truck);
                        // Keep full unload/audio coverage; the early skip is checked
                        // separately both before the ramp opens and during unloading.
                        if (IsUnloading(UnityEngine.Object.FindFirstObjectByType<VoxelCarrierIntro>())) return;
                        if (Time.frameCount <= phaseFrame + 1) return;
                        truckReachedRightLane = truckFellBehind = false;
                        PressSpace(2); break;
                    case 2:
                        VerifyUnloadingSmoke(Truck);
                        if (!AfterPress) return;
                        Check(SceneManager.GetActiveScene().name == "SampleScene" && !VoxelMissionBriefing.IsShowing && VoxelStartCountdown.Active != null,
                            "Race repeated or bypassed confirmed briefing.");
                        Check(!VoxelStartCountdown.Active.IsComplete && !VoxelStartCountdown.Active.IsTrafficSpawnWindowOpen, "Loading did not retain opening gate.");
                        Check(!VoxelStartCountdown.Active.HasReachedFinalCount && Truck != null &&
                            !SkipFadeActive(VoxelStartCountdown.Active),
                            "Start after unloading did not retain the normal countdown.");
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
                        Check(exhaustPuffs.Count == 1 && touchdownPuffs.Count == 4,
                            $"Expected one stock exhaust puff and four tyre puffs, saw {exhaustPuffs.Count} and {touchdownPuffs.Count}.");
                        Check(exhaustPuffs.Concat(touchdownPuffs).All(puffsWithParticles.Contains),
                            "A carrier smoke emitter never produced particles.");
                        Check(!VoxelStartCountdown.Active.target.BrakeLightsActive,
                            "The startup brake-light flash stayed on through the countdown.");
                        Check(heardRoadAudio && drivingAudio.Any(source => source.volume > .02f),
                            "The driving recordings did not become audible immediately after the rear tyres touched down.");
                        Check(heardTouchdownMidrange && touchdownCruiseRate > touchdownLowRate * 1.7f,
                            "The touchdown revs did not rise smoothly from low to cruising over the first second.");
                        Check(ignitionAt >= 0f && idleAt >= 0f && Mathf.Abs(ignitionAt - startupBrakeOnAt - .2f) < .08f &&
                            Mathf.Abs(idleAt - ignitionAt - 1f) < .08f,
                            $"Startup and idle playback missed their cues ({ignitionAt - startupBrakeOnAt:F3}s and {idleAt - ignitionAt:F3}s).");
                        Check(idleFadeStartedAt >= 0f && idleFadeEndedAt >= 0f,
                            "The idle overlap did not complete after the rear tyres touched down.");
                        rollingUpshift = default;
                        rollingUpshift.Sample(highAudio.pitch);
                        VoxelPauseMenu.Active.SetPaused(true);
                        Check(VoxelPauseMenu.IsPaused, "Could not open pause for Space validation.");
                        Next(44); break;
                    case 44:
                        if (!AfterPress) return;
                        var raceVolume = VoxelPauseMenu.Active.GetComponentInChildren<VoxelAudioVolumeSlider>().GetComponent<Slider>();
                        CheckVolume(raceVolume, Mathf.RoundToInt(savedVolume * 100));
                        DragVolume(raceVolume, 37);
                        CheckVolume(raceVolume, 37);
                        Check(AudioListener.pause && Time.timeScale == 0f, "Adjusting the volume released the race pause.");
                        PressSpace(13); break;
                    case 13:
                        if (highAudio.volume > .025f)
                            rollingUpshift.Sample(highAudio.pitch);
                        if (!AfterPress) return;
                        Check(!VoxelPauseMenu.IsPaused, "Space did not activate Resume.");
                        Check(!SpaceBrakes(), "Held Resume press leaked into the brake.");
                        Check(Mathf.Approximately(AudioListener.volume, .37f) && !AudioListener.pause,
                            "The adjusted master volume did not take effect on Resume.");
                        ReleaseSpace(); Next(24); break;
                    case 24:
                        if (keyboard.spaceKey.isPressed || Time.frameCount <= phaseFrame) return;
                        if (highAudio.volume > .025f)
                            rollingUpshift.Sample(highAudio.pitch);
                        if (!rollingUpshift.Completed && EditorApplication.timeSinceStartup - phaseAt < 1.2) return;
                        Check(rollingUpshift.Completed, "The upshift after the rolling start lacked a strong rev dip and recovery.");
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
                        var requestedLane = typeof(VoxelCarController).GetField("currentLane", BindingFlags.Instance | BindingFlags.NonPublic);
                        requestedLane.SetValue(car, pickupRoad.laneCount - 1);
                        car.SetTrack(pickupRoad, car.TrackDistance);
                        requestedLane.SetValue(car, pickupRoad.laneCount - 2);
                        pickupSteeringPivots = car.GetComponentsInChildren<Transform>()
                            .Where(pivot => pivot.name == "Front Wheel Steering").ToArray();
                        pickupWheels = car.GetComponentsInChildren<Transform>()
                            .Where(wheel => wheel.name == "Voxel Wheel").ToArray();
                        Check(pickupSteeringPivots.Length == 2, "The player car did not have both front steering pivots.");
                        foreach (var pivot in pickupSteeringPivots)
                            pivot.localRotation = Quaternion.Euler(0f, car.frontWheelTurnDegrees * .6f, 0f);
                        float pickupLane = pickupRoad.GetLaneOffset(pickupRoad.laneCount - 2);
                        float topSpeed = car.EffectiveTopSpeed;
                        pickupCruiseSpeed = Mathf.Min(26f, topSpeed);
                        previousPickupSpeed = topSpeed;
                        pickupInitialPitch = 0f;
                        pickupSawSlowMotion = pickupEasedSpeed = pickupLowerRevs = false;
                        pickupLaneBlocker = new GameObject("Pickup lane blocker").AddComponent<VoxelObstacle>();
                        pickupLaneBlocker.Configure(car, pickupRoad, null, car.TrackDistance + 3f * pickupCruiseSpeed + 18f, pickupLane);
                        pickupLoadingTraffic = SpawnPickupTraffic(car, pickupRoad, "Pacing traffic in loading lane", true,
                            18f, pickupLane, topSpeed);
                        pickupPassingTraffic = SpawnPickupTraffic(car, pickupRoad, "Following traffic in passing lane", true,
                            -17f, car.CurrentLaneOffset, .65f * topSpeed);
                        pickupOncoming = SpawnPickupTraffic(car, pickupRoad, "Oncoming traffic in loading lane", false,
                            5.44f * pickupCruiseSpeed, pickupLane, .25f * pickupCruiseSpeed);
                        pickupBlocked = pickupPan = pickupRamp = pickupLoaded = false;
                        pickupEngineShutdown = false;
                        pickupWaitedForTraffic = false;
                        pickupLoadingTrafficAccelerated = pickupPassingTrafficAccelerated = pickupPassingTrafficFaded = false;
                        pickupRearView = pickupBoardingFramed = pickupBraked = false;
                        resultsAtFront = enemyPulledAway = enemyDroppedBack = false;
                        SpawnFinishEnemies(car, pickupRoad);
                        var body = car.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
                        body.gameObject.SetActive(false); VoxelCurrencyState.Add(123);
                        VoxelCarRunState.Capture(car); wallet = VoxelCurrencyState.Balance; missing = VoxelCarRunState.MissingVoxelCount;
                        oldIndex = VoxelTrackProgressState.CurrentTrackIndex; nextTrack = VoxelTrackProgressState.NextTrack;
                        // Let VoxelRunFinish start the real success presentation.
                        typeof(VoxelMissionProgress).GetProperty("IsComplete").SetValue(VoxelMissionProgress.Active, true);
                        typeof(VoxelMissionProgress).GetMethod("BeginCompletionSlowMotion", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(VoxelMissionProgress.Active, null);
                        Next(14); break;
                    case 14:
                        VerifyCarrierPickup();
                        var results = UnityEngine.Object.FindFirstObjectByType<VoxelPostRaceContinue>();
                        if (!pickupLoaded || results == null || results.ContinueButton == null || keyboard.spaceKey.isPressed) return;
                        if (Vector3.Distance(Camera.main.transform.position, pickupCamera) <= 10f ||
                            Vector3.Distance(Camera.main.transform.position, Truck.transform.position) <
                            Vector3.Distance(pickupCamera, pickupTruck) + 8f) return;
                        Check(pickupBlocked && pickupWaitedForTraffic && pickupPan && pickupRamp && pickupRearView && pickupBoardingFramed &&
                            pickupLoadingTrafficAccelerated && pickupPassingTrafficAccelerated && pickupPassingTrafficFaded &&
                            pickupLoadingTraffic == null && pickupPassingTraffic == null &&
                            resultsAtFront && enemyPulledAway && enemyDroppedBack && finishEnemyAhead == null && finishEnemyBehind == null &&
                            Vector3.Distance(Truck.transform.position, pickupTruck) > 15f,
                            $"Pickup validation failed. Obstacle wait: {pickupBlocked}, oncoming wait: {pickupWaitedForTraffic}, " +
                            $"pan: {pickupPan}, ramp: {pickupRamp}, rear view: {pickupRearView}, framing: {pickupBoardingFramed}, " +
                            $"loading traffic exit: {pickupLoadingTrafficAccelerated}, following traffic acceleration: {pickupPassingTrafficAccelerated}, " +
                            $"following traffic fade: {pickupPassingTrafficFaded}, traffic gone: {pickupLoadingTraffic == null && pickupPassingTraffic == null}, " +
                            $"front results: {resultsAtFront}, enemy exits: {enemyPulledAway && enemyDroppedBack}, " +
                            $"enemies gone: {finishEnemyAhead == null && finishEnemyBehind == null}.");
                        Check(pickupSawSlowMotion && pickupEasedSpeed && pickupLowerRevs,
                            "The player did not ease down to convoy speed with lower revs after slow motion.");
                        PressSpace(4); break;
                    case 4:
                        if (!AfterPress) return;
                        var shop = UnityEngine.Object.FindFirstObjectByType<VoxelRepairUpgradeSceneController>();
                        Check(shop != null && shop.DisplayedCar != null && VoxelCarRunState.MissingVoxelCount == missing, "Garage lost damage.");
                        shop.GetComponentsInChildren<Button>().Single(button => button.name == "Settings Button").onClick.Invoke();
                        ReleaseSpace(); Next(45); break;
                    case 45:
                        if (!AfterPress) return;
                        var garageVolume = UnityEngine.Object.FindFirstObjectByType<VoxelAudioVolumeSlider>().GetComponent<Slider>();
                        CheckVolume(garageVolume, 37);
                        DragVolume(garageVolume, 0);
                        CheckVolume(garageVolume, 0);
                        DragVolume(garageVolume, 72);
                        CheckVolume(garageVolume, 72);
                        garageVolume.transform.parent.parent.Find("Close Panel").GetComponent<Button>().onClick.Invoke();
                        Next(15); break;
                    case 15:
                        if (keyboard.spaceKey.isPressed) return;
                        UnityEngine.Object.FindFirstObjectByType<VoxelRepairUpgradeSceneController>()
                            .GetComponentsInChildren<Button>().Single(button => button.name == "Level Preview Button").onClick.Invoke();
                        Next(50); break;
                    case 50:
                        if (!AfterPress) return;
                        var previewGarage = UnityEngine.Object.FindFirstObjectByType<VoxelRepairUpgradeSceneController>();
                        Check(previewGarage != null && VoxelMissionBriefing.IsShowing && VoxelMissionBriefing.Active.Track == nextTrack &&
                            VoxelTrackProgressState.CurrentTrackIndex == oldIndex &&
                            !previewGarage.transform.Find("Player Integrity HUD").gameObject.activeSelf &&
                            !previewGarage.transform.Find("Repair Upgrade UI").gameObject.activeSelf,
                            "Garage preview advanced the campaign, showed the wrong mission or left UI/integrity visible.");
                        Check(VoxelMissionBriefing.Active.GetComponentsInChildren<Button>().Length == 1 &&
                            VoxelMissionBriefing.Active.GetComponentsInChildren<Button>()[0].name == "Back", "Garage preview must only offer Back.");
                        PressSpace(51); break;
                    case 51:
                        if (!AfterPress) return;
                        Check(VoxelMissionBriefing.IsShowing && SceneManager.GetActiveScene().name == "RepairUpgrade" &&
                            VoxelTrackProgressState.CurrentTrackIndex == oldIndex, "Space started a mission from the garage preview.");
                        VoxelMissionBriefing.Active.Accept();
                        Check(VoxelMissionBriefing.IsShowing, "Preview accepted a launch action.");
                        ReleaseSpace(); Click("Back"); Next(52); break;
                    case 52:
                        if (!AfterPress || keyboard.spaceKey.isPressed) return;
                        var restoredGarage = UnityEngine.Object.FindFirstObjectByType<VoxelRepairUpgradeSceneController>();
                        Check(!VoxelMissionBriefing.IsShowing && restoredGarage.transform.Find("Player Integrity HUD").gameObject.activeSelf &&
                            restoredGarage.transform.Find("Repair Upgrade UI").gameObject.activeSelf && VoxelCurrencyState.Balance == wallet &&
                            VoxelCarRunState.MissingVoxelCount == missing, "Preview Back did not restore the unchanged garage.");
                        restoredGarage.GetComponentsInChildren<Button>().Single(button => button.name == "Level Preview Button").onClick.Invoke();
                        Next(53); break;
                    case 53:
                        if (!AfterPress) return;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); Next(54); break;
                    case 54:
                        if (!AfterPress) return;
                        Check(!VoxelMissionBriefing.IsShowing && VoxelTrackProgressState.CurrentTrackIndex == oldIndex,
                            "Escape did not close the garage preview without advancing the campaign.");
                        ReleaseSpace(); Next(56); break;
                    case 56:
                        if (!AfterPress || keyboard.escapeKey.isPressed) return;
                        PressSpace(5); break;
                    case 5:
                        if (!AfterPress) return;
                        Check(VoxelMissionBriefing.IsShowing && VoxelMissionBriefing.Active.Track == nextTrack, "Space in the garage did not brief the next level.");
                        Check(VoxelCurrencyState.Balance == wallet && VoxelCarRunState.MissingVoxelCount == missing,
                            "Garage briefing changed cash or damage before Start.");
                        Check(VoxelMissionBriefing.Active.GetComponentsInChildren<Button>().All(button => button.name != "Back"),
                            "Actual mission briefing still offered Back.");
                        VoxelMissionBriefing.Active.GoBack();
                        Check(VoxelMissionBriefing.IsShowing, "Mission briefing allowed Back dismissal.");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); Next(55); break;
                    case 55:
                        if (!AfterPress) return;
                        Check(VoxelMissionBriefing.IsShowing && SceneManager.GetActiveScene().name == "SampleScene",
                            "Escape dismissed or left the actual mission briefing.");
                        ReleaseSpace(); ploughLifted = false; Next(26); break;
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
                        VerifySettledFraming(upgraded);
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
                        Check(Mathf.Approximately(AudioListener.volume, .72f), "The next race did not keep the garage volume.");
                        ReleaseSpace(); Next(27); break;
                    case 27:
                        if (!VoxelStartCountdown.Active.HasReachedFinalCount) return;
                        Check(Truck == null, "The carrier returned when Start was pressed after departure.");
                        Check(EditorApplication.timeSinceStartup - phaseAt < 2.8, "Late Start replayed an opening delay; elapsed wall time=" +
                            (EditorApplication.timeSinceStartup - phaseAt).ToString("F3") + "s, time scale=" + Time.timeScale);
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
                        BeginSkipCheck(); break;
                    case 60:
                        VerifySkipCheck(); break;
                    case 70:
                        var midIntro = UnityEngine.Object.FindFirstObjectByType<VoxelCarrierIntro>();
                        Check(VoxelMissionBriefing.IsShowing && midIntro != null, "Second skip did not reach its briefing.");
                        Check(IsUnloading(midIntro), "Second skip missed the unloading phase.");
                        if (Truck == null || !Truck.IsOpen ||
                            (float)typeof(VoxelCarrierIntro).GetField("unloadTime", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(midIntro) < .4f) return;
                        BeginSkipCheck(); break;
                    case 71:
                        Check(VoxelMissionBriefing.IsShowing && VoxelMissionBriefing.Active.GetComponentsInChildren<Button>().All(button => button.name != "Back"),
                            "Direct-entry mission briefing must offer Continue only.");
                        VoxelMissionBriefing.Active.GoBack();
                        Check(VoxelMissionBriefing.IsShowing, "Direct-entry briefing allowed Back dismissal.");
                        Load("MainMenu"); Next(9); break;
                    case 9:
                        Check(SceneManager.GetActiveScene().name == "MainMenu" && !VoxelMissionBriefing.IsShowing, "Explicit scene transition retained the old briefing.");
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
                        ReleaseSpace(); Next(32); break;
                    case 32:
                        if (Truck != null) return;
                        VerifySettledFraming(VoxelStartCountdown.Active.target);
                        ScreenCapture.CaptureScreenshot("Temp/MissionBriefing/LiveSettledBriefing.png");
                        Next(33); break;
                    case 33:
                        if (Time.frameCount <= phaseFrame + 1) return;
                        Click("Start Mission"); Next(20); break;
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
                        BindEngineAudio(VoxelStartCountdown.Active.target);
                        heardStationaryStartup = heardStationaryIdle = heardCoasting = false;
                        lowPitch = cruisePitch = 0f;
                        upshift = downshift = default;
                        ReleaseSpace();
                        Next(40); break;
                    case 40:
                        heardStationaryStartup |= starterAudio.isPlaying && starterAudio.volume > .02f;
                        if (!VoxelStartCountdown.Active.IsComplete)
                        {
                            Check(drivingAudio.All(source => source.volume < .001f),
                                "The stationary car played a driving loop during the countdown.");
                            heardStationaryIdle |= idleAudio.isPlaying && idleAudio.volume > .1f;
                            return;
                        }
                        Check(heardStationaryStartup && heardStationaryIdle, "Retry skipped the startup or idle recording.");
                        Next(41); break;
                    case 41:
                        var drivingCar = VoxelStartCountdown.Active.target;
                        float speedRatio = drivingCar.CurrentSpeed / drivingCar.EffectiveTopSpeed;
                        if (lowPitch == 0f && speedRatio < .27f && lowAudio.volume > .04f)
                            lowPitch = lowAudio.pitch;
                        if (speedRatio >= .44f && speedRatio <= .8f && mediumOnAudio.volume > .025f)
                            upshift.Sample(mediumOnAudio.pitch);
                        if (speedRatio < .999f || highAudio.pitch < .994f || highAudio.volume < .12f) return;
                        Check(lowPitch > 0f && lowAudio.pitch > lowPitch && idleAudio.volume < .001f,
                            "Engine pitch did not rise with speed, or the idle loop leaked into cruising.");
                        Check(upshift.Completed, "Revs did not dip and rise again through the upshift.");
                        cruisePitch = highAudio.pitch;
                        cruiseCoastingVolume = coastingAudio.volume;
                        heardLaneLift = false;
                        float firstLane = drivingCar.TrackPath.GetLaneOffset(0);
                        drivingCar.RequestLaneChangeDirection(drivingCar.TargetLaneOffset > firstLane ? -1 : 1);
                        Check(!Mathf.Approximately(drivingCar.CurrentLaneOffset, drivingCar.TargetLaneOffset),
                            "The engine audio test could not start a lane change.");
                        Next(46); break;
                    case 46:
                        var laneCar = VoxelStartCountdown.Active.target;
                        heardLaneLift |= highAudio.pitch < cruisePitch - .017f &&
                            coastingAudio.volume > cruiseCoastingVolume + .012f;
                        float laneTestElapsed = (float)(EditorApplication.timeSinceStartup - phaseAt);
                        if ((!Mathf.Approximately(laneCar.TargetLaneOffset, laneCar.CurrentLaneOffset) ||
                             laneTestElapsed < .55f || Mathf.Abs(highAudio.pitch - cruisePitch) > .01f) &&
                            laneTestElapsed < 1.8f) return;
                        Check(heardLaneLift && Mathf.Abs(highAudio.pitch - cruisePitch) <= .01f &&
                            Mathf.Approximately(laneCar.CurrentSpeed, laneCar.EffectiveTopSpeed),
                            "The lane change did not lift off and restore engine pitch at constant speed.");
                        Check(UnityEngine.Object.FindFirstObjectByType<VoxelBoostController>().TryActivateBoost(),
                            "The engine audio test could not activate boost.");
                        Next(43); break;
                    case 43:
                        if (maxRpmAudio.volume < .12f && EditorApplication.timeSinceStartup - phaseAt < 1.3) return;
                        Check(maxRpmAudio.isPlaying && maxRpmAudio.volume >= .12f && highAudio.pitch > cruisePitch,
                            "Boost did not raise the pitch and blend into the max-RPM recording.");
                        PressSpace(42); break;
                    case 42:
                        var brakingCar = VoxelStartCountdown.Active.target;
                        float brakeSpeed = brakingCar.CurrentSpeed / brakingCar.EffectiveTopSpeed;
                        if (brakeSpeed >= .17f && brakeSpeed <= .57f && mediumOffAudio.volume > .025f)
                            downshift.Sample(-mediumOffAudio.pitch);
                        heardCoasting |= coastingAudio.isPlaying && coastingAudio.volume > .035f &&
                            highAudio.pitch < cruisePitch;
                        if (brakingCar.CurrentSpeed > 0f || drivingAudio.Any(source => source.volume > .001f)) return;
                        Check(heardCoasting && idleAudio.isPlaying && idleAudio.volume > .1f && idleAudio.pitch == 1f,
                            "Braking did not lower pitch and blend through coasting to idle.");
                        Check(downshift.Completed, "Revs did not spike and fall again through the downshift.");
                        ReleaseSpace();
                        Finish(true, $"PASS (Play Mode): ignition delay {ignitionAt - startupBrakeOnAt:F3}s, idle at {idleAt - ignitionAt:F3}s through starter playback, road audio ramps from {touchdownLowRate:F1} to {touchdownCruiseRate:F1} engine cycles/s over the first second after rear tyre touchdown, idle starts fading at {idleFadeStartedAt - rearTouchdownAt:F3}s and goes silent at {idleFadeEndedAt - rearTouchdownAt:F3}s after touchdown. Also verified rolling-start upshift {rollingUpshift.PitchChange:P0}, stationary-start upshift {upshift.PitchChange:P0}, braking downshift {downshift.PitchChange:P0}, early skips before ramp opening and midway through unloading (covered car/road/scenery handoff, forced bends remain stable from fade-in through HUNT, final colliders, paused fade/countdown, repeated input, chase view, traffic/weapon/timer gates and overlay cleanup), normal Start after unloading/departure, camera at 1, upgraded unload and plough clearance, exhaust and tyre puffs, acceleration and boost, lane-change pitch, braking and idle, master volume, rolling wheels and ramp effects, pickup camera, easing to 26 m/s and lower revs after slow motion, lane clearance at convoy speed, pickup brake lights, trailer engine shutdown, garage Level Preview Back/Escape, hidden/restored garage UI and integrity, no preview launch through Space/Accept, Continue-only mission controls with Back/Escape ignored, damage and cash continuity, and Space navigation."); break;
                }
            }
            catch (Exception error) { Finish(false, "FAIL at phase " + phase + ": " + error); }
        }

        private struct ShiftObservation
        {
            private const float MinimumPitchChange = .17f;
            private float before, trough;
            private bool dipped;
            internal bool Completed { get; private set; }
            internal float PitchChange => before == 0f ? 0f : (before - trough) / Mathf.Abs(before);

            // Negating a downshift's samples lets the same check detect a spike and fall.
            internal void Sample(float pitch)
            {
                if (!dipped)
                {
                    before = before == 0f ? pitch : Mathf.Max(before, pitch);
                    trough = pitch;
                    dipped = PitchChange >= MinimumPitchChange;
                    return;
                }

                trough = Mathf.Min(trough, pitch);
                Completed |= pitch - trough >= Mathf.Abs(before) * .10f;
            }
        }

        private static void VerifySettledFraming(VoxelCarController car)
        {
            Camera camera = Camera.main;
            float left = 1f, right = 0f;
            foreach (var renderer in car.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                Bounds bounds = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = new((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    float x = camera.WorldToViewportPoint(renderer.transform.TransformPoint(local)).x;
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                }
            }

            var panel = VoxelMissionBriefing.Active.GetComponentsInChildren<RectTransform>()
                .Single(rect => rect.name == "Briefing Panel");
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            float panelRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]).x / Screen.width;
            float leftGap = left - panelRight;
            float rightGap = Screen.safeArea.xMax / Screen.width - right;
            Check(leftGap > .01f && rightGap > .01f && Mathf.Abs(leftGap - rightGap) < .06f,
                $"Settled car was not centred beside the briefing. Panel gap: {leftGap:P1}, screen gap: {rightGap:P1}.");
        }

        private static void SpawnFinishEnemies(VoxelCarController car, EndlessVoxelRoad road)
        {
            var traffic = VoxelRacerBootstrap.ActiveTrack.obstacleCarTuning;
            var interceptor = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning");
            var radar = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/RadarInterceptorTuning");
            finishEnemyAhead = new GameObject("Finish interceptor ahead").AddComponent<VoxelEnemyCar>();
            finishEnemyAhead.Configure(car, traffic, interceptor, road, car.TrackDistance + 24f, car.CurrentLaneOffset);
            finishEnemyBehind = new GameObject("Finish radar behind").AddComponent<VoxelObstacleCar>();
            Check(finishEnemyBehind.Configure(car, traffic, true, road, car.TrackDistance - 12f, road.GetLaneOffset(0), 22f, radar),
                "Could not stage intact radar traffic behind the player.");
        }

        private static VoxelObstacleCar SpawnPickupTraffic(VoxelCarController car, EndlessVoxelRoad road,
            string name, bool sameDirection, float gap, float lane, float speed)
        {
            var vehicle = new GameObject(name).AddComponent<VoxelObstacleCar>();
            Check(vehicle.Configure(car, VoxelRacerBootstrap.ActiveTrack.obstacleCarTuning, sameDirection,
                road, car.TrackDistance + gap, lane, speed), "Could not stage pickup traffic: " + name);
            return vehicle;
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
            if (VoxelMissionProgress.Active.IsCompletionSlowMotionActive)
            {
                Check(truck == null && Mathf.Approximately(car.CurrentSpeed, car.EffectiveTopSpeed),
                    "The recovery slowdown started during completion slow motion.");
                pickupSawSlowMotion = true;
                pickupInitialPitch = Mathf.Max(pickupInitialPitch, highAudio.pitch);
            }
            else if (truck == null)
            {
                Check(car.CurrentSpeed >= pickupCruiseSpeed - .001f && car.CurrentSpeed <= previousPickupSpeed + .001f,
                    "The recovery did not ease monotonically to the unloading speed.");
                previousPickupSpeed = car.CurrentSpeed;
                if (car.CurrentSpeed > pickupCruiseSpeed + .01f)
                {
                    Check(Mathf.Abs(car.CurrentLaneOffset - originalLane) < .01f,
                        "The player began merging before settling at convoy speed.");
                    pickupEasedSpeed |= car.CurrentSpeed < car.EffectiveTopSpeed - 1f;
                }
                else
                    pickupLowerRevs |= highAudio.pitch < pickupInitialPitch - .1f &&
                        mediumOnAudio.volume + mediumOffAudio.volume > .07f;
            }
            else if (!pickupLoaded)
                Check(Mathf.Approximately(car.CurrentSpeed, pickupCruiseSpeed),
                    "Pickup approach or boarding used a different speed from unloading.");
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
            if (pickupLoadingTraffic != null)
            {
                Check(!pickupLoadingTraffic.IsDrivingTraffic, "Traffic pacing the player stayed active in the loading lane.");
                var fade = pickupLoadingTraffic.GetComponent<VoxelFadeIn>();
                pickupLoadingTrafficAccelerated |= pickupLoadingTraffic.TravelSpeed > car.EffectiveTopSpeed + 5f &&
                    fade != null && fade.Opacity < 1f;
            }
            if (pickupPassingTraffic != null)
            {
                Check(!pickupPassingTraffic.IsDrivingTraffic && car.TrackDistance - pickupPassingTraffic.TrackDistance > 3f,
                    "Following traffic stayed active or passed through the player in the truck's lane.");
                var fade = pickupPassingTraffic.GetComponent<VoxelFadeIn>();
                pickupPassingTrafficAccelerated |= pickupPassingTraffic.TravelSpeed > pickupCruiseSpeed + 1f;
                pickupPassingTrafficFaded |= fade != null && fade.Opacity < 1f;
            }
            if (pickupOncoming != null)
                Check(pickupOncoming.IsDrivingTraffic && pickupOncoming.isActiveAndEnabled,
                    "Pickup clearance removed oncoming traffic.");
            if (truck == null)
            {
                if (!pickupBlocked)
                {
                    Check(pickupLaneBlocker != null && pickupLaneBlocker.isActiveAndEnabled,
                        "The finish cleared the obstacle from the requested destination lane.");
                    pickupInitialLaneOffset = car.CurrentLaneOffset;
                    Check(Mathf.Abs(pickupInitialLaneOffset - originalLane) < .5f,
                        "The player was no longer in the original lane when recovery began.");
                }
                bool waitingForObstacle = pickupLaneBlocker != null && car.TrackDistance - pickupLaneBlocker.TrackDistance < 10f;
                float trafficGap = pickupOncoming != null ? pickupOncoming.TrackDistance - car.TrackDistance : float.NegativeInfinity;
                bool waitingForTraffic = !waitingForObstacle && trafficGap is > -8f and < 14f;
                if (waitingForObstacle || waitingForTraffic)
                {
                    Check(Mathf.Abs(car.CurrentLaneOffset - pickupInitialLaneOffset) < .01f,
                        $"The player moved from {pickupInitialLaneOffset:F2} to {car.CurrentLaneOffset:F2} while the pickup lane was blocked " +
                        $"(obstacle: {waitingForObstacle}, oncoming gap: {trafficGap:F2}).");
                    pickupBlocked |= waitingForObstacle;
                    pickupWaitedForTraffic |= waitingForTraffic;
                }
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
                var follow = Camera.main.GetComponent<VoxelCameraFollow>();
                var settings = follow.tuning != null ? follow.tuning : VoxelCameraTuning.Load();
                var fit = recovery.GetType().GetField("fit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(recovery);
                var bounds = (Bounds)fit.GetType().GetField("Bounds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fit);
                var startOffset = (Vector3)recovery.GetType().GetField("panOffset",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(recovery);
                Vector3 expectedOffset = (settings != null ? settings.finishOffset : follow.finishOffset) -
                    Vector3.Scale(bounds.center, car.transform.lossyScale);
                float handoffError = Vector3.Distance(startOffset, expectedOffset);
                Check(handoffError < .01f, $"Pickup camera used a stale finish pose ({handoffError:F3} m from the moving car's finish offset).");
                pickupPan = true;
            }
            bool boarding = truck.IsOpen && carLocal.y > .35f;
            if (truck.IsOpen && truck.Opening)
            {
                float rearWheelZ = pickupWheels.Min(wheel => truck.transform.InverseTransformPoint(wheel.position).z);
                if (Mathf.Abs(rearWheelZ - truck.BayRear) > .001f)
                    Check(car.BrakeLightsActive == (rearWheelZ > truck.BayRear),
                        "Pickup brake lights did not match the rear wheels reaching the top of the ramp.");
                pickupBraked |= car.BrakeLightsActive;
            }
            else
                Check(!car.BrakeLightsActive, "Pickup brake lights lit outside boarding.");
            if (car.BrakeLightsActive)
                Check(idleAudio.isPlaying, "The engine stopped before the car finished boarding.");
            else if (pickupBraked && !pickupEngineShutdown)
            {
                Check(!idleAudio.isPlaying || idleAudio.pitch < 1f,
                    "The engine did not begin shutting down when the pickup brake lights went out.");
                pickupEngineShutdown = true;
            }
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
            if (pickupRamp && truck.IsClosed)
                Check(pickupEngineShutdown && !idleAudio.isPlaying && drivingAudio.All(source => !source.isPlaying),
                    "The engine continued playing after the trailer closed.");
            if (pickupRamp && truck.IsClosed && !pickupLoaded)
            {
                Check(pickupBraked, "The car boarded without using its brake lights.");
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

        private static void BeginSkipCheck()
        {
            var countdown = VoxelStartCountdown.Active;
            var intro = UnityEngine.Object.FindFirstObjectByType<VoxelCarrierIntro>();
            Check(intro != null && IsUnloading(intro), "Early Continue was not requested during unloading.");
            // Force a bend in this temporary runtime road so the regression cannot
            // pass accidentally on a randomly straight opening. No asset is edited.
            var road = countdown.target.TrackPath;
            road.turnChancePerSegment = 1f;
            typeof(EndlessVoxelRoad).GetMethod("CaptureAppliedValues", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(road, null);
            skipRoadSamples = new[] { 150f, 300f, 450f, 600f }.Select(offset => countdown.target.TrackDistance + offset).ToArray();
            skipStraightRoad = skipRoadSamples.Select(road.Evaluate).ToArray();
            skipVisibleRoad = null;
            skipWallet = VoxelCurrencyState.Balance;
            skipMissing = countdown.target.MissingIntegrityVoxels;
            skipTimer = VoxelMissionProgress.Active.RemainingTime;
            skipRequestedAt = Time.time;
            skipSawBlack = skipSawRoad = skipPauseTested = skipFadeCleared = false;
            skipPausedAt = -1;
            Click("Start Mission");
            var evaluatePlanned = (Func<float, VoxelTrackPose>)Delegate.CreateDelegate(typeof(Func<float, VoxelTrackPose>), road,
                typeof(EndlessVoxelRoad).GetMethod("EvaluateOpeningTarget", BindingFlags.Instance | BindingFlags.NonPublic));
            skipPlannedRoad = skipRoadSamples.Select(evaluatePlanned).ToArray();
            Check(skipPlannedRoad.Where((pose, i) => pose.position != skipStraightRoad[i].position || pose.forward != skipStraightRoad[i].forward).Any(),
                "The skipped-opening regression did not stage any visible road bends.");
            Check(SkipFadeActive(countdown) && CountdownAge(countdown) < .05f,
                "Early Continue did not immediately start its fade/countdown.");
            Check((bool)typeof(VoxelStartCountdown).GetField("started", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(countdown),
                "Early Continue only queued the countdown.");
            intro.RequestStart(); // Repeated input cannot restart either clock.
            Next(60);
        }

        private static void VerifySkipCheck()
        {
            var countdown = VoxelStartCountdown.Active;
            var car = countdown.target;
            var fade = (CanvasGroup)OpeningFade.GetValue(countdown);
            if (skipPausedAt >= 0)
            {
                Check(Mathf.Abs(fade.alpha - skipPauseAlpha) < .0001f &&
                    Mathf.Abs(CountdownAge(countdown) - skipPauseCountdown) < .0001f,
                    "The unload skip or countdown advanced while time was paused.");
                if (EditorApplication.timeSinceStartup - skipPausedAt < .2) return;
                Time.timeScale = skipPauseScale;
                skipPausedAt = -1;
                return;
            }
            if (!skipPauseTested && SkipFadeActive(countdown))
            {
                skipPauseTested = true;
                skipPauseAlpha = fade.alpha;
                skipPauseCountdown = CountdownAge(countdown);
                skipPauseScale = Time.timeScale;
                Time.timeScale = 0f;
                skipPausedAt = EditorApplication.timeSinceStartup;
                return;
            }
            skipSawBlack |= fade.alpha >= .999f;
            var currentRoad = skipRoadSamples.Select(car.TrackPath.Evaluate).ToArray();
            bool roadChanged = currentRoad.Where((pose, i) => pose.position != skipStraightRoad[i].position || pose.forward != skipStraightRoad[i].forward).Any();
            Check(!roadChanged || skipSawBlack, "The road began bending before the skip was fully covered.");
            var intro = UnityEngine.Object.FindFirstObjectByType<VoxelCarrierIntro>();
            if (intro != null && !IsUnloading(intro) && Truck == null)
            {
                Check(skipSawBlack, "The car/truck changed before a fully covered frame.");
                skipSawRoad = true;
                Check(!car.BrakeLightsActive, "The skipped startup left brake lights on.");
                var camera = Camera.main.GetComponent<VoxelCameraFollow>();
                var pose = car.TrackPath.Evaluate(car.TrackDistance);
                var expected = car.transform.position + pose.rotation * camera.tuning.chaseOffset;
                Check(Vector3.Distance(camera.transform.position, expected) < .06f, "The skip revealed an unfinished carrier camera blend.");
                VerifyPlough(car);
                if (fade.alpha < .999f)
                {
                    Check(!(bool)typeof(EndlessVoxelRoad).GetField("straightOpening", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(car.TrackPath),
                        "The road was still staging when the skip began revealing it.");
                    var scenery = car.TrackPath.GetComponent<VoxelDistantScenery>();
                    Check(scenery == null || typeof(VoxelDistantScenery).GetField("openingScenery", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scenery) == null,
                        "The skip revealed scenery before its final-route tiles were ready.");
                    for (int i = 0; i < currentRoad.Length; i++)
                        Check(currentRoad[i].position == skipPlannedRoad[i].position && currentRoad[i].forward == skipPlannedRoad[i].forward,
                            "The road kept morphing during the skip fade-in.");
                    Check(car.TrackPath.GetComponentsInChildren<VoxelRoadStripMesh>().All(strip =>
                        strip.GetComponent<MeshCollider>().sharedMesh == strip.GetComponent<MeshFilter>().sharedMesh),
                        "The skip revealed road collision before its final shape.");
                    skipVisibleRoad ??= currentRoad;
                }
            }
            if (skipVisibleRoad != null)
                for (int i = 0; i < currentRoad.Length; i++)
                    Check(currentRoad[i].position == skipVisibleRoad[i].position && currentRoad[i].forward == skipVisibleRoad[i].forward,
                        "The visible route changed after the skip revealed it.");
            Check(VoxelCurrencyState.Balance == skipWallet && car.MissingIntegrityVoxels == skipMissing,
                "Skipping the unload changed cash or damage.");
            if (!countdown.IsComplete)
            {
                Check(!countdown.IsTrafficSpawnWindowOpen && VoxelMissionProgress.Active.RemainingTime == skipTimer,
                    "The skipped unload released traffic or the mission timer before HUNT.");
                var gun = car.GetComponentInChildren<VoxelGunMount>();
                Check(gun == null || !gun.TryBeginShot(out _), "A weapon fired during the skipped countdown.");
            }
            if (!SkipFadeActive(countdown) && !skipFadeCleared)
            {
                Check(skipSawBlack && skipSawRoad && skipPauseTested && fade.alpha == 0f && !fade.gameObject.activeSelf,
                    "The skip did not finish its covered handoff and clear the overlay.");
                Check(Time.time - skipRequestedAt < 1f, "The quick unload fade took too long.");
                ScreenCapture.CaptureScreenshot($"Temp/MissionBriefing/LiveUnloadSkip{skipPass}.png");
                skipFadeCleared = true;
            }
            if (!countdown.IsComplete) return;
            Check(skipFadeCleared && Truck == null && countdown.IsTrafficSpawnWindowOpen,
                "The skipped countdown did not hand off to normal driving.");
            skipPass++;
            Load("SampleScene");
            Next(skipPass < 2 ? 70 : 71);
        }

        private static void VerifyRollingDetails(VoxelCarrierTruck truck)
        {
            Check(truck != null, "Carrier disappeared during unload.");
            if (phase == 1 || phase == 10 || phase == 3 || phase == 11)
                VerifyUnloadingSmoke(truck);
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

        private static void VerifyUnloadingSmoke(VoxelCarrierTruck truck)
        {
            var player = VoxelStartCountdown.Active.target;
            float now = Time.time;
            if (bayRevealedAt < 0f && (bool)BayRevealed.GetValue(truck))
            {
                Check(startupBrakeOnAt >= 0f && Mathf.Abs(now - startupBrakeOnAt - .1f) < .08f,
                    "The startup brake lights did not turn on 0.1 seconds before the bay reveal.");
                bayRevealedAt = now;
            }
            if (startupBrakeOnAt < 0f && player.BrakeLightsActive)
            {
                startupBrakeOnAt = now;
                Check(starterAudio.timeSamples == 0 && idleAudio.timeSamples == 0,
                    "Engine audio was already playing at the brake-light flash.");
            }
            if (ignitionAt < 0f && starterAudio.isPlaying && starterAudio.timeSamples > 0)
                ignitionAt = now - starterAudio.time;
            if (idleAt < 0f && idleAudio.isPlaying && idleAudio.timeSamples > 0 && idleAudio.volume > 0f)
                idleAt = now - idleAudio.time;
            if (startupBrakeOnAt >= 0f && startupBrakeOffAt < 0f && !player.BrakeLightsActive)
            {
                Check(Mathf.Abs(now - startupBrakeOnAt - .75f) < .15f, "The startup brake lights did not stay lit for 0.75 seconds.");
                startupBrakeOffAt = now;
            }
            foreach (var puff in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (puff.name == "Carrier Exhaust Puff")
                {
                    int id = puff.GetInstanceID();
                    if (exhaustPuffs.Add(id))
                    {
                        Check(truck.Opening && puff.transform.IsChildOf(player.transform) &&
                            puff.main.simulationSpace == ParticleSystemSimulationSpace.Local,
                            "The exhaust puff did not originate on the car during unloading.");
                        Check(ignitionAt >= 0f && Mathf.Abs(now - ignitionAt - 1f) < .08f,
                            "The exhaust puff did not match the idle cue one second into ignition.");
                        Check(startupBrakeOffAt >= 0f && !player.BrakeLightsActive,
                            "The exhaust puff appeared before the startup brake lights went out.");
                    }
                    if (puff.particleCount > 0)
                        puffsWithParticles.Add(id);
                }
                else if (puff.name == "Carrier Tyre Touchdown Puff")
                {
                    int id = puff.GetInstanceID();
                    if (touchdownPuffs.Add(id))
                    {
                        Vector3 point = truck.transform.InverseTransformPoint(puff.transform.position);
                        Check(point.z <= truck.RampTip + .005f &&
                            Mathf.Abs(point.y - .045f) < .01f &&
                            puff.main.simulationSpace == ParticleSystemSimulationSpace.World,
                            "A tyre puff originated before road contact or did not stay on the road.");
                    }
                    if (puff.particleCount > 0)
                        puffsWithParticles.Add(id);
                }
            }
            if (rearTouchdownAt < 0f)
            {
                var intro = player.GetComponentInParent<VoxelCarrierIntro>();
                if ((float)IntroRearAxle.GetValue(intro) > truck.RampTip)
                {
                    Check(drivingAudio.All(source => !source.isPlaying), "Driving recordings started before the rear tyres touched down.");
                    return;
                }
                Check(touchdownPuffs.Count == 2 && drivingAudio.All(source => source.isPlaying),
                    "Driving recordings did not start with the rear tyres first touching the road.");
                rearTouchdownAt = now;
                idleGainAtTouchdown = idleAudio.volume;
                Check(idleGainAtTouchdown > .1f && idleAudio.pitch == 1f, "Idle had already faded by rear tyre touchdown.");
            }
            float elapsed = now - rearTouchdownAt;
            if (!heardRoadAudio && elapsed < .12f && drivingAudio.Any(source => source.volume > .02f))
            {
                Check(lowAudio.volume > .02f && mediumOnAudio.volume < .001f && highAudio.volume < .001f,
                    "Touchdown driving audio did not start in the low-rev band.");
                touchdownLowRate = lowAudio.pitch * 22.13f;
                heardRoadAudio = true;
            }
            if (elapsed >= .4f && elapsed <= .6f)
                heardTouchdownMidrange |= mediumOnAudio.volume > .04f && highAudio.volume < .01f;
            if (elapsed >= .97f && elapsed <= 1.1f && highAudio.volume > .04f)
                touchdownCruiseRate = highAudio.pitch * 45.94f;
            if (idleFadeEndedAt >= 0f)
                return;
            float idleFraction = idleAudio.volume / idleGainAtTouchdown;
            float expected = Mathf.SmoothStep(1f, 0f, (elapsed - .5f) / .5f);
            Check(Mathf.Abs(idleFraction - expected) < .13f, $"Idle gain was {idleFraction:P0} at {elapsed:F3}s after touchdown.");
            if (idleFadeStartedAt < 0f && idleFraction < .99f)
            {
                Check(Mathf.Abs(elapsed - .5f) < .10f, $"Idle started fading {elapsed:F3}s after touchdown.");
                idleFadeStartedAt = now;
            }
            if (idleFraction <= .001f)
            {
                Check(Mathf.Abs(elapsed - 1f) < .10f, $"Idle went silent {elapsed:F3}s after touchdown.");
                idleFadeEndedAt = now;
            }
        }

        private static void BindEngineAudio(VoxelCarController car)
        {
            var sources = car.GetComponents<AudioSource>().ToDictionary(source => source.clip.name);
            Check(sources.Count == 9, "Expected one starter and eight exterior engine loops.");
            starterAudio = sources["startup"];
            idleAudio = sources["idle"];
            lowAudio = sources["low_on"];
            mediumOnAudio = sources["med_on"];
            mediumOffAudio = sources["med_off"];
            highAudio = sources["high_on"];
            maxRpmAudio = sources["maxRPM"];
            coastingAudio = sources["high_off"];
            drivingAudio = sources.Values.Where(source => source != starterAudio && source != idleAudio).ToArray();
            Check(!starterAudio.loop && idleAudio.loop && drivingAudio.All(source => source.loop) &&
                sources.Values.All(source => !source.playOnAwake && !source.ignoreListenerPause),
                "The engine recordings have incorrect playback or pause settings.");
        }

        private static void DragVolume(Slider slider, int percent)
        {
            Canvas.ForceUpdateCanvases();
            var track = (RectTransform)slider.handleRect.parent;
            Vector2 Point(float fraction) => RectTransformUtility.WorldToScreenPoint(null,
                track.TransformPoint(new Vector2(Mathf.Lerp(track.rect.xMin, track.rect.xMax, fraction), 0f)));
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = Point(slider.normalizedValue < .5f ? .8f : .2f)
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject == slider.gameObject,
                $"The slider track did not receive pointer input at {pointer.position}. Hits: {string.Join(", ", hits.Select(hit => hit.gameObject.name))}.");
            pointer.pointerPressRaycast = hits[0];
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            pointer.position = Point(percent / 100f);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        }

        private static void CheckVolume(Slider slider, int percent)
        {
            float gain = percent / 100f;
            Check(slider.value == percent && Mathf.Approximately(AudioListener.volume, gain) &&
                Mathf.Approximately(PlayerPrefs.GetFloat(VolumePreference, 1f), gain) &&
                slider.transform.parent.Find("Volume Percentage").GetComponent<Text>().text == $"{percent}%",
                $"The volume slider, percentage, saved preference and master volume should all be {percent}%.");
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
            if (skipPausedAt >= 0) { Time.timeScale = skipPauseScale; skipPausedAt = -1; }
            VoxelMissionBriefing.Active?.Close(); saved?.Dispose(); saved = null;
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            if (hadSelectedCar) PlayerPrefs.SetString(VoxelCarSelectionState.SelectedCarKey, selectedCar);
            else PlayerPrefs.DeleteKey(VoxelCarSelectionState.SelectedCarKey);
            if (hadVolumePreference) PlayerPrefs.SetFloat(VolumePreference, savedVolume);
            else PlayerPrefs.DeleteKey(VolumePreference);
            AudioListener.volume = savedVolume;
            PlayerPrefs.Save();
            UnityEngine.Random.state = random; Application.runInBackground = oldBackground;
            Directory.CreateDirectory("Temp/MissionBriefing"); File.WriteAllText("Temp/MissionBriefing/PlayValidation.txt", report);
            if (success) Debug.Log(report); else Debug.LogError(report);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
