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
    public static class VoxelTimeRewardPlayValidation
    {
        private const string Pending = "VoxelRacer.TimeRewardPlayQA", Output = "Temp/TimeReward";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameObject root;
        private static VoxelMissionProgress mission;
        private static VoxelMissionTuning tuning;
        private static VoxelGarageUpgradeDockValidation.SavedState saved;
        private static UnityEngine.Random.State random;
        private static int phase;
        private static float began, stageAt;
        private static Texture2D[] releasedTextures;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static void SetSize(int width, int height, bool save) => typeof(VoxelKillChainPlayValidation)
            .GetMethod("SetCaptureSize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { width, height, save });

        [InitializeOnLoadMethod]
        private static void Hook() { EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed; }

        [MenuItem("Tools/Voxel Racer/Validate and Capture Time Reward in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            VoxelLiveMultiplierValidation.Run(); VoxelMissionCrateRewardValidation.Run();
            SessionState.SetBool(Pending + ".Background", Application.runInBackground);
            SessionState.SetFloat(Pending + ".Time", Time.timeScale);
            SetSize(1920, 1080, true); SessionState.SetBool(Pending, true);
            Application.runInBackground = true; EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                if (tuning != null) Object.DestroyImmediate(tuning);
                saved?.Dispose(); saved = null; UnityEngine.Random.state = random;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                Application.runInBackground = SessionState.GetBool(Pending + ".Background", false);
                Time.timeScale = SessionState.GetFloat(Pending + ".Time", 1);
                VoxelKillChainPlayValidation.RestoreCaptureSize(); SessionState.SetBool(Pending, false);
            }
        }

        private static void Setup()
        {
            saved = new VoxelGarageUpgradeDockValidation.SavedState(); random = UnityEngine.Random.state;
            try
            {
                typeof(VoxelMissileUpgradeState).GetField("leftPurchased", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
                foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) go.SetActive(false);
                var scene = SceneManager.CreateScene("Temporary Stopwatch Time Reward QA"); SceneManager.SetActiveScene(scene);
                root = new GameObject("Temporary Stopwatch Time Reward QA"); VoxelRacerBootstrap.BuildPrototype(root.transform);
                root.GetComponentInChildren<VoxelMissionBriefing>()?.Close();
                foreach (var fade in root.GetComponentsInChildren<VoxelFadeIn>()) { Call(fade, "RestoreOpaqueMaterials"); fade.enabled = false; }
                foreach (var countdown in Object.FindObjectsByType<VoxelStartCountdown>(FindObjectsSortMode.None)) countdown.enabled = false;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == "Race Opening Fade") t.gameObject.SetActive(false);
                foreach (var spawner in root.GetComponentsInChildren<VoxelObstacleSpawner>()) spawner.enabled = false;
                foreach (var spawner in root.GetComponentsInChildren<VoxelRoadsideTurretSpawner>()) spawner.enabled = false;
                mission = root.GetComponentInChildren<VoxelMissionProgress>(); mission.SetStartCountdown(null);
                tuning = Object.Instantiate(mission.Tuning); tuning.requiredPoints = 100000; tuning.timeLimitSeconds = 120; mission.Configure(tuning);
                float before = mission.RemainingTime;
                Check(mission.AddBonusTime(10) && mission.AddBonusTime(5), "actual time reward rejected");
                Check(Mathf.Abs(mission.RemainingTime - before - 15) < .001f && (int)typeof(VoxelMissionProgress).GetField("timeExtensionAmount", Private).GetValue(mission) == 15,
                    "rapid awards changed timer/grouping");
                Time.timeScale = 0; began = stageAt = Time.realtimeSinceStartup; phase = 0;
                Directory.CreateDirectory(Output); EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Next(int value) { phase = value; stageAt = Time.realtimeSinceStartup; }
        private static void Capture(string name, float age = .18f)
        {
            typeof(VoxelMissionProgress).GetField("timeExtensionStartedAt", Private).SetValue(mission, Time.unscaledTime - age);
            ValidateLayout(); ScreenCapture.CaptureScreenshot(Output + "/" + name + ".png");
        }

        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            try
            {
                Check(Time.realtimeSinceStartup - began < 35, "time reward QA timed out");
                if (Time.realtimeSinceStartup - stageAt < .6f) return;
                switch (phase)
                {
                    case 0:
                        Check(Screen.width == 1920 && Screen.height == 1080, "capture size did not apply");
                        Capture("Stopwatch-1920"); Next(1); break;
                    case 1:
                        for (int i = 0; i < 6; i++) VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                        Capture("Stopwatch-Combo-1920"); Next(2); break;
                    case 2:
                        SetSize(2400, 1080, false); Next(3); break;
                    case 3:
                        Check(Screen.width == 2400, "wide size did not apply"); Capture("Stopwatch-Combo-2400"); Next(4); break;
                    case 4:
                        SetSize(1440, 1080, false); Next(5); break;
                    case 5:
                        Check(Screen.width == 1440, "4:3 size did not apply"); Capture("Stopwatch-Combo-1440"); Next(6); break;
                    case 6:
                        // Urgent timer reserves its larger combined warning/extension pulse.
                        Call(mission, "ResetKillChain"); mission.Configure(tuning); mission.AdvanceBonusClock(118);
                        Check(mission.AddBonusTime(5), "urgent reward rejected"); Capture("Stopwatch-Urgent-1440"); Next(7); break;
                    case 7:
                        typeof(VoxelMissionProgress).GetField("timeExtensionAmount", Private).SetValue(mission, 12345);
                        Capture("Stopwatch-LargeAmount-1440"); Next(8); break;
                    case 8:
                        var style = (GUIStyle)typeof(VoxelMissionProgress).GetField("timeRewardNumberStyle", Private).GetValue(mission);
                        Check(style.CalcSize(new GUIContent("12345")).x <= 121, "large reward amount clips");
                        releasedTextures = new[] { "timeRewardRing", "timeRewardArc", "timeRewardDisc", "timeRewardStrip" }
                            .Select(n => (Texture2D)typeof(VoxelMissionProgress).GetField(n, Private).GetValue(mission)).ToArray();
                        Check(releasedTextures.All(t => t != null), "badge art did not render");
                        mission.Configure(tuning);
                        Check(float.IsNegativeInfinity((float)typeof(VoxelMissionProgress).GetField("timeExtensionStartedAt", Private).GetValue(mission)), "reset retained award");
                        Object.DestroyImmediate(mission); Next(9); break;
                    case 9:
                        // Runtime Destroy releases the textures at the end of the frame.
                        Check(releasedTextures.All(t => t == null), "owned badge textures leaked");
                        Finish("PASS (Play Mode): real +10/+5 time awards retain +15 grouping and timer arithmetic; stopwatch rendered at 1920/2400/1440x1080, solo and with x6 combo; full peak-pulse/lift bounds clear timer, header, multiplier, rotated combo and active touch controls; urgency clearance; large amount fits; Configure hides stale reward; owned procedural textures released. Native runtime IMGUI/canvas captures under Temp/TimeReward. Original Game View, run state, RNG, clock and background restored."); break;
                }
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void ValidateLayout()
        {
            Canvas.ForceUpdateCanvases();
            Rect timer = (Rect)typeof(VoxelMissionProgress).GetMethod("GetCountdownRect", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Rect area = (Rect)Call(mission, "GetTimeRewardArea", timer);
            Check(area.width >= 150 * Screen.height / 1080f, "badge became too small to read");
            Check(area.xMin >= 0 && area.yMin >= 0 && area.xMax <= Screen.width && area.yMax <= Screen.height, "badge outside screen");
            float pulsePad = timer.height * (mission.RemainingTime <= 10 ? .215f : .125f);
            Check(area.yMin - 14 * Screen.height / 1080f > timer.yMax + pulsePad, "badge lift crosses timer pulse");
            Rect header = (Rect)Call(mission, "GetMissionHudArea", mission.IsBossEncounter);
            Check(!area.Overlaps(header) && !area.Overlaps((Rect)Call(mission, "GetMultiplierArea", header)), "badge crosses mission/multiplier");
            if (mission.IsKillChainAnnouncementShowing)
                Check(!(bool)Call(mission, "KillChainOverlaps", (Rect)Call(mission, "GetKillChainArea", header), area), "badge crosses combo shake bounds");
            var corners = new Vector3[4];
            foreach (var r in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r.gameObject.activeInHierarchy && (r.name == "Integrity Dial Backplate" || r.name == "Move Left Button" || r.name == "Move Right Button" ||
                    r.name == "Brake Button" || r.name == "Boost Button" || r.name == "Fire Button" || r.name == "Missile Button"))
                {
                    r.GetWorldCorners(corners); var bounds = new Rect(corners[0].x, Screen.height - corners[1].y, corners[2].x - corners[0].x, corners[1].y - corners[0].y);
                    Check(!area.Overlaps(bounds), "badge overlaps " + r.name);
                }
        }

        private static void Finish(string report)
        {
            EditorApplication.update -= Tick; Directory.CreateDirectory(Output); File.WriteAllText(Output + "/PlayValidation.txt", report);
            if (report.StartsWith("FAIL")) Debug.LogError(report); else Debug.Log(report); EditorApplication.isPlaying = false;
        }
    }
}
