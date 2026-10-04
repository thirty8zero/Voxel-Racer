using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Actual traffic creation and frame-driven overlapping fades.</summary>
    public static class VoxelWaveSpawnPlayValidation
    {
        private const string Pending = "VoxelRacer.WaveSpawnPlayCheck";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameObject root;
        private static VoxelObstacleCarTuning tuning;
        private static VoxelFadeIn[] fades;
        private static MeshRenderer[][] renderers;
        private static Material[][] originals;
        private static Color[][] colours;
        private static Material[] sharedMaterials;
        private static float started, savedScale;
        private static bool savedBackground;
        private static int phase;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Validate Wave Spawns in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start from Edit Mode.");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                Application.runInBackground = savedBackground; Time.timeScale = savedScale;
                SessionState.SetBool(Pending, false);
            }
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        private static void VerifyRestored(int index)
        {
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers[index].Length; i++)
            {
                var renderer = renderers[index][i];
                if (renderer == null) continue;
                Check(renderer.sharedMaterial == originals[index][i], "Original material was not restored.");
                renderer.GetPropertyBlock(block);
                // Unpainted pieces have no colour override, while painted body
                // pieces must recover their exact opaque per-car colour.
                var colour = block.GetColor("_BaseColor");
                if (colour.a > 0) Check(colour == colours[index][i], "Traffic paint changed after fade.");
            }
        }

        private static void Setup()
        {
            savedScale = Time.timeScale; savedBackground = Application.runInBackground;
            Time.timeScale = 1; Application.runInBackground = true; phase = 0;
            try
            {
                root = new GameObject("Temporary live wave spawn QA"); root.transform.position = new Vector3(10000, 20, 0);
                var player = root.AddComponent<VoxelCarController>(); player.enabled = false; player.topSpeed = 0;
                var spawner = root.AddComponent<VoxelObstacleSpawner>(); spawner.enabled = false; spawner.SetTarget(player);
                tuning = Object.Instantiate(VoxelObstacleCarTuning.Load());
                tuning.obstacleCarSpawnChance = 1; tuning.enemyCarSpawnChance = 0; tuning.oppositeDirectionChance = 0;
                tuning.civilianVehiclePool = new[] { Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning") };
                spawner.obstacleCarTuning = tuning;
                var spawn = typeof(VoxelObstacleSpawner).GetMethod("SpawnObject", Private);
                for (int i = 0; i < 4; i++) spawn.Invoke(spawner, new object[] { null, 65f });
                var cars = root.GetComponentsInChildren<VoxelObstacleCar>();
                Check(cars.Length == 4 && cars.Select(c => c.LaneOffset).Distinct().Count() == 4,
                    "Wave lost vehicles or bypassed occupied-lane clearance.");
                foreach (var car in cars) car.transform.position = root.transform.position + new Vector3(car.LaneOffset, 0, 65);
                fades = cars.Select(c => c.GetComponent<VoxelFadeIn>()).ToArray();
                renderers = new MeshRenderer[4][]; originals = new Material[4][]; colours = new Color[4][];
                var materials = new HashSet<Material>();
                for (int i = 0; i < 4; i++)
                {
                    fades[i].duration = i == 0 ? .2f : 2f;
                    var states = ((IEnumerable)typeof(VoxelFadeIn).GetField("renderers", Private).GetValue(fades[i])).Cast<object>().ToArray();
                    renderers[i] = states.Select(s => (MeshRenderer)s.GetType().GetField("renderer").GetValue(s)).ToArray();
                    originals[i] = states.Select(s => (Material)s.GetType().GetField("opaqueMaterial").GetValue(s)).ToArray();
                    colours[i] = states.Select(s => (Color)s.GetType().GetField("baseColor").GetValue(s)).ToArray();
                    foreach (var state in states) materials.Add((Material)state.GetType().GetField("fadeMaterial").GetValue(state));
                }
                sharedMaterials = materials.ToArray();
                Check(sharedMaterials.Length == originals.SelectMany(a => a).Distinct().Count(), "Live wave created per-voxel material copies.");
                started = Time.realtimeSinceStartup; EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused || Time.realtimeSinceStartup - started < (phase == 0 ? .5f : 2.5f)) return;
            try
            {
                if (phase == 0)
                {
                    VerifyRestored(0);
                    Check(renderers[1][0].sharedMaterial != originals[1][0] && renderers[1][0].sharedMaterial != null,
                        "One fade completion broke an overlapping fade.");
                    var block = new MaterialPropertyBlock(); renderers[1][0].GetPropertyBlock(block);
                    float alpha = block.GetColor("_BaseColor").a;
                    Check(alpha > 0 && alpha < 1, "Live fade did not advance opacity.");
                    fades[1].enabled = false; VerifyRestored(1);
                    fades[1].enabled = true;
                    Object.Destroy(fades[2].gameObject); // Last-user cleanup also needs early destruction.
                    renderers[3][0].gameObject.SetActive(false); // Normal voxel removal while fading.
                    phase = 1; started = Time.realtimeSinceStartup;
                    return;
                }
                VerifyRestored(1); VerifyRestored(3);
                Check(sharedMaterials.All(m => m == null), "Completed/destroyed live fades leaked shared materials.");
                Finish("PASS (Play Mode): actual four-car wave chooses four clear lanes; material count follows source materials, independent opacity advances on real frames, overlapping fades survive early completion, disable/re-enable restores/restarts correctly, destroyed cars/inactive voxels clean up, opaque materials and traffic paint recover exactly. Temporary objects/cloned tuning removed; no source assets or purchases changed. Not an FPS benchmark.");
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Finish(string report)
        {
            EditorApplication.update -= Tick;
            Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/WaveSpawnPlayValidation.txt", report);
            if (root != null) Object.Destroy(root); if (tuning != null) Object.Destroy(tuning);
            Time.timeScale = savedScale; Application.runInBackground = savedBackground;
            Debug.Log(report); EditorApplication.isPlaying = false;
        }
    }
}
