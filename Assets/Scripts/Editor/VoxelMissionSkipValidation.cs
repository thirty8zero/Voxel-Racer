using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Runs the real pause-to-garage-to-next-race transitions without editing source assets.</summary>
    public static class VoxelMissionSkipValidation
    {
        private const string Pending = "VoxelRacer.MissionSkipValidation";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static VoxelGarageUpgradeDockValidation.SavedState saved;
        private static VoxelTrackSequence sequence;
        private static int phase, testCase, skippedIndex, nextIndex, wallet, expectedGuns;
        private static float scale, step;
        private static bool audio, background, captured;
        private static double phaseStarted;
        private static string sourceBefore;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Pause Menu/Validate Mission Skip in Play Mode")]
        public static void Run()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.GetActiveScene().name == "MainMenu",
                "Start mission-skip validation from MainMenu in Edit Mode.");
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                phase = testCase = 0; captured = false;
                phaseStarted = Time.realtimeSinceStartupAsDouble;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.ExitingPlayMode) Finish(false, "Interrupted validation.");
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
        private static void Load(string sceneName)
        {
            phaseStarted = Time.realtimeSinceStartupAsDouble;
            Check(VoxelLoadingScreen.LoadScene(SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + sceneName + ".unity")),
                "Failed to request " + sceneName);
        }

        private static void StartCase()
        {
            sequence.loopSequence = testCase != 2;
            skippedIndex = testCase == 0 ? 0 : Array.FindLastIndex(sequence.tracks, t => t != null && t.isBossLevel);
            Check(skippedIndex >= 0, "Boss track missing from sequence.");
            typeof(VoxelTrackProgressState).GetField("currentTrackIndex", Static).SetValue(null, skippedIndex);
            nextIndex = VoxelTrackProgressState.NextTrackIndex;
            VoxelCarRunState.BeginNewRun(VoxelCarSelectionState.GetSelectedOrDefault());
            VoxelCurrencyState.Add(100000);
            Check(VoxelGunUpgradeState.TryPurchase(VoxelGunUpgradeState.LongGunTuning, 1), "QA gun purchase failed.");
            if (testCase == 0)
            {
                var definition = VoxelCarSelectionState.GetSelectedOrDefault();
                Check(VoxelWheelSpikeUpgradeState.TryPurchase(VoxelWheelSpikeTuning.Load()), "QA basic spikes failed.");
                Check(VoxelBoostUpgradeState.TryPurchase(VoxelBoostUpgradeTuning.LoadUpgrade(), definition), "QA single boost failed.");
                Check(VoxelArmorUpgradeState.TryPurchase(VoxelArmorTuning.Load(), definition, VoxelArmorSide.Right), "QA right armour failed.");
            }
            expectedGuns = 1;
            VoxelCurrencyState.Reset(); VoxelCurrencyState.Add(222); wallet = VoxelCurrencyState.Balance;
            Time.timeScale = .65f; AudioListener.pause = testCase == 1;
            Load(VoxelTrackProgressState.CurrentTrack.raceSceneName);
            phase = 1;
        }

        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            try
            {
                Check(Time.realtimeSinceStartupAsDouble - phaseStarted < 90, "Mission-skip phase timed out: " + phase);
                if (VoxelLoadingScreen.IsLoading) return;
                if (phase == 0)
                {
                    saved = new VoxelGarageUpgradeDockValidation.SavedState();
                    scale = Time.timeScale; step = Time.fixedDeltaTime; audio = AudioListener.pause;
                    background = Application.runInBackground; captured = true; Application.runInBackground = true;
                    sourceBefore = EditorJsonUtility.ToJson(VoxelTrackSequence.Load());
                    sequence = Object.Instantiate(VoxelTrackSequence.Load());
                    typeof(VoxelTrackProgressState).GetField("sequence", Static).SetValue(null, sequence);
                    StartCase();
                }
                else if (phase == 1)
                {
                    var pause = VoxelPauseMenu.Active;
                    Check(pause != null && VoxelRacerBootstrap.ActiveTrack == sequence.tracks[skippedIndex], "Wrong race loaded.");
                    var car = (VoxelCarController)typeof(VoxelPauseMenu).GetField("target", Private).GetValue(pause);
                    Check(car != null && !car.IsDestroyed, "No live player to skip with.");
                    Call(pause, "SkipMission");
                    Check(!VoxelLoadingScreen.IsLoading, "Skip accepted outside pause.");
                    var pieces = car.GetComponentsInChildren<MeshRenderer>().Where(r =>
                        r.GetComponentInParent<VoxelIndestructiblePart>() == null &&
                        r.GetComponentInParent<VoxelWheelIntegrity>() == null && r.GetComponent<VoxelArmorVoxel>() == null).Take(5).ToArray();
                    Check(pieces.Length == 5, "Missing destructible test voxels.");
                    foreach (var piece in pieces) piece.gameObject.SetActive(false);
                    pause.SetPaused(true);
                    Check(VoxelPauseMenu.IsPaused && Time.timeScale == 0 && AudioListener.pause, "Pause did not freeze the race.");
                    Check(!pause.GetComponentsInChildren<Button>().Any(b => b.name == "Skip Mission Button"), "Skip leaked onto the main page.");
                    pause.GetComponentsInChildren<Button>().Single(b => b.name == "DEV Button").onClick.Invoke();
                    Check(pause.GetComponentsInChildren<Button>().Any(b => b.name == "DEV Back Button") &&
                        !pause.GetComponentsInChildren<Toggle>().Any(), "DEV page did not replace the settings page.");
                    if (testCase == 0) CheckDeveloperActions(pause, car, pieces);
                    pause.GetComponentsInChildren<Button>().Single(b => b.name == "DEV Back Button").onClick.Invoke();
                    Check(VoxelPauseMenu.IsPaused && Time.timeScale == 0 && AudioListener.pause &&
                        pause.GetComponentsInChildren<Button>().Any(b => b.name == "Resume Button"), "Back resumed gameplay or lost settings.");
                    pause.GetComponentsInChildren<Button>().Single(b => b.name == "DEV Button").onClick.Invoke();
                    var button = pause.GetComponentsInChildren<Button>().Single(b => b.name == "Skip Mission Button");
                    var mission = VoxelMissionProgress.Active;
                    foreach (string property in new[] { "IsComplete", "IsFailed" })
                    {
                        typeof(VoxelMissionProgress).GetProperty(property).SetValue(mission, true);
                        button.onClick.Invoke();
                        Check(!VoxelLoadingScreen.IsLoading && VoxelPauseMenu.IsPaused, "Skip accepted after " + property);
                        typeof(VoxelMissionProgress).GetProperty(property).SetValue(mission, false);
                    }
                    button.onClick.Invoke();
                    Check(VoxelLoadingScreen.IsLoading && !VoxelPauseMenu.IsPaused, "Skip did not start the garage transition.");
                    Check(Mathf.Approximately(Time.timeScale, .65f) && AudioListener.pause == (testCase == 1), "Skip lost the previous game/audio clock.");
                    Check(!mission.IsComplete && mission.TotalCurrencyEarned == 0 && VoxelCurrencyState.Balance == wallet, "Skip awarded completion rewards.");
                    Check(VoxelCarRunState.MissingVoxelCount == 5, "Skip failed to capture existing damage.");
                    button.onClick.Invoke();
                    Check(VoxelTrackProgressState.CurrentTrackIndex == skippedIndex, "Skip advanced before the garage or accepted a duplicate.");
                    phaseStarted = Time.realtimeSinceStartupAsDouble; phase = 2;
                }
                else if (phase == 2)
                {
                    Check(SceneManager.GetActiveScene().name == "RepairUpgrade", "Skip did not open the garage.");
                    var garage = Object.FindFirstObjectByType<VoxelRepairUpgradeSceneController>();
                    Check(garage != null && garage.DisplayedCar != null, "Garage failed to build.");
                    Check(garage.DisplayedCar.RepairableIntegrityVoxels == 5 && VoxelCarRunState.MissingVoxelCount == 5, "Garage lost damaged voxels.");
                    Check(VoxelCurrencyState.Balance == wallet && VoxelGunUpgradeState.IsPurchased(1), "Garage lost cash or installed right gun.");
                    Check(VoxelGunUpgradeState.PurchasedLongGunCount == expectedGuns, "Garage lost DEV gun ownership.");
                    if (testCase == 0) CheckMaxUpgrades(garage.DisplayedCar);
                    Check(VoxelTrackProgressState.CurrentTrackIndex == skippedIndex, "Garage skipped an extra track.");
                    var cost = (int)typeof(VoxelRepairUpgradeSceneController).GetMethod("GetRepairCost", Private).Invoke(garage, new object[] { 100f });
                    Check(cost == garage.repairTuning.GetRepairCost(garage.DisplayedCar, 100,
                        sequence.tracks[skippedIndex].missionTuning.repairCostPerPixel), "Garage used the wrong mission's repair price.");
                    Check(garage.NextRaceButton.GetComponentInChildren<Text>().text.Contains(VoxelTrackDefinition.ResolveDisplayName(sequence.tracks[nextIndex])),
                        "Garage did not preview the correct next mission.");
                    garage.NextRaceButton.onClick.Invoke();
                    garage.NextRaceButton.onClick.Invoke();
                    Check(VoxelLoadingScreen.IsLoading && VoxelTrackProgressState.CurrentTrackIndex == nextIndex,
                        "Garage failed to advance exactly once.");
                    phaseStarted = Time.realtimeSinceStartupAsDouble; phase = 3;
                }
                else if (phase == 3)
                {
                    Check(VoxelRacerBootstrap.ActiveTrack == sequence.tracks[nextIndex] && VoxelTrackProgressState.CurrentTrackIndex == nextIndex,
                        "Next race used the wrong track.");
                    Check(VoxelCurrencyState.Balance == wallet && VoxelGunUpgradeState.IsPurchased(1) && VoxelCarRunState.MissingVoxelCount == 5,
                        "Next race lost run state.");
                    if (testCase == 0)
                    {
                        var car = (VoxelCarController)typeof(VoxelPauseMenu).GetField("target", Private).GetValue(VoxelPauseMenu.Active);
                        CheckMaxUpgrades(car);
                        Check(Object.FindFirstObjectByType<VoxelBoostController>().ChargeCount == 2, "Next race lost dual boost charges.");
                    }
                    testCase++;
                    if (testCase < 3) StartCase();
                    else { Load("MainMenu"); phase = 4; }
                }
                else if (phase == 4)
                {
                    Check(EditorJsonUtility.ToJson(VoxelTrackSequence.Load()) == sourceBefore, "QA edited the authored sequence.");
                    Finish(true, "PASS (Play Mode): DEV/Back navigation while paused; immediate full body/tyre/armour repair; free maximum compatible upgrades, basic-to-Star spikes and single-to-dual boost replacement, both armour/gun/missile sides, live performance/boost/missile HUD refresh, repeated actions without duplicates or healing existing damage, persistence through garage and next race; actual DEV Skip Mission -> garage -> next race for ordinary mission, final looping boss and non-looping boss; damage (5 voxels), upgrades, cash and skipped-mission repair price preserved; no completion rewards; next-mission preview; advances exactly once; invalid/repeated-click guards; prior time/audio restored; source sequence unchanged; returned to MainMenu.");
                }
            }
            catch (Exception error) { Finish(false, "FAIL: " + error); }
        }

        private static void CheckDeveloperActions(VoxelPauseMenu pause, VoxelCarController car, MeshRenderer[] pieces)
        {
            var wheel = car.GetComponentsInChildren<Transform>().First(t => t.name == "Voxel Wheel");
            var tyre = wheel.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
            if (tyre != null) tyre.gameObject.SetActive(false);
            var armour = car.GetComponentsInChildren<VoxelArmorVoxel>().First();
            armour.RestoreHealth(0);
            var restore = pause.GetComponentsInChildren<Button>().Single(b => b.name == "Restore Health Button");
            restore.onClick.Invoke(); restore.onClick.Invoke();
            Check(car.IntegrityPercent == 100 && car.RepairableIntegrityVoxels == 0 && VoxelCarRunState.MissingVoxelCount == 0 &&
                !car.GetComponentsInChildren<VoxelArmorVoxel>(true).Any(a => a.NeedsRepair), "DEV repair left body, tyre or armour damage.");
            Check(VoxelCurrencyState.Balance == wallet && VoxelPauseMenu.IsPaused && Time.timeScale == 0, "Repair charged cash or resumed.");
            foreach (var piece in pieces) piece.gameObject.SetActive(false);
            armour.RestoreHealth(0);
            var upgrades = pause.GetComponentsInChildren<Button>().Single(b => b.name == "Give All Upgrades Button");
            upgrades.onClick.Invoke();
            CheckMaxUpgrades(car);
            Check(pieces.All(r => !r.gameObject.activeSelf) && armour.NeedsRepair, "Giving upgrades healed existing damage.");
            var liveGuns = car.GetComponentsInChildren<VoxelGunMount>(true);
            upgrades.onClick.Invoke();
            Check(car.GetComponentsInChildren<VoxelGunMount>(true).SequenceEqual(liveGuns), "Repeated grants rebuilt weapons.");
            Check(VoxelCurrencyState.Balance == wallet && VoxelPauseMenu.IsPaused && Time.timeScale == 0 && AudioListener.pause,
                "Giving upgrades charged cash, resumed, or released audio.");
            var boost = Object.FindFirstObjectByType<VoxelBoostController>();
            Check(boost.Target == car && boost.ChargeCount == 2 && boost.Tuning == VoxelBoostUpgradeState.InstalledTuning, "Live boost was not refreshed.");
            Check(car.EffectiveTopSpeed > car.topSpeed && car.EffectiveAcceleration > car.acceleration && car.EffectiveBrakingForce > car.brakingForce,
                "Live performance bonuses were not applied.");
            var display = Object.FindFirstObjectByType<VoxelMissileButtonDisplay>();
            var mounts = (VoxelGunMount[])typeof(VoxelMissileButtonDisplay).GetField("mounts", Private).GetValue(display);
            Check(mounts.Count(m => m.tuning.projectileKind == VoxelProjectileKind.Missile) == 2, "Live missile HUD retained stale mounts.");
            restore.onClick.Invoke();
            Check(car.IntegrityPercent == 100 && car.RepairableIntegrityVoxels == 0, "Full repair failed with max upgrades.");
            foreach (var piece in pieces) piece.gameObject.SetActive(false);
            expectedGuns = VoxelGunUpgradeState.PurchasedLongGunCount;
        }

        private static void CheckMaxUpgrades(VoxelCarController car)
        {
            Check(VoxelGunUpgradeState.PurchasedLongGunCount == 2 && VoxelArmorUpgradeState.IsFullyPurchased &&
                VoxelMissileUpgradeState.IsPurchased(false) && VoxelMissileUpgradeState.IsPurchased(true) &&
                VoxelPerformanceWheelUpgradeState.IsPurchased && VoxelEngineUpgradeState.IsPurchased && VoxelPloughUpgradeState.IsPurchased,
                "DEV did not grant all compatible upgrade families/slots.");
            Check(VoxelWheelSpikeUpgradeState.InstalledTuning == VoxelWheelSpikeTuning.LoadStar() &&
                VoxelBoostUpgradeState.InstalledTuning == VoxelBoostUpgradeTuning.LoadDualUpgrade(), "DEV did not select highest replacement levels.");
            Check(car.GetComponentsInChildren<VoxelWheelSpikeMount>(true).Count(m => m.tuning == VoxelWheelSpikeTuning.LoadStar()) == 4 &&
                car.GetComponentsInChildren<VoxelGunMount>(true).Count(m => m.tuning.projectileKind == VoxelProjectileKind.Missile) == 2,
                "Maximum spike/missile visuals missing or duplicated.");
        }

        private static void Finish(bool success, string report)
        {
            EditorApplication.update -= Tick; SessionState.SetBool(Pending, false);
            VoxelPauseMenu.ResumeActivePause();
            if (captured)
            {
                saved?.Dispose(); saved = null;
                Time.timeScale = scale; Time.fixedDeltaTime = step; AudioListener.pause = audio;
                Application.runInBackground = background; captured = false;
            }
            if (sequence != null) Object.Destroy(sequence);
            Directory.CreateDirectory("Temp/PauseMenu"); File.WriteAllText("Temp/PauseMenu/MissionSkipValidation.txt", report);
            if (success) Debug.Log(report); else Debug.LogError(report);
            EditorApplication.isPlaying = false;
        }
    }
}
