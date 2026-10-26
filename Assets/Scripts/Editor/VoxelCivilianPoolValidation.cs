using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelCivilianPoolValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);

        [MenuItem("Tools/Voxel Racer/Validate Civilian Vehicle Pools")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Run from Edit Mode.");
            var random = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary civilian pool QA");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var traffic = Object.Instantiate(VoxelObstacleCarTuning.Load());
            using var civilianSetScope = new VoxelCivilianTrafficSetScope(traffic);
            var invalid = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>();
            var large = Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning"));
            try
            {
                var player = root.AddComponent<VoxelCarController>(); player.enabled = false; player.topSpeed = 35;
                var spawner = root.AddComponent<VoxelObstacleSpawner>(); spawner.enabled = false;
                spawner.SetTarget(player); spawner.obstacleCarTuning = traffic; spawner.laneCount = 1;
                traffic.obstacleCarSpawnChance = 1; traffic.enemyCarSpawnChance = 0;
                var models = VoxelObstacleCarTuning.Load().CivilianVehiclePool;
                Check(models.Length == 6 && models.All(v => v != null && v.modelPrefab != null), "Expected current six civilian models.");
                foreach (var pool in new[] { null, Array.Empty<VoxelEnemyVehicleTuning>(), new[] { null, invalid } })
                {
                    traffic.civilianTrafficSet.civilianVehiclePool = pool;
                    Check(!traffic.HasCivilianVehicles && traffic.ChooseCivilianVehicle() == null, "Empty/invalid pool used a fallback.");
                    Call(spawner, "SpawnObject", null, 220f);
                    Check(root.GetComponentsInChildren<VoxelObstacleCar>().Length == 0, "Empty pool created traffic.");
                    var empty = new GameObject("Empty configuration"); empty.transform.SetParent(root.transform);
                    var emptyCar = empty.AddComponent<VoxelObstacleCar>();
                    Check(!emptyCar.Configure(player, traffic, true, null, 220f, 0f) && !emptyCar.enabled &&
                        empty.GetComponentsInChildren<MeshRenderer>().Length == 0, "Direct empty configuration created fallback visuals.");
                    Object.DestroyImmediate(empty);
                }
                foreach (var model in models)
                {
                    traffic.civilianTrafficSet.civilianVehiclePool = new[] { null, invalid, model };
                    for (int pick = 0; pick < 100; pick++) Check(traffic.ChooseCivilianVehicle() == model, "Single valid model ignored.");
                    foreach (bool withPlayer in new[] { true, false })
                    {
                        var go = new GameObject("Civilian model QA"); go.transform.SetParent(root.transform);
                        var car = go.AddComponent<VoxelObstacleCar>(); car.enabled = false;
                        Check(car.Configure(player, traffic, withPlayer, null, 220f, 0f, 12f), "Valid pooled configuration rejected.");
                        Check(car.EnemyTuning == model && !car.IsEnemyTraffic && car.CurrentHealth == model.vehicleHealth,
                            "Model identity/durability changed: " + model.name);
                        Check(car.TrafficHalfWidth == model.collisionHalfWidth && car.TrafficHalfLength == model.collisionHalfLength,
                            "Collision bounds did not come from pooled model: " + model.name);
                        Check(car.TravelsWithPlayer == withPlayer && car.TravelSpeed == 12f && go.GetComponentInChildren<VoxelTrafficPaint>() != null,
                            "Traffic movement/visuals missing: " + model.name);
                        Check((bool)typeof(VoxelObstacleCar).GetProperty("UsesVanImpactSettings", Private).GetValue(car) == model.useVanImpactSettings,
                            "Van impact profile not read from model.");
                        car.GetPlayerCollisionDamageRange(out int min, out int max);
                        Check(min == traffic.playerDamageVoxelsMin && max == traffic.playerDamageVoxelsMax, "Civilian player damage changed.");
                        Object.DestroyImmediate(go);
                    }
                }
                var hatch = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning");
                Check(hatch.collisionHalfWidth == 1.35f && hatch.collisionHalfLength == 2.3f, "Hatchback runtime bounds migration changed its size.");
                Check(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/CivilianVanTuning").useVanImpactSettings, "Van impact profile not preserved.");
                traffic.civilianTrafficSet.civilianVehiclePool = models;
                var counts = new int[models.Length]; UnityEngine.Random.InitState(81321);
                for (int pick = 0; pick < 18000; pick++) counts[Array.IndexOf(models, traffic.ChooseCivilianVehicle())]++;
                Check(counts.All(n => n > 2700 && n < 3300), "Equal model selection changed.");

                // Spawn clearance must use the longest configured pool model.
                var leaderRoot = new GameObject("Traffic clearance QA"); leaderRoot.transform.SetParent(root.transform);
                var leader = leaderRoot.AddComponent<VoxelObstacleCar>(); leader.enabled = false;
                traffic.civilianTrafficSet.civilianVehiclePool = new[] { hatch };
                Check(leader.Configure(player, traffic, true, null, 210f, 0f, 12f), "Leader setup failed.");
                bool FindLane() => (bool)Call(spawner, "TryFindCivilianLane", true, 220f, 0f, -1f);
                Check(FindLane(), "Old unused vehicle reference still blocks clear pooled spawn.");
                large.collisionHalfLength = 20f; traffic.civilianTrafficSet.civilianVehiclePool = new[] { hatch, large };
                Check(!FindLane(), "Longest pool model ignored by spawn clearance."); Object.DestroyImmediate(leaderRoot);

                // An explicit radar objective remains valid even with no civilians enabled.
                traffic.civilianTrafficSet.civilianVehiclePool = Array.Empty<VoxelEnemyVehicleTuning>();
                var radar = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/RadarInterceptorTuning");
                var radarRoot = new GameObject("Radar override QA"); radarRoot.transform.SetParent(root.transform);
                var radarCar = radarRoot.AddComponent<VoxelObstacleCar>(); radarCar.enabled = false;
                Check(radarCar.Configure(player, traffic, true, null, 220f, 0f, 12f, radar) && radarCar.IsEnemyTraffic && radarCar.EnemyTuning == radar,
                    "Empty civilian pool blocked radar override.");
                radarCar.GetPlayerCollisionDamageRange(out int radarMin, out int radarMax);
                Check(radarMin == radar.playerDamageVoxelsMin && radarMax == radar.playerDamageVoxelsMax, "Radar collision damage changed.");
                Object.DestroyImmediate(radarRoot);
                foreach (var tuning in Resources.LoadAll<VoxelTrackDefinition>("Tracks").Select(t => t.obstacleCarTuning).Append(VoxelObstacleCarTuning.Load()).Distinct())
                {
                    Check(tuning.CivilianVehiclePool.SequenceEqual(models), "Existing pool changed: " + tuning.name);
                    var serialized = new SerializedObject(tuning);
                    foreach (string field in new[] { "semiTrailerSpawnChance", "semiTrailerEnemyTuning", "trafficCarEnemyTuning" })
                        Check(serialized.FindProperty(field) == null, "Legacy setting remains serialized/visible: " + field);
                }
                Directory.CreateDirectory("Temp/CivilianPoolOnly");
                File.WriteAllText("Temp/CivilianPoolOnly/Validation.txt", "PASS: null/empty/invalid pools produce no traffic or procedural fallback; single valid entries and equal six-model selection; actual configuration of all six models in both directions preserves models, health, traffic speeds, paint, collision sizes, player damage and van impact profile; hatchback runtime dimensions preserved; spawn clearance follows longest pool member; radar override/damage works with an empty civilian pool; all twelve current traffic pools unchanged; removed legacy properties absent from serialized Inspector.");
                Debug.Log("PASS Civilian Vehicle Pools: selection, empty pools, all six models, collision/impact migration, clearance and radar override.");
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(traffic); Object.DestroyImmediate(invalid); Object.DestroyImmediate(large);
                EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Random.state = random;
            }
        }
    }
}
