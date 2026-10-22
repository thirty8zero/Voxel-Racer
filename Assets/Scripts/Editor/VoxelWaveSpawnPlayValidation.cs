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
        private static VoxelSpawnScaleIn[] growth;
        private static Transform[][] growthTargets;
        private static Vector3[][] growthScales, growthPositions;
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

        private static void VerifyFullScale(int index)
        {
            Check(growth[index].transform.localScale == Vector3.one, "Vehicle gameplay root was scaled.");
            for (int i = 0; i < growthTargets[index].Length; i++)
            {
                var target = growthTargets[index][i]; if (target == null) continue;
                Check(target.localScale == growthScales[index][i] && target.localPosition == growthPositions[index][i],
                    "Spawn growth did not restore the authored visual size/offset.");
            }
        }

        private static void VerifyAuthoredScale()
        {
            var prop = new GameObject("Nonuniform spawn root"); prop.transform.SetParent(root.transform);
            prop.transform.localScale = new Vector3(2, 3, 4);
            var collider = prop.AddComponent<BoxCollider>(); Vector3 collisionSize = collider.bounds.size;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube).transform; visual.SetParent(prop.transform, false);
            Vector3 position = new(1, 2, 3), scale = new(.5f, 1, 2);
            visual.localPosition = position; visual.localScale = scale;
            VoxelSpawnScaleIn.Play(prop); var effect = prop.GetComponent<VoxelSpawnScaleIn>();
            Check(visual.localScale == Vector3.zero && visual.localPosition == Vector3.zero, "Visual did not start at zero.");
            typeof(VoxelSpawnScaleIn).GetMethod("Advance", Private).Invoke(effect, new object[] { effect.duration * .5f });
            Check((visual.localScale - scale * .5f).sqrMagnitude < .00001f && (visual.localPosition - position * .5f).sqrMagnitude < .00001f,
                "Mid-growth size/offset lost authored proportions.");
            Check(prop.transform.localScale == new Vector3(2, 3, 4) && collider.bounds.size == collisionSize,
                "Spawn presentation changed the gameplay root/collider.");
            effect.enabled = false;
            Check(visual.localScale == scale && visual.localPosition == position, "Interrupted growth left a shrunken prop.");
            VoxelSpawnScaleIn.Play(prop); Check(effect.enabled && visual.localScale == Vector3.zero, "Disabled effect did not restart.");
            // A detached piece is now owned by debris; cleanup must not snap it back.
            visual.SetParent(root.transform, false); visual.localScale = Vector3.one; visual.localPosition = Vector3.one * 10;
            effect.enabled = false;
            Check(visual.localScale == Vector3.one && visual.localPosition == Vector3.one * 10, "Cleanup moved detached debris.");
            Object.Destroy(visual.gameObject); Object.Destroy(prop);
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
                for (int i = 0; i < 4; i++) spawn.Invoke(spawner, new object[] { null, tuning.spawnDistanceAhead });
                var cars = root.GetComponentsInChildren<VoxelObstacleCar>();
                Check(cars.Length == 4 && cars.Select(c => c.LaneOffset).Distinct().Count() == 4,
                    "Wave lost vehicles or bypassed occupied-lane clearance.");
                Check(cars.All(c => Mathf.Abs(c.TrackDistance - tuning.spawnDistanceAhead) < .001f),
                    "Traffic did not use the configured spawn distance.");
                foreach (var car in cars) car.transform.position = root.transform.position + new Vector3(car.LaneOffset, 0, tuning.spawnDistanceAhead);
                fades = cars.Select(c => c.GetComponent<VoxelFadeIn>()).ToArray();
                growth = cars.Select(c => c.GetComponent<VoxelSpawnScaleIn>()).ToArray();
                Check(growth.All(g => g != null), "Actual traffic spawns did not start scale-in.");
                Check(growth.All(g => Mathf.Approximately(g.duration, 1f)), "Spawn growth did not default to one second.");
                growthTargets = new Transform[4][]; growthScales = new Vector3[4][]; growthPositions = new Vector3[4][];
                renderers = new MeshRenderer[4][]; originals = new Material[4][]; colours = new Color[4][];
                var materials = new HashSet<Material>();
                for (int i = 0; i < 4; i++)
                {
                    fades[i].duration = i == 0 ? .2f : 2f;
                    growth[i].duration = fades[i].duration;
                    var visuals = ((IEnumerable)typeof(VoxelSpawnScaleIn).GetField("visuals", Private).GetValue(growth[i])).Cast<object>().ToArray();
                    growthTargets[i] = visuals.Select(v => (Transform)v.GetType().GetField("target").GetValue(v)).ToArray();
                    growthScales[i] = visuals.Select(v => (Vector3)v.GetType().GetField("scale").GetValue(v)).ToArray();
                    growthPositions[i] = visuals.Select(v => (Vector3)v.GetType().GetField("position").GetValue(v)).ToArray();
                    Check(visuals.Length > 0 && growthTargets[i].All(t => t.localScale == Vector3.zero), "Traffic visuals did not start at zero scale.");
                    var states = ((IEnumerable)typeof(VoxelFadeIn).GetField("renderers", Private).GetValue(fades[i])).Cast<object>().ToArray();
                    renderers[i] = states.Select(s => (MeshRenderer)s.GetType().GetField("renderer").GetValue(s)).ToArray();
                    originals[i] = states.Select(s => (Material)s.GetType().GetField("opaqueMaterial").GetValue(s)).ToArray();
                    colours[i] = states.Select(s => (Color)s.GetType().GetField("baseColor").GetValue(s)).ToArray();
                    foreach (var state in states) materials.Add((Material)state.GetType().GetField("fadeMaterial").GetValue(state));
                }
                sharedMaterials = materials.ToArray();
                VerifyAuthoredScale();
                Check(sharedMaterials.Length == originals.SelectMany(a => a).Distinct().Count(), "Live wave created per-voxel material copies.");
                Time.timeScale = 0; phase = -1;
                started = Time.realtimeSinceStartup; EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused || Time.realtimeSinceStartup - started < (phase < 0 ? .15f : phase == 0 ? .5f : 2.5f)) return;
            try
            {
                if (phase < 0)
                {
                    Check(growthTargets.SelectMany(t => t).All(t => t.localScale == Vector3.zero), "Growth advanced while the game clock was paused.");
                    Time.timeScale = 1; phase = 0; started = Time.realtimeSinceStartup; return;
                }
                if (phase == 0)
                {
                    VerifyRestored(0);
                    VerifyFullScale(0);
                    Check(growthTargets[1][0].localScale.sqrMagnitude > 0 &&
                        growthTargets[1][0].localScale.sqrMagnitude < growthScales[1][0].sqrMagnitude,
                        "Live growth did not advance smoothly between zero and full size.");
                    Check(renderers[1][0].sharedMaterial != originals[1][0] && renderers[1][0].sharedMaterial != null,
                        "One fade completion broke an overlapping fade.");
                    var block = new MaterialPropertyBlock(); renderers[1][0].GetPropertyBlock(block);
                    float alpha = block.GetColor("_BaseColor").a;
                    Check(alpha > 0 && alpha < 1, "Live fade did not advance opacity.");
                    fades[1].enabled = false; VerifyRestored(1);
                    fades[1].enabled = true;
                    growth[1].enabled = false; VerifyFullScale(1);
                    VoxelSpawnScaleIn.Play(growth[1].gameObject);
                    Check(growthTargets[1].All(t => t.localScale == Vector3.zero), "Re-enabled growth did not restart at zero.");
                    Object.Destroy(fades[2].gameObject); // Last-user cleanup also needs early destruction.
                    renderers[3][0].gameObject.SetActive(false); // Normal voxel removal while fading.
                    phase = 1; started = Time.realtimeSinceStartup;
                    return;
                }
                VerifyRestored(1); VerifyRestored(3);
                VerifyFullScale(1); VerifyFullScale(3);
                Check(sharedMaterials.All(m => m == null), "Completed/destroyed live fades leaked shared materials.");
                Finish("PASS (Play Mode): actual four-car wave chooses four clear lanes; visuals grow from zero to authored size on real frames while gameplay roots remain full size; paused clock freezes growth; nonuniform proportions/offsets restored; interrupted/restarted growth and detached debris handled; shared opacity/paint restoration and early destruction/inactive-voxel cleanup preserved. Temporary objects/cloned tuning removed; no source assets or purchases changed. Not an FPS benchmark.");
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
