using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Runs inside the existing missile Play Mode check, with isolated targets and cloned tuning.</summary>
    public static class VoxelMissileObstacleValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, Private).GetValue(target);
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
        private static void Equal(float actual, float expected, string message) =>
            Check(Mathf.Abs(actual - expected) < .001f, message + $": expected {expected}, got {actual}.");
        private static float Health(VoxelObstacle crate) => Field<float>(crate, "currentHealth");
        private static Dictionary<Transform, float> Health(VoxelFuelDrumObstacle drums) =>
            Field<Dictionary<Transform, float>>(drums, "drumHealth");

        public static void Run()
        {
            Check(Application.isPlaying, "Obstacle missile checks require Play Mode.");
            var previousScene = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("Temporary missile obstacle QA");
            var random = UnityEngine.Random.state;
            var previousMission = VoxelMissionProgress.Active;
            var assets = new List<Object>();
            var activeProperty = typeof(VoxelMissionProgress).GetProperty("Active");
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("Missile obstacle fixtures").transform;
                root.position = new Vector3(10000, 20, 0);
                var missionTuning = ScriptableObject.CreateInstance<VoxelMissionTuning>(); assets.Add(missionTuning);
                missionTuning.requiredPoints = int.MaxValue;
                missionTuning.maximumTimeMultiplier = 100f;
                var mission = Child(root, "Temporary mission").AddComponent<VoxelMissionProgress>();
                mission.enabled = false;
                mission.Configure(missionTuning);
                activeProperty.SetValue(null, mission);
                var weapon = Object.Instantiate(Resources.Load<VoxelGunTuning>("Weapons/MissileWeaponTuning"));
                assets.Add(weapon);
                var drumTuning = Object.Instantiate(Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/FuelDrums"));
                assets.Add(drumTuning);
                drumTuning.hitPoints = 50;
                var crateTuning = Object.Instantiate(Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/VoxelBox"));
                assets.Add(crateTuning);
                crateTuning.hitPoints = 100;
                crateTuning.modelPrefab = null;

                CheckCrateBlast(root, crateTuning, weapon);
                CheckDrumBlast(root, drumTuning, weapon, mission);
                CheckSharedBlast(root, crateTuning, drumTuning, weapon, assets);
                CheckAuthoredCrate(root, weapon, mission);
                foreach (int fps in new[] { 30, 60, 120 })
                foreach (float yaw in new[] { 0f, 65f, -95f })
                    CheckFlight(root, crateTuning, drumTuning, weapon, fps, yaw);
                CheckExpiryAndWall(root, crateTuning, weapon);

                const string report = "PASS (Play Mode): configured damage once per crate/drum; 50-health barrel " +
                    "survives 35 damage with 15 remaining; fractional and zero damage; spherical carving; " +
                    "protected/hidden/outside voxels; independent drum health and smoke/fire; shared yellow-to-red barrel health outline; multi-drum/car/crate splash; " +
                    "cached hollow-target direct hits; actual authored crate destruction; one barrel-group reward and " +
                    "no repeat rewards; real missile Advance against collider-free props at 30/60/120 FPS and rotated " +
                    "headings; nearest target, disabled/re-enabled targets, wall obstruction and range-expiry splash. " +
                    "Cloned tuning, temporary runtime scene, mission/random state restored; editor scene unchanged.";
                Directory.CreateDirectory("Temp/Missile");
                File.WriteAllText("Temp/Missile/ObstacleValidation.txt", report);
                Debug.Log(report);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("Temp/Missile");
                File.WriteAllText("Temp/Missile/ObstacleValidation.txt", "FAIL: " + error);
                throw;
            }
            finally
            {
                activeProperty.SetValue(null, previousMission);
                SceneManager.SetActiveScene(previousScene);
                foreach (var asset in assets) Object.Destroy(asset);
                SceneManager.UnloadSceneAsync(scene);
                UnityEngine.Random.state = random;
            }
        }

        private static GameObject Child(Transform parent, string name, Vector3 position = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            return go;
        }
        private static VoxelObstacle Crate(Transform parent, VoxelStaticObstacleDefinition tuning, Vector3 position)
        {
            var crate = Child(parent, "Crate", position).AddComponent<VoxelObstacle>();
            crate.enabled = false; crate.Configure(null, null, tuning, 0, 0);
            RemovePendingColliders(crate.gameObject);
            return crate;
        }
        private static VoxelFuelDrumObstacle Drums(Transform parent, VoxelStaticObstacleDefinition tuning, Vector3 position)
        {
            var drums = Child(parent, "Drums", position).AddComponent<VoxelFuelDrumObstacle>();
            drums.enabled = false; drums.Configure(null, null, tuning, 0, 0);
            RemovePendingColliders(drums.gameObject);
            return drums;
        }
        private static VoxelMissileTarget Trace(Vector3 from, Vector3 direction, float length, out Vector3 point)
        {
            Check(VoxelMissileTarget.Trace(from, direction, length, out var hit, out _, out point, out _, 1.5f),
                "Collider-free missile trace missed its target.");
            return hit;
        }
        private static void Blast(VoxelMissileTarget target, Vector3 point, float radius, float damage) =>
            VoxelMissileTarget.Blast(point, radius, damage, Vector3.forward, target);

        private static void CheckCrateBlast(Transform root, VoxelStaticObstacleDefinition tuning, VoxelGunTuning weapon)
        {
            var crate = Crate(root, tuning, Vector3.zero);
            var target = Trace(crate.transform.position + new Vector3(0, .65f, -10), Vector3.forward, 15, out var point);
            Check(target.gameObject == crate.gameObject, "Crate did not register as a missile target.");
            var renderers = crate.GetComponentsInChildren<MeshRenderer>();
            var protectedPiece = renderers.First(r => VoxelSphericalBlast.Contains(r, point, weapon.areaOfEffectRadius));
            protectedPiece.gameObject.AddComponent<VoxelIndestructiblePart>();
            var hiddenPiece = renderers.First(r => r != protectedPiece && VoxelSphericalBlast.Contains(r, point, weapon.areaOfEffectRadius));
            hiddenPiece.enabled = false;
            var outside = Child(crate.transform, "Outside blast", new Vector3(20, 0, 0));
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(outside.transform, false); Object.Destroy(cube.GetComponent<Collider>());
            int before = renderers.Count(r => r.gameObject.activeSelf);
            Blast(target, point, weapon.areaOfEffectRadius, weapon.damagePerBullet);
            Equal(Health(crate), 100 - weapon.damagePerBullet, "Crate missile damage multiplied by voxel count");
            Check(before - renderers.Count(r => r.gameObject.activeSelf) > 1, "Blast did not carve multiple crate voxels.");
            Check(protectedPiece.gameObject.activeSelf && hiddenPiece.gameObject.activeSelf && cube.activeSelf,
                "Blast removed protected, renderer-hidden or outside voxels.");
            Blast(target, point, 0f, .5f);
            Equal(Health(crate), 99.5f - weapon.damagePerBullet, "Fractional direct missile damage was rounded");
            Blast(target, point, 10f, 0f);
            Equal(Health(crate), 99.5f - weapon.damagePerBullet, "Zero-damage blast changed health");
            crate.gameObject.SetActive(false);
            Check(!VoxelMissileTarget.Trace(crate.transform.position + new Vector3(0, .65f, -10), Vector3.forward, 15,
                out _, out _, out _, out _), "Disabled crate remained targetable.");
            crate.gameObject.SetActive(true);
            Check(Trace(crate.transform.position + new Vector3(0, .65f, -10), Vector3.forward, 15, out _) == target,
                "Re-enabled crate did not re-register.");
        }

        private static void CheckDrumBlast(Transform root, VoxelStaticObstacleDefinition tuning,
            VoxelGunTuning weapon, VoxelMissionProgress mission)
        {
            var drums = Drums(root, tuning, new Vector3(30, 0, 0));
            var bar = drums.GetComponentInChildren<VoxelEnemyHealthBar>(true);
            Check(bar != null && !bar.gameObject.activeSelf, "Barrel health bar should remain hidden until damaged.");
            var outline = Field<Material>(bar, "outlineMaterial");
            Check(outline != null, "Barrel health bar has no shared enemy-style outline.");
            Color yellow = new Color(1f, .92f, 0f);
            Check(outline.color == yellow, "Full-health barrel outline is not yellow.");
            var frame = bar.transform.Find("Health Outline");
            Vector3 frameScale = frame.localScale;
            bar.SetHealth(.5f);
            Check(outline.color == yellow, "Barrel outline changed before the half-health threshold.");
            bar.SetHealth(.25f);
            Check(outline.color == Color.Lerp(Color.red, yellow, .5f), "Quarter-health barrel outline did not turn orange.");
            bar.SetHealth(0f);
            Check(outline.color == Color.red && frame.localScale == frameScale,
                "Empty barrel outline is not red or lost its full-size frame.");
            bar.SetHealth(1f);
            Check(Field<Material>(bar, "outlineMaterial") == outline, "Barrel health changes replaced the owned material.");
            var target = Trace(drums.transform.position + new Vector3(0, .65f, -10), Vector3.forward, 15, out var point);
            Check(target.transform.parent == drums.transform, "Missile trace did not select an individual drum.");
            var touched = Health(drums).Keys.ToDictionary(d => d,
                d => d == target.transform || d.GetComponentsInChildren<MeshRenderer>().Any(r => VoxelSphericalBlast.Contains(r, point, weapon.areaOfEffectRadius)));
            Blast(target, point, weapon.areaOfEffectRadius, weapon.damagePerBullet);
            foreach (var pair in Health(drums)) Equal(pair.Value, touched[pair.Key] ? 50 - weapon.damagePerBullet : 50,
                "Blast did not respect independent drum health/radius");
            Check(bar.gameObject.activeSelf, "Damaged barrel health bar did not appear.");
            Color expectedOutline = Color.Lerp(Color.red, yellow, Mathf.Clamp01((50 - weapon.damagePerBullet) / 25f));
            Check(outline.color == expectedOutline, "Actual barrel damage did not update the health outline.");
            var effects = target.GetComponent<VoxelVehicleDamageEffects>();
            Check(effects != null && effects.SmokeActive && effects.FireActive, "Missile damage did not update barrel smoke/fire.");
            Blast(target, point, 0f, .5f);
            Equal(Health(drums)[target.transform], 49.5f - weapon.damagePerBullet, "Fractional drum damage was rounded");
            int score = mission.Points;
            float multiplier = mission.EffectiveTimeBonusMultiplier;
            Blast(target, point, 10f, weapon.damagePerBullet);
            Check(!drums.IsRoadHazard, "Second 35-damage missile failed to detonate a 50-health barrel.");
            Equal(mission.Points - score, mission.Tuning.fuelDrumDestroyedPoints, "Barrel group score was duplicated");
            Equal(mission.EffectiveTimeBonusMultiplier - multiplier, mission.Tuning.barrelMultiplier, "Barrel multiplier was duplicated");
            Check(drums.GetComponentsInChildren<VoxelVehicleDamageEffects>().All(e => !e.FireActive && !e.SmokeActive),
                "Lethal missile left barrel damage effects running.");
            var random = UnityEngine.Random.state;
            Blast(target, point, 10f, weapon.damagePerBullet);
            Equal(mission.Points - score, mission.Tuning.fuelDrumDestroyedPoints, "Destroyed group awarded score again");
            Check(random.Equals(UnityEngine.Random.state), "Destroyed group ran destruction/reward effects again.");
        }

        private static void CheckSharedBlast(Transform root, VoxelStaticObstacleDefinition crateTuning,
            VoxelStaticObstacleDefinition drumTuning, VoxelGunTuning weapon, List<Object> assets)
        {
            var fixture = Child(root, "Shared blast", new Vector3(60, 0, 0)).transform;
            var crate = Crate(fixture, crateTuning, Vector3.zero);
            var drums = Drums(fixture, drumTuning, new Vector3(3, 0, 0));
            var enemyTuning = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>(); assets.Add(enemyTuning);
            enemyTuning.vehicleHealth = 1000;
            var car = VoxelMissileValidation.MakeTarget(fixture, enemyTuning, fixture.position + new Vector3(-3, 0, 0));
            Blast(null, fixture.position, 10f, weapon.damagePerBullet);
            Equal(Health(crate), 100 - weapon.damagePerBullet, "Shared blast missed crate");
            Equal(car.CurrentHealth, 1000 - weapon.damagePerBullet, "Shared blast changed vehicle damage");
            foreach (float health in Health(drums).Values) Equal(health, 50 - weapon.damagePerBullet,
                "Shared blast applied damage more than once per drum");
            var direct = Trace(drums.transform.position + new Vector3(0, .65f, -10), Vector3.forward, 15, out var point);
            float before = Health(drums)[direct.transform];
            Blast(direct, point, .01f, .5f);
            Equal(Health(drums)[direct.transform], before - .5f, "Direct hit leaked through a fully carved drum");
        }

        private static void CheckAuthoredCrate(Transform root, VoxelGunTuning weapon, VoxelMissionProgress mission)
        {
            var tuning = Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/VoxelBox");
            var crate = Crate(root, tuning, new Vector3(90, 0, 0));
            var direct = crate.GetComponent<VoxelMissileTarget>();
            Blast(direct, crate.transform.position + Vector3.up * .65f, weapon.areaOfEffectRadius, weapon.damagePerBullet);
            Check(!crate.IsRoadHazard, "Authored 10-health crate survived 35 damage.");
            int cash = mission.BonusCashEarned;
            float multiplier = mission.EffectiveTimeBonusMultiplier;
            var random = UnityEngine.Random.state;
            Blast(direct, crate.transform.position, 10, weapon.damagePerBullet);
            Equal(mission.BonusCashEarned, cash, "Crate rewarded cash twice");
            Equal(mission.EffectiveTimeBonusMultiplier, multiplier, "Crate rewarded multiplier twice");
            Check(random.Equals(UnityEngine.Random.state), "Crate rolled a second reward or explosion.");
        }

        private static void Advance(VoxelMissileProjectile missile, float dt) =>
            typeof(VoxelMissileProjectile).GetMethod("Advance", Private).Invoke(missile, new object[] { dt });
        private static void Fly(VoxelMissileProjectile missile, int fps)
        {
            // These deterministic steps share one real frame. Runtime builders and
            // debris schedule collider removal for frame end; flush those removals
            // before simulating later frames, keeping the actual obstruction wall.
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == "Box Damage Voxel" || go.name == "Fuel Drum Damage Voxel" || go.name == "Box Explosion Voxel")
                    RemovePendingColliders(go);
            Physics.SyncTransforms();
            for (int frame = 0; frame < fps * 3 && !Field<bool>(missile, "finished"); frame++) Advance(missile, 1f / fps);
            Check(Field<bool>(missile, "finished"), "Missile did not detonate during flight.");
        }
        private static void RemovePendingColliders(GameObject go)
        {
            foreach (var collider in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        }
        private static VoxelCarController Owner(Transform fixture)
        {
            var car = Child(fixture, "Owner").AddComponent<VoxelCarController>(); car.enabled = false;
            car.transform.rotation = fixture.rotation;
            return car;
        }
        private static VoxelMissileProjectile Launch(VoxelCarController owner, VoxelGunTuning weapon) =>
            VoxelMissileProjectile.Create(owner.transform.position + Vector3.up * weapon.missileCruiseHeight, owner, 1, weapon);

        private static void CheckFlight(Transform root, VoxelStaticObstacleDefinition crateTuning,
            VoxelStaticObstacleDefinition drumTuning, VoxelGunTuning weapon, int fps, float yaw)
        {
            var fixture = Child(root, "Flight case", new Vector3(120 + fps * 3 + yaw, 0, 100)).transform;
            fixture.rotation = Quaternion.Euler(0, yaw, 0);
            var owner = Owner(fixture);
            var crate = Crate(fixture, crateTuning, new Vector3(0, 0, 20));
            var drums = Drums(fixture, drumTuning, new Vector3(0, 0, 40));
            Check(crate.GetComponentsInChildren<Collider>().Length == 0 && drums.GetComponentsInChildren<Collider>().Length == 0,
                "Flight fixture must exercise collider-free targets.");
            Fly(Launch(owner, weapon), fps);
            Equal(Health(crate), 100 - weapon.damagePerBullet, "Flying missile missed nearest crate");
            foreach (float health in Health(drums).Values) Equal(health, 50, "Missile damaged drums behind the crate");
            crate.gameObject.SetActive(false);
            Fly(Launch(owner, weapon), fps);
            Check(Health(drums).Values.Any(h => Mathf.Abs(h - (50 - weapon.damagePerBullet)) < .001f),
                "Flying missile missed drums after disabling the nearer crate.");
            fixture.gameObject.SetActive(false);
        }

        private static void CheckExpiryAndWall(Transform root, VoxelStaticObstacleDefinition tuning, VoxelGunTuning weapon)
        {
            var fixture = Child(root, "Expiry and wall", new Vector3(900, 0, 0)).transform;
            var owner = Owner(fixture);
            float range = weapon.maximumRange;
            weapon.maximumRange = 5f;
            var crate = Crate(fixture, tuning, new Vector3(1.5f, 0, 5f));
            Fly(Launch(owner, weapon), 30);
            Equal(Health(crate), 100 - weapon.damagePerBullet, "Range-expiry splash failed to damage nearby crate");
            crate.gameObject.SetActive(false);
            weapon.maximumRange = range;
            crate = Crate(fixture, tuning, new Vector3(0, 0, 20));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(fixture, false); wall.transform.localPosition = new Vector3(0, 1, 10);
            wall.transform.localScale = new Vector3(10, 10, 1);
            Physics.SyncTransforms();
            Fly(Launch(owner, weapon), 30);
            Equal(Health(crate), 100, "Missile passed through nearer wall to damage crate");
        }
    }
}
