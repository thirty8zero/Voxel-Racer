using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelExtendedArmorPlayValidation
    {
        private const string Pending = "VoxelRacer.ExtendedArmourPlayQA";
        private static GameObject root;
        private static VoxelGarageUpgradeDockValidation.SavedState saved;
        private static VoxelCarController car;
        private static VoxelCarDefinition definition;
        private static VoxelArmorTuning tuning;
        private static VoxelArmorVoxel partial;
        private static int firstFrame, baseline;
        private static float started;
        private static UnityEngine.Random.State random;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        [InitializeOnLoadMethod]
        private static void Hook() { EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed; }

        [MenuItem("Tools/Voxel Racer/Validate Extended Door Armour in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            SessionState.SetBool(Pending + ".Background", Application.runInBackground);
            SessionState.SetBool(Pending, true); Application.runInBackground = true; EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick; saved?.Dispose(); saved = null;
                UnityEngine.Random.state = random;
                if (root != null) Object.Destroy(root);
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                Application.runInBackground = SessionState.GetBool(Pending + ".Background", false);
                SessionState.SetBool(Pending, false);
            }
        }

        private static VoxelCarController NewCar()
        {
            var go = new GameObject("Temporary Extended Armour Runtime Car"); go.transform.SetParent(root.transform, false);
            Object.Instantiate(definition.visualPrefab, go.transform);
            var controller = go.AddComponent<VoxelCarController>(); controller.enabled = false;
            controller.debrisVoxelsPerDamagedVoxel = 0; controller.ResetIntegrityBaseline(); return controller;
        }

        private static void Setup()
        {
            saved = new VoxelGarageUpgradeDockValidation.SavedState();
            random = UnityEngine.Random.state;
            try
            {
                definition = AssetDatabase.LoadAssetAtPath<VoxelCarDefinition>("Assets/Resources/Cars/SpyCar2PlayerCar.asset");
                tuning = VoxelArmorTuning.LoadExtended(); VoxelArmorUpgradeState.BeginNewRun(); VoxelCurrencyState.Add(10000);
                root = new GameObject("Temporary Extended Armour Runtime QA");
                SceneManager.MoveGameObjectToScene(root, SceneManager.CreateScene("Extended Armour Runtime QA"));
                car = NewCar(); baseline = car.TotalIntegrityVoxels;
                var body = car.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
                body.gameObject.SetActive(false);
                Check(VoxelArmorUpgradeState.TryPurchase(VoxelArmorTuning.Load(), definition, VoxelArmorSide.Left), "base purchase failed");
                VoxelArmorUpgradeState.ApplyTo(car.transform, definition);
                Check(VoxelArmorUpgradeState.TryPurchase(tuning, definition, VoxelArmorSide.Left), "replacement purchase failed");
                VoxelArmorUpgradeState.ApplyTo(car.transform, definition); car.ResetIntegrityBaseline();
                var pieces = car.GetComponentsInChildren<VoxelArmorVoxel>(true);
                Check(pieces.Length == 45 && car.TotalIntegrityVoxels == baseline + 45, "deferred destruction left old panel in integrity/cache");
                partial = pieces.Where(p => p.name.StartsWith("Plate Row")).OrderBy(p => p.transform.position.z).First();
                car.damageVoxelsPerHit = 1;
                car.ApplyDamage(partial.transform.position, Vector3.right);
                Check(partial.RemainingHealth == tuning.voxelHitPoints - 1 && car.MissingIntegrityVoxels == 1, "real damage failed to hit outer added coverage without removing body");
                var missing = pieces.First(p => p != partial); missing.AbsorbDamage(missing.MaximumHealth); missing.gameObject.SetActive(false);
                VoxelCarRunState.Capture(car, definition);
                firstFrame = Time.frameCount; started = Time.realtimeSinceStartup; EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Tick()
        {
            try
            {
                if (Time.frameCount <= firstFrame) { if (Time.realtimeSinceStartup - started > 10) throw new TimeoutException("Waiting for real frame"); return; }
                Check(car.GetComponentsInChildren<VoxelArmorVoxel>(true).Length == 45, "old panel survived replacement frame");
                VoxelArmorUpgradeState.ApplyTo(car.transform, definition); car.ResetIntegrityBaseline();
                Check(partial.NeedsRepair && car.MissingIntegrityVoxels == 2, "reapply healed runtime damage");
                var next = NewCar(); VoxelArmorUpgradeState.ApplyTo(next.transform, definition); next.ResetIntegrityBaseline(); VoxelCarRunState.Apply(next, definition);
                Check(next.GetComponentsInChildren<VoxelArmorVoxel>(true).Length == 45 && next.MissingIntegrityVoxels == 2 &&
                    next.GetComponentsInChildren<VoxelArmorVoxel>().Count(p => p.NeedsRepair) == 1, "next car lost selected variant or partial/missing damage");
                next.RepairPercent(100); Check(next.RepairableIntegrityVoxels == 0, "runtime repair failed");
                Finish("PASS (Play Mode): actual base-to-extended purchase; immediate and next-frame deferred-destruction isolation; real damage at added outer coverage absorbs through armour HP; no body healing; repeat application preserves damage; selected variant, missing body/armour and partial HP survive a rebuilt car; normal full repair. Temporary scene and saved state restored on exit.");
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Finish(string report)
        {
            EditorApplication.update -= Tick; Directory.CreateDirectory(VoxelExtendedArmorValidation.Output);
            File.WriteAllText(VoxelExtendedArmorValidation.Output + "/PlayValidation.txt", report);
            if (report.StartsWith("FAIL")) Debug.LogError(report); else Debug.Log(report);
            EditorApplication.isPlaying = false;
        }
    }
}
