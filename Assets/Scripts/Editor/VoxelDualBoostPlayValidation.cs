using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelDualBoostPlayValidation
    {
        private const string Pending = "VoxelRacer.DualBoostPlayValidation";
        private static GameObject root;
        private static VoxelBoostController boost;
        private static VoxelBoostUpgradeTuning tuning;
        private static VoxelPauseMenu pause;
        private static GameObject button;
        private static float started, phaseAt, pausedCharge, originalScale;
        private static bool background, sawIndependentRecharge;
        private static int phase;
        private static UnityEngine.Random.State random;
        private static VoxelGarageUpgradeDockValidation.SavedState state;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Validate Dual Boost in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            // An empty temporary scene avoids live race clocks, traffic and purchases during QA.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetString(Pending + ".scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }

        public static void RunBatch() => Run();

        private static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                background = Application.runInBackground; Application.runInBackground = true;
                originalScale = Time.timeScale; Time.timeScale = 1;
                random = UnityEngine.Random.state; state = new VoxelGarageUpgradeDockValidation.SavedState();
                started = Time.realtimeSinceStartup; phase = 0; sawIndependentRecharge = false;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Pending, false);
                string scene = SessionState.GetString(Pending + ".scene", "");
                if (!string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Pending + ".passed", false) ? 0 : 1);
            }
        }

        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Press() => ExecuteEvents.Execute<IPointerDownHandler>(button,
            new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, (handler, data) => handler.OnPointerDown((PointerEventData)data));

        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
                if (Application.isBatchMode && EditorApplication.isPaused) EditorApplication.isPaused = false;
                if (Time.realtimeSinceStartup - started > 20) throw new TimeoutException("Dual boost play check timed out in phase " + phase);
                if (phase == 0)
                {
                    if (VoxelLoadingScreen.IsLoading) return;
                    root = new GameObject("Temporary Dual Boost Play QA");
                    var definition = Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
                    var model = Object.Instantiate(definition.visualPrefab, root.transform);
                    var car = model.AddComponent<VoxelCarController>(); car.SetDrivingEnabled(false); car.ResetIntegrityBaseline();
                    boost = root.AddComponent<VoxelBoostController>();
                    tuning = Object.Instantiate(VoxelBoostUpgradeTuning.LoadDualUpgrade());
                    tuning.boostLength = .35f; tuning.rechargeCooldownLength = .85f;
                    boost.Configure(car, tuning);
                    root.AddComponent<VoxelBoostDisplay>().Configure(boost);
                    pause = root.AddComponent<VoxelPauseMenu>(); pause.Configure(car);
                    button = root.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Boost Button").gameObject;
                    phase = 1; return;
                }
                if (phase == 1)
                {
                    if (!root.transform.Find("Boost HUD").GetComponent<CanvasGroup>().blocksRaycasts) return;
                    Press(); Check(boost.IsBoosting && boost.ActiveChargeIndex == 0, "Pointer failed to use first bottle");
                    Press(); Check(boost.ActiveChargeIndex == 0 && boost.GetChargePercent(1) == 1, "Repeated input consumed the spare bottle");
                    pause.SetPaused(true); Check(VoxelPauseMenu.IsPaused, "Pause failed to engage");
                    pausedCharge = boost.GetChargePercent(0); phaseAt = Time.realtimeSinceStartup; phase = 2;
                }
                else if (phase == 2 && Time.realtimeSinceStartup - phaseAt > .15f)
                {
                    Press(); Check(!boost.TryActivateBoost() && boost.GetChargePercent(0) == pausedCharge && boost.GetChargePercent(1) == 1, "Pause did not freeze/gate both bottles");
                    pause.SetPaused(false); phase = 3;
                }
                else if (phase == 3 && !boost.IsBoosting)
                {
                    Check(boost.IsReady && boost.GetChargePercent(1) == 1, "First burst consumed or locked spare charge");
                    Press(); Check(boost.IsBoosting && boost.ActiveChargeIndex == 1, "Pointer failed to use second independent charge"); phase = 4;
                }
                else if (phase == 4)
                {
                    if (boost.IsBoosting)
                    {
                        if (boost.GetChargePercent(0) > .05f) sawIndependentRecharge = true;
                        Check(!boost.TryActivateBoost(), "Overlapping boosts accepted");
                    }
                    else
                    {
                        Check(sawIndependentRecharge && !boost.IsReady && !boost.TryActivateBoost(), "Independent recharge or partial-charge gate failed");
                        phase = 5;
                    }
                }
                else if (phase == 5 && boost.GetChargePercent(0) == 1 && boost.GetChargePercent(1) == 1)
                {
                    Press(); Check(boost.IsBoosting, "Refilled bottles unavailable");
                    boost.enabled = false;
                    Check(!boost.IsBoosting && boost.ActiveChargeIndex == -1 && boost.RamDamageMultiplier == 1, "Disable did not release active boost");
                    Finish("PASS: actual pointer input uses each independent bottle; repeated input cannot overlap boosts; pause freezes both charges and blocks activation; first bottle recharges while second is active; partial charges cannot fire; both recharge fully; disabling releases boost. Temporary state restored.");
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
            state?.Dispose(); state = null;
            Time.timeScale = originalScale; Application.runInBackground = background; UnityEngine.Random.state = random;
            string report = Application.isBatchMode ? Path.GetFullPath("../DualBoost/PlayValidation.txt") : "Temp/DualBoost/PlayValidation.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(report)); File.WriteAllText(report, message);
            bool passed = message.StartsWith("PASS"); SessionState.SetBool(Pending + ".passed", passed);
            if (passed) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.isPlaying = false;
        }
    }
}
