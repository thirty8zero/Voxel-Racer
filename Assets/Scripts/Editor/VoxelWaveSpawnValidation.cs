using System;
using System.Collections;
using System.Collections.Generic;
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
    public static class VoxelWaveSpawnValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private delegate bool ChooseLane(bool direction, float distance, out float lane, out float speed);
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        [MenuItem("Tools/Voxel Racer/Validate Wave Spawn Performance")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run from Edit Mode.");
            var random = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary wave spawn QA");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(10000, 10000, 10000);
            var fades = new List<VoxelFadeIn>();
            var fadeMaterials = new HashSet<Material>();
            try
            {
                var tuning = VoxelObstacleCarTuning.Load();
                var model = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning").modelPrefab;
                var spawner = root.AddComponent<VoxelObstacleSpawner>(); spawner.enabled = false;
                spawner.obstacleCarTuning = tuning;
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < 8; i++)
                {
                    var carObject = Object.Instantiate(model, root.transform);
                    carObject.transform.localPosition = new Vector3((i % 4 - 1.5f) * 3, 0, 50 + i * 12);
                    var car = carObject.AddComponent<VoxelObstacleCar>(); car.enabled = false;
                    typeof(VoxelObstacleCar).GetField("laneOffset", Private).SetValue(car, (i % 4 - 1.5f) * 3);
                    typeof(VoxelObstacleCar).GetField("trackDistance", Private).SetValue(car, 50f + i * 12);
                    typeof(VoxelObstacleCar).GetField("travelsWithPlayer", Private).SetValue(car, true);
                    fades.Add(carObject.AddComponent<VoxelFadeIn>());
                }
                watch.Stop(); double modelMs = watch.Elapsed.TotalMilliseconds;
                var choose = (ChooseLane)Delegate.CreateDelegate(typeof(ChooseLane), spawner,
                    typeof(VoxelObstacleSpawner).GetMethod("TryFindCivilianLane", Private));
                for (int i = 0; i < 10; i++) choose(true, 200, out _, out _);
                using var allocations = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 1,
                    ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame |
                    ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                watch.Restart();
                allocations.Start();
                for (int i = 0; i < 200; i++) choose(true, 200, out _, out _);
                allocations.Stop();
                watch.Stop(); double laneMs = watch.Elapsed.TotalMilliseconds;
                long allocationEvents = allocations.Count > 0 ? allocations.GetSample(0).Count : 0;
                Check(allocations.Valid && allocationEvents == 0, "Warmed lane selection allocated " + allocationEvents + " times.");
                var originalMaterials = root.GetComponentsInChildren<MeshRenderer>().Select(r => r.sharedMaterial).ToArray();
                var distinctMaterials = new HashSet<Material>(originalMaterials);
                var cache = (Action<VoxelFadeIn>)Delegate.CreateDelegate(typeof(Action<VoxelFadeIn>),
                    typeof(VoxelFadeIn).GetMethod("CacheRenderers", Private));
                watch.Restart();
                foreach (var fade in fades) cache(fade);
                watch.Stop(); double fadeMs = watch.Elapsed.TotalMilliseconds;
                foreach (var fade in fades)
                foreach (var state in (IEnumerable)typeof(VoxelFadeIn).GetField("renderers", Private).GetValue(fade))
                    fadeMaterials.Add((Material)state.GetType().GetField("fadeMaterial").GetValue(state));
                Check(fadeMaterials.Count == distinctMaterials.Count, "Fade material sharing did not follow source materials.");
                var alpha = (Action<VoxelFadeIn, float>)Delegate.CreateDelegate(typeof(Action<VoxelFadeIn, float>),
                    typeof(VoxelFadeIn).GetMethod("SetAlpha", Private));
                alpha(fades[0], .2f); alpha(fades[1], .7f);
                var block = new MaterialPropertyBlock();
                var first = fades[0].GetComponentInChildren<MeshRenderer>();
                var second = fades[1].GetComponentInChildren<MeshRenderer>();
                first.GetPropertyBlock(block); Check(Mathf.Approximately(block.GetColor("_BaseColor").a, .2f), "First fade alpha changed.");
                second.GetPropertyBlock(block); Check(Mathf.Approximately(block.GetColor("_BaseColor").a, .7f), "Shared material coupled fade alpha.");
                typeof(VoxelFadeIn).GetMethod("RestoreOpaqueMaterials", Private).Invoke(fades[0], null);
                Check(second.sharedMaterial != null && fadeMaterials.Contains(second.sharedMaterial), "Early fade completion destroyed another fade's material.");
                foreach (var fade in fades) typeof(VoxelFadeIn).GetMethod("RestoreOpaqueMaterials", Private).Invoke(fade, null);
                var restored = root.GetComponentsInChildren<MeshRenderer>();
                Check(restored.Select(r => r.sharedMaterial).SequenceEqual(originalMaterials), "Opaque materials were not restored.");
                Check(fadeMaterials.All(m => m == null), "Unused fade materials leaked.");
                int rendererCount = restored.Length;
                var configuredRoot = new GameObject("Temporary configured traffic wave");
                configuredRoot.transform.SetParent(root.transform, false);
                var player = configuredRoot.AddComponent<VoxelCarController>(); player.enabled = false;
                var configuredTuning = Object.Instantiate(tuning);
                try
                {
                    configuredTuning.civilianVehiclePool = new[] { Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning") };
                    watch.Restart();
                    for (int i = 0; i < 8; i++)
                    {
                        var obj = new GameObject("Configured hatchback"); obj.transform.SetParent(configuredRoot.transform, false);
                        var car = obj.AddComponent<VoxelObstacleCar>();
                        car.Configure(player, configuredTuning, true, null, 65 + i * 12, 0);
                        car.enabled = false;
                    }
                    watch.Stop();
                }
                finally { Object.DestroyImmediate(configuredTuning); }
                double configuredMs = watch.Elapsed.TotalMilliseconds;
                string report = "PASS (Edit Mode): shared fade materials, independent per-object opacity, overlapping fade lifetimes, original material restoration and cleanup; zero warmed lane-query allocations.\n" +
                    "Wave setup benchmark (8 intact hatchbacks, " +
                    rendererCount + " voxel renderers):\n" +
                    "Model instantiation/fixture setup: " + modelMs.ToString("F2") + " ms.\n" +
                    "200 civilian lane selections: " + laneMs.ToString("F2") + " ms; " + allocationEvents + " GC allocation events.\n" +
                    "Fade setup: " + fadeMs.ToString("F2") + " ms; " + fadeMaterials.Count + " distinct temporary materials.\n" +
                    "Separate complete eight-hatchback Configure pass (model, paint, bullet cache, wheels and damage particles; excludes fade): " + configuredMs.ToString("F2") + " ms.\n" +
                    "Isolated synchronous Editor timings, not gameplay FPS. User scenes/assets and random state preserved.";
                Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/WaveSpawnValidation.txt", report);
                Debug.Log(report);
            }
            finally
            {
                // Edit Mode bypasses runtime fade lifecycle. Destroy only the
                // temporary copies created by this fixture, never source materials.
                foreach (var fade in fades)
                    if (fade != null) typeof(VoxelFadeIn).GetMethod("RestoreOpaqueMaterials", Private).Invoke(fade, null);
                foreach (var missile in root.GetComponentsInChildren<VoxelMissileTarget>(true))
                    typeof(VoxelMissileTarget).GetMethod("OnDisable", Private).Invoke(missile, null);
                foreach (var traffic in root.GetComponentsInChildren<VoxelObstacleCar>(true))
                    typeof(VoxelObstacleCar).GetMethod("OnDisable", Private).Invoke(traffic, null);
                EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Random.state = random;
            }
        }
    }
}
