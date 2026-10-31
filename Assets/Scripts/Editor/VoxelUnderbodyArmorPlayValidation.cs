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
    public static class VoxelUnderbodyArmorPlayValidation
    {
        private const string Pending = "VoxelRacer.UnderbodyMineQA";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static VoxelGarageUpgradeDockValidation.SavedState saved;
        private static UnityEngine.Random.State random;
        private static GameObject root;
        private static VoxelCarController car;
        private static EndlessVoxelRoad road;
        private static VoxelMineLayerTuning[] mines;
        private static int frame;
        private static float started;

        [InitializeOnLoadMethod]
        private static void Hook() { EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed; }

        [MenuItem("Tools/Voxel Racer/Validate Underbody Mine Armour in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            SessionState.SetBool(Pending + ".Background", Application.runInBackground);
            SessionState.SetFloat(Pending + ".Time", Time.timeScale);
            SessionState.SetBool(Pending, true); Application.runInBackground = true; EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                if (mines != null) foreach (var mine in mines) if (mine != null) Object.DestroyImmediate(mine);
                saved?.Dispose(); saved = null; UnityEngine.Random.state = random;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                Application.runInBackground = SessionState.GetBool(Pending + ".Background", false);
                Time.timeScale = SessionState.GetFloat(Pending + ".Time", 1); SessionState.SetBool(Pending, false);
            }
        }

        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Setup()
        {
            saved = new VoxelGarageUpgradeDockValidation.SavedState(); random = UnityEngine.Random.state;
            try
            {
                var definition = VoxelCarSelectionState.GetSelectedOrDefault(); var tuning = VoxelUnderbodyArmorTuning.Load();
                Check(tuning.Fits(definition), "Choose compatible Spy Car 2 before running the live bootstrap validation.");
                VoxelCarRunState.BeginNewRun(definition); VoxelCurrencyState.Add(700);
                Check(VoxelUnderbodyArmorUpgradeState.TryPurchase(tuning, definition) && VoxelCurrencyState.Balance == 0, "actual purchase failed");
                foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) go.SetActive(false);
                var scene = SceneManager.CreateScene("Temporary Underbody Mine Runtime QA"); SceneManager.SetActiveScene(scene);
                root = new GameObject("Temporary Underbody Mine Runtime QA"); VoxelRacerBootstrap.BuildPrototype(root.transform);
                root.GetComponentInChildren<VoxelMissionBriefing>()?.Close();
                foreach (var spawn in root.GetComponentsInChildren<VoxelObstacleSpawner>()) spawn.enabled = false;
                foreach (var spawn in root.GetComponentsInChildren<VoxelRoadsideTurretSpawner>()) spawn.enabled = false;
                foreach (var boss in root.GetComponentsInChildren<VoxelBossEncounter>()) boss.enabled = false;
                foreach (var countdown in Object.FindObjectsByType<VoxelStartCountdown>(FindObjectsSortMode.None)) countdown.enabled = false;
                car = root.GetComponentInChildren<VoxelCarController>(); car.enabled = false;
                road = root.GetComponentInChildren<EndlessVoxelRoad>();
                Check(car.GetComponentsInChildren<VoxelUnderbodyArmorMount>().Length == 1 && Mathf.Approximately(VoxelUnderbodyArmorUpgradeState.MineDamageMultiplier(car.transform), .7f),
                    "real race bootstrap did not mount/apply purchased kit");
                mines = VoxelUpgradeFitCatalog.Assets<VoxelMineLayerTuning>().Where(m => m.minePrefab != null).Select(Object.Instantiate).ToArray();
                Check(mines.Length >= 3, "ordinary and both boss mine sources must be present");
                foreach (var mine in mines) { mine.playerDamageVoxelsMin = mine.playerDamageVoxelsMax = 10; mine.armingDelay = 0; mine.lifetime = 1000; }
                Time.timeScale = 0; frame = Time.frameCount; started = Time.realtimeSinceStartup; EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Detonate(VoxelMineLayerTuning tuning, int expected)
        {
            VoxelUnderbodyArmorValidation.Ready(car);
            int before = car.RemainingIntegrityVoxels, original = car.damageVoxelsPerHit;
            var go = new GameObject("Actual " + tuning.name + " protection QA"); go.transform.SetParent(root.transform, false);
            var mine = go.AddComponent<VoxelRoadMine>(); mine.enabled = false;
            var point = car.CollisionTrackPosition; mine.Configure(car, road, tuning, point.y, point.x);
            typeof(VoxelRoadMine).GetMethod("Update", Private).Invoke(mine, null);
            Check((bool)typeof(VoxelRoadMine).GetField("detonated", Private).GetValue(mine), "real mine did not detonate: " + tuning.name);
            Check(before - car.RemainingIntegrityVoxels == expected && car.damageVoxelsPerHit == original, "real mine damage/restore failed: " + tuning.name);
            typeof(VoxelRoadMine).GetMethod("Update", Private).Invoke(mine, null);
            Check(before - car.RemainingIntegrityVoxels == expected, "mine hit twice");
        }

        private static void Tick()
        {
            try
            {
                if (Time.frameCount <= frame) { Check(Time.realtimeSinceStartup - started < 10, "waiting for frame timed out"); return; }
                foreach (var mine in mines) Detonate(mine, 7);
                VoxelUnderbodyArmorUpgradeState.BeginNewRun(); Detonate(mines[0], 10);
                Finish("PASS (Play Mode): actual $700 purchase survives real race bootstrap with one mounted kit; all " + mines.Length +
                    " authored ordinary/boss mine prefab variants use real Configure/swept detonation/explosion/damage logic; each controlled 10-point mine causes 7 damage while protected, and 10 without ownership; damage settings restored, no double hit; temporary tuning/scene, purchases, cash, run state, RNG, time and background restored.");
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Finish(string report)
        {
            EditorApplication.update -= Tick; Directory.CreateDirectory(VoxelUnderbodyArmorValidation.Output);
            File.WriteAllText(VoxelUnderbodyArmorValidation.Output + "/PlayValidation.txt", report);
            if (report.StartsWith("FAIL")) Debug.LogError(report); else Debug.Log(report); EditorApplication.isPlaying = false;
        }
    }
}
