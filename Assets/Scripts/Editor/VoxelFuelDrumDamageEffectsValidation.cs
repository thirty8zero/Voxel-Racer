using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Exercises real projectile hits and frame-driven effects on temporary barrels.</summary>
    public static class VoxelFuelDrumDamageEffectsValidation
    {
        private const string Pending = "VoxelRacer.FuelDrumDamageEffectsCheck";
        private static VoxelFuelDrumObstacle obstacle;
        private static VoxelStaticObstacleDefinition tuning;
        private static Transform[] drums;
        private static float started;
        private static bool savedBackground;
        private static float savedTimeScale;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Validate Fuel Drum Damage Effects in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start barrel validation from Edit Mode.");
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(Pending, false);
            }
        }

        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        private static void Hit(Transform drum, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var voxel = drum.GetComponentsInChildren<MeshRenderer>()
                    .First(r => r.gameObject.activeInHierarchy).transform;
                obstacle.TakeProjectileHit(voxel, voxel.position, Vector3.forward);
            }
        }

        private static VoxelVehicleDamageEffects Verify(Transform drum, bool smoke, bool fire)
        {
            var effects = drum.GetComponent<VoxelVehicleDamageEffects>();
            if (effects == null)
            {
                Check(!smoke && !fire, "Missing damaged-barrel effects.");
                return null;
            }
            Check(effects.SmokeActive == smoke && effects.FireActive == fire, "Wrong per-barrel health threshold.");
            var particles = drum.GetComponentsInChildren<ParticleSystem>();
            Check(particles.Length == 2, "Repeated hits created duplicate effects.");
            foreach (var ps in particles)
            {
                bool expected = ps.name == "Damage Smoke" ? smoke : fire;
                Check(ps.emission.enabled == expected, "Particle emission disagrees with health.");
                if (ps.name == "Damage Smoke")
                    Check(ps.colorOverLifetime.color.gradient.colorKeys.All(k => k.color.r == 0 && k.color.g == 0 && k.color.b == 0),
                        "Barrel smoke must remain black throughout its lifetime.");
                Check(ps.main.simulationSpace == ParticleSystemSimulationSpace.World, "Effects lost world-space simulation.");
                Check(ps.GetComponent<Collider>() == null, "Particles added projectile colliders.");
                Check(ps.transform.localPosition.z == 0 && ps.transform.localPosition.y >= .8f,
                    "Emitter is not over the barrel.");
                if (expected)
                {
                    ps.Simulate(.3f, false, true, false);
                    Check(ps.particleCount > 0, "Enabled effect failed to emit particles.");
                    ps.Play(false);
                }
            }
            return effects;
        }

        private static void Setup()
        {
            savedBackground = Application.runInBackground;
            savedTimeScale = Time.timeScale;
            Application.runInBackground = true;
            Time.timeScale = 1;
            try
            {
                tuning = Object.Instantiate(Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/FuelDrums"));
                tuning.hitPoints = 100;
                obstacle = new GameObject("Temporary barrel effects QA").AddComponent<VoxelFuelDrumObstacle>();
                obstacle.transform.position = new Vector3(10000, 20, 0);
                // Keep the temporary group stationary without creating a gameplay player.
                obstacle.enabled = false;
                obstacle.Configure(null, null, tuning, 0, 0);
                drums = obstacle.GetComponentsInChildren<Transform>().Where(t => t.name == "Fuel Drum").ToArray();
                Check(drums.Length == 3 && obstacle.GetComponentsInChildren<ParticleSystem>().Length == 0,
                    "Healthy barrels should not allocate particle systems.");
                Hit(drums[0], 24); Verify(drums[0], false, false); // 76% remaining.
                Hit(drums[0], 1); Verify(drums[0], true, false); // Exactly 75%.
                Hit(drums[1], 49); Verify(drums[1], true, false); // 51% remaining.
                Hit(drums[1], 1); Verify(drums[1], true, true); // Exactly 50%.
                Verify(drums[0], true, false); Verify(drums[2], false, false);
                started = Time.realtimeSinceStartup;
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused || Time.realtimeSinceStartup - started < .3f) return;
            try
            {
                // Real LateUpdate frames must not replace externally supplied barrel health.
                Verify(drums[0], true, false); Verify(drums[1], true, true); Verify(drums[2], false, false);
                Hit(drums[1], 49); Verify(drums[1], true, true); // 1% remaining.
                var effects = obstacle.GetComponentsInChildren<VoxelVehicleDamageEffects>();
                Hit(drums[1], 1); // Existing group detonation at zero health.
                Check((bool)typeof(VoxelFuelDrumObstacle).GetField("hasExploded", Private).GetValue(obstacle),
                    "Lethal hit no longer detonates the barrels.");
                Check(effects.All(e => !e.enabled && !e.SmokeActive && !e.FireActive), "Effects continued after detonation.");
                Check(effects.SelectMany(e => e.GetComponentsInChildren<ParticleSystem>()).All(p => p.particleCount == 0 && !p.isPlaying),
                    "Damage particles were not cleared at detonation.");
                Check(drums.All(d => d.GetComponentsInChildren<MeshRenderer>().Length == 0),
                    "Damage effects prevented barrel disintegration.");
                Finish("PASS (Play Mode): real per-barrel projectile damage; no effects above 75%; black smoke exactly at 75%, fire exactly at 50%; independent healthy/smoking/burning barrels; actual emission and world-space placement; sustained LateUpdate state; no duplicate systems or effect colliders; lethal-hit detonation, complete disintegration and immediate smoke/fire cleanup.");
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Finish(string message)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/FuelDrumDamageEffectsValidation.txt", message);
            EditorApplication.update -= Tick;
            if (obstacle != null) Object.Destroy(obstacle.gameObject);
            if (tuning != null) Object.Destroy(tuning);
            Application.runInBackground = savedBackground;
            Time.timeScale = savedTimeScale;
            Debug.Log(message);
            EditorApplication.isPlaying = false;
        }
    }
}
