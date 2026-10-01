using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Exercises frame-driven pause, keyboard/UI input and real gameplay clocks.</summary>
    public static class VoxelPauseMenuValidation
    {
        private const string Pending = "VoxelRacer.PauseMenuCheck";
        private static GameObject root;
        private static VoxelPauseMenu pause;
        private static VoxelCarController car;
        private static VoxelStartCountdown countdown;
        private static VoxelMissionProgress mission;
        private static VoxelBoostController boost;
        private static VoxelGunMount gun;
        private static VoxelGunMount readyGun;
        private static VoxelObstacle pausedCrate;
        private static VoxelGunTuning weapon;
        private static VoxelMissionTuning missionTuning;
        private static VoxelBoostTuning boostTuning;
        private static Keyboard keyboard;
        private static bool savedBackground, savedAudio;
        private static float savedScale, started, phaseStarted, gameClock, bonusClock, cooldown, lane;
        private static Vector3 position;
        private static int phase;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Validate Pause Menu in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start pause validation from Edit Mode.");
            SessionState.SetBool(Pending + ".HadPreference", PlayerPrefs.HasKey(VoxelFpsCounter.PreferenceKey));
            SessionState.SetInt(Pending + ".Preference", PlayerPrefs.GetInt(VoxelFpsCounter.PreferenceKey, 1));
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void RestorePreference()
        {
            if (SessionState.GetBool(Pending + ".HadPreference", false))
                PlayerPrefs.SetInt(VoxelFpsCounter.PreferenceKey, SessionState.GetInt(Pending + ".Preference", 1));
            else PlayerPrefs.DeleteKey(VoxelFpsCounter.PreferenceKey);
            PlayerPrefs.Save();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                RestorePreference();
                SessionState.SetBool(Pending, false);
            }
        }

        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        private static void Setup()
        {
            savedBackground = Application.runInBackground;
            savedScale = Time.timeScale;
            savedAudio = AudioListener.pause;
            Application.runInBackground = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            try
            {
                PlayerPrefs.DeleteKey(VoxelFpsCounter.PreferenceKey);
                Check(VoxelFpsCounter.ShowCounter, "FPS must default on without a saved preference.");
                VoxelFpsCounter.EnsureExists();
                VoxelFpsCounter.SetVisible(true);
                if (VoxelPauseMenu.Active != null) VoxelPauseMenu.Active.enabled = false;
                root = new GameObject("Temporary live pause QA");
                root.transform.position = new Vector3(10000f, 20f, 0f);
                car = root.AddComponent<VoxelCarController>();
                countdown = root.AddComponent<VoxelStartCountdown>();
                countdown.blackScreenDuration = 0f;
                countdown.fadeInDuration = .05f;
                countdown.Prepare(car);
                countdown.BeginCountdown();
                missionTuning = ScriptableObject.CreateInstance<VoxelMissionTuning>();
                mission = root.AddComponent<VoxelMissionProgress>();
                mission.Configure(missionTuning);
                mission.SetStartCountdown(countdown);
                boostTuning = ScriptableObject.CreateInstance<VoxelBoostTuning>();
                boost = root.AddComponent<VoxelBoostController>();
                boost.Configure(car, boostTuning);
                weapon = ScriptableObject.CreateInstance<VoxelGunTuning>();
                weapon.ammunitionPerStage = 2;
                weapon.shotsPerSecond = 1f;
                var mount = new GameObject("QA Gun");
                mount.transform.SetParent(root.transform, false);
                mount.SetActive(false);
                gun = mount.AddComponent<VoxelGunMount>();
                gun.tuning = weapon;
                mount.SetActive(true);
                pause = root.AddComponent<VoxelPauseMenu>();
                pause.Configure(car);
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                phase = 0;
                started = phaseStarted = Time.realtimeSinceStartup;
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Snapshot()
        {
            gameClock = Time.time;
            bonusClock = mission.RemainingTime;
            position = car.transform.position;
            lane = car.TargetLaneOffset;
            cooldown = gun.CooldownProgress;
            phaseStarted = Time.realtimeSinceStartup;
        }

        private static void VerifyFrozen()
        {
            Check(VoxelPauseMenu.IsPaused && Time.timeScale == 0f && AudioListener.pause, "Pause lost ownership.");
            Check(Mathf.Approximately(gameClock, Time.time), "Gameplay clock advanced during pause.");
            Check(Mathf.Approximately(bonusClock, mission.RemainingTime), "Mission bonus clock advanced during pause.");
            Check(car.transform.position == position && car.TargetLaneOffset == lane, "Car moved or queued a lane change while paused.");
            Check(Mathf.Approximately(cooldown, gun.CooldownProgress), "Weapon cooldown advanced during pause.");
            Check(!gun.TryBeginShot(out _) && !boost.TryActivateBoost(), "A weapon or boost activated during pause.");
            if (readyGun != null)
                Check(!readyGun.IsReady && !readyGun.TryBeginShot(out _), "A ready weapon accepted a shot during pause.");
        }

        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            try
            {
                Check(Time.realtimeSinceStartup - started < 25f, "Pause check timed out (phase " + phase + ").");
                if (phase == 0 && VoxelPauseMenu.IsPaused)
                {
                    Snapshot();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.D, Key.LeftAlt));
                    phase = 1;
                }
                else if (phase == 1 && Time.realtimeSinceStartup - phaseStarted > 1.2f)
                {
                    car.RequestLaneChangeDirection(1);
                    VerifyFrozen();
                    Check(!countdown.IsComplete && car.CurrentSpeed == 0f, "Start countdown advanced while paused.");
                    Check(VoxelFpsCounter.Active.FramesPerSecond > 0, "FPS counter stopped during pause.");
                    Text fps = VoxelFpsCounter.Active.GetComponentInChildren<Text>();
                    Check(fps.text.StartsWith("FPS: ") && !fps.text.Contains("--"), "FPS display never sampled real frames.");
                    Check(fps.rectTransform.anchorMin == new Vector2(.5f, 0f) && fps.rectTransform.anchorMax == new Vector2(.5f, 0f),
                        "FPS counter is not anchored at the bottom centre.");
                    Canvas fpsCanvas = fps.GetComponentInParent<Canvas>();
                    Check(!fps.raycastTarget && fps.rectTransform.anchoredPosition.y * fpsCanvas.scaleFactor >= Screen.safeArea.yMin,
                        "FPS interferes with input or sits below the safe area.");
                    Toggle toggle = pause.GetComponentInChildren<Toggle>();
                    toggle.isOn = false;
                    Check(!VoxelFpsCounter.ShowCounter && !fpsCanvas.enabled, "FPS checkbox failed to hide and save.");
                    toggle.isOn = true;
                    Check(VoxelFpsCounter.ShowCounter && fpsCanvas.enabled, "FPS checkbox failed to show and save.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    pause.GetComponentsInChildren<Button>().Single(b => b.name == "Resume Button").onClick.Invoke();
                    Check(!VoxelPauseMenu.IsPaused && Time.timeScale == 1f && !AudioListener.pause, "Resume button failed to restore race/audio.");
                    phase = 2;
                }
                else if (phase == 2 && countdown.IsComplete && car.CurrentSpeed > 0f)
                {
                    Check(gun.TryBeginShot(out int count) && count == 1, "Gun did not become usable after countdown/resume.");
                    var readyMount = new GameObject("QA Ready Gun");
                    readyMount.transform.SetParent(root.transform, false);
                    readyMount.SetActive(false);
                    readyGun = readyMount.AddComponent<VoxelGunMount>();
                    readyGun.tuning = weapon;
                    readyMount.SetActive(true);
                    Time.timeScale = .35f;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    phase = 3;
                }
                else if (phase == 3 && VoxelPauseMenu.IsPaused)
                {
                    Snapshot();
                    var hazard = new GameObject("QA overlapping crate");
                    hazard.transform.SetParent(root.transform, false);
                    pausedCrate = hazard.AddComponent<VoxelObstacle>();
                    pausedCrate.Configure(car, null, null, car.TrackDistance, car.CurrentLaneOffset);
                    Directory.CreateDirectory("Temp");
                    ScreenCapture.CaptureScreenshot("Temp/PauseMenu.png");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.D, Key.LeftAlt));
                    phase = 4;
                }
                else if (phase == 4 && Time.realtimeSinceStartup - phaseStarted > 1f)
                {
                    car.RequestLaneChangeDirection(1);
                    VerifyFrozen();
                    Check(pausedCrate != null && !car.IsDestroyed &&
                        !(bool)typeof(VoxelObstacle).GetField("hasBeenHit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .GetValue(pausedCrate), "An overlapping hazard collided with the paused player.");
                    pausedCrate.gameObject.SetActive(false);
                    Check(!boost.IsBoosting, "Alt key activated boost while paused.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    phase = 5;
                }
                else if (phase == 5 && !VoxelPauseMenu.IsPaused)
                {
                    Check(Mathf.Approximately(Time.timeScale, .35f) && !AudioListener.pause, "Escape resume failed to restore the previous speed/audio.");
                    Check(car.TargetLaneOffset == lane, "Paused keyboard lane input leaked into resume.");
                    Check(readyGun.TryBeginShot(out _), "Held Ctrl during pause consumed the ready gun's ammunition/cooldown.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Snapshot();
                    phase = 6;
                }
                else if (phase == 6 && Time.realtimeSinceStartup - phaseStarted > .5f)
                {
                    Check(Time.time > gameClock && mission.RemainingTime < bonusClock && car.transform.position != position,
                        "Gameplay did not continue after resume.");
                    pause.SetPaused(true);
                    pause.enabled = false;
                    Check(!VoxelPauseMenu.IsPaused && Mathf.Approximately(Time.timeScale, .35f) && !AudioListener.pause,
                        "Disabling/unloading a paused menu left game or audio frozen.");
                    pause.enabled = true;
                    mission.SetBossEncounter(true);
                    mission.FailBossEncounter();
                    pause.SetPaused(true);
                    Check(!VoxelPauseMenu.IsPaused, "Pause opened over mission failure.");
                    Finish("PASS (Play Mode): Escape pause/resume and Resume button; default-on FPS, persistent checkbox and bottom-centre safe-area placement; FPS samples while paused; opening countdown, driving, mission time and weapon cooldown freeze; keyboard/touch lane and firing/boost requests blocked; overlapping crate cannot collide/destroy paused player; previous non-default time scale/audio restored; gameplay resumes; disabled-menu cleanup; mission-failure gate. Preview: Temp/PauseMenu.png.");
                }
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Finish(string message)
        {
            EditorApplication.update -= Tick;
            if (pause != null) pause.SetPaused(false);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (root != null) Object.Destroy(root);
            if (weapon != null) Object.Destroy(weapon);
            if (missionTuning != null) Object.Destroy(missionTuning);
            if (boostTuning != null) Object.Destroy(boostTuning);
            RestorePreference();
            Time.timeScale = savedScale;
            AudioListener.pause = savedAudio;
            Application.runInBackground = savedBackground;
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/PauseMenuValidation.txt", message);
            Debug.Log(message);
            EditorApplication.isPlaying = false;
        }
    }
}
