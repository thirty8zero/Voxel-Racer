using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelProjectilePerformanceValidation
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private delegate bool SelectVoxel(Vector3 start, Vector3 direction, float length, out Transform voxel);

        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        // The pre-optimization selectors are retained here as an independent
        // brute-force oracle: no cached membership and no distance rejection.
        private static bool LegacyPeel(Component owner, Vector3 start, Vector3 direction, float length,
            float width, float depth, float randomness, out Transform voxel)
        {
            voxel = null;
            if (length <= 0) return false;
            bool Include(MeshRenderer r)
            {
                if (owner is VoxelEnemyCar)
                    return r.transform != owner.transform && r.GetComponentInParent<VoxelEnemyHealthBar>() == null &&
                        r.GetComponentInParent<VoxelIndestructiblePart>() == null;
                if (owner is VoxelFuelDrumObstacle)
                {
                    for (var t = r.transform; t != null; t = t.parent)
                        if (t.name == "Fuel Drum") return true;
                    return false;
                }
                return true;
            }
            var right = Vector3.Cross(Vector3.up, direction).normalized;
            float rear = float.PositiveInfinity;
            foreach (var r in owner.GetComponentsInChildren<MeshRenderer>())
            {
                if (!Include(r)) continue;
                var offset = r.transform.position - start;
                float forward = Vector3.Dot(offset, direction);
                if (forward < 0 || forward > length || Mathf.Abs(Vector3.Dot(offset, right)) > width) continue;
                rear = Mathf.Min(rear, forward);
            }
            if (float.IsPositiveInfinity(rear)) return false;
            float best = float.PositiveInfinity;
            foreach (var r in owner.GetComponentsInChildren<MeshRenderer>())
            {
                if (!Include(r)) continue;
                var offset = r.transform.position - start;
                float forward = Vector3.Dot(offset, direction);
                if (forward < rear || forward > rear + depth) continue;
                float lateral = Mathf.Abs(Vector3.Dot(offset, right));
                if (lateral > width) continue;
                var onPath = start + direction * forward;
                float score = Mathf.Abs(r.transform.position.y - onPath.y) * 2 + lateral + UnityEngine.Random.value * randomness;
                if (score >= best) continue;
                best = score; voxel = r.transform;
            }
            return voxel != null;
        }

        private static bool LegacyTraffic(VoxelObstacleCar[] cars, Vector3 start, Vector3 direction, float length,
            out Transform voxel, out float distance, MeshRenderer[][] renderers = null)
        {
            voxel = null; distance = float.PositiveInfinity;
            if (length <= 0) return false;
            for (int i = 0; i < cars.Length; i++)
            foreach (var r in renderers != null ? renderers[i] : cars[i].GetComponentsInChildren<MeshRenderer>())
            {
                if (r == null || !r.gameObject.activeInHierarchy || !r.enabled ||
                    r.GetComponentInParent<VoxelIndestructiblePart>() != null) continue;
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var origin = r.transform.InverseTransformPoint(start);
                var localDirection = r.transform.InverseTransformVector(direction).normalized;
                var bounds = filter.sharedMesh.bounds;
                float entry = 0;
                if (!bounds.Contains(origin) && !bounds.IntersectRay(new Ray(origin, localDirection), out entry)) continue;
                var point = r.transform.TransformPoint(origin + localDirection * entry);
                float along = Vector3.Dot(point - start, direction);
                if (along < 0 || along > length || along >= distance) continue;
                distance = along; voxel = r.transform;
            }
            return voxel != null;
        }

        private static void ValidatePeel(Component owner, SelectVoxel select, float width, float depth, float randomness)
        {
            int hits = 0;
            foreach (float angle in new[] { 0f, 37f, 180f })
            foreach (float scale in new[] { .5f, 1f, 2.4f })
            {
                owner.transform.localRotation = Quaternion.Euler(0, angle, 0);
                owner.transform.localScale = new Vector3(scale, scale * .8f, scale * 1.1f);
                for (int i = 0; i < 48; i++)
                {
                    var start = owner.transform.TransformPoint(new Vector3((i % 8 - 3) * .7f, i % 4, -8));
                    var direction = owner.transform.forward;
                    float length = i % 3 == 0 ? 1f : 30f;
                    UnityEngine.Random.InitState(i);
                    bool oldHit = LegacyPeel(owner, start, direction, length, width, depth, randomness, out var oldVoxel);
                    var oldState = UnityEngine.Random.state;
                    UnityEngine.Random.InitState(i);
                    bool newHit = select(start, direction, length, out var newVoxel);
                    var newState = UnityEngine.Random.state;
                    Check(oldHit == newHit && oldVoxel == newVoxel, owner.GetType().Name + " changed surface selection.");
                    Check(JsonUtility.ToJson(oldState) == JsonUtility.ToJson(newState), "Query changed random consumption.");
                    if (newHit)
                    {
                        hits++;
                        // Keep holes after cache construction; membership must still
                        // follow live activity and the original traversal order.
                        if (i % 4 == 0) newVoxel.gameObject.SetActive(false);
                    }
                }
            }
            Check(hits > 0, "Surface parity fixture never hit " + owner.GetType().Name);
        }

        private static void ValidateModels(Transform root, VoxelEnemyVehicleTuning enemyTuning)
        {
            var enemyObject = Object.Instantiate(enemyTuning.modelPrefab, root);
            enemyObject.transform.localPosition = Vector3.right * 500;
            var enemy = enemyObject.AddComponent<VoxelEnemyCar>();
            typeof(VoxelEnemyCar).GetField("<Tuning>k__BackingField", Instance).SetValue(enemy, enemyTuning);
            ValidatePeel(enemy, enemy.TryGetNextProjectileVoxel, 1.45f, .4f, enemyTuning.rearSurfaceHitRandomness);

            var crateDefinition = Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/VoxelBox");
            var crateObject = Object.Instantiate(crateDefinition.modelPrefab, root);
            crateObject.transform.localPosition = Vector3.right * 1000;
            var crate = crateObject.AddComponent<VoxelObstacle>();
            typeof(VoxelObstacle).GetField("definition", Instance).SetValue(crate, crateDefinition);
            ValidatePeel(crate, crate.TryGetNextProjectileVoxel, 1.8f, .55f, crateDefinition.rearSurfaceHitRandomness);

            var drumObject = new GameObject("Temporary fuel drums");
            drumObject.transform.SetParent(root, false);
            drumObject.transform.localPosition = Vector3.right * 1500;
            var drums = drumObject.AddComponent<VoxelFuelDrumObstacle>();
            var drumDefinition = Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/FuelDrums");
            typeof(VoxelFuelDrumObstacle).GetField("definition", Instance).SetValue(drums, drumDefinition);
            typeof(VoxelFuelDrumObstacle).GetMethod("BuildDrums", Instance).Invoke(drums, null);
            ValidatePeel(drums, drums.TryGetNextProjectileVoxel, 2.5f, .6f, drumDefinition.rearSurfaceHitRandomness);

            var bossObject = new GameObject("Temporary entrance boss");
            bossObject.transform.SetParent(root, false);
            bossObject.transform.localPosition = Vector3.right * 2000;
            var bossDefinition = Resources.Load<VoxelBossDefinition>("Bosses/RedVanBoss");
            var model = Object.Instantiate(bossDefinition.bossPrefab, bossObject.transform);
            model.transform.localScale = Vector3.one * 2.4f;
            var entrance = model.AddComponent<VoxelBossEntrance>();
            entrance.Configure(2);
            var boss = bossObject.AddComponent<VoxelEnemyCar>();
            typeof(VoxelEnemyCar).GetField("<Tuning>k__BackingField", Instance).SetValue(boss, enemyTuning);
            // Build at zero scale, then check the full-size animated model.
            boss.TryGetNextProjectileVoxel(bossObject.transform.position - Vector3.forward * 20, Vector3.forward, 1, out _);
            typeof(VoxelBossEntrance).GetMethod("Advance", Instance).Invoke(entrance, new object[] { 3f });
            var rig = model.GetComponentInChildren<VoxelBossSpikeRig>();
            if (rig != null) rig.SetPose(1, 1);
            foreach (var wheel in model.GetComponentsInChildren<Transform>().Where(t => t.name == "Obstacle Voxel Wheel"))
                wheel.Rotate(Vector3.right, 71);
            ValidatePeel(boss, boss.TryGetNextProjectileVoxel, 1.45f, .4f, enemyTuning.rearSurfaceHitRandomness);

            foreach (var component in new Component[] { enemy, crate, drums })
            {
                var list = (System.Collections.IList)component.GetType().GetField("ActiveProjectileTargets",
                    BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                int before = list.Count;
                var on = component.GetType().GetMethod("OnEnable", Instance);
                var off = component.GetType().GetMethod("OnDisable", Instance);
                on.Invoke(component, null); on.Invoke(component, null);
                Check(list.Contains(component), "Target registration failed.");
                off.Invoke(component, null);
                Check(!list.Contains(component), "Disabled target remained registered.");
                on.Invoke(component, null);
                Check(list.Count <= before + 1, "Duplicate target registration.");
            }
        }

        [MenuItem("Tools/Voxel Racer/Validate Bullet Target Performance")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run from Edit Mode.");
            var random = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary shooting performance validation");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(10000, 10000, 10000);
            try
            {
                var enemyTuning = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning");
                var trafficTuning = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning");
                for (int i = 0; i < 8; i++)
                {
                    var obj = Object.Instantiate(i < 4 ? enemyTuning.modelPrefab : trafficTuning.modelPrefab, root.transform);
                    obj.transform.localPosition = new Vector3((i % 3) * 3, 0, 30 + i * 10);
                    if (i < 4) obj.AddComponent<VoxelEnemyCar>();
                    else
                    {
                        var car = obj.AddComponent<VoxelObstacleCar>();
                        typeof(VoxelObstacleCar).GetMethod("OnEnable", Instance).Invoke(car, null);
                    }
                }
                var enemies = root.GetComponentsInChildren<VoxelEnemyCar>();
                var traffic = root.GetComponentsInChildren<VoxelObstacleCar>();
                var trafficRenderers = traffic.Select(car => car.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
                var origin = root.transform.position + Vector3.up * .7f;
                void Query()
                {
                    VoxelObstacleCar.TryFindProjectileHit(origin, Vector3.forward, 2.5f, out _, out _, out _, out _);
                    foreach (var enemy in enemies)
                        enemy.TryGetNextProjectileVoxel(origin, Vector3.forward, 2.5f, out _);
                }
                void LegacyQuery()
                {
                    LegacyTraffic(traffic, origin, Vector3.forward, 2.5f, out _, out _, trafficRenderers);
                    foreach (var enemy in enemies)
                        LegacyPeel(enemy, origin, Vector3.forward, 2.5f, 1.45f, .4f, 0, out _);
                }
                for (int i = 0; i < 20; i++) Query();
                using var gc = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 1,
                    ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame |
                    ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                var watch = new Stopwatch();
                watch.Start(); gc.Start();
                for (int i = 0; i < 1000; i++) Query();
                gc.Stop(); watch.Stop();
                long allocations = gc.Count > 0 ? gc.GetSample(0).Count : 0;
                double optimizedMs = watch.Elapsed.TotalMilliseconds;
                Check(gc.Valid && allocations == 0, "Warmed target queries allocate: " + allocations);
                watch.Restart();
                for (int i = 0; i < 1000; i++) LegacyQuery();
                watch.Stop();
                double legacyMs = watch.Elapsed.TotalMilliseconds;

                int renderers = root.GetComponentsInChildren<MeshRenderer>().Length;
                root.transform.localScale = new Vector3(1.2f, .7f, 1.8f);
                ValidateModels(root.transform, enemyTuning);
                // Traffic: compare intact mesh intersections/closest voxel across
                // translations, rotations, live wheel/dish rotations and holes.
                foreach (var car in traffic)
                {
                    foreach (var wheel in car.GetComponentsInChildren<Transform>().Where(t => t.name == "Obstacle Voxel Wheel"))
                        wheel.Rotate(Vector3.right, 71);
                    foreach (float angle in new[] { 0f, 37f, 180f })
                    {
                        car.transform.localRotation = Quaternion.Euler(0, angle, 0);
                        for (int i = 0; i < 32; i++)
                        {
                            var start = car.transform.TransformPoint(new Vector3((i % 8 - 3) * .4f, (i % 4) * .5f, -6));
                            var direction = car.transform.forward;
                            float length = i % 3 == 0 ? 1f : 12f;
                            bool oldHit = LegacyTraffic(traffic, start, direction, length, out var oldVoxel, out float oldDistance);
                            bool newHit = VoxelObstacleCar.TryFindProjectileHit(start, direction, length,
                                out _, out var newVoxel, out _, out float newDistance);
                            Check(oldHit == newHit && oldVoxel == newVoxel &&
                                (!oldHit || Mathf.Abs(oldDistance - newDistance) < .001f), "Traffic intersection parity failed.");
                            if (newHit && i % 4 == 0) newVoxel.gameObject.SetActive(false);
                        }
                    }
                }
                string report = "PASS (Edit Mode): cached vs brute-force enemy/crate/drum/boss surface selection and random consumption, " +
                    "traffic closest-voxel/distance parity, finite segments, holes, rotations, non-uniform scales, zero-scale boss entrance and target lifecycle.\n" +
                    "1000 warmed bullet segments, four enemies + four civilians, " + renderers + " renderers: original scans " +
                    legacyMs.ToString("F2") + " ms; optimized " + optimizedMs.ToString("F2") + " ms; optimized " + allocations +
                    " GC allocation events. Excludes scene searches, spawning, physics and FX; not a game FPS benchmark.\n" +
                    "Temporary preview objects removed and random state restored; no source assets or user scenes changed.";
                Directory.CreateDirectory("Temp");
                File.WriteAllText("Temp/BulletTargetPerformance.txt", report);
                Debug.Log(report);
            }
            finally
            {
                foreach (var car in root.GetComponentsInChildren<VoxelObstacleCar>(true))
                    typeof(VoxelObstacleCar).GetMethod("OnDisable", Instance).Invoke(car, null);
                foreach (var type in new[] { typeof(VoxelEnemyCar), typeof(VoxelObstacle), typeof(VoxelFuelDrumObstacle) })
                foreach (var component in root.GetComponentsInChildren(type, true))
                    type.GetMethod("OnDisable", Instance).Invoke(component, null);
                EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Random.state = random;
            }
        }
    }
}
