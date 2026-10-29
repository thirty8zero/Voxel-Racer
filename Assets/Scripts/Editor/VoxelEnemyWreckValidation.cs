using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Exercises the real vehicle death handlers against instantiated authored models.</summary>
    public static class VoxelEnemyWreckValidation
    {
        private const string Pending = "VoxelRacer.EnemyWreckValidation";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
        private static void Property(object owner, string name, object value) => owner.GetType().GetProperty(name).SetValue(owner, value);
        private static void Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Validate Enemy Wreck Voxel Counts in Play Mode")]
        public static void Run()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Start in Edit Mode.");
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
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
            var scene = SceneManager.CreateScene("Temporary enemy wreck QA");
            SceneManager.SetActiveScene(scene);
            var random = UnityEngine.Random.state;
            var globalFields = new[] { typeof(VoxelMissionProgress), typeof(VoxelStartCountdown), typeof(VoxelPauseMenu), typeof(VoxelLoadingScreen) }
                .Select(t => t.GetField("<Active>k__BackingField", Static)).ToArray();
            var globals = globalFields.Select(f => f.GetValue(null)).ToArray();
            var snapshots = new Dictionary<Object, string>();
            using var saved = new VoxelGarageUpgradeDockValidation.SavedState();
            var report = new List<string>();
            try
            {
                foreach (var field in globalFields) field.SetValue(null, null);
                var sources = Resources.LoadAll<VoxelEnemyVehicleTuning>("EnemyVehicles").Where(t => t.modelPrefab != null).ToArray();
                foreach (var source in sources) snapshots[source] = EditorJsonUtility.ToJson(source);
                var track = Resources.Load<VoxelTrackDefinition>("Tracks/Track01");
                snapshots[track.obstacleCarTuning] = EditorJsonUtility.ToJson(track.obstacleCarTuning);
                var bossSource = Resources.Load<VoxelBossDefinition>("Bosses/RedVanBoss");
                snapshots[bossSource] = EditorJsonUtility.ToJson(bossSource);

                foreach (string name in new[] { "BlackInterceptorTuning", "BI_MineLayerTuning", "PsychoBugEnemyTuning", "RadarInterceptorTuning" })
                foreach (string mode in new[] { "Basic", "Thump", "Hostile", "HealthOnly", "MissileDirect", "MissileSplash", "RearRam", "LeftRam", "RightRam", "BoostRam", "PloughRam", "SpikeRam", "Mixed", "Completion", "AlreadyStripped" })
                {
                    if ((mode == "Completion" || mode == "AlreadyStripped") && name == "RadarInterceptorTuning") continue;
                    RunCase(sources.Single(t => t.name == name), mode, track.obstacleCarTuning, null, false);
                }
                RunCase(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning"), "Boss", track.obstacleCarTuning, bossSource, false);
                foreach (var source in track.obstacleCarTuning.CivilianVehiclePool)
                    RunCase(source, "CivilianWeapon", track.obstacleCarTuning, null, true);
                RunCase(track.obstacleCarTuning.CivilianVehiclePool[0], "CivilianRam", track.obstacleCarTuning, null, true);
                foreach (var pair in snapshots) Check(pair.Value == EditorJsonUtility.ToJson(pair.Key), "Authored tuning changed: " + pair.Key.name);
                report.Insert(0, "PASS (Play Mode controlled simulation): " + report.Count + " authored-model cases. Destructible-only original counts; all ordinary enemy and Radar death paths; protected structures; body-first removal; capped physical debris; unchanged damage/rewards and duplicate guards; no live-damage floor or restored voxels; boss/civilian exclusions; authored tuning and gameplay state preserved.");
            }
            catch (Exception error) { report.Insert(0, "FAIL: " + error); Debug.LogException(error); }
            finally
            {
                for (int i = 0; i < globalFields.Length; i++) globalFields[i].SetValue(null, globals[i]);
                UnityEngine.Random.state = random;
                SceneManager.SetActiveScene(originalScene);
                SceneManager.UnloadSceneAsync(scene);
                Directory.CreateDirectory("Temp/EnemyWrecks");
                File.WriteAllLines("Temp/EnemyWrecks/Validation.txt", report);
                Debug.Log(report[0]);
                SessionState.SetBool(Pending, false);
                EditorApplication.isPlaying = false;
            }

            void RunCase(VoxelEnemyVehicleTuning source, string mode, VoxelObstacleCarTuning trafficSource,
                VoxelBossDefinition bossSource, bool civilian)
            {
                var existingRoots = scene.GetRootGameObjects().ToHashSet();
                var assets = new List<Object>();
                string label = source.displayName + "/" + mode;
                try
                {
                    VoxelPloughUpgradeState.BeginNewRun(); VoxelWheelSpikeUpgradeState.BeginNewRun();
                    var tuning = Object.Instantiate(source); assets.Add(tuning);
                    var traffic = Object.Instantiate(trafficSource); assets.Add(traffic);
                    // Exercise an intentionally small debris budget; it must not limit voxel cleanup.
                    tuning.explosionVoxelCount = 8;
                    var root = new GameObject("Wreck case: " + label);
                    var playerRoot = new GameObject("QA player"); playerRoot.transform.SetParent(root.transform, false);
                    var player = playerRoot.AddComponent<VoxelCarController>(); player.enabled = false;
                    Set(player, "nextDamageTime", float.PositiveInfinity);
                    var missionTuning = ScriptableObject.CreateInstance<VoxelMissionTuning>(); assets.Add(missionTuning);
                    missionTuning.requiredPoints = int.MaxValue; missionTuning.timeLimitSeconds = 10000;
                    missionTuning.enemyVoxelDamagePoints = 0; missionTuning.enemyVoxelMultiplier = 0;
                    missionTuning.enemyDestroyedMultiplier = 0;
                    var mission = root.AddComponent<VoxelMissionProgress>(); mission.enabled = false;
                    mission.Configure(missionTuning);
                    globalFields[0].SetValue(null, mission);
                    bool radar = source.name == "RadarInterceptorTuning";
                    bool trafficController = radar || civilian;
                    var vehicle = new GameObject("Vehicle"); vehicle.transform.SetParent(root.transform, false);
                    vehicle.transform.position = Vector3.forward * 10;
                    VoxelEnemyCar enemy = null; VoxelObstacleCar obstacle = null;
                    int callbacks = 0;
                    if (bossSource != null)
                    {
                        var boss = Object.Instantiate(bossSource); assets.Add(boss); boss.entranceDuration = 0;
                        boss.onDestroyVoxelsRemoved = boss.maximumDestructionDebris = 8;
                        tuning.modelPrefab = boss.bossPrefab; tuning.voxelHealth = boss.voxelHealth;
                        tuning.vehicleHealth = 1; tuning.playerRamDamage = boss.playerRamDamage; tuning.destructionScore = 0;
                        enemy = vehicle.AddComponent<VoxelEnemyCar>(); enemy.enabled = false;
                        enemy.ConfigureBoss(boss, 3, 4);
                    }
                    else if (!trafficController) { enemy = vehicle.AddComponent<VoxelEnemyCar>(); enemy.enabled = false; }
                    if (enemy != null) enemy.Configure(player, traffic, tuning, null, 10, 0);
                    else
                    {
                        obstacle = vehicle.AddComponent<VoxelObstacleCar>(); obstacle.enabled = false;
                        if (civilian)
                        {
                            var set = ScriptableObject.CreateInstance<VoxelCivilianTrafficSet>(); assets.Add(set);
                            set.civilianVehiclePool = new[] { tuning }; traffic.civilianTrafficSet = set;
                            Check(obstacle.Configure(player, traffic, true, null, 10, 0), label + ": civilian configure failed");
                        }
                        else obstacle.Configure(player, traffic, true, null, 10, 0, -1, tuning);
                        obstacle.Defeated += _ => callbacks++;
                    }
                    var pieces = vehicle.GetComponentsInChildren<MeshRenderer>(true)
                        .Where(r => r.GetComponentInParent<VoxelEnemyHealthBar>() == null).ToArray();
                    var destructible = pieces.Where(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null && r.enabled && r.gameObject.activeInHierarchy).ToArray();
                    var protectedPieces = pieces.Where(r => r.GetComponentInParent<VoxelIndestructiblePart>() != null && r.gameObject.activeInHierarchy).ToArray();
                    int original = destructible.Length, target = Mathf.FloorToInt(original * .095f);
                    Check(original > 100 && protectedPieces.Length > 0, label + ": incomplete model/protection test");
                    Bounds bounds = pieces[0].bounds; foreach (var piece in pieces) bounds.Encapsulate(piece.bounds);
                    Vector3 rear = new Vector3(0, .65f, bounds.min.z);
                    Vector3 direction = mode == "LeftRam" ? Vector3.left : mode == "RightRam" || mode == "SpikeRam" ? Vector3.right : Vector3.forward;
                    if (mode == "BoostRam")
                    {
                        var boostTuning = Object.Instantiate(VoxelBoostTuning.Load()); assets.Add(boostTuning);
                        var boost = root.AddComponent<VoxelBoostController>(); boost.Configure(player, boostTuning);
                        Check(boost.TryActivateBoost(), label + ": boost failed");
                    }
                    if (mode == "PloughRam")
                    {
                        typeof(VoxelPloughUpgradeState).GetField("purchased", Static).SetValue(null, true);
                        new GameObject(VoxelPloughUpgradeState.InstanceName).transform.SetParent(player.transform, false);
                    }
                    if (mode == "SpikeRam")
                    {
                        typeof(VoxelWheelSpikeUpgradeState).GetField("purchased", Static).SetValue(null, true);
                        typeof(VoxelWheelSpikeUpgradeState).GetField("installedTuning", Static)
                            .SetValue(null, Resources.Load<VoxelWheelSpikeTuning>("Upgrades/StarWheelSpikeTuning"));
                    }
                    int expectedRamPoints = 0, hits = 0;
                    if (mode == "AlreadyStripped")
                    {
                        Property(enemy, "CurrentHealth", 10000f);
                        foreach (var piece in destructible.Skip(12)) piece.gameObject.SetActive(false);
                        var hit = destructible.First(r => r.gameObject.activeInHierarchy).transform;
                        enemy.TakeHostileProjectileHit(hit, tuning.voxelHealth, hit.position, direction);
                        Check(Count() == 11 && enemy.CurrentHealth > 0, label + ": live damage was clamped to a floor");
                        enemy.TakeHostileProjectileHit(null, enemy.CurrentHealth, rear, direction);
                    }
                    else if (mode == "Completion")
                    {
                        enemy.TakeHostileProjectileHit(null, 1f, rear, direction);
                        enemy.DetonateForMissionCompletion();
                    }
                    else
                    {
                        while (Health() > 0 && hits++ < 2000)
                        {
                            if (mode.EndsWith("Ram") || mode == "Mixed" && hits > 1)
                            {
                                if (enemy != null)
                                    expectedRamPoints += Mathf.RoundToInt(VoxelPloughUpgradeState.ImpactDamage(
                                        VoxelWheelSpikeUpgradeState.CalculateRamDamage(tuning.playerRamDamage, direction == Vector3.forward), player.transform, direction) *
                                        (root.GetComponent<VoxelBoostController>() != null ? root.GetComponent<VoxelBoostController>().RamDamageMultiplier : 1f));
                                Call((object)enemy ?? obstacle, enemy != null ? "RamByPlayer" : "HitCar", (Vector3?)direction);
                            }
                            else if (mode.StartsWith("Missile"))
                            {
                                Vector3 centre = mode == "MissileDirect" ? rear : destructible.First(r => r.gameObject.activeInHierarchy).bounds.center;
                                if (enemy != null) enemy.TakeMissileBlast(centre, 1.8f, 35, direction, pieces, mode == "MissileDirect");
                                else obstacle.TakeMissileBlast(centre, 1.8f, 35, direction, pieces, mode == "MissileDirect");
                            }
                            else
                            {
                                Transform voxel = mode == "HealthOnly" ? null : destructible.First(r => r.gameObject.activeInHierarchy).transform;
                                float damage = mode == "Thump" ? 2 : mode == "HealthOnly" || civilian ? Health() : 1;
                                if (mode == "Hostile")
                                {
                                    if (enemy != null) enemy.TakeHostileProjectileHit(voxel, damage, rear, direction);
                                    else obstacle.TakeHostileProjectileHit(voxel, damage, rear, direction);
                                }
                                else if (enemy != null) enemy.TakeProjectileHit(voxel, damage, rear, direction);
                                else obstacle.TakeProjectileHit(voxel, damage, rear, direction);
                            }
                        }
                        Check(hits < 2000, label + ": damage did not kill target");
                    }
                    int expected = mode == "AlreadyStripped" ? 11 : target;
                    if (civilian) expected = original - 1 - tuning.explosionVoxelCount;
                    if (bossSource != null) expected = original - Mathf.Min(8, Mathf.FloorToInt(original * tuning.maximumExplosionVoxelRemovalPercent));
                    // The boss's voxel HP absorbs its one lethal bullet without losing a piece.
                    if (civilian && mode == "CivilianRam") Check(Count() >= original - traffic.obstacleDamageVoxelsMax && Count() <= original - traffic.obstacleDamageVoxelsMin, label + ": civilian ram changed");
                    else Check(Count() == expected, label + ": expected " + expected + " destructible pieces, got " + Count());
                    Check(protectedPieces.All(r => r.gameObject.activeInHierarchy), label + ": protected structure removed");
                    if (!civilian && bossSource == null && mode != "AlreadyStripped")
                        Check(destructible.Where(r => r.gameObject.activeInHierarchy).All(r =>
                            r.GetComponentsInParent<Transform>().Any(t => t.name == "Obstacle Voxel Wheel")),
                            label + ": wreck retained bodywork ahead of wheel pieces");
                    if (!civilian && mode != "AlreadyStripped")
                    {
                        int expectedScore = mode == "Hostile" || mode == "Completion" ? 0 : tuning.destructionScore + expectedRamPoints;
                        Check(mission.Points == expectedScore, label + ": extra/missing score, got " + mission.Points + ", expected " + expectedScore);
                    }
                    if (mode == "HealthOnly")
                    {
                        Check((int)typeof(VoxelMissionProgress).GetField("enemyVoxelsTowardsReward", Private).GetValue(mission) == 0, label + ": cleanup awarded voxel rewards");
                        Check(scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<VoxelDebris>()).Count() == 8, label + ": debris budget ignored");
                    }
                    int score = mission.Points, count = Count(), callbackCount = callbacks;
                    if (enemy != null)
                    {
                        enemy.TakeProjectileHit(null, 10000, rear, direction);
                        enemy.TakeMissileBlast(rear, 100, 10000, direction, pieces, true);
                        enemy.DetonateForMissionCompletion();
                    }
                    else
                    {
                        obstacle.TakeProjectileHit(null, 10000, rear, direction);
                        obstacle.TakeMissileBlast(rear, 100, 10000, direction, pieces, true);
                    }
                    Check(mission.Points == score && Count() == count && callbacks == callbackCount, label + ": repeated death changed rewards/wreck/callbacks");
                    if (obstacle != null) Check(callbacks == 1, label + ": death callback count incorrect");
                    report.Add(label + ": " + count + "/" + original + " destructible pieces remain (" + (100f * count / original).ToString("F2") + "%), protected=" + protectedPieces.Length);

                    int Count() => destructible.Count(r => r.gameObject.activeInHierarchy && r.enabled);
                    float Health() => enemy != null ? enemy.CurrentHealth : obstacle.CurrentHealth;
                }
                finally
                {
                    foreach (var go in scene.GetRootGameObjects()) if (!existingRoots.Contains(go)) Object.DestroyImmediate(go);
                    foreach (var asset in assets) Object.DestroyImmediate(asset);
                    globalFields[0].SetValue(null, null);
                }
            }
        }
    }
}
