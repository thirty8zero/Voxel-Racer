using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelMissionCompletionSlowMotionValidation
    {
        private const string Pending = "VoxelRacer.CompletionSlowMotionValidation";
        private const string Batch = Pending + ".Batch";
        private const string Output = Pending + ".Output";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static VoxelMissionProgress mission;
        private static VoxelMissionTuning tuning;
        private static VoxelPostRaceContinue results;
        private static GameObject root;
        private static Scene scene;
        private static AsyncOperation unloading;
        private static int phase, savedCash;
        private static float savedScale, savedStep, bankedTime;
        private static bool savedBackground, capturedState;
        private static double startedAt;
        private static UnityEngine.Random.State savedRandom;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Validate Mission Completion Slow Motion in Play Mode")]
        public static void Run()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Start in Edit Mode.");
            Check(SceneManager.GetActiveScene().name == "MainMenu", "Open MainMenu before running this isolated Play Mode check.");
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            var args = Environment.GetCommandLineArgs();
            int output = Array.IndexOf(args, "-voxelValidationOutput");
            if (output >= 0 && output + 1 < args.Length) SessionState.SetString(Output, args[output + 1]);
            SessionState.SetBool(Batch, true);
            Run();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (SessionState.GetBool(Pending, false)) Debug.Log("Completion slow-motion validation: " + state);
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Batch, false))
            {
                SessionState.SetBool(Batch, false);
                EditorApplication.Exit(SessionState.GetBool(Pending + ".Success", false) ? 0 : 1);
                return;
            }
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                phase = 0; capturedState = false;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
                Finish(false, "Validation interrupted before completion.");
        }

        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
        private static void Equal(float actual, float expected, string message)
        { Check(Mathf.Abs(actual - expected) < .000001f, message + ": " + actual + " != " + expected); }
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);

        private static void Tick()
        {
            if (EditorApplication.isPaused && SessionState.GetBool(Batch, false)) EditorApplication.isPaused = false;
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            try
            {
                if (phase == 0)
                {
                    Check(VoxelMissionProgress.Active == null && !VoxelPauseMenu.IsPaused && !VoxelPlayerDeathScreen.IsShowing,
                        "Use an idle main menu, without a race/death/pause sequence.");
                    savedScale = Time.timeScale; savedStep = Time.fixedDeltaTime;
                    savedCash = VoxelCurrencyState.Balance; savedBackground = Application.runInBackground;
                    savedRandom = UnityEngine.Random.state; capturedState = true;
                    Application.runInBackground = true;
                    Time.timeScale = .8f; Time.fixedDeltaTime = .03f;
                    scene = SceneManager.CreateScene("Temporary completion slow motion QA");
                    root = new GameObject("Temporary completion slow motion QA");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    mission = root.AddComponent<VoxelMissionProgress>();
                    tuning = ScriptableObject.CreateInstance<VoxelMissionTuning>();
                    tuning.requiredPoints = 2; tuning.enemyVoxelDamagePoints = 1;
                    tuning.completionCurrencyAward = 5;
                    mission.Configure(tuning);
                    var player = root.AddComponent<VoxelCarController>(); player.enabled = false;
                    var finish = root.AddComponent<VoxelRunFinish>(); finish.enabled = false;
                    finish.Configure(player, mission);
                    typeof(VoxelRunFinish).GetProperty("HasFinished").SetValue(finish, true);
                    results = root.AddComponent<VoxelPostRaceContinue>(); results.enabled = false;
                    results.Configure(finish, mission);
                    VoxelMissionProgress.ReportEnemyVoxelDamage();
                    Check(!mission.IsComplete && !mission.IsCompletionSlowMotionActive, "Partial damage triggered slow motion.");
                    Equal(Time.timeScale, .8f, "Partial damage clock");
                    VoxelMissionProgress.ReportEnemyVoxelDamage();
                    startedAt = Time.realtimeSinceStartupAsDouble;
                    Check(mission.IsComplete && mission.IsCompletionSlowMotionActive, "Final damage did not trigger immediately.");
                    Equal(Time.timeScale, .2f, "Quarter-speed clock"); Equal(Time.fixedDeltaTime, .0075f, "Physics cadence");
                    bankedTime = mission.RemainingTime;
                    Check(VoxelCurrencyState.Balance == savedCash + mission.TotalCurrencyEarned, "Completion reward missing.");
                    Invoke(results, "Update");
                    Check(root.transform.Find("Mission Reward UI") == null, "Results appeared during slow motion.");
                    phase = 1;
                }
                else if (phase == 1)
                {
                    double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                    if (elapsed < 1.48)
                    {
                        Check(mission.IsCompletionSlowMotionActive, "Slow motion ended before 1.5 real seconds.");
                        Equal(Time.timeScale, .2f, "Slow motion did not hold quarter speed");
                        return;
                    }
                    if (elapsed < 1.5) return;
                    Invoke(mission, "TickCompletionSlowMotion");
                    Check(!mission.IsCompletionSlowMotionActive, "Slow motion did not finish after 1.5 real seconds.");
                    Equal(Time.timeScale, .8f, "Prior speed restoration"); Equal(Time.fixedDeltaTime, .03f, "Prior physics step restoration");
                    Equal(mission.RemainingTime, bankedTime, "Completion changed banked timer");
                    int balance = VoxelCurrencyState.Balance;
                    VoxelMissionProgress.ReportEnemyVoxelDamage(10);
                    Check(!mission.IsCompletionSlowMotionActive && VoxelCurrencyState.Balance == balance, "Repeated damage restarted slowdown or paid twice.");
                    Invoke(results, "Update");
                    Check(root.transform.Find("Mission Reward UI") != null, "Results did not resume after slow motion.");
                    CheckInterruptions();
                    mission.Configure(tuning);
                    VoxelMissionProgress.ReportEnemyVoxelDamage(2);
                    unloading = SceneManager.UnloadSceneAsync(scene);
                    phase = 2;
                }
                else if (phase == 2 && unloading.isDone)
                {
                    Equal(Time.timeScale, .8f, "Scene unload leaked slow motion"); Equal(Time.fixedDeltaTime, .03f, "Scene unload leaked physics step");
                    Check(VoxelMissionProgress.Active == null, "Scene unload retained mission ownership.");
                    Finish(true, "PASS (Play Mode): final damage immediately triggers quarter speed for 1.5 real seconds; partial/repeated damage, one reward, frozen banked timer, results gate, prior non-default speed/fixed-step restoration; disable, reconfigure, external clock takeover, boss defeat/failure, zero-speed clock, death handoff and actual scene-unload cleanup. Temporary objects/tuning removed; currency, random state and editor clock/background settings restored.");
                }
            }
            catch (Exception error) { Finish(false, "FAIL: " + error); }
        }

        private static void CheckInterruptions()
        {
            void Complete() { mission.Configure(tuning); VoxelMissionProgress.ReportEnemyVoxelDamage(2); }
            Complete(); mission.enabled = false;
            Equal(Time.timeScale, .8f, "Disable leaked clock"); Equal(Time.fixedDeltaTime, .03f, "Disable leaked fixed step");
            mission.enabled = true;
            Complete(); mission.Configure(tuning);
            Check(!mission.IsCompletionSlowMotionActive, "Reconfigure kept slowdown"); Equal(Time.timeScale, .8f, "Reconfigure leaked clock");
            Complete(); Time.timeScale = .6f; Time.fixedDeltaTime = .012f;
            mission.CancelCompletionSlowMotion();
            Equal(Time.timeScale, .6f, "Cleanup overwrote another clock owner"); Equal(Time.fixedDeltaTime, .012f, "Cleanup overwrote another fixed step");
            Time.timeScale = .8f; Time.fixedDeltaTime = .03f;
            Complete();
            var player = root.GetComponent<VoxelCarController>();
            typeof(VoxelCarController).GetProperty("IsDestroyed").SetValue(player, true);
            var death = root.AddComponent<VoxelPlayerDeathScreen>(); death.Configure(player); death.BeginPlayerDeath(player);
            Check(!mission.IsCompletionSlowMotionActive && VoxelPlayerDeathScreen.IsShowing, "Death did not release completion ownership.");
            Equal(Time.timeScale, .2f, "Death stacked quarter-speed effects"); Equal(Time.fixedDeltaTime, .0075f, "Death stacked physics cadence");
            death.enabled = false;
            Equal(Time.timeScale, .8f, "Death handoff failed to restore prior clock"); Equal(Time.fixedDeltaTime, .03f, "Death handoff failed to restore physics");
            Object.Destroy(death);
            typeof(VoxelCarController).GetProperty("IsDestroyed").SetValue(player, false);
            mission.Configure(tuning); mission.SetBossEncounter(true);
            var bossRoot = new GameObject("Temporary boss"); bossRoot.transform.SetParent(root.transform); bossRoot.SetActive(false);
            var boss = bossRoot.AddComponent<VoxelEnemyCar>();
            typeof(VoxelEnemyCar).GetProperty("CurrentHealth").SetValue(boss, 1f);
            mission.ShowBoss(boss, "QA boss"); mission.CompleteBossEncounter();
            Check(!mission.IsComplete, "Living boss completed mission.");
            typeof(VoxelEnemyCar).GetProperty("CurrentHealth").SetValue(boss, 0f); mission.CompleteBossEncounter();
            Check(mission.IsComplete && mission.IsCompletionSlowMotionActive, "Boss defeat missed slow motion.");
            mission.Configure(tuning); mission.SetBossEncounter(true); mission.FailBossEncounter(); mission.CompleteBossEncounter();
            Check(!mission.IsCompletionSlowMotionActive && mission.IsFailed, "Failure triggered success slow motion.");
            mission.Configure(tuning); Time.timeScale = 0f; VoxelMissionProgress.ReportEnemyVoxelDamage(2);
            Check(!mission.IsCompletionSlowMotionActive && Time.timeScale == 0f, "Completion took a paused clock.");
            mission.Configure(tuning); Time.timeScale = .8f;
        }

        private static void Finish(bool success, string report)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Pending, false);
            SessionState.SetBool(Pending + ".Success", success);
            if (mission != null) mission.CancelCompletionSlowMotion();
            if (root != null) Object.DestroyImmediate(root);
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            if (tuning != null) Object.DestroyImmediate(tuning);
            if (capturedState)
            {
                Time.timeScale = savedScale; Time.fixedDeltaTime = savedStep;
                Application.runInBackground = savedBackground; UnityEngine.Random.state = savedRandom;
                VoxelCurrencyState.Reset(); VoxelCurrencyState.Add(savedCash);
            }
            string path = SessionState.GetString(Output, "Temp/MissionCompletionSlowMotionValidation.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, report);
            if (success) Debug.Log(report); else Debug.LogError(report);
            EditorApplication.isPlaying = false;
        }
    }
}
