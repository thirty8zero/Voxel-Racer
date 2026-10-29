using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelEnemyDroughtValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Remove(GameObject owner)
        {
            foreach (var bar in owner.GetComponentsInChildren<VoxelEnemyHealthBar>(true)) Call(bar, "OnDestroy");
            Object.DestroyImmediate(owner);
        }

        [MenuItem("Tools/Voxel Racer/Validate Enemy Drought Spawning")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Run from Edit Mode.");
            var random = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary enemy drought QA");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var traffic = Object.Instantiate(VoxelObstacleCarTuning.Load());
            var vehicle = Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning"));
            var large = Object.Instantiate(vehicle);
            try
            {
                var playerRoot = new GameObject("Player"); playerRoot.transform.SetParent(root.transform);
                var player = playerRoot.AddComponent<VoxelCarController>(); player.enabled = false; player.topSpeed = 35;
                var spawner = root.AddComponent<VoxelObstacleSpawner>(); spawner.enabled = false;
                spawner.SetTarget(player); spawner.obstacleCarTuning = traffic; spawner.laneCount = 1;
                traffic.obstacleCarSpawnChance = traffic.enemyCarSpawnChance = 0;
                traffic.enemyDroughtWaveLimit = 6;
                traffic.enemyVehicleSpawns = new[] { new VoxelEnemyVehicleSpawnEntry { vehicle = vehicle } };
                vehicle.psychoBug = null; vehicle.minimumDrivingSpeed = 20;
                void Wave(int wave, float distance = 220) => Call(spawner, "SpawnWaveObject", null, distance, wave);
                int Count() => root.GetComponentsInChildren<VoxelEnemyCar>().Length;
                void ClearEnemies() { foreach (var enemy in root.GetComponentsInChildren<VoxelEnemyCar>()) Remove(enemy.gameObject); }
                bool Safe(float distance = 220)
                {
                    Call(spawner, "RefreshLaneOccupants");
                    return (bool)Call(spawner, "IsDroughtEnemyLaneSafe", vehicle, 0f, distance);
                }
                for (int wave = 1; wave <= 6; wave++) { Wave(wave); Check(Count() == 0, "Forced before six missed waves."); }
                Wave(7); Check(Count() == 1, "Seventh wave did not override zero random chances.");
                ClearEnemies(); Wave(7); Check(Count() == 0, "Multiple forced enemies in one wave.");
                for (int wave = 8; wave <= 13; wave++) Wave(wave);
                Check(Count() == 0, "Drought failed to reset on successful spawn.");
                Wave(14); Check(Count() == 1, "Second seventh-wave cycle failed."); ClearEnemies();
                traffic.enemyDroughtWaveLimit = 0; Wave(100); Check(Count() == 0, "Zero must disable drought fallback.");
                traffic.enemyDroughtWaveLimit = 2; Set(spawner, "lastEnemySpawnWave", 0);
                Wave(2); Check(Count() == 0, "Custom threshold triggered early."); Wave(3); Check(Count() == 1, "Custom threshold ignored."); ClearEnemies();

                traffic.enemyDroughtWaveLimit = 6; traffic.obstacleCarSpawnChance = traffic.enemyCarSpawnChance = 1;
                Set(spawner, "lastEnemySpawnWave", 0); Wave(4); Check(Count() == 1, "Natural enemy spawn missing."); ClearEnemies();
                traffic.obstacleCarSpawnChance = traffic.enemyCarSpawnChance = 0;
                Wave(10); Check(Count() == 0, "Natural spawn did not reset drought."); Wave(11); Check(Count() == 1, "Drought after natural spawn failed."); ClearEnemies();

                CheckRadarIsolation(spawner, traffic, root, wave => Wave(wave));

                // Real controller types with controlled positions, without building unrelated models.
                foreach (var type in new[] { typeof(VoxelObstacle), typeof(VoxelFuelDrumObstacle), typeof(VoxelPotholeObstacle), typeof(VoxelOilSlickObstacle) })
                {
                    var go = new GameObject(type.Name); go.transform.SetParent(root.transform);
                    var obstacle = (MonoBehaviour)go.AddComponent(type); obstacle.enabled = false;
                    void Position(float distance)
                    {
                        Set(obstacle, type == typeof(VoxelOilSlickObstacle) ? "<TrackDistance>k__BackingField" : "trackDistance", distance);
                    }
                    Position(180); Check(Safe(), type.Name + " already behind must allow forced spawn.");
                    Check(!(bool)Call(spawner, "TryFindEmptyVehicleLane", new object[] { 0f }), "Ordinary enemy lane rules were relaxed.");
                    Position(218); Check(!Safe(), type.Name + " rear overlap accepted.");
                    Position(240); Check(!Safe(), type.Name + " reachable ahead accepted.");
                    Set(spawner, "lastEnemySpawnWave", 0); Wave(7); Check(Count() == 0, "Unsafe forced spawn created.");
                    Position(180); Wave(7); Check(Count() == 1, "Later slot did not retry blocked forced spawn."); ClearEnemies();
                    Remove(go);
                }
                var civilianRoot = new GameObject("Civilian"); civilianRoot.transform.SetParent(root.transform);
                var civilian = civilianRoot.AddComponent<VoxelObstacleCar>(); civilian.enabled = false;
                Set(civilian, "trackDistance", 180f); Set(civilian, "travelsWithPlayer", true);
                Set(civilian, "travelSpeed", 15.75f); Set(civilian, "spawnSpeed", 15.75f);
                Set(civilian, "approachSpeed", 15f); Set(civilian, "engageSpeed", 14f);
                Check(Safe(), "Passed slower civilian blocked.");
                Set(civilian, "engageSpeed", 25f); Check(!Safe(), "Later faster phase could catch new enemy.");
                Set(civilian, "travelsWithPlayer", false); Check(Safe(), "Passed oncoming car should move away.");
                Set(civilian, "trackDistance", 218f); Check(!Safe(), "Civilian overlap accepted.");
                Set(civilian, "trackDistance", 250f); Check(!Safe(), "Approaching oncoming car accepted.");
                Set(civilian, "trackDistance", 180f); Set(civilian, "hasBeenHit", true); Check(!Safe(), "Moving wreck accepted.");
                Remove(civilianRoot);

                // A disabled controller on an active object still reserves its merge destination.
                var existingRoot = new GameObject("Reserved enemy"); existingRoot.transform.SetParent(root.transform);
                var existing = existingRoot.AddComponent<VoxelEnemyCar>(); existing.enabled = false;
                Set(existing, "laneOffset", -3f); Set(existing, "targetLaneOffset", 0f); Set(existing, "trackDistance", 100f);
                Check(!Safe(), "Reserved enemy merge lane ignored."); Remove(existingRoot);

                // Only eligible, safe pool members are candidates, including size differences.
                var blockerRoot = new GameObject("Clearance limit"); blockerRoot.transform.SetParent(root.transform);
                var blocker = blockerRoot.AddComponent<VoxelObstacle>(); Set(blocker, "trackDistance", 208f);
                large.collisionHalfLength = 20;
                traffic.enemyVehicleSpawns = new[] {
                    new VoxelEnemyVehicleSpawnEntry { vehicle = large, spawnWeight = 10000 },
                    new VoxelEnemyVehicleSpawnEntry { vehicle = vehicle, spawnWeight = 1 }
                };
                Set(spawner, "lastEnemySpawnWave", 0); Wave(7);
                Check(Count() == 1 && root.GetComponentInChildren<VoxelEnemyCar>().Tuning == vehicle, "Unsafe large model hid safe pool member.");
                ClearEnemies(); Remove(blockerRoot);
                foreach (var pool in new[] { Array.Empty<VoxelEnemyVehicleSpawnEntry>(), new[] { new VoxelEnemyVehicleSpawnEntry { vehicle = vehicle, spawnWeight = 0 } } })
                {
                    traffic.enemyVehicleSpawns = pool; Set(spawner, "lastEnemySpawnWave", 0); Wave(99);
                    Check(Count() == 0, "Empty/disabled pool bypassed by fallback.");
                }
                foreach (var tuning in Resources.LoadAll<VoxelTrackDefinition>("Tracks").Select(t => t.obstacleCarTuning).Append(VoxelObstacleCarTuning.Load()).Where(t => t != null).Distinct())
                {
                    Check(tuning.enemyDroughtWaveLimit == 6, "Track default is not six: " + tuning.name);
                    using var tree = PropertyTree.Create(tuning); tree.UpdateTree();
                    var property = tree.EnumerateTree(true).First(p => p.Name == "enemyDroughtWaveLimit");
                    Check(property.Attributes.OfType<FoldoutGroupAttribute>().Any(a => a.GroupName == "Enemy Vehicles"), "Drought setting missing Enemy Vehicles foldout.");
                }
                Directory.CreateDirectory("Temp/EnemyDrought");
                File.WriteAllText("Temp/EnemyDrought/Validation.txt", "PASS: seventh-wave default, custom threshold, disabled fallback, natural/forced success reset, one forced enemy per wave, blocked slot retry, passed/overlapping/ahead crates, drums, potholes and oil, unchanged ordinary lane exclusions, rear civilian phase-speed safety, oncoming separation, wreck rejection, disabled enemy merge reservations, safe eligible weighted pool members, empty/disabled pools, all track defaults and Odin Inspector integration. Boss/radar isolation covers every encounter stage, ordinary chance 0/1, ordinary and overdue queued-wave paths, actual Radar Interceptor spawning and one active objective at a time; ordinary drought spawning resumes when the objective is removed. Temporary preview only; source assets and RNG preserved.");
                Debug.Log("PASS Enemy Drought Spawning: wave accounting, safe local clearance, traffic forecasts and track Inspector integration.");
            }
            finally
            {
                Remove(root); Object.DestroyImmediate(traffic); Object.DestroyImmediate(vehicle); Object.DestroyImmediate(large);
                EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Random.state = random;
            }
        }

        private static void CheckRadarIsolation(VoxelObstacleSpawner spawner, VoxelObstacleCarTuning traffic,
            GameObject root, Action<int> wave)
        {
            using var civilianScope = new VoxelCivilianTrafficSetScope(traffic);
            traffic.civilianTrafficSet.civilianVehiclePool = new[] { Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning") };
            var encounter = root.AddComponent<VoxelBossEncounter>(); encounter.enabled = false;
            spawner.ConfigureRadarObjective(encounter, 100);
            traffic.obstacleCarSpawnChance = 1;
            void ClearTraffic()
            {
                foreach (var car in root.GetComponentsInChildren<VoxelObstacleCar>())
                { Call(car, "OnDisable"); Remove(car.gameObject); }
            }
            try
            {
                foreach (VoxelBossEncounter.Stage stage in Enum.GetValues(typeof(VoxelBossEncounter.Stage)))
                foreach (float chance in new[] { 0f, 1f })
                foreach (int queuedWave in new[] { 1, 7, 70 })
                {
                    typeof(VoxelBossEncounter).GetProperty("CurrentStage").SetValue(encounter, stage);
                    traffic.enemyCarSpawnChance = chance; Set(spawner, "lastEnemySpawnWave", 0);
                    wave(queuedWave);
                    Check(root.GetComponentsInChildren<VoxelEnemyCar>().Length == 0,
                        "Ordinary enemy entered boss/radar stage " + stage + ", chance=" + chance + ", queued wave=" + queuedWave);
                    var cars = root.GetComponentsInChildren<VoxelObstacleCar>();
                    Check(cars.Length == 1 && cars[0].IsEnemyTraffic == (stage == VoxelBossEncounter.Stage.Traffic),
                        "Radar/civilian traffic was suppressed or misclassified in " + stage);
                    ClearTraffic();
                }
                typeof(VoxelBossEncounter).GetProperty("CurrentStage").SetValue(encounter, VoxelBossEncounter.Stage.Traffic);
                spawner.laneCount = 2; Set(spawner, "lastEnemySpawnWave", 0);
                wave(70); wave(71);
                Check(root.GetComponentsInChildren<VoxelObstacleCar>().Count(c => c.IsEnemyTraffic) == 1,
                    "Boss approach did not preserve the one-active-radar limit.");
                Check(root.GetComponentsInChildren<VoxelEnemyCar>().Length == 0, "Later radar waves leaked an ordinary enemy.");
                ClearTraffic();
                spawner.ConfigureRadarObjective(null, 10); traffic.enemyCarSpawnChance = traffic.obstacleCarSpawnChance = 0;
                Set(spawner, "lastEnemySpawnWave", 0); wave(7);
                Check(root.GetComponentsInChildren<VoxelEnemyCar>().Length == 1, "Removing radar objective did not restore normal drought spawning.");
                foreach (var enemy in root.GetComponentsInChildren<VoxelEnemyCar>()) Remove(enemy.gameObject);
            }
            finally
            {
                ClearTraffic(); spawner.ConfigureRadarObjective(null, 10); spawner.laneCount = 1;
                traffic.enemyCarSpawnChance = traffic.obstacleCarSpawnChance = 0;
                Set(spawner, "lastEnemySpawnWave", 0); Object.DestroyImmediate(encounter);
            }
        }
    }
}
