using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer.Editor
{
    public static class VoxelMissionBriefingPlayValidation
    {
        private const string Pending = "VoxelRacer.MissionBriefingPlayQA";
        private static int phase, oldIndex, wallet, missing;
        private static double began, phaseAt;
        private static bool oldBackground;
        private static VoxelGarageUpgradeDockValidation.SavedState saved;
        private static UnityEngine.Random.State random;
        private static VoxelTrackDefinition nextTrack;
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
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode) Finish(false, "Interrupted before completion.");
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Next(int value) { phase = value; phaseAt = EditorApplication.timeSinceStartup; }
        private static void Click(string name) => VoxelMissionBriefing.Active.GetComponentsInChildren<Button>().Single(b => b.name == name).onClick.Invoke();
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
                        UnityEngine.Object.FindFirstObjectByType<VoxelMainMenuController>().StartMission(); Click("Start Mission");
                        Check(VoxelLoadingScreen.IsLoading && !VoxelMissionBriefing.IsShowing, "Start did not enter loading flow."); Next(2); break;
                    case 2:
                        Check(SceneManager.GetActiveScene().name == "SampleScene" && !VoxelMissionBriefing.IsShowing && VoxelStartCountdown.Active != null,
                            "Race repeated or bypassed confirmed briefing.");
                        Check(!VoxelStartCountdown.Active.IsComplete && !VoxelStartCountdown.Active.IsTrafficSpawnWindowOpen, "Loading did not retain opening gate.");
                        Next(3); break;
                    case 3:
                        if (!VoxelStartCountdown.Active.IsComplete) return;
                        var car = VoxelStartCountdown.Active.target;
                        car.SetDrivingEnabled(false);
                        var body = car.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
                        body.gameObject.SetActive(false); VoxelCurrencyState.Add(123);
                        VoxelCarRunState.Capture(car); wallet = VoxelCurrencyState.Balance; missing = VoxelCarRunState.MissingVoxelCount;
                        oldIndex = VoxelTrackProgressState.CurrentTrackIndex; nextTrack = VoxelTrackProgressState.NextTrack;
                        Load("RepairUpgrade"); Next(4); break;
                    case 4:
                        var shop = UnityEngine.Object.FindFirstObjectByType<VoxelRepairUpgradeSceneController>();
                        Check(shop != null && shop.DisplayedCar != null && VoxelCarRunState.MissingVoxelCount == missing, "Garage lost damage.");
                        shop.StartNextRace(); Check(VoxelMissionBriefing.IsShowing && VoxelMissionBriefing.Active.Track == nextTrack, "Garage did not brief the next level."); Next(5); break;
                    case 5:
                        if (EditorApplication.timeSinceStartup - phaseAt < .3) return;
                        Check(VoxelTrackProgressState.CurrentTrackIndex == oldIndex && VoxelCurrencyState.Balance == wallet && VoxelCarRunState.MissingVoxelCount == missing,
                            "Garage briefing changed campaign, cash or damage before Start.");
                        Click("Back"); Check(!VoxelMissionBriefing.IsShowing && SceneManager.GetActiveScene().name == "RepairUpgrade", "Garage Back left the garage.");
                        UnityEngine.Object.FindFirstObjectByType<VoxelRepairUpgradeSceneController>().StartNextRace();
                        ScreenCapture.CaptureScreenshot("Temp/MissionBriefing/LiveGarageBriefing.png");
                        Click("Start Mission"); Check(VoxelLoadingScreen.IsLoading, "Garage confirmation did not load race."); Next(6); break;
                    case 6:
                        Check(VoxelTrackProgressState.CurrentTrack == nextTrack && !VoxelMissionBriefing.IsShowing && VoxelStartCountdown.Active != null,
                            "Next race advanced incorrectly or repeated the briefing.");
                        Check(VoxelCurrencyState.Balance == wallet && VoxelStartCountdown.Active.target.MissingIntegrityVoxels == missing,
                            "Next race lost cash or damage.");
                        // Opening the shared race directly must still show a briefing, with the clock held.
                        Load("SampleScene"); Next(7); break;
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
                        Finish(true, "PASS (Play Mode): native main-menu and garage Back/Start buttons, read-only cancellation, shared async loading, no duplicate briefing, once-only campaign advancement, cash/body-damage continuity, countdown/driving/traffic/HUD/weapon/pause gates, direct-race fallback and scene cleanup. Returned to MainMenu/Edit Mode; run state and RNG restored."); break;
                }
            }
            catch (Exception error) { Finish(false, "FAIL at phase " + phase + ": " + error); }
        }
        private static void Finish(bool success, string report)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false); EditorApplication.update -= Tick;
            VoxelMissionBriefing.Active?.Close(); saved?.Dispose(); saved = null;
            UnityEngine.Random.state = random; Application.runInBackground = oldBackground;
            Directory.CreateDirectory("Temp/MissionBriefing"); File.WriteAllText("Temp/MissionBriefing/PlayValidation.txt", report);
            if (success) Debug.Log(report); else Debug.LogError(report);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
