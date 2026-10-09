using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Checks the real ram paths and player damage using isolated Play Mode objects.</summary>
    public static class VoxelBoostRamValidation
    {
        private const string Pending = "VoxelRacer.BoostRamValidation";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Equal(float actual, float expected, string label) =>
            Check(Mathf.Abs(actual - expected) < .001f, label + ": " + actual + " != " + expected);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
        private static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }
        [MenuItem("Tools/Voxel Racer/Validate Boost Ram Damage in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            foreach (var asset in new VoxelBoostTuning[] { VoxelBoostTuning.Load(), VoxelBoostUpgradeTuning.LoadUpgrade() })
            {
                Check(asset != null, "Missing boost tuning");
                Equal(asset.boostRamDamageBonusPercent, 10, "Default no-plough percentage");
                Equal(asset.boostRamDamageWithPloughBonusPercent, 15, "Default fitted-plough percentage");
                string before = EditorJsonUtility.ToJson(asset);
                using (var tree = PropertyTree.Create(asset))
                {
                    tree.UpdateTree();
                    foreach (string field in new[] { "boostRamDamageBonusPercent", "boostRamDamageWithPloughBonusPercent" })
                    {
                        var property = tree.EnumerateTree(true).Single(p => p.Name == field);
                        Check(property.Attributes.OfType<FoldoutGroupAttribute>().Count(a => a.GroupName == "Boost Ram Damage") == 1,
                            "Missing or duplicated ram-damage foldout: " + asset.name + "/" + field);
                        Check(property.Attributes.OfType<LabelTextAttribute>().Any(a => a.Text.Contains("%")), "Missing percentage units");
                    }
                }
                Check(before == EditorJsonUtility.ToJson(asset), "Inspector changed authored tuning");
            }
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Validate;
            if (state == PlayModeStateChange.ExitingPlayMode) SessionState.SetBool(Pending, false);
        }
        private static void Validate()
        {
            var originalScene = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("Temporary Boost Ram QA"); SceneManager.SetActiveScene(scene);
            var random = UnityEngine.Random.state;
            bool savedPlough = VoxelPloughUpgradeState.IsPurchased, savedSpikes = VoxelWheelSpikeUpgradeState.IsPurchased;
            var ploughOwned = typeof(VoxelPloughUpgradeState).GetField("purchased", Static);
            var spikesOwned = typeof(VoxelWheelSpikeUpgradeState).GetField("purchased", Static);
            var spikeSelection = typeof(VoxelWheelSpikeUpgradeState).GetField("installedTuning", Static);
            var savedSpikeSelection = spikeSelection.GetValue(null);
            spikeSelection.SetValue(null,null); // This matrix validates the original spike bonus.
            Type[] globalTypes = { typeof(VoxelMissionProgress), typeof(VoxelStartCountdown), typeof(VoxelPauseMenu) };
            var globalFields = globalTypes.Select(t => t.GetField("<Active>k__BackingField", Static)).ToArray();
            var globals = globalFields.Select(f => f.GetValue(null)).ToArray();
            foreach (var field in globalFields) field.SetValue(null, null);
            var plough = VoxelPloughTuning.Load();
            var vehicle = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>();
            vehicle.vehicleHealth = 1000; vehicle.playerRamDamage = 20;
            vehicle.playerDamageVoxelsMin = vehicle.playerDamageVoxelsMax = 4;
            var traffic = ScriptableObject.CreateInstance<VoxelObstacleCarTuning>();
            traffic.obstacleDamageVoxelsMin = traffic.obstacleDamageVoxelsMax = 20;
            traffic.playerDamageVoxelsMin = traffic.playerDamageVoxelsMax = 4;
            var boss = ScriptableObject.CreateInstance<VoxelBossDefinition>(); boss.maximumVoxelsRemovedPerRam = 7;
            var prototype = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = prototype.GetComponent<MeshFilter>().sharedMesh;
            var material = prototype.GetComponent<MeshRenderer>().sharedMaterial;
            Object.DestroyImmediate(prototype);
            VoxelBoostTuning tuning = null;
            string report;
            int checks = 0;
            try
            {
                foreach (var source in new VoxelBoostTuning[] { VoxelBoostTuning.Load(), VoxelBoostUpgradeTuning.LoadUpgrade() })
                {
                    tuning = Object.Instantiate(source);
                    tuning.boostLength = 30; tuning.exhaustEffectPrefab = null;
                    float p = 1f + plough.impactDamageBonusPercent / 100f;
                    int protectedPlayer = Mathf.FloorToInt(4f * (1f - plough.playerDamageReductionPercent / 100f));
                    Collision(false, false, 20, 20, 4);
                    Collision(true, false, 22, 22, 4);
                    Collision(false, true, 20 * p, Mathf.RoundToInt(20 * p), protectedPlayer);
                    Collision(true, true, 23 * p, Mathf.RoundToInt(23 * p), protectedPlayer);
                    Collision(true, true, 23, 23, 4, Vector3.right);
                    Collision(true, true, 23, 23, 4, Vector3.back);
                    Collision(true, true, 22, 22, 4, inactivePlough: true);
                    Collision(true, true, 22, 22, 4, owned: false);
                    Collision(true, false, 20, 20, 4, disableBoost: true);
                    Collision(true, false, 20, 20, 4, expire: true);
                    Collision(true, true, 23 * p, 7, protectedPlayer, isBoss: true);
                    float spikeMultiplier = 1f + VoxelWheelSpikeTuning.Load().sideRamDamageBonusPercent / 100f;
                    Collision(true, true, 23 * spikeMultiplier, 23, 4, Vector3.right, spikes: true);
                    Collision(true, false, 0, 22, 4, enemyTraffic: true);
                    Collision(true, true, 0, Mathf.RoundToInt(23 * p), protectedPlayer, enemyTraffic: true);
                    Collision(true, false, 0, 20, 4, civilian: true);
                    Collision(true, true, 0, Mathf.RoundToInt(20 * p), protectedPlayer, civilian: true);
                    tuning.boostRamDamageBonusPercent = 0; tuning.boostRamDamageWithPloughBonusPercent = 0;
                    Collision(true, false, 20, 20, 4);
                    Collision(true, true, 20 * p, Mathf.RoundToInt(20 * p), protectedPlayer);
                    tuning.boostRamDamageBonusPercent = 50; tuning.boostRamDamageWithPloughBonusPercent = 75;
                    Collision(true, false, 30, 30, 4);
                    Collision(true, true, 35 * p, Mathf.RoundToInt(35 * p), protectedPlayer);
                    Object.DestroyImmediate(tuning); tuning = null;
                }
                report = "PASS (Play Mode): " + checks + " real enemy/boss/Radar/civilian ram cases across default and upgraded boost; active 10%/15% health and voxel bonuses, front-plough and side-spike stacking, inactive/unowned plough fallback, disabled/expired boost and residual speed, configurable zero/custom percentages, boss voxel cap, unchanged player damage and civilian/one-hit traffic behavior. Both assets expose percentage fields once in Boost Ram Damage; authored tuning, ownership, gameplay references, random state and user scene restored.";

                void Collision(bool boosting, bool fitted, float expectedHealth, int expectedVoxels, int expectedPlayer,
                    Vector3? direction = null, bool isBoss = false, bool enemyTraffic = false, bool civilian = false,
                    bool disableBoost = false, bool expire = false, bool inactivePlough = false, bool owned = true, bool spikes = false)
                {
                    var root = new GameObject("Temporary ram case");
                    try
                    {
                        ploughOwned.SetValue(null, fitted && owned); spikesOwned.SetValue(null, spikes);
                        var playerObject = new GameObject("Player"); playerObject.transform.SetParent(root.transform, false);
                        Voxels(playerObject.transform);
                        var player = playerObject.AddComponent<VoxelCarController>(); player.enabled = false;
                        player.debrisVoxelsPerDamagedVoxel = 0; player.ResetIntegrityBaseline();
                        if (fitted)
                        {
                            var mount = new GameObject(VoxelPloughUpgradeState.InstanceName); mount.transform.SetParent(player.transform, false);
                            mount.SetActive(!inactivePlough);
                        }
                        var environment = new GameObject("Separate race environment"); environment.transform.SetParent(root.transform, false);
                        var boost = environment.AddComponent<VoxelBoostController>(); boost.Configure(player, tuning);
                        if (boosting) Check(boost.TryActivateBoost(), "Actual boost activation failed");
                        if (disableBoost) boost.enabled = false;
                        if (expire)
                        {
                            Set(boost, "boostEndsAt", Time.time - 1); Call(boost, "Update");
                            Set(player, "<CurrentSpeed>k__BackingField", 100f);
                            Check(!boost.IsBoosting, "Boost did not expire");
                        }
                        var targetObject = new GameObject("Enemy"); targetObject.transform.SetParent(root.transform, false);
                        targetObject.transform.position = Vector3.forward * 4;
                        Voxels(targetObject.transform);
                        int playerBefore = player.RemainingIntegrityVoxels;
                        if (enemyTraffic || civilian)
                        {
                            var enemy = targetObject.AddComponent<VoxelObstacleCar>(); enemy.enabled = false;
                            enemy.tuning = traffic; Set(enemy, "target", player);
                            typeof(VoxelObstacleCar).GetProperty("EnemyTuning").SetValue(enemy, vehicle);
                            typeof(VoxelObstacleCar).GetProperty("IsEnemyTraffic").SetValue(enemy, enemyTraffic);
                            typeof(VoxelObstacleCar).GetProperty("CurrentHealth").SetValue(enemy, 1000f);
                            Call(enemy, "HitCar", (Vector3?)(direction ?? Vector3.forward));
                            Equal(enemy.CurrentHealth, 0, "Traffic must retain one-hit wreck rule");
                        }
                        else
                        {
                            var enemy = targetObject.AddComponent<VoxelEnemyCar>(); enemy.enabled = false;
                            Set(enemy, "target", player); Set(enemy, "trafficTuning", traffic); Set(enemy, "bossSettings", isBoss ? boss : null);
                            typeof(VoxelEnemyCar).GetProperty("Tuning").SetValue(enemy, vehicle);
                            typeof(VoxelEnemyCar).GetProperty("CurrentHealth").SetValue(enemy, 1000f);
                            Call(enemy, "RamByPlayer", (Vector3?)(direction ?? Vector3.forward));
                            Equal(1000f - enemy.CurrentHealth, expectedHealth, "Health damage");
                        }
                        Equal(60 - targetObject.GetComponentsInChildren<MeshRenderer>().Length, expectedVoxels, "Enemy voxel removal");
                        Equal(playerBefore - player.RemainingIntegrityVoxels, expectedPlayer, "Player damage must not change with boost");
                        checks++;
                    }
                    finally { Object.DestroyImmediate(root); }
                }
                void Voxels(Transform parent)
                {
                    for (int i = 0; i < 60; i++)
                    {
                        var voxel = new GameObject("Voxel " + i, typeof(MeshFilter), typeof(MeshRenderer)); voxel.transform.SetParent(parent, false);
                        voxel.transform.localPosition = new Vector3((i % 5 - 2) * .15f, .2f + i / 20 * .15f, (i / 5 % 4 - 2) * .15f);
                        voxel.transform.localScale = Vector3.one * .14f;
                        voxel.GetComponent<MeshFilter>().sharedMesh = mesh; voxel.GetComponent<MeshRenderer>().sharedMaterial = material;
                    }
                }
            }
            catch (Exception e) { report = "FAIL: " + e; }
            finally
            {
                ploughOwned.SetValue(null, savedPlough); spikesOwned.SetValue(null, savedSpikes);
                spikeSelection.SetValue(null,savedSpikeSelection);
                for (int i = 0; i < globalFields.Length; i++) globalFields[i].SetValue(null, globals[i]);
                UnityEngine.Random.state = random;
                if (tuning != null) Object.DestroyImmediate(tuning);
                Object.DestroyImmediate(vehicle); Object.DestroyImmediate(traffic); Object.DestroyImmediate(boss);
                SceneManager.SetActiveScene(originalScene); SceneManager.UnloadSceneAsync(scene);
            }
            Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/BoostRamValidation.txt", report);
            if (report.StartsWith("PASS")) Debug.Log(report); else Debug.LogError(report);
            EditorApplication.isPlaying = false;
        }
    }
}
