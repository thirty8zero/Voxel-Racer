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
        private static bool IntersectTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0;
            Vector3 edge = b - a, other = c - a;
            Vector3 cross = Vector3.Cross(ray.direction, other);
            float determinant = Vector3.Dot(edge, cross);
            if (Mathf.Abs(determinant) < .000001f) return false;
            float inverse = 1f / determinant;
            Vector3 offset = ray.origin - a;
            float u = Vector3.Dot(offset, cross) * inverse;
            if (u < 0 || u > 1) return false;
            Vector3 second = Vector3.Cross(offset, edge);
            float v = Vector3.Dot(ray.direction, second) * inverse;
            if (v < 0 || u + v > 1) return false;
            distance = Vector3.Dot(other, second) * inverse;
            return distance >= 0;
        }
        public static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        public static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        public static void ValidateRoadExit()
        {
            var root = new GameObject("Temporary missile bend validation");
            var weapon = Object.Instantiate(VoxelMissileLauncherTuning.Load().weapon);
            try
            {
                var road = root.AddComponent<EndlessVoxelRoad>(); road.enabled = false;
                road.turnChancePerSegment = 1; road.minimumStraightSegmentsBetweenTurns = 0;
                road.minimumTurnAngle = road.maximumTurnAngle = 30;
                Set(road, "turnRandom", new System.Random(123));
                road.segmentLength=30;
                var segments=(System.Collections.IList)typeof(EndlessVoxelRoad).GetField("pathSegments",Private).GetValue(road);
                segments.Clear();
                for(int i=0;i<12;i++)
                {
                    Call(road,"AppendSegment",false);
                    var segment=segments[i];
                    segment.GetType().GetField("turnAngle").SetValue(segment,i<3?0f:30f);
                }
                foreach(int side in new[]{-1,1})
                {
                    var go = new GameObject("Trajectory test"); go.transform.SetParent(root.transform);
                    var missile = go.AddComponent<VoxelMissileProjectile>();
                    Set(missile,"tuning",weapon); Set(missile,"road",road); Set(missile,"startDistance",50f);
                    Set(missile,"side",(float)side); Set(missile,"edgeOffset",.8f); Set(missile,"launchHeight",1.7f); Set(missile,"launchOffset",side*.8f);
                    float settle=weapon.missileDescentDistance;
                    weapon.missileFollowRoad=false;
                    Vector3 a=(Vector3)Call(missile,"PositionAt",settle);
                    Vector3 before=(Vector3)Call(missile,"PositionAt",settle-.001f);
                    Vector3 b=(Vector3)Call(missile,"PositionAt",settle+20);
                    Vector3 c=(Vector3)Call(missile,"PositionAt",settle+40);
                    Check(Vector3.Distance(a,before)<.01f,"Discontinuity when missile settles");
                    Check(Vector3.Distance(b-a,c-b)<.001f,"Straight missile curved after settling");
                    Check(Vector3.Dot((b-a).normalized,road.Evaluate(50+settle).forward)>.9999f,"Wrong exit heading");
                    weapon.missileFollowRoad=true;
                    Vector3 follows=(Vector3)Call(missile,"PositionAt",settle+40);
                    var pose=road.Evaluate(50+settle+40);
                    Check(Vector3.Distance(follows,pose.position+pose.right*(side*.8f)+Vector3.up*weapon.missileCruiseHeight)<.001f,"Follow toggle did not track curved road");
                    Check(Vector3.Distance(follows,c)>1,"Bend test did not distinguish flight modes");
                }
                Debug.Log("PASS both missile sides: smooth settlement, straight tangent through bends, optional road following.");
            }
            finally {Object.DestroyImmediate(root);Object.DestroyImmediate(weapon);}
        }
        public static void ValidateSolidHits()
        {
            var root = new GameObject("Temporary missile envelope validation");
            var tuning = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>(); tuning.vehicleHealth = 1000;
            try
            {
                var near = MakeTarget(root.transform, tuning, new Vector3(500, 20, 20));
                var far = MakeTarget(root.transform, tuning, new Vector3(500, 20, 30));
                foreach(var r in near.GetComponentsInChildren<MeshRenderer>())
                    if(Mathf.Abs(r.transform.localPosition.x) < 1.9f) r.gameObject.SetActive(false);
                Check(VoxelMissileTarget.Trace(new Vector3(500,20,10), Vector3.forward, 40, out var target, out var voxel, out var point, out var distance, 1.5f) && target.gameObject == near.gameObject, "Gap path must hit nearest vehicle envelope");
                Check(distance > 0 && distance < 10, "Wrong envelope contact distance");
                VoxelMissileTarget.Blast(point, .01f, 35, Vector3.forward, target, voxel);
                Check(near.CurrentHealth == 965 && far.CurrentHealth == 1000, "Empty direct impact must damage once, without damaging far target");
                Check(!VoxelMissileTarget.Trace(new Vector3(510,20,10), Vector3.forward, 40, out _, out _, out _, out _, 1.5f), "Unrelated lane must not be hit");
                Check(!VoxelMissileTarget.Trace(new Vector3(500,20,35), Vector3.forward, 10, out _, out _, out _, out _, 1.5f), "Vehicle behind must not be hit");
                Check(VoxelMissileTarget.Trace(new Vector3(500,20,20), Vector3.forward, .1f, out _, out _, out _, out float inside, 1.5f) && inside == 0, "Starting inside vehicle must hit immediately");
                var civilianObject = Object.Instantiate(near.gameObject, root.transform);
                civilianObject.transform.position = new Vector3(520,20,20);
                Object.DestroyImmediate(civilianObject.GetComponent<VoxelEnemyCar>());
                Object.DestroyImmediate(civilianObject.GetComponent<VoxelMissileTarget>());
                var civilian = civilianObject.AddComponent<VoxelObstacleCar>(); civilian.enabled = false;
                typeof(VoxelObstacleCar).GetProperty("EnemyTuning").SetValue(civilian, tuning);
                typeof(VoxelObstacleCar).GetProperty("CurrentHealth").SetValue(civilian, 1000f);
                VoxelMissileTarget.Register(civilianObject);
                Check(VoxelMissileTarget.Trace(new Vector3(520,20,10), Vector3.forward, 20, out target, out voxel, out point, out _, 1.5f), "Civilian gap hit missed");
                VoxelMissileTarget.Blast(point, .01f, 35, Vector3.forward, target, voxel);
                Check(civilian.CurrentHealth == 965, "Civilian direct hit through a missing voxel must deal damage");
                Debug.Log("PASS solid missile envelopes: hollow path, nearest vehicle, direct damage through missing voxels, adjacent-lane exclusion, behind exclusion and inside contact.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tuning); }
        }
        public static void ValidateFireControls()
        {
            bool savedLeft = VoxelMissileUpgradeState.IsPurchased(false), savedRight = VoxelMissileUpgradeState.IsPurchased(true);
            var oldEvent = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var root = new GameObject("Temporary fire controls validation");
            var bullet = ScriptableObject.CreateInstance<VoxelGunTuning>();
            var missile = ScriptableObject.CreateInstance<VoxelGunTuning>();
            missile.projectileKind = VoxelProjectileKind.Missile;
            try
            {
                typeof(VoxelMissileUpgradeState).GetField("leftPurchased", Static).SetValue(null, true);
                var car = root.AddComponent<VoxelCarController>(); car.enabled = false;
                var controls = root.AddComponent<VoxelMobileControls>(); controls.Configure(car);
                if (root.transform.childCount == 0) Call(controls, "BuildHud");
                var buttons = root.GetComponentsInChildren<RectTransform>();
                var fire = buttons.First(r => r.name == "Fire Button");
                var launch = buttons.First(r => r.name == "Missile Button");
                Check(fire.anchorMin == new Vector2(1, 0) && fire.anchorMax == fire.anchorMin &&
                    fire.anchoredPosition == new Vector2(-150, 150) && fire.sizeDelta == new Vector2(200, 200) &&
                    fire.GetComponentInChildren<UnityEngine.UI.Text>() == null &&
                    fire.GetComponent<UnityEngine.UI.Image>().sprite != null,
                    "Reticule gun button must retain its 200px size in the bottom-right corner without a text label");
                Check(launch.anchorMin == new Vector2(1, 0) && launch.anchorMax == launch.anchorMin &&
                    launch.anchoredPosition == new Vector2(-150, 570),
                    "Missile button must sit directly above BOOST on the right");
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
                Debug.Log("PASS: bottom-right gun button, missiles above BOOST, independent gun/missile input, simultaneous hold, release, exit, focus loss and hidden HUD.");
            }
            finally
            {
                typeof(VoxelMissileUpgradeState).GetField("leftPurchased", Static).SetValue(null, savedLeft);
                typeof(VoxelMissileUpgradeState).GetField("rightPurchased", Static).SetValue(null, savedRight);
                Object.DestroyImmediate(root); Object.DestroyImmediate(bullet); Object.DestroyImmediate(missile);
                if (oldEvent == null) { var e = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(); if (e != null) Object.DestroyImmediate(e.gameObject); }
            }
        }
        [MenuItem("Tools/Voxel Racer/Validate Missile Launcher")]
        public static void Run()
        {
            if (Application.isPlaying) throw new Exception("Use Edit Mode validation outside Play Mode.");
            var random = UnityEngine.Random.state;
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
                    Check(button.interactable, "Missile shop button disabled");
                    int beforePurchase = VoxelCurrencyState.Balance;
                    button.onClick.Invoke();
                    Check(!VoxelMissileUpgradeState.IsPurchased(side) && VoxelCurrencyState.Balance == beforePurchase,
                        "Placement preview purchased a launcher before confirmation");
                    Call(shop, "SelectUpgradePlacement", side ? 1 : 0);
                    Call(shop, "ConfirmUpgradePurchase");
                    // Both legacy buttons now open the shared placement picker, which
                    // remains available while the other side is still unpurchased.
                    Check(VoxelMissileUpgradeState.IsPurchased(side) && VoxelCurrencyState.Balance == beforePurchase - gun.purchasePrice,
                        "Confirmed missile side was not purchased at its quoted price");
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
                    Check(Mathf.Abs(mount.transform.localPosition.z + .56f) < .001f &&
                        Mathf.Abs(Mathf.Abs(mount.transform.localPosition.x) - .77f) < .001f, "Launcher must sit centrally on its roof edge");
                    var bracket = mount.transform.Find(VoxelMissileUpgradeState.RoofBracketName);
                    Check(bracket != null && bracket.GetComponentsInChildren<MeshRenderer>().Length == 3,
                        "Braced roof bracket should use three combined material meshes");
                    var casing = mount.GetComponentsInChildren<MeshRenderer>().Where(r => !r.transform.IsChildOf(bracket)).ToArray();
                    Check(casing.Length == 4, "Launcher casing should use four combined material meshes");
                    Check(mount.GetComponentsInChildren<Collider>().Length == 0, "Launcher contains colliders");
                    var burst = mount.missileLaunchBurst;
                    Check(burst != null && !burst.main.loop && !burst.main.playOnAwake &&
                        burst.main.simulationSpace == ParticleSystemSimulationSpace.Local &&
                        Vector3.Dot(burst.transform.forward, -mount.FireDirection) > .999f &&
                        burst.transform.localPosition.z < -.5825f && Mathf.Abs(burst.transform.localPosition.y - .17f) < .001f,
                        "Rear flame burst must sit outside the rear opening, follow the tube and stay idle in previews");
                    Check(Vector3.Dot(bracket.up, next.transform.up) > .9999f &&
                        Vector3.Dot(bracket.right, next.transform.right) * (side ? 1 : -1) > .9999f,
                        "Right-angle bracket must stay upright with its foot inboard on both sides");
                    foreach (int end in new[] { -1, 1 })
                    {
                        var foot = bracket.TransformPoint(new Vector3(-.11f, -.018f, end * .34f));
                        Check(next.GetComponentsInChildren<MeshRenderer>().Where(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null)
                            .Any(r => r.GetComponent<MeshFilter>().sharedMesh.bounds.Contains(r.transform.InverseTransformPoint(foot))),
                            "Bracket foot must contact the actual roof at both anchor points");
                        Vector3 lower = new Vector3(-.39f, -.29f, end * .34f);
                        Vector3 upper = new Vector3(-.18f, -.010f, end * .34f);
                        var anchor = bracket.TransformPoint(lower);
                        Check(next.GetComponentsInChildren<MeshRenderer>().Where(r => r.GetComponentInParent<VoxelIndestructiblePart>() != null &&
                                (r.name.EndsWith("Roof Rail") || r.name.EndsWith("Roof Cross Brace")))
                            .Any(r => r.GetComponent<MeshFilter>().sharedMesh.bounds.Contains(r.transform.InverseTransformPoint(anchor))),
                            "Launcher support must attach to protected chassis on both sides");
                        var supportMesh = bracket.GetComponentsInChildren<MeshFilter>().Single(f => f.sharedMesh.name.EndsWith("_MissileLauncherSteel")).sharedMesh;
                        // Check actual support geometry along the full gap, not just its combined bounds.
                        var vertices = supportMesh.vertices;
                        var triangles = supportMesh.triangles;
                        for (int step = 1; step < 5; step++)
                        {
                            var ray = new Ray(Vector3.Lerp(lower, upper, step / 5f) + Vector3.forward * .15f, Vector3.back);
                            bool supportHit = false;
                            for (int triangle = 0; triangle < triangles.Length && !supportHit; triangle += 3)
                                supportHit = IntersectTriangle(ray, vertices[triangles[triangle]],
                                    vertices[triangles[triangle + 1]], vertices[triangles[triangle + 2]], out float supportDistance) && supportDistance < .25f;
                            Check(supportHit, "Chassis support has a gap between launcher and frame");
                        }
                    }
                    var launcherBounds = new Bounds(); bool firstBounds = true;
                    foreach (var r in mount.GetComponentsInChildren<MeshRenderer>())
                    {
                        Check(r.GetComponentInParent<VoxelIndestructiblePart>() != null, "Launcher detail changed car integrity");
                        if (r.transform.IsChildOf(bracket)) continue;
                        var bounds = r.GetComponent<MeshFilter>().sharedMesh.bounds;
                        if (firstBounds) { launcherBounds = bounds; firstBounds = false; } else launcherBounds.Encapsulate(bounds);
                    }
                    Check(launcherBounds.size.x < .27f && launcherBounds.size.y < .32f, "Launcher casing is not thinner");
                    var projectileBounds = new Bounds(); firstBounds = true;
                    foreach (var r in gun.missilePrefab.GetComponentsInChildren<MeshRenderer>())
                    {
                        var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                        Check(AssetDatabase.Contains(mesh) && mesh.triangles.Length > 24, "Missile detail mesh is not saved");
                        if (firstBounds) { projectileBounds = mesh.bounds; firstBounds = false; } else projectileBounds.Encapsulate(mesh.bounds);
                    }
                    Check(mount.muzzle.localPosition.z + projectileBounds.min.z >= launcherBounds.max.z - .001f, "Missile tail starts inside the launcher");
                    Check(Vector3.Dot(mount.FireDirection, next.transform.forward) > .999f, "Launcher points backwards");
                    Vector3 up = next.transform.InverseTransformDirection(mount.transform.up);
                    Check(Mathf.Abs(up.x - (side ? 1 : -1) * .7071068f) < .001f && Mathf.Abs(up.y - .7071068f) < .001f, "Launcher outward tilt must be mirrored 45 degrees");
                    var projectile = VoxelMissileProjectile.Create(mount.MuzzlePosition, next, side ? 1 : -1, gun);
                    projectile.transform.SetParent(root.transform);
                    Vector3 atStart = (Vector3)Call(projectile, "PositionAt", 0f);
                    Vector3 atEnd = (Vector3)Call(projectile, "PositionAt", gun.missileDescentDistance);
                    Check(Vector3.Distance(atStart, mount.MuzzlePosition) < .001f, "Missile teleports at launch");
                    Check(Mathf.Abs(atEnd.y - gun.missileCruiseHeight) < .001f && Mathf.Abs(atEnd.x - (side ? 1 : -1) * Mathf.Max(0, next.laneWidth * .5f - gun.missileLaneEdgeInset)) < .001f, "Missile descent or lane offset wrong");
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
                Render(preview, "RightBracket", true, true); Render(preview, "LeftBracket", false, true);
                var bodyVisibility = body.ToDictionary(r => r, r => r.enabled);
                foreach (var r in body) if (r.GetComponentInParent<VoxelIndestructiblePart>() == null) r.enabled = false;
                Render(preview, "ChassisSupports", true);
                foreach (var saved in bodyVisibility) saved.Key.enabled = saved.Value;
                foreach (var entry in entries.Where(e => e.Asset != fit && e.Fits(definition))) entry.Build(preview.transform);
                Render(preview, "Combined", false);
                foreach (var r in body) if (r.GetComponentInParent<VoxelIndestructiblePart>() == null) r.enabled = false;
                Render(preview, "BodyHidden", true);
                foreach (var r in body) r.enabled = false;
                Render(preview, "UpgradesOnly", false);
                Check(VoxelCurrencyState.Balance == 20 && VoxelMissileUpgradeState.IsPurchased(false) && VoxelMissileUpgradeState.IsPurchased(true), "Preview changed ownership");
                VoxelMissileUpgradeState.BeginNewRun(); Check(!VoxelMissileUpgradeState.IsPurchased(false) && !VoxelMissileUpgradeState.IsPurchased(true), "New run did not reset launchers");
                File.WriteAllText("Temp/Missile/Validation.txt", "PASS: independent shop sides with placement/confirmation, affordability, duplicate prevention, persistence, integrity, automatic left/right fit slots, paired continuous chassis supports contacting protected roof rails/cross brace on both sides, three combined bracket meshes with no added draw calls/colliders, isolated/combined/body-hidden/upgrades-only previews, mirrored muzzle/flight, bumper descent, smoke/exhaust configuration, swept collider-free contact, spherical voxel carve, once-per-car damage, protected/outside voxels, run reset. Edit Mode; cash/ownership/damage/random state restored.");
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
                UnityEngine.Random.state = random;
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
        private static void Render(GameObject source, string name, bool right, bool bracketDetail = false)
        {
            var p = new PreviewRenderUtility();
            try
            {
                p.AddSingleGO(Object.Instantiate(source)); p.camera.transform.position = new Vector3(right ? 5 : -5, 3.5f, 7);
                p.camera.transform.LookAt(new Vector3(0, .75f, 0)); p.camera.fieldOfView = 38; p.camera.nearClipPlane = .1f; p.camera.farClipPlane = 50;
                if (bracketDetail)
                {
                    var target = new Vector3(right ? .82f : -.82f, 1.51f, -.45f);
                    p.camera.transform.position = target + new Vector3(right ? -.70f : .70f, .46f, 1.90f);
                    p.camera.transform.LookAt(target); p.camera.orthographic = true; p.camera.orthographicSize = .4f;
                }
                else if (name == "ChassisSupports")
                {
                    p.camera.transform.position = new Vector3(4, 1.7f, 6);
                    p.camera.transform.LookAt(new Vector3(0, .95f, 0));
                    p.camera.orthographic = true; p.camera.orthographicSize = 1.5f;
                }
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
