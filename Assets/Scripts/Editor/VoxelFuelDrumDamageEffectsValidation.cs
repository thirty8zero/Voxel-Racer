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
        private static VoxelMissionProgress mission, previousMission;
        private static VoxelMissionTuning missionTuning;
        private static Camera previousCamera, camera;
        private static UnityEngine.Random.State savedRandom;
        private static HashSet<VoxelScorePopup> previousPopups;
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

        private static void Hit(Transform drum, int count, VoxelScorePopup.WeaponSource source = VoxelScorePopup.WeaponSource.DefaultGun)
        {
            for (int i = 0; i < count; i++)
            {
                var voxel = drum.GetComponentsInChildren<MeshRenderer>()
                    .First(r => r.gameObject.activeInHierarchy).transform;
                obstacle.TakeProjectileHit(voxel, voxel.position, Vector3.forward, source);
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
            if (VoxelLoadingScreen.IsLoading) { EditorApplication.delayCall += Setup; return; }
            savedBackground = Application.runInBackground;
            savedTimeScale = Time.timeScale;
            Application.runInBackground = true;
            Time.timeScale = 1;
            try
            {
                savedRandom = UnityEngine.Random.state;
                previousPopups = Resources.FindObjectsOfTypeAll<VoxelScorePopup>().ToHashSet();
                previousMission = VoxelMissionProgress.Active;
                missionTuning = Object.Instantiate(VoxelMissionTuning.Load());
                missionTuning.requiredPoints = int.MaxValue;
                missionTuning.maximumTimeMultiplier = 100f;
                mission = new GameObject("Temporary barrel feedback mission").AddComponent<VoxelMissionProgress>();
                mission.enabled = false;
                mission.Configure(missionTuning);
                typeof(VoxelMissionProgress).GetProperty("Active").SetValue(null, mission);
                tuning = Object.Instantiate(Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/FuelDrums"));
                tuning.hitPoints = 100;
                obstacle = new GameObject("Temporary barrel effects QA").AddComponent<VoxelFuelDrumObstacle>();
                obstacle.transform.position = new Vector3(10000, 20, 0);
                // Keep the temporary group stationary without creating a gameplay player.
                obstacle.enabled = false;
                obstacle.Configure(null, null, tuning, 0, 0);
                previousCamera = Camera.main;
                if (previousCamera != null) previousCamera.tag = "Untagged";
                camera = new GameObject("Temporary barrel feedback camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.depth = 100;
                camera.transform.position = obstacle.transform.position + new Vector3(-5f, 5f, -9f);
                camera.transform.LookAt(obstacle.transform.position + Vector3.up * 1.2f);
                drums = obstacle.GetComponentsInChildren<Transform>().Where(t => t.name == "Fuel Drum").ToArray();
                Check(drums.Length == 3 && obstacle.GetComponentsInChildren<ParticleSystem>().Length == 0,
                    "Healthy barrels should not allocate particle systems.");
                for (int level = 1; level <= 4; level++)
                {
                    Hit(drums[0], 1, (VoxelScorePopup.WeaponSource)level);
                    VerifyPopup("+1", (VoxelScorePopup.WeaponSource)level);
                }
                Hit(drums[0], 20); Verify(drums[0], false, false); // 76% remaining.
                VerifyPopup("+1", VoxelScorePopup.WeaponSource.DefaultGun);
                Check(mission.Points == 0, "Barrel damage feedback awarded unintended hit score.");
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
                const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
                var white = (GUIStyle)typeof(VoxelScorePopup).GetField("fuelDrumDamageStyle", Static).GetValue(null);
                var rarity = (GUIStyle[])typeof(VoxelScorePopup).GetField("rarityFuelDrumStyles", Static).GetValue(null);
                if (white == null || rarity.Any(style => style == null))
                {
                    Check(Time.realtimeSinceStartup - started < 5f, "Barrel damage labels did not reach their real draw path.");
                    return;
                }
                Check(white.fontSize == 48 && white.normal.textColor == Color.white &&
                    rarity.Select((style, index) => style.fontSize == 48 && style.normal.textColor == VoxelUpgradeRarity.ForLevel(index + 1)).All(value => value),
                    "Barrel numbers did not render at 80% enemy size with the correct weapon colours.");
                // Real LateUpdate frames must not replace externally supplied barrel health.
                Verify(drums[0], true, false); Verify(drums[1], true, true); Verify(drums[2], false, false);
                Hit(drums[1], 49); Verify(drums[1], true, true); // 1% remaining.
                var effects = obstacle.GetComponentsInChildren<VoxelVehicleDamageEffects>();
                float multiplier = mission.EffectiveTimeBonusMultiplier;
                Hit(drums[1], 1); // Existing group detonation at zero health.
                Check(Mathf.Abs(mission.EffectiveTimeBonusMultiplier - multiplier - .1f) < .0001f,
                    "Barrel destruction did not add exactly 0.1 multiplier.");
                Check(mission.Points == missionTuning.fuelDrumDestroyedPoints, "Damage feedback changed the group score reward.");
                Check((bool)typeof(VoxelFuelDrumObstacle).GetField("hasExploded", Private).GetValue(obstacle),
                    "Lethal hit no longer detonates the barrels.");
                Check(effects.All(e => !e.enabled && !e.SmokeActive && !e.FireActive), "Effects continued after detonation.");
                Check(effects.SelectMany(e => e.GetComponentsInChildren<ParticleSystem>()).All(p => p.particleCount == 0 && !p.isPlaying),
                    "Damage particles were not cleared at detonation.");
                Check(drums.All(d => d.GetComponentsInChildren<MeshRenderer>().Length == 0),
                    "Damage effects prevented barrel disintegration.");
                VerifyFractionalFeedback();
                VerifyWeaponTierDispatch();
                VoxelMissileObstacleValidation.Run();
                Finish("PASS (Play Mode): actual 48px white/starter and green/blue/purple/gold tiered barrel damage labels (80% of enemy size); real bullet and missile dispatch preserve the launch tier after tuning changes; fractional missile feedback, clamped lethal damage, no added hit score, exactly +0.1 destruction multiplier and one group score reward; independent barrel health/effects, exact smoke/fire thresholds, live emission and placement, cleanup and no duplicate rewards. Existing missile obstacle suite also passed. Temporary mission/camera/popups, tuning and RNG restored.");
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void VerifyPopup(string text, VoxelScorePopup.WeaponSource source)
        {
            var popups = (List<VoxelScorePopup>)typeof(VoxelScorePopup).GetField("ActiveMissionPopups",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var popup = popups.Last(p => (VoxelScorePopup.Style)typeof(VoxelScorePopup).GetField("displayStyle", Private).GetValue(p) == VoxelScorePopup.Style.FuelDrumDamage);
            Check((string)typeof(VoxelScorePopup).GetField("displayText", Private).GetValue(popup) == text &&
                (VoxelScorePopup.WeaponSource)typeof(VoxelScorePopup).GetField("weaponSource", Private).GetValue(popup) == source,
                "Barrel damage number lost its amount or weapon identity.");
            Check(!(bool)typeof(VoxelScorePopup).GetProperty("FliesToMission", Private).GetValue(popup),
                "Barrel health damage implied an extra mission score award.");
        }

        private static void VerifyFractionalFeedback()
        {
            var extra = new GameObject("Temporary missile barrel feedback").AddComponent<VoxelFuelDrumObstacle>();
            extra.enabled = false;
            extra.transform.position = obstacle.transform.position + Vector3.right * 15f;
            extra.Configure(null, null, tuning, 0, 0);
            try
            {
                var drum = extra.transform.Cast<Transform>().First(t => t.name == "Fuel Drum");
                extra.TakeMissileBlast(drum, drum.position, 0f, .5f, Vector3.forward, Array.Empty<MeshRenderer>(), true);
                VerifyPopup("+0.5", VoxelScorePopup.WeaponSource.OtherWeapon);
                extra.TakeMissileBlast(drum, drum.position, 0f, 35f, Vector3.forward, Array.Empty<MeshRenderer>(), true);
                VerifyPopup("+35", VoxelScorePopup.WeaponSource.OtherWeapon);
                extra.TakeMissileBlast(drum, drum.position, 0f, 1000f, Vector3.forward, Array.Empty<MeshRenderer>(), true);
                VerifyPopup("+64.5", VoxelScorePopup.WeaponSource.OtherWeapon);
                int points = mission.Points;
                float multiplier = mission.EffectiveTimeBonusMultiplier;
                extra.TakeMissileBlast(drum, drum.position, 0f, 1000f, Vector3.forward, Array.Empty<MeshRenderer>(), true);
                Check(mission.Points == points && mission.EffectiveTimeBonusMultiplier == multiplier, "A destroyed barrel awarded twice.");
            }
            finally { Object.Destroy(extra.gameObject); }
        }

        private static void VerifyWeaponTierDispatch()
        {
            var root = new GameObject("Temporary weapon tier dispatch");
            var gun = Object.Instantiate(Resources.Load<VoxelGunTuning>("Weapons/LongBarrelHoodGunTuning"));
            var missileTuning = Object.Instantiate(Resources.Load<VoxelGunTuning>("Weapons/MissileWeaponTuning"));
            try
            {
                var barrel = new GameObject("Tier target").AddComponent<VoxelFuelDrumObstacle>();
                barrel.transform.SetParent(root.transform);
                barrel.transform.position = obstacle.transform.position + Vector3.right * 30f;
                barrel.enabled = false;
                barrel.Configure(null, null, tuning, 0, 0);
                gun.upgradeLevel = 4; gun.projectileSpeed = 2000f; gun.maximumRange = 50f;
                var bullet = VoxelProjectile.Create(barrel.transform.position + new Vector3(0f, .7f, -10f), Vector3.forward, gun);
                bullet.enabled = false;
                bullet.transform.SetParent(root.transform);
                gun.upgradeLevel = 1;
                Physics.SyncTransforms();
                typeof(VoxelProjectile).GetMethod("Update", Private).Invoke(bullet, null);
                VerifyPopup("+1", VoxelScorePopup.WeaponSource.Level4);

                var owner = new GameObject("Tier missile owner").AddComponent<VoxelCarController>();
                owner.transform.SetParent(root.transform);
                owner.enabled = false;
                owner.transform.position = barrel.transform.position + Vector3.back * 10f;
                missileTuning.upgradeLevel = 3; missileTuning.areaOfEffectRadius = 0f;
                var missile = VoxelMissileProjectile.Create(owner.transform.position + Vector3.up, owner, 1f, missileTuning);
                missile.enabled = false;
                missile.transform.SetParent(root.transform);
                missileTuning.upgradeLevel = 1;
                var target = barrel.transform.Cast<Transform>().First(t => t.name == "Fuel Drum").GetComponent<VoxelMissileTarget>();
                typeof(VoxelMissileProjectile).GetMethod("Detonate", Private).Invoke(missile,
                    new object[] { target.transform.position, Vector3.forward, target, null, true });
                VerifyPopup($"+{missileTuning.damagePerBullet:0.##}", VoxelScorePopup.WeaponSource.Level3);
            }
            finally { Object.Destroy(root); Object.Destroy(gun); Object.Destroy(missileTuning); }
        }

        private static void Finish(string message)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/FuelDrumDamageEffectsValidation.txt", message);
            EditorApplication.update -= Tick;
            if (obstacle != null) Object.Destroy(obstacle.gameObject);
            if (tuning != null) Object.Destroy(tuning);
            if (mission != null) Object.Destroy(mission.gameObject);
            if (missionTuning != null) Object.Destroy(missionTuning);
            typeof(VoxelMissionProgress).GetProperty("Active").SetValue(null, previousMission);
            if (camera != null) Object.Destroy(camera.gameObject);
            if (previousCamera != null) previousCamera.tag = "MainCamera";
            foreach (var popup in Resources.FindObjectsOfTypeAll<VoxelScorePopup>())
                if (previousPopups != null && !previousPopups.Contains(popup)) Object.Destroy(popup.gameObject);
            UnityEngine.Random.state = savedRandom;
            Application.runInBackground = savedBackground;
            Time.timeScale = savedTimeScale;
            Debug.Log(message);
            EditorApplication.isPlaying = false;
        }
    }
}
