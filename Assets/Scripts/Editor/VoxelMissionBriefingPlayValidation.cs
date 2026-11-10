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
                        oldIndex = VoxelTrackProgressState.CurrentTrackIndex; wallet = VoxelCurrencyState.Balance;
                        menu.StartMission(); Check(VoxelMissionBriefing.IsShowing, "Main-menu Start bypassed briefing."); Next(1); break;
                    case 1:
                        if (EditorApplication.timeSinceStartup - phaseAt < .3) return;
                        Check(SceneManager.GetActiveScene().name == "MainMenu" && VoxelCurrencyState.Balance == wallet && VoxelTrackProgressState.CurrentTrackIndex == oldIndex,
                            "Menu briefing prematurely reset the run or loaded a race.");
                        ScreenCapture.CaptureScreenshot("Temp/MissionBriefing/LiveMenuBriefing.png");
                        Click("Back"); Check(!VoxelMissionBriefing.IsShowing && !VoxelLoadingScreen.IsLoading, "Menu Back launched or retained briefing.");
                        PressSpace(10); break;
                    case 10:
                        if (!AfterPress) return;
                        Check(VoxelMissionBriefing.IsShowing && SceneManager.GetActiveScene().name == "MainMenu",
                            "Space did not open the briefing, or the held press launched through it.");
                        ReleaseSpace(); Next(11); break;
                    case 11:
                        if (keyboard.spaceKey.isPressed) return;
                        PressSpace(2); break;
                    case 2:
                        if (!AfterPress) return;
                        Check(SceneManager.GetActiveScene().name == "SampleScene" && !VoxelMissionBriefing.IsShowing && VoxelStartCountdown.Active != null,
                            "Race repeated or bypassed confirmed briefing.");
                        Check(!VoxelStartCountdown.Active.IsComplete && !VoxelStartCountdown.Active.IsTrafficSpawnWindowOpen, "Loading did not retain opening gate.");
                        ReleaseSpace();
                        Next(3); break;
                    case 3:
                        if (!VoxelStartCountdown.Active.IsComplete) return;
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
                        car.SetDrivingEnabled(false);
                        var body = car.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
                        body.gameObject.SetActive(false); VoxelCurrencyState.Add(123);
                        VoxelCarRunState.Capture(car); wallet = VoxelCurrencyState.Balance; missing = VoxelCarRunState.MissingVoxelCount;
                        oldIndex = VoxelTrackProgressState.CurrentTrackIndex; nextTrack = VoxelTrackProgressState.NextTrack;
                        // Let VoxelRunFinish.Update run its normal stop and finish-camera setup.
                        // Setting HasFinished directly leaves the post-race UI waiting on a
                        // finish sequence that was never started.
                        typeof(VoxelMissionProgress).GetProperty("IsComplete").SetValue(VoxelMissionProgress.Active, true);
                        Next(14); break;
                    case 14:
                        var results = UnityEngine.Object.FindFirstObjectByType<VoxelPostRaceContinue>();
                        if (results == null || results.ContinueButton == null || keyboard.spaceKey.isPressed) return;
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
                        Check(VoxelTrackProgressState.CurrentTrackIndex == oldIndex && VoxelCurrencyState.Balance == wallet && VoxelCarRunState.MissingVoxelCount == missing,
                            "Garage briefing changed campaign, cash or damage before Start.");
                        Click("Back"); Check(!VoxelMissionBriefing.IsShowing && SceneManager.GetActiveScene().name == "RepairUpgrade", "Garage Back left the garage.");
                        UnityEngine.Object.FindFirstObjectByType<VoxelRepairUpgradeSceneController>().StartNextRace();
                        ScreenCapture.CaptureScreenshot("Temp/MissionBriefing/LiveGarageBriefing.png");
                        ReleaseSpace(); Next(12); break;
                    case 12:
                        if (keyboard.spaceKey.isPressed || !AfterPress) return;
                        PressSpace(6); break;
                    case 6:
                        if (!AfterPress) return;
                        Check(VoxelTrackProgressState.CurrentTrack == nextTrack && !VoxelMissionBriefing.IsShowing && VoxelStartCountdown.Active != null,
                            "Next race advanced incorrectly or repeated the briefing.");
                        Check(VoxelCurrencyState.Balance == wallet && VoxelStartCountdown.Active.target.MissingIntegrityVoxels == missing,
                            "Next race lost cash or damage.");
                        // Opening the shared race directly must still show a briefing, with the clock held.
                        ReleaseSpace(); Load("SampleScene"); Next(7); break;
                    case 7:
                        Check(VoxelMissionBriefing.IsShowing && VoxelMissionBriefing.Active.Track == nextTrack && !VoxelStartCountdown.Active.IsComplete &&
                            !VoxelStartCountdown.Active.IsTrafficSpawnWindowOpen && VoxelStartCountdown.CurrentGameplayHudAlpha == 0,
                            "Direct race entry did not hold driving/traffic/HUD behind its briefing.");
                        Next(8); break;
                    case 8:
                        if (EditorApplication.timeSinceStartup - phaseAt < .4) return;
                        var countdown = VoxelStartCountdown.Active;
                        Check(!countdown.IsComplete && !countdown.IsTrafficSpawnWindowOpen && countdown.target.CurrentSpeed == 0,
                            "Race advanced while briefing was visible.");
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
                        Finish(true, "PASS (Play Mode): Space advanced Start, Select/Race, briefings, Resume, Continue, garage Next and Restart. Holding Space did not advance twice or brake after Resume. Disabled Restart, read-only briefing cancellation, shared loading, campaign advancement, cash/body-damage continuity, direct-race gates and scene cleanup verified."); break;
                }
            }
            catch (Exception error) { Finish(false, "FAIL at phase " + phase + ": " + error); }
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
