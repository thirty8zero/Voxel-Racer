using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Uses the real race bootstrap and screen capture, including IMGUI and canvas HUDs.</summary>
    public static class VoxelKillChainPlayValidation
    {
        private const string Pending = "VoxelRacer.KillChainPlayQA";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static VoxelMissionProgress mission;
        private static VoxelMissionTuning tuning;
        private static VoxelCarController player;
        private static VoxelPauseMenu pause;
        private static GameObject root;
        private static float started, stageStarted, pausedRemaining, initialShake;
        private static int phase;
        private static UnityEngine.Random.State random;
        private static bool savedMissile;
        private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
        private static void Equal(float a, float b, string message) => Check(Mathf.Abs(a - b) < .001f, message + ": " + a);
        private static float ShakeStrength() => (float)Call(mission, "GetKillChainShakeStrength");

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Validate and Capture Kill Chain in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            VoxelKillChainValidation.Run();
            SessionState.SetBool(Pending + ".Background", Application.runInBackground);
            SessionState.SetFloat(Pending + ".Scale", Time.timeScale);
            foreach (string key in new[] { VoxelFpsCounter.PreferenceKey, VoxelFpsCounter.WavePreferenceKey })
            {
                SessionState.SetBool(Pending + key + ".Exists", PlayerPrefs.HasKey(key));
                SessionState.SetInt(Pending + key, PlayerPrefs.GetInt(key, 1));
            }
            SetCaptureSize(1920, 1080, true);
            SessionState.SetBool(Pending + ".Restore", true); SessionState.SetBool(Pending, true);
            Application.runInBackground = true; EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending + ".Restore", false))
            {
                Application.runInBackground = SessionState.GetBool(Pending + ".Background", false);
                Time.timeScale = SessionState.GetFloat(Pending + ".Scale", 1);
                foreach (string key in new[] { VoxelFpsCounter.PreferenceKey, VoxelFpsCounter.WavePreferenceKey })
                {
                    if (SessionState.GetBool(Pending + key + ".Exists", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(Pending + key, 1));
                    else PlayerPrefs.DeleteKey(key);
                }
                PlayerPrefs.Save(); RestoreCaptureSize();
                SessionState.SetBool(Pending + ".Restore", false);
            }
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick; SessionState.SetBool(Pending, false);
                if (pause != null) pause.SetPaused(false);
                if (tuning != null) Object.DestroyImmediate(tuning);
                typeof(VoxelMissileUpgradeState).GetField("leftPurchased", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, savedMissile);
                UnityEngine.Random.state = random;
            }
        }

        private static void Setup()
        {
            try
            {
                random = UnityEngine.Random.state; savedMissile = VoxelMissileUpgradeState.IsPurchased(false);
                typeof(VoxelMissileUpgradeState).GetField("leftPurchased", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
                foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) go.SetActive(false);
                var scene = SceneManager.CreateScene("Temporary Kill Chain Live QA"); SceneManager.SetActiveScene(scene);
                root = new GameObject("Temporary Kill Chain Live QA"); VoxelRacerBootstrap.BuildPrototype(root.transform);
                player = root.GetComponentInChildren<VoxelCarController>();
                foreach (var fade in root.GetComponentsInChildren<VoxelFadeIn>())
                { Call(fade, "RestoreOpaqueMaterials"); fade.enabled = false; }
                foreach (var countdown in Object.FindObjectsByType<VoxelStartCountdown>(FindObjectsSortMode.None)) countdown.enabled = false;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == "Race Opening Fade") t.gameObject.SetActive(false);
                mission = root.GetComponentInChildren<VoxelMissionProgress>(); mission.SetStartCountdown(null);
                tuning = Object.Instantiate(mission.Tuning); tuning.requiredPoints = 100000; tuning.timeLimitSeconds = 120;
                tuning.timeBonusCurrencyMultiplier = 1; tuning.maximumTimeMultiplier = 5; tuning.enemyDestroyedMultiplier = 0;
                tuning.civilianDestroyedMultiplierPenalty = 0; mission.Configure(tuning);
                foreach (var spawner in root.GetComponentsInChildren<VoxelObstacleSpawner>()) spawner.SetStartCountdown(null);
                foreach (var spawner in root.GetComponentsInChildren<VoxelRoadsideTurretSpawner>()) spawner.enabled = false;
                pause = root.GetComponentInChildren<VoxelPauseMenu>();
                Check(player != null && pause != null && mission != null, "Missing runtime race objects");
                VoxelFpsCounter.SetVisible(true); VoxelFpsCounter.SetWaveDebugVisible(true);
                VoxelMissionProgress.ReportEnemyVehicleDestroyed(); VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                Equal(mission.KillChainCount, 2, "Real frame setup collects combo");
                Check(mission.IsKillChainAnnouncementShowing && VoxelKillChainValidation.ComboFlights(mission) == 0,
                    "Combo must appear immediately with reward still pending");
                initialShake = ShakeStrength();
                Time.timeScale = 1; pause.SetPaused(true); Check(VoxelPauseMenu.IsPaused, "Actual pause menu failed");
                pausedRemaining = mission.KillChainSecondsRemaining;
                mission.AdvanceBonusClock(5); Equal(mission.KillChainSecondsRemaining, pausedRemaining, "Explicit clock advance respects pause");
                started = stageStarted = Time.realtimeSinceStartup; phase = 0;
                Directory.CreateDirectory("Temp/KillChain"); EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Next(int value) { phase = value; stageStarted = Time.realtimeSinceStartup; }
        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            try
            {
                Check(Time.realtimeSinceStartup - started < 35, "Live kill chain QA timed out");
                switch (phase)
                {
                    case 0:
                        if (Time.realtimeSinceStartup - stageStarted < .5f) return;
                        Equal(mission.KillChainSecondsRemaining, pausedRemaining, "Paused real frames freeze combo");
                        Equal(ShakeStrength(), initialShake, "Pause freezes shake animation");
                        Equal(mission.KillChainDisplayCount, 2, "Paused combo retains live x2");
                        ValidateLayout(); ScreenCapture.CaptureScreenshot("Temp/KillChain/KillChainHUD-x2.png");
                        pause.SetPaused(false); Next(1); break;
                    case 1:
                        if (mission.KillChainSecondsRemaining > 1.5f) return;
                        Check(mission.IsKillChainAnnouncementShowing, "UI must persist during real countdown");
                        Equal(mission.KillChainDisplayCount, 2, "Countdown keeps current count");
                        Equal(mission.EffectiveTimeBonusMultiplier, 1, "Reward held during collection");
                        Equal(VoxelKillChainValidation.ComboFlights(mission), 0, "No premature multiplier flight");
                        VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                        Equal(mission.KillChainDisplayCount, 3, "New real-frame kill updates count immediately");
                        Equal(mission.KillChainSecondsRemaining, 3, "New kill refreshes countdown");
                        Check(ShakeStrength() > initialShake, "New kill strengthens shake");
                        pause.SetPaused(true); pausedRemaining = mission.KillChainAnnouncementRemaining; Next(2); break;
                    case 2:
                        if (Time.realtimeSinceStartup - stageStarted < .5f) return;
                        Equal(mission.KillChainAnnouncementRemaining, pausedRemaining, "Pause freezes renewed countdown");
                        pause.SetPaused(false); Next(3); break;
                    case 3:
                        if (mission.IsKillChainAnnouncementShowing) return;
                        Equal(mission.EffectiveTimeBonusMultiplier, 1.5f, "Real timeout releases x3 total bonus");
                        Equal(VoxelKillChainValidation.ComboFlights(mission), 1, "Multiplier starts as announcement disappears");
                        VoxelMissionProgress.ReportEnemyVehicleDestroyed(); VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                        VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                        Check(mission.IsKillChainAnnouncementShowing, "New x3 must immediately appear");
                        Equal(mission.KillChainDisplayCount, 3, "Live runtime x3 combo");
                        typeof(VoxelMissionProgress).GetField("killChainFeedbackAge", Private).SetValue(mission, .15f);
                        Time.timeScale = 0; Next(4); break;
                    case 4:
                        if (Time.realtimeSinceStartup - stageStarted < 1f) return;
                        Check(Screen.width == 1920 && Screen.height == 1080, "Game View didn't apply 1920x1080: " + Screen.width + "x" + Screen.height);
                        ValidateLayout(); ScreenCapture.CaptureScreenshot("Temp/KillChain/KillChainHUD.png"); Next(5); break;
                    case 5:
                        if (Time.realtimeSinceStartup - stageStarted < .6f) return;
                        Check(File.Exists("Temp/KillChain/KillChainHUD.png"), "HUD capture missing");
                        for (int i = 0; i < 7; i++) VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                        Equal(mission.KillChainDisplayCount, 10, "Long chain updates to two-digit count");
                        ValidateLayout();
                        SetCaptureSize(2400, 1080, false); Next(6); break;
                    case 6:
                        if (Time.realtimeSinceStartup - stageStarted < 1f) return;
                        Check(Screen.width == 2400 && Screen.height == 1080, "Game View didn't apply 20:9");
                        ValidateLayout(); ScreenCapture.CaptureScreenshot("Temp/KillChain/KillChainHUD-wide.png"); Next(7); break;
                    case 7:
                        if (Time.realtimeSinceStartup - stageStarted < .6f) return;
                        Check(File.Exists("Temp/KillChain/KillChainHUD-wide.png"), "Wide HUD capture missing");
                        mission.AdvanceBonusClock(3); Check(!mission.IsKillChainAnnouncementShowing, "Live combo didn't disappear on expiry");
                        Equal(mission.EffectiveTimeBonusMultiplier, 3.75f, "x10 releases +2.25 total immediately");
                        Equal(VoxelKillChainValidation.ComboFlights(mission), 1, "Exactly one new multiplier flight");
                        ScreenCapture.CaptureScreenshot("Temp/KillChain/KillChainMultiplier.png"); Next(8); break;
                    case 8:
                        if (Time.realtimeSinceStartup - stageStarted < .3f) return;
                        Check(File.Exists("Temp/KillChain/KillChainMultiplier.png"), "Multiplier capture missing");
                        VoxelMissionProgress.ReportEnemyVehicleDestroyed(); VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                        VoxelMissionProgress.ReportCivilianVehicleDestroyed(); mission.AdvanceBonusClock(5);
                        Equal(mission.KillChainCount, 0, "Runtime civilian kill cancels");
                        Equal(mission.EffectiveTimeBonusMultiplier, 3.75f, "Cancelled unfinished chain grants no bonus");
                        Finish("PASS (Play Mode): combo immediately visible at x2; UI persists through real countdown; new kill increments to x3, refreshes timer and renews stronger shake; actual pause freezes countdown and shake; real expiry hides UI and starts one total +0.50x flight; subsequent x10 releases +2.25x only on expiry; civilian death cancels unfinished chains. Rotated combo reserves maximum shake and peak pulse, staying clear of HUD at 1920x1080 and 2400x1080 with single/two-digit counts. ScreenCapture includes real runtime IMGUI and canvas HUDs. Temporary bootstrap scene/tuning removed; preferences, missile ownership, random state, background setting, time scale and original Game View size restored."); break;
                }
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void ValidateLayout()
        {
            Canvas.ForceUpdateCanvases();
            var header = (Rect)Call(mission, "GetMissionHudArea", false);
            var combo = (Rect)Call(mission, "GetKillChainArea", header);
            var multiplier = (Rect)Call(mission, "GetMultiplierArea", header);
            Check(!combo.Overlaps(header) && !combo.Overlaps(multiplier), "Combo overlaps header or multiplier");
            Check(combo.xMin >= 0 && combo.yMin >= 0 && combo.xMax <= Screen.width && combo.yMax <= Screen.height, "Combo outside screen");
            string[] names = { "Integrity Dial Backplate", "Timer Ring Background", "Move Left Button", "Move Right Button", "Brake Button", "Boost Button", "Fire Button", "Missile Button" };
            foreach (string name in names)
            {
                var rect = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(r => r.name == name && r.gameObject.activeInHierarchy);
                Check(rect != null, "Missing HUD element " + name);
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Rect bounds = new Rect(corners[0].x, Screen.height - corners[1].y, corners[2].x - corners[0].x, corners[1].y - corners[0].y);
                bool overlaps = name == "Boost Button"
                    ? (bool)Call(mission, "KillChainOverlapsCircle", combo, bounds.center, bounds.width * .5f)
                    : (bool)Call(mission, "KillChainOverlaps", combo, bounds);
                Check(!overlaps, "Angled combo text overlaps visible " + name);
            }
        }

        private static void Finish(string result)
        {
            EditorApplication.update -= Tick; Directory.CreateDirectory("Temp/KillChain"); File.WriteAllText("Temp/KillChain/PlayValidation.txt", result);
            if (result.StartsWith("FAIL")) Debug.LogError(result); else Debug.Log(result);
            EditorApplication.isPlaying = false;
        }

        // Fixed Game View sizes affect only editor preview state, restored after validation.
        private static void SetCaptureSize(int width, int height, bool saveOriginal)
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
            Type viewType = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType); view.Show();
            PropertyInfo selected = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (saveOriginal)
            {
                SessionState.SetInt(Pending + ".SizeIndex", (int)selected.GetValue(view));
                SessionState.SetInt(Pending + ".CustomCount", (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null));
                SessionState.SetInt(Pending + ".Group", Convert.ToInt32(groupType));
            }
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var enumType = assembly.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { Enum.Parse(enumType, "FixedResolution"), (object)width, height, "Kill Chain QA" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null) +
                (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null) - 1;
            selected.SetValue(view, index); view.Repaint();
        }

        public static void RestoreCaptureSize()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            object groupType = Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeGroupType"), SessionState.GetInt(Pending + ".Group", 0));
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
            Type viewType = assembly.GetType("UnityEditor.GameView"); var view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(view, SessionState.GetInt(Pending + ".SizeIndex", 0));
            int count = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
            int saved = SessionState.GetInt(Pending + ".CustomCount", count);
            int builtins = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
            for (int i = count - 1; i >= saved; i--) group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { builtins + i });
            view.Repaint();
        }
    }
}
