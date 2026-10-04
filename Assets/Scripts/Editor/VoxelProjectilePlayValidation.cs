using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Real frame-driven bullet dispatch and registry lifecycle, with temporary targets.</summary>
    public static class VoxelProjectilePlayValidation
    {
        private const string Pending = "VoxelRacer.BulletPlayCheck";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameObject root;
        private static VoxelEnemyCar enemy, occluded;
        private static VoxelObstacleCar traffic;
        private static VoxelObstacle crate;
        private static VoxelFuelDrumObstacle drums;
        private static VoxelGunTuning weapon;
        private static float started, savedScale;
        private static bool savedBackground;
        private static int phase, crateHealth, drumHealth;
        private static readonly List<Object> tunings = new();

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Validate Bullet Targets in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start from Edit Mode.");
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
                Application.runInBackground = savedBackground;
                Time.timeScale = savedScale;
                SessionState.SetBool(Pending, false);
            }
        }

        private static T Clone<T>(T value) where T : Object
        { var clone = Object.Instantiate(value); tunings.Add(clone); return clone; }

        private static GameObject Target(string name, float x)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(root.transform, false);
            obj.transform.localPosition = new Vector3(x, 0, 12);
            return obj;
        }

        private static void Shoot(float x)
        { VoxelProjectile.Create(root.transform.position + new Vector3(x, .7f, 0), Vector3.forward, weapon); }

        private static int DrumHealth() => ((Dictionary<Transform, int>)typeof(VoxelFuelDrumObstacle)
            .GetField("drumHealth", Private).GetValue(drums)).Values.Sum();

        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        private static void Setup()
        {
            savedBackground = Application.runInBackground; savedScale = Time.timeScale;
            Application.runInBackground = true; Time.timeScale = 1;
            phase = 0; tunings.Clear();
            try
            {
                root = new GameObject("Temporary live bullet target QA");
                root.transform.position = new Vector3(10000, 20, 0);
                var player = root.AddComponent<VoxelCarController>();
                player.enabled = false; player.topSpeed = 0;
                var trafficTuning = Clone(Resources.Load<VoxelObstacleCarTuning>("FallbackTrafficTuning"));
                var enemyTuning = Clone(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning"));
                enemyTuning.vehicleHealth = 1000; enemyTuning.voxelHealth = 1000;
                enemy = Target("QA enemy", 0).AddComponent<VoxelEnemyCar>();
                enemy.Configure(player, trafficTuning, enemyTuning, null, 50, 0);
                traffic = Target("QA traffic", 20).AddComponent<VoxelObstacleCar>();
                traffic.Configure(player, trafficTuning, true, null, 50, 20, 0, enemyTuning);
                crate = Target("QA crate", 40).AddComponent<VoxelObstacle>();
                var crateDefinition = Clone(Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/VoxelBox"));
                crate.Configure(player, null, crateDefinition, 50, 40);
                crateHealth = (int)typeof(VoxelObstacle).GetField("currentHealth", Private).GetValue(crate);
                drums = Target("QA drums", 60).AddComponent<VoxelFuelDrumObstacle>();
                var drumDefinition = Clone(Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/FuelDrums"));
                drums.Configure(player, null, drumDefinition, 50, 60); drumHealth = DrumHealth();
                occluded = Target("QA occluded enemy", 80).AddComponent<VoxelEnemyCar>();
                occluded.Configure(player, trafficTuning, enemyTuning, null, 50, 80);
                var wall = new GameObject("QA closer physics wall");
                wall.transform.SetParent(root.transform, false);
                wall.transform.localPosition = new Vector3(80, 1, 6);
                wall.AddComponent<BoxCollider>().size = new Vector3(4, 4, 1);
                weapon = Clone(Resources.Load<VoxelGunTuning>("Weapons/BasicHoodGunTuning"));
                weapon.spreadDegrees = 0; weapon.damagePerBullet = 1;
                Shoot(0); Shoot(20); Shoot(40); Shoot(60); Shoot(80);
                started = Time.realtimeSinceStartup;
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused || Time.realtimeSinceStartup - started < .8f) return;
            try
            {
                if (phase == 0)
                {
                    Check(enemy.CurrentHealth == 999 && traffic.CurrentHealth == 999, "Real bullet failed to damage collider-free enemy/traffic.");
                    Check((int)typeof(VoxelObstacle).GetField("currentHealth", Private).GetValue(crate) == crateHealth - 1,
                        "Real bullet failed to damage crate.");
                    Check(DrumHealth() == drumHealth - 1, "Real bullet failed to damage fuel drums.");
                    Check(occluded.CurrentHealth == 1000, "Bullet pierced closer physics wall.");
                    enemy.gameObject.SetActive(false); traffic.gameObject.SetActive(false);
                    Shoot(0); Shoot(20);
                    phase = 1; started = Time.realtimeSinceStartup;
                    return;
                }
                if (phase == 1)
                {
                    Check(enemy.CurrentHealth == 999 && traffic.CurrentHealth == 999, "Disabled targets absorbed bullets.");
                    enemy.gameObject.SetActive(true); traffic.gameObject.SetActive(true);
                    Shoot(0); Shoot(20);
                    phase = 2; started = Time.realtimeSinceStartup;
                    return;
                }
                Check(enemy.CurrentHealth == 998 && traffic.CurrentHealth == 998, "Re-enabled targets were not re-registered.");
                Finish("PASS (Play Mode): real moving bullets damage enemy, traffic, crate and fuel drums once; closer physics wall blocks fallback; disabled targets ignore shots and re-enabled targets receive damage again. Temporary objects/tuning clones removed; no purchases or source assets changed. This is a gameplay regression, not an FPS benchmark.");
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Finish(string report)
        {
            EditorApplication.update -= Tick;
            Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/BulletTargetPlayValidation.txt", report);
            if (root != null) Object.Destroy(root);
            foreach (var tuning in tunings) if (tuning != null) Object.Destroy(tuning);
            tunings.Clear();
            Application.runInBackground = savedBackground; Time.timeScale = savedScale;
            Debug.Log(report);
            EditorApplication.isPlaying = false;
        }
    }
}
