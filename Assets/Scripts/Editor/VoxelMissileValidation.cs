using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace VoxelRacer.Editor
{
    public static class VoxelMissileValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        public static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        public static void ValidateFireControls()
        {
            var oldEvent = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var root = new GameObject("Temporary fire controls validation");
            var bullet = ScriptableObject.CreateInstance<VoxelGunTuning>();
            var missile = ScriptableObject.CreateInstance<VoxelGunTuning>();
            missile.projectileKind = VoxelProjectileKind.Missile;
            try
            {
                var car = root.AddComponent<VoxelCarController>(); car.enabled = false;
                var controls = root.AddComponent<VoxelMobileControls>(); controls.Configure(car);
                if (root.transform.childCount == 0) Call(controls, "BuildHud");
                var buttons = root.GetComponentsInChildren<RectTransform>();
                var fire = buttons.First(r => r.name == "Fire Button");
                var launch = buttons.First(r => r.name == "Missile Button");
                Check(fire.anchorMin.x == 1 && launch.anchorMin.x == 0 &&
                    fire.anchoredPosition.x == -launch.anchoredPosition.x &&
                    fire.anchoredPosition.y == launch.anchoredPosition.y, "Buttons must mirror each other");
                var gunMount = root.AddComponent<VoxelGunMount>(); gunMount.tuning = bullet;
                var rocketMount = root.AddComponent<VoxelGunMount>(); rocketMount.tuning = missile;
                fire.GetComponent<UnityEngine.EventSystems.IPointerDownHandler>().OnPointerDown(null);
                Check((bool)Call(gunMount, "IsFireHeld") && !(bool)Call(rocketMount, "IsFireHeld"), "FIRE must fire guns only");
                launch.GetComponent<UnityEngine.EventSystems.IPointerDownHandler>().OnPointerDown(null);
                Check((bool)Call(gunMount, "IsFireHeld") && (bool)Call(rocketMount, "IsFireHeld"), "Both buttons must work together");
                fire.GetComponent<UnityEngine.EventSystems.IPointerUpHandler>().OnPointerUp(null);
                Check(!(bool)Call(gunMount, "IsFireHeld") && (bool)Call(rocketMount, "IsFireHeld"), "MISSILE must fire missiles only");
                launch.GetComponent<UnityEngine.EventSystems.IPointerExitHandler>().OnPointerExit(null);
                Check(!VoxelMobileControls.IsFireHeld && !VoxelMobileControls.IsMissileHeld, "Exit must release missiles");
                launch.GetComponent<UnityEngine.EventSystems.IPointerDownHandler>().OnPointerDown(null);
                Call(controls, "OnApplicationFocus", false);
                Check(!VoxelMobileControls.IsMissileHeld, "Focus loss must clear input");
                launch.GetComponent<UnityEngine.EventSystems.IPointerDownHandler>().OnPointerDown(null);
                Call(controls, "Update");
                Check(!VoxelMobileControls.IsMissileHeld, "Hidden HUD must clear input");
                Debug.Log("PASS: mirrored buttons, independent gun/missile input, simultaneous hold, release, exit, focus loss and hidden HUD.");
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(bullet); Object.DestroyImmediate(missile);
                if (oldEvent == null) { var e = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(); if (e != null) Object.DestroyImmediate(e.gameObject); }
            }
        }
        [MenuItem("Tools/Voxel Racer/Validate Missile Launcher")]
        public static void Run()
        {
            if (Application.isPlaying) throw new Exception("Use Edit Mode validation outside Play Mode.");
            int cash = VoxelCurrencyState.Balance;
            bool left = VoxelMissileUpgradeState.IsPurchased(false), right = VoxelMissileUpgradeState.IsPurchased(true);
            var missing = (HashSet<string>)typeof(VoxelCarRunState).GetField("missingVoxelPaths", Static).GetValue(null);
            var armor = (Dictionary<string, int>)typeof(VoxelCarRunState).GetField("armorHealth", Static).GetValue(null);
            var savedMissing = missing.ToArray(); var savedArmor = armor.ToArray();
            var nameField = typeof(VoxelCarRunState).GetField("carDefinitionName", Static); var savedName = nameField.GetValue(null);
            var oldEvent = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var root = new GameObject("Temporary missile validation");
            var wrongCar = ScriptableObject.CreateInstance<VoxelCarDefinition>();
            var enemyTuning = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>(); enemyTuning.vehicleHealth = 1000;
            try
            {
                var fit = VoxelMissileLauncherTuning.Load(); var gun = fit.weapon;
                var definition = Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
                Check(fit.Fits(definition) && !fit.Fits(wrongCar) && !fit.Fits(null), "Compatibility incorrect");
                var car = MakeCar(root.transform, definition); int integrity = car.TotalIntegrityVoxels;
                car.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null).gameObject.SetActive(false);
                VoxelMissileUpgradeState.BeginNewRun(); VoxelCurrencyState.Reset();
                Check(!VoxelMissileUpgradeState.TryPurchase(fit, definition, false), "Unaffordable launcher purchased");
                var shop = root.AddComponent<VoxelRepairUpgradeSceneController>();
                Set(shop, "definition", definition); typeof(VoxelRepairUpgradeSceneController).GetProperty("DisplayedCar").SetValue(shop, car);
                Call(shop, "BuildUi"); VoxelCurrencyState.Add(gun.purchasePrice * 2 + 20); Call(shop, "RefreshUi");
                foreach (bool side in new[] { false, true })
                {
                    var button = root.GetComponentsInChildren<Button>(true).Single(b => b.name == (side ? "Right" : "Left") + " Missile Purchase Button");
                    Check(button.interactable, "Missile shop button disabled"); button.onClick.Invoke();
                    Check(!button.interactable && VoxelMissileUpgradeState.IsPurchased(side), "Purchase failed");
                    Check(!VoxelMissileUpgradeState.TryPurchase(fit, definition, side), "Duplicate side purchase allowed");
                }
                Check(VoxelCurrencyState.Balance == 20, "Wrong purchase price");
                VoxelMissileUpgradeState.ApplyTo(car.transform, definition);
                Check(car.TotalIntegrityVoxels == integrity && car.MissingIntegrityVoxels == 1, "Missiles altered existing integrity");
                var next = MakeCar(root.transform, definition); VoxelMissileUpgradeState.ApplyTo(next.transform, definition); VoxelCarRunState.Apply(next, definition);
                Check(next.MissingIntegrityVoxels == 1, "Missiles broke damage persistence");
                foreach (var mount in next.GetComponentsInChildren<VoxelGunMount>()) Check(!mount.IsReady, "Workshop can fire");
                foreach (bool side in new[] { false, true })
                {
                    var mount = next.transform.Find(VoxelMissileUpgradeState.MountName(side)).GetComponent<VoxelGunMount>();
                    Check(Vector3.Dot(mount.FireDirection, next.transform.forward) > .999f, "Launcher points backwards");
                    var projectile = VoxelMissileProjectile.Create(mount.MuzzlePosition, next, side ? 1 : -1, gun);
                    projectile.transform.SetParent(root.transform);
                    Vector3 atStart = (Vector3)Call(projectile, "PositionAt", 0f);
                    Vector3 atEnd = (Vector3)Call(projectile, "PositionAt", gun.missileDescentDistance);
                    Check(Vector3.Distance(atStart, mount.MuzzlePosition) < .001f, "Missile teleports at launch");
                    Check(Mathf.Abs(atEnd.y - gun.missileCruiseHeight) < .001f && Mathf.Abs(atEnd.x - (side ? 1 : -1) * gun.missileLaneSideOffset) < .001f, "Missile descent or lane offset wrong");
                    var ps = projectile.GetComponentsInChildren<ParticleSystem>();
                    Check(ps.Length == 2 && ps.Any(p => p.main.simulationSpace == ParticleSystemSimulationSpace.World), "Rocket/smoke effects missing");
                    Object.DestroyImmediate(projectile.gameObject);
                }
                var enemy = MakeTarget(root.transform, enemyTuning, new Vector3(0, 0, 30));
                Check(VoxelMissileTarget.Trace(new Vector3(0, 0, 20), Vector3.forward, 20, out var hit, out _, out var point, out float distance) && hit.gameObject == enemy.gameObject && distance < 10, "Swept missile missed collider-free car");
                var pieces = enemy.GetComponentsInChildren<MeshRenderer>();
                var protectedPiece = pieces.Single(r => r.transform.localPosition == Vector3.right);
                protectedPiece.gameObject.AddComponent<VoxelIndestructiblePart>();
                enemy.TakeMissileBlast(enemy.transform.position, 1.01f, 35, Vector3.forward, pieces);
                Check(Mathf.Approximately(enemy.CurrentHealth, 965), "Blast applied health damage per voxel");
                Check(pieces.Count(r => !r.gameObject.activeSelf) == 6 && protectedPiece.gameObject.activeSelf, "Blast sphere or indestructible protection wrong");
                Check(pieces.Where(r => (r.bounds.center - enemy.transform.position).sqrMagnitude > 1.0201f).All(r => r.gameObject.activeSelf), "Blast removed voxels outside sphere");
                var nearby = MakeTarget(root.transform, enemyTuning, new Vector3(10, 0, 30));
                var trafficObject = Object.Instantiate(nearby.gameObject, root.transform);
                trafficObject.transform.position = new Vector3(10.5f, 0, 30);
                Object.DestroyImmediate(trafficObject.GetComponent<VoxelEnemyCar>());
                var traffic = trafficObject.AddComponent<VoxelObstacleCar>(); traffic.enabled = false;
                typeof(VoxelObstacleCar).GetProperty("EnemyTuning").SetValue(traffic, enemyTuning);
                typeof(VoxelObstacleCar).GetProperty("CurrentHealth").SetValue(traffic, 1000f);
                VoxelMissileTarget.Register(trafficObject);
                VoxelMissileTarget.Blast(nearby.transform.position, 1.01f, 35, Vector3.forward);
                Check(Mathf.Approximately(nearby.CurrentHealth, 965) && Mathf.Approximately(traffic.CurrentHealth, 965), "AOE did not damage nearby enemy and civilian once each");
                var entries = VoxelUpgradeFitCatalog.Discover(); var mounts = entries.Where(e => e.Asset == fit).ToArray();
                Check(mounts.Length == 2 && mounts.All(e => e.Fits(definition)), "Both fit slots not discovered");
                var preview = Object.Instantiate(definition.visualPrefab, root.transform); var body = preview.GetComponentsInChildren<Renderer>(true);
                Directory.CreateDirectory("Temp/Missile");
                mounts[0].Build(preview.transform); Render(preview, "LeftOnly", false);
                var leftMount = preview.transform.Find(VoxelMissileUpgradeState.MountName(false));
                leftMount.gameObject.SetActive(false); mounts[1].Build(preview.transform); Render(preview, "RightOnly", true); leftMount.gameObject.SetActive(true);
                mounts[1].Build(preview.transform); Render(preview, "Both", true);
                foreach (var entry in entries.Where(e => e.Asset != fit && e.Fits(definition))) entry.Build(preview.transform);
                Render(preview, "Combined", false);
                foreach (var r in body) if (r.GetComponentInParent<VoxelIndestructiblePart>() == null) r.enabled = false;
                Render(preview, "BodyHidden", true);
                foreach (var r in body) r.enabled = false;
                Render(preview, "UpgradesOnly", false);
                Check(VoxelCurrencyState.Balance == 20 && VoxelMissileUpgradeState.IsPurchased(false) && VoxelMissileUpgradeState.IsPurchased(true), "Preview changed ownership");
                VoxelMissileUpgradeState.BeginNewRun(); Check(!VoxelMissileUpgradeState.IsPurchased(false) && !VoxelMissileUpgradeState.IsPurchased(true), "New run did not reset launchers");
                File.WriteAllText("Temp/Missile/Validation.txt", "PASS: independent shop sides, affordability, duplicate prevention, persistence, integrity, preview fit, mirrored muzzle/flight, bumper descent, smoke/exhaust configuration, swept collider-free contact, spherical voxel carve, once-per-car damage, protected/outside voxels, run reset. Edit Mode.");
                Debug.Log(File.ReadAllText("Temp/Missile/Validation.txt"));
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(wrongCar); Object.DestroyImmediate(enemyTuning);
                typeof(VoxelMissileUpgradeState).GetField("leftPurchased", Static).SetValue(null, left);
                typeof(VoxelMissileUpgradeState).GetField("rightPurchased", Static).SetValue(null, right);
                VoxelCurrencyState.Reset(); VoxelCurrencyState.Add(cash);
                missing.Clear(); foreach (var p in savedMissing) missing.Add(p);
                armor.Clear(); foreach (var p in savedArmor) armor.Add(p.Key, p.Value); nameField.SetValue(null, savedName);
                if (oldEvent == null) { var e = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(); if (e != null) Object.DestroyImmediate(e.gameObject); }
            }
        }
        public static VoxelEnemyCar MakeTarget(Transform root, VoxelEnemyVehicleTuning tuning, Vector3 position)
        {
            var go = new GameObject("Missile test target"); go.transform.SetParent(root, false); go.transform.position = position;
            var enemy = go.AddComponent<VoxelEnemyCar>(); enemy.enabled = false;
            typeof(VoxelEnemyCar).GetProperty("Tuning").SetValue(enemy, tuning);
            typeof(VoxelEnemyCar).GetProperty("CurrentHealth").SetValue(enemy, tuning.vehicleHealth);
            for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++) for (int z = -2; z <= 2; z++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(go.transform, false); cube.transform.localPosition = new Vector3(x, y, z); cube.transform.localScale = Vector3.one * .8f;
            }
            VoxelMissileTarget.Register(go); return enemy;
        }
        private static VoxelCarController MakeCar(Transform parent, VoxelCarDefinition definition)
        {
            var go = new GameObject("Missile test car"); go.transform.SetParent(parent, false); Object.Instantiate(definition.visualPrefab, go.transform);
            var car = go.AddComponent<VoxelCarController>(); car.enabled = false; car.SetTuning(definition.tuning); car.ResetIntegrityBaseline(); return car;
        }
        private static void Render(GameObject source, string name, bool right)
        {
            var p = new PreviewRenderUtility();
            try
            {
                p.AddSingleGO(Object.Instantiate(source)); p.camera.transform.position = new Vector3(right ? 5 : -5, 3.5f, 7);
                p.camera.transform.LookAt(new Vector3(0, .75f, 0)); p.camera.fieldOfView = 38; p.camera.nearClipPlane = .1f; p.camera.farClipPlane = 50;
                p.camera.clearFlags = CameraClearFlags.SolidColor; p.camera.backgroundColor = new Color(.17f, .2f, .24f);
                p.ambientColor = Color.gray; p.lights[0].intensity = 1.5f; p.lights[0].transform.rotation = Quaternion.Euler(40, 150, 0);
                p.lights[1].intensity = 1; p.lights[1].transform.rotation = Quaternion.Euler(30, 210, 0);
                p.BeginStaticPreview(new Rect(0, 0, 1280, 800)); p.Render(true); var image = p.EndStaticPreview();
                File.WriteAllBytes("Temp/Missile/" + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
            }
            finally { p.Cleanup(); }
        }
    }
}
