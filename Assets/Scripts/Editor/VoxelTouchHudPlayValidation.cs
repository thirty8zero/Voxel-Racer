using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelTouchHudPlayValidation
    {
        private const string Pending = "VoxelRacer.TouchHudCheck";
        private static GameObject root;
        private static VoxelBoostController boost;
        private static VoxelPauseMenu pause;
        private static VoxelBoostTuning tuning;
        private static RectTransform button, cap;
        private static float started, phaseAt, pausedCharge, lastCap, previousScale;
        private static bool previousBackground;
        private static UnityEngine.Random.State previousRandom;
        private static int phase;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }
        [MenuItem("Tools/Voxel Racer/Validate Touch HUD in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            foreach (string key in new[] { VoxelFpsCounter.PreferenceKey, VoxelFpsCounter.WavePreferenceKey })
            {
                SessionState.SetBool(Pending + key + ".Exists", PlayerPrefs.HasKey(key));
                SessionState.SetInt(Pending + key, PlayerPrefs.GetInt(key, 1));
            }
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick; Restore(); SessionState.SetBool(Pending, false);
            }
        }
        private static void Restore()
        {
            foreach (string key in new[] { VoxelFpsCounter.PreferenceKey, VoxelFpsCounter.WavePreferenceKey })
            {
                if (SessionState.GetBool(Pending + key + ".Exists", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(Pending + key, 1));
                else PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private static void Setup()
        {
            try
            {
                previousBackground = Application.runInBackground; Application.runInBackground = true;
                previousRandom = UnityEngine.Random.state;
                previousScale = Time.timeScale; Time.timeScale = 1;
                started = phaseAt = Time.realtimeSinceStartup; phase = 0;
                root = new GameObject("Temporary Touch HUD Play QA");
                var car = root.AddComponent<VoxelCarController>(); car.SetDrivingEnabled(false);
                boost = root.AddComponent<VoxelBoostController>();
                tuning = ScriptableObject.CreateInstance<VoxelBoostTuning>(); tuning.boostLength = .35f; tuning.rechargeCooldownLength = .65f;
                boost.Configure(car, tuning);
                var display = root.AddComponent<VoxelBoostDisplay>(); display.Configure(boost);
                button = root.GetComponentsInChildren<RectTransform>().First(r => r.name == "Boost Button");
                cap = root.GetComponentsInChildren<RectTransform>().First(r => r.name == "Boost Cap");
                pause = root.AddComponent<VoxelPauseMenu>(); pause.Configure(car);
                root.AddComponent<VoxelObstacleSpawner>(); // A race-owned row without creating traffic content.
                VoxelFpsCounter.EnsureExists(); VoxelFpsCounter.SetVisible(true); VoxelFpsCounter.SetWaveDebugVisible(true);
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        private static void Press()
        {
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            foreach (var handler in button.GetComponents<MonoBehaviour>().OfType<IPointerDownHandler>()) handler.OnPointerDown(data);
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.isPaused) return;
                Check(Time.realtimeSinceStartup - started < 12, "Touch HUD check timed out");
                if (phase == 0 && Time.realtimeSinceStartup - phaseAt > .15f)
                {
                    Press(); Check(boost.IsBoosting && cap.anchoredPosition.y == -8, "Pointer-down did not immediately depress/activate boost");
                    phaseAt = Time.realtimeSinceStartup; phase = 1;
                }
                else if (phase == 1 && Time.realtimeSinceStartup - phaseAt > .08f)
                {
                    Check(boost.IsBoosting && cap.anchoredPosition.y == -8, "Active boost incorrectly raised the cap");
                    pause.SetPaused(true); Check(VoxelPauseMenu.IsPaused, "Pause failed"); pausedCharge = boost.ChargePercent;
                    var waveToggle = root.GetComponentsInChildren<Toggle>().First(t => t.name == "Wave Debug Toggle");
                    waveToggle.isOn = false; Check(!VoxelFpsCounter.ShowWaveDebug, "Pause-menu wave preference did not turn off");
                    waveToggle.isOn = true; Check(VoxelFpsCounter.ShowWaveDebug, "Pause-menu wave preference did not turn on");
                    VoxelFpsCounter.SetVisible(false);
                    var texts = VoxelFpsCounter.Active.GetComponentsInChildren<Text>();
                    Check(!texts.First(t => t.name == "FPS").enabled && texts.First(t => t.name == "Wave Debug").enabled,
                        "Wave row must stay visible independently when FPS is off");
                    VoxelFpsCounter.SetVisible(true); waveToggle.isOn = false;
                    Check(texts.First(t => t.name == "FPS").enabled && !texts.First(t => t.name == "Wave Debug").enabled,
                        "Wave toggle must not hide FPS");
                    phaseAt = Time.realtimeSinceStartup; phase = 2;
                }
                else if (phase == 2 && Time.realtimeSinceStartup - phaseAt > .2f)
                {
                    Press(); Check(boost.ChargePercent == pausedCharge && cap.anchoredPosition.y == -8, "Pause did not freeze boost/cap");
                    Check(!button.GetComponentInParent<CanvasGroup>().blocksRaycasts, "Paused boost intercepted touch");
                    pause.SetPaused(false); phase = 3;
                }
                else if (phase == 3 && !boost.IsBoosting)
                { lastCap = cap.anchoredPosition.y; phase = 4; }
                else if (phase == 4)
                {
                    Check(cap.anchoredPosition.y >= lastCap - .001f, "Cap moved down during recharge"); lastCap = cap.anchoredPosition.y;
                    Check(button.anchoredPosition == new Vector2(260, 630) && button.sizeDelta == new Vector2(220, 220), "Animation changed touch area");
                    if (boost.IsReady && Mathf.Abs(cap.anchoredPosition.y - 24) < .001f)
                        Finish("PASS (Play Mode): real pointer-down activates and depresses boost immediately; cap stays down during burst, freezes through pause, then rises monotonically over the live cooldown to ready; fixed touch area; paused input blocked; persistent pause-menu wave toggle and independent FPS/wave visibility. Temporary objects/tuning removed; preferences restored.");
                }
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        private static void Finish(string message)
        {
            EditorApplication.update -= Tick;
            if (pause != null) pause.SetPaused(false);
            if (root != null) Object.Destroy(root);
            if (tuning != null) Object.Destroy(tuning);
            Restore(); Time.timeScale = previousScale; Application.runInBackground = previousBackground;
            UnityEngine.Random.state = previousRandom;
            Directory.CreateDirectory("Temp/TouchHud"); File.WriteAllText("Temp/TouchHud/PlayValidation.txt", message);
            if (message.StartsWith("PASS")) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.isPlaying = false;
        }
    }
}
