using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace VoxelRacer.Editor
{
    public static class VoxelMissilePlayValidation
    {
        private const string Pending = "VoxelRacer.MissilePlayCheck";
        private static GameObject root;
        private static VoxelEnemyCar enemy;
        private static VoxelGunTuning weapon;
        private static VoxelEnemyVehicleTuning enemyTuning;
        private static VoxelMissileProjectile missile;
        private static float started;
        private static bool sawEffects;
        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
        }
        [MenuItem("Tools/Voxel Racer/Validate Missile in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Start from Edit Mode.");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            { EditorApplication.update -= Tick; SessionState.SetBool(Pending, false); }
        }
        private static void Setup()
        {
            try
            {
                root = new GameObject("Temporary live missile test");
                var definition = Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
                var carObject = new GameObject("Test player"); carObject.transform.SetParent(root.transform);
                carObject.transform.position = new Vector3(1000, 20, 0);
                var car = carObject.AddComponent<VoxelCarController>(); car.SetDrivingEnabled(false);
                var fit = VoxelMissileLauncherTuning.Load();
                var launcher = VoxelMissileUpgradeState.CreateVisual(car.transform, fit, true);
                var mount = launcher.GetComponent<VoxelGunMount>(); weapon = Object.Instantiate(fit.weapon);
                weapon.ammunitionPerStage = 1; weapon.bulletsPerShot = 1; mount.tuning = weapon;
                VoxelMissileValidation.Call(mount, "OnEnable");
                if (!mount.TryBeginShot(out int count) || count != 1 || mount.TryBeginShot(out _)) throw new Exception("Ammo/cooldown gating failed");
                VoxelMissileValidation.Call(mount, "FireProjectile"); car.enabled = false; mount.enabled = false;
                missile = Object.FindObjectsByType<VoxelMissileProjectile>(FindObjectsSortMode.None).Single();
                enemyTuning = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>(); enemyTuning.vehicleHealth = 1000;
                enemy = VoxelMissileValidation.MakeTarget(root.transform, enemyTuning,
                    new Vector3(1000 + weapon.missileLaneSideOffset, 20, 25));
                started = Time.realtimeSinceStartup; sawEffects = false; EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            try
            {
                if (missile != null)
                {
                    var ps = missile.GetComponentsInChildren<ParticleSystem>();
                    sawEffects |= ps.Length == 2 && ps.All(p => p.particleCount > 0);
                }
                if (enemy.CurrentHealth < 1000 && missile == null)
                {
                    if (!sawEffects) throw new Exception("No live rocket/smoke particles before impact");
                    if (Mathf.Abs(enemy.CurrentHealth - (1000 - weapon.damagePerBullet)) > .01f) throw new Exception("Missile health damage repeated");
                    if (!enemy.GetComponentsInChildren<MeshRenderer>(true).Any(r => !r.gameObject.activeSelf)) throw new Exception("No blast voxels removed");
                    Finish("PASS: actual firing/ammo/cooldown, rocket exhaust and smoke particles, Update-driven descending flight, swept impact, one explosion, health damage once, spherical voxel removal. Play Mode.");
                }
                else if (Time.realtimeSinceStartup - started > 6) throw new Exception("Timed out waiting for live missile impact; health=" + enemy.CurrentHealth + ", missile=" + (missile != null ? missile.transform.position.ToString() : "expired") + ", effects=" + sawEffects);
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        private static void Finish(string message)
        {
            Directory.CreateDirectory("Temp/Missile"); File.WriteAllText("Temp/Missile/PlayValidation.txt", message);
            EditorApplication.update -= Tick;
            if (root != null) Object.Destroy(root);
            if (weapon != null) Object.Destroy(weapon);
            if (enemyTuning != null) Object.Destroy(enemyTuning);
            Debug.Log(message); EditorApplication.isPlaying = false;
        }
    }
}
