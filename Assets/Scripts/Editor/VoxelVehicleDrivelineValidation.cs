using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelVehicleDrivelineValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Output = "Temp/VehicleDrivelines";
        private static IEnumerable<GameObject> Models() => VoxelUpgradeFitCatalog.Assets<VoxelEnemyVehicleTuning>()
            .Select(t => t.modelPrefab).Concat(VoxelUpgradeFitCatalog.Assets<VoxelBossDefinition>().Select(b => b.bossPrefab))
            .Where(p => p != null).Distinct();

        [MenuItem("Tools/Voxel Racer/Update All Vehicle Axles and Drivelines")]
        public static void UpdatePrefabs()
        {
            Check(!Application.isPlaying, "Update vehicle prefabs in Edit Mode.");
            int updated = 0;
            foreach (var model in Models())
            {
                if (VoxelVehicleDriveline.FindStructure(model.transform) != null) continue;
                string path = AssetDatabase.GetAssetPath(model);
                var copy = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Check(VoxelVehicleDriveline.Ensure(copy) != null, "Unable to fit axles: " + model.name);
                    PrefabUtility.SaveAsPrefabAsset(copy, path); updated++;
                }
                finally { PrefabUtility.UnloadPrefabContents(copy); }
            }
            Debug.Log("Updated " + updated + " vehicle prefabs with protected axles/driveshafts; retained existing civilian/boss assemblies.");
        }

        [MenuItem("Tools/Voxel Racer/Validate All Vehicle Drivelines and Wheel Damage")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Run vehicle driveline validation in Edit Mode.");
            Directory.CreateDirectory(Output);
            var scene = EditorSceneManager.NewPreviewScene();
            var host = new GameObject("Temporary vehicle structure QA"); SceneManager.MoveGameObjectToScene(host, scene);
            var random = UnityEngine.Random.state;
            var activeField = typeof(VoxelMissionProgress).GetField("<Active>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previousMission = activeField.GetValue(null); activeField.SetValue(null, null);
            var tuningAssets = VoxelUpgradeFitCatalog.Assets<VoxelEnemyVehicleTuning>().ToArray();
            var before = tuningAssets.ToDictionary(t => t, t => EditorJsonUtility.ToJson(t));
            var tuning = Object.Instantiate(tuningAssets.First()); tuning.vehicleHealth = 1000000; tuning.voxelHealth = 1;
            var report = new List<string>();
            try
            {
                foreach (var source in Models())
                {
                    var model = Object.Instantiate(source, host.transform);
                    try
                    {
                        int count = model.GetComponentsInChildren<MeshRenderer>(true).Length;
                        Check(VoxelVehicleDriveline.Ensure(model) != null, "Missing structure: " + source.name);
                        VoxelVehicleDriveline.Ensure(model);
                        Check(model.GetComponentsInChildren<MeshRenderer>(true).Length == count, "Duplicated structure: " + source.name);
                        var protectedPieces = model.GetComponentsInChildren<MeshRenderer>(true).Where(Protected).ToArray();
                        var wheels = model.GetComponentsInChildren<Transform>(true).Where(t => t.name == VoxelVehicleDriveline.WheelName).ToArray();
                        Check(wheels.Length >= 4 && protectedPieces.Length > 0, "Wheel/structure coverage missing: " + source.name);
                        foreach (var wheel in wheels)
                        {
                            Check(wheel.GetComponentInParent<VoxelIndestructiblePart>() == null, "Wheel became indestructible.");
                            Vector3 centre = model.transform.InverseTransformPoint(wheel.position);
                            Vector3 inner = wheel.position + model.transform.right * (centre.x > 0 ? -.12f : .12f);
                            Check(protectedPieces.Any(p => p.bounds.Contains(inner)), "Axle misses wheel centre: " + source.name);
                        }
                        Check(model.GetComponentsInChildren<Collider>(true).Length == 0, "Unexpected colliders: " + source.name);
                    }
                    finally { Object.DestroyImmediate(model); }
                    foreach (bool enemy in new[] { true, false })
                    {
                        Probe(host.transform, source, tuning, enemy, false);
                        Probe(host.transform, source, tuning, enemy, true);
                    }
                    report.Add("PASS " + source.name + ": protected aligned structure, idempotence, no colliders; both damage controllers; body-first rams/explosion selectors, direct wheel shots, late wheel removal, local/full missile blasts.");
                }
                ProbeRuntimeFallbacks(host.transform, tuning);
                report.Add("PASS configured enemy/civilian procedural fallback, existing axle reuse and six-wheel semi support.");
                Check(before.All(pair => pair.Value == EditorJsonUtility.ToJson(pair.Key)), "Source tuning changed.");
                report.Add("PASS source tuning unchanged; mission and random state restored on exit.");
                File.WriteAllLines(Output + "/Validation.txt", report);
                Debug.Log(string.Join("\n", report));
            }
            finally
            {
                Object.DestroyImmediate(tuning); EditorSceneManager.ClosePreviewScene(scene);
                activeField.SetValue(null, previousMission); UnityEngine.Random.state = random;
            }
        }

        private static void Probe(Transform host, GameObject source, VoxelEnemyVehicleTuning tuning, bool enemy, bool missile)
        {
            var owner = new GameObject("Temporary damage order probe"); owner.transform.SetParent(host, false);
            owner.transform.localRotation = Quaternion.Euler(0, enemy ? 37 : 180, 0);
            Object.Instantiate(source, owner.transform);
            Component controller = enemy ? (Component)owner.AddComponent<VoxelEnemyCar>() : owner.AddComponent<VoxelObstacleCar>();
            ((Behaviour)controller).enabled = false;
            var type = controller.GetType();
            type.GetField(enemy ? "<Tuning>k__BackingField" : "<EnemyTuning>k__BackingField", Private).SetValue(controller, tuning);
            type.GetField("<CurrentHealth>k__BackingField", Private).SetValue(controller, tuning.vehicleHealth);
            try
            {
                var pieces = owner.GetComponentsInChildren<MeshRenderer>().ToArray();
                var safe = pieces.Where(Protected).ToArray();
                var wheel = pieces.Where(p => !Protected(p) && Wheel(p.transform)).ToArray();
                var body = pieces.Where(p => !Protected(p) && !Wheel(p.transform)).ToArray();
                Check(body.Length > 0 && wheel.Length > 0, "Missing body/wheel pieces.");
                var point = wheel[0].bounds.center;
                float Health() => enemy ? ((VoxelEnemyCar)controller).CurrentHealth : ((VoxelObstacleCar)controller).CurrentHealth;
                void Shot(Transform part)
                {
                    if (enemy) ((VoxelEnemyCar)controller).TakeHostileProjectileHit(part, 1, point, Vector3.forward);
                    else ((VoxelObstacleCar)controller).TakeHostileProjectileHit(part, 1, point, Vector3.forward);
                }
                foreach (var piece in safe) Shot(piece.transform);
                Check(Health() == tuning.vehicleHealth, "Protected structure took weapon health damage.");
                if (missile)
                {
                    void Blast(float radius)
                    {
                        if (enemy) ((VoxelEnemyCar)controller).TakeMissileBlast(point, radius, 1, Vector3.forward, pieces);
                        else ((VoxelObstacleCar)controller).TakeMissileBlast(point, radius, 1, Vector3.forward, pieces);
                    }
                    Blast(.001f);
                    Check(wheel.All(Active) && body.All(Active) && Health() == tuning.vehicleHealth - 1, "Local tyre blast removed wheels early or lost health damage.");
                    Blast(100);
                    Check(body.All(p => !Active(p)) && wheel.All(p => !Active(p)), "Full blast failed to remove final wheels after bodywork.");
                }
                else
                {
                    Shot(wheel[0].transform);
                    Check(wheel.All(Active) && body.Any(p => !Active(p)) && Health() == tuning.vehicleHealth - 1, "Direct wheel shot did not remove body first.");
                    var damage = type.GetMethod("ApplyVoxelDamage", Private);
                    void Remove(int count, int style)
                    {
                        if (enemy) damage.Invoke(controller, new object[] { point, Vector3.forward, count });
                        else damage.Invoke(controller, new object[] { point, Vector3.forward, count, Enum.ToObject(damage.GetParameters()[3].ParameterType, style) });
                    }
                    for (int style = 0; style < 3; style++) Remove(1, style);
                    Check(wheel.All(Active), "Bulk damage removed a wheel before bodywork.");
                    Remove(body.Count(Active), 2);
                    Check(body.All(p => !Active(p)) && wheel.All(Active), "Body stripping removed wheels too soon.");
                    if (enemy) Render(owner, source.name + "_Stripped");
                    Shot(wheel[0].transform);
                    Check(!Active(wheel[0]), "Wheel remained indestructible after body stripping.");
                }
                Check(safe.All(Active), "Damage detached protected drivetrain.");
            }
            finally { Object.DestroyImmediate(owner); }
        }

        private static void ProbeRuntimeFallbacks(Transform host, VoxelEnemyVehicleTuning source)
        {
            typeof(VoxelRacerBootstrap).GetMethod("EnsureMaterials", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            var tuning = Object.Instantiate(source); tuning.modelPrefab = null;
            var traffic = Object.Instantiate(VoxelObstacleCarTuning.Load());
            var playerObject = new GameObject("Temporary fallback target"); playerObject.transform.SetParent(host, false);
            var player = playerObject.AddComponent<VoxelCarController>(); player.enabled = false;
            try
            {
                foreach (bool enemy in new[] { true, false })
                {
                    var owner = new GameObject("Fallback vehicle"); owner.transform.SetParent(host, false);
                    try
                    {
                        if (enemy) { var c = owner.AddComponent<VoxelEnemyCar>(); c.enabled = false; c.Configure(player, traffic, tuning, null, 70, 0); }
                        else { var c = owner.AddComponent<VoxelObstacleCar>(); c.enabled = false; c.Configure(player, traffic, true, null, 70, 0, 12, tuning); }
                        Check(owner.GetComponentsInChildren<MeshRenderer>().Count(Protected) == 3, "Fallback duplicated/missed axles.");
                        foreach (var bar in owner.GetComponentsInChildren<VoxelEnemyHealthBar>(true))
                            typeof(VoxelEnemyHealthBar).GetMethod("OnDestroy", Private).Invoke(bar, null);
                    }
                    finally { Object.DestroyImmediate(owner); }
                }
                var semi = new GameObject("Temporary six-wheel semi"); semi.transform.SetParent(host, false);
                try
                {
                    typeof(VoxelRacerBootstrap).GetMethod("CreateObstacleSemiTrailerVisuals", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { semi.transform });
                    Check(semi.GetComponentsInChildren<MeshRenderer>().Count(Protected) == 4, "Six-wheel semi needs three axles and a shaft.");
                    int count = semi.GetComponentsInChildren<MeshRenderer>().Length;
                    VoxelVehicleDriveline.Ensure(semi);
                    Check(count == semi.GetComponentsInChildren<MeshRenderer>().Length, "Semi duplicated structure.");
                    foreach (var renderer in semi.GetComponentsInChildren<MeshRenderer>()) renderer.enabled = Protected(renderer) || Wheel(renderer.transform);
                    Render(semi, "Semi_Stripped");
                }
                finally { Object.DestroyImmediate(semi); }
            }
            finally { Object.DestroyImmediate(playerObject); Object.DestroyImmediate(tuning); Object.DestroyImmediate(traffic); }
        }

        private static bool Protected(MeshRenderer r) => r.GetComponentInParent<VoxelIndestructiblePart>() != null;
        private static bool Active(MeshRenderer r) => r != null && r.enabled && r.gameObject.activeInHierarchy;
        private static bool Wheel(Transform part)
        { for (; part != null; part = part.parent) if (part.name == VoxelVehicleDriveline.WheelName) return true; return false; }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Render(GameObject source, string name)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                var copy = Object.Instantiate(source);
                foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
                copy.transform.position = Vector3.zero; copy.transform.rotation = Quaternion.identity;
                preview.AddSingleGO(copy);
                preview.camera.transform.position = new Vector3(5, 3, 6); preview.camera.transform.LookAt(new Vector3(0, .45f, 0));
                preview.camera.orthographic = true; preview.camera.orthographicSize = name.StartsWith("Semi") ? 4.8f : 2.4f;
                preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 50; preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.2f, .23f, .26f); preview.ambientColor = Color.gray;
                preview.lights[0].intensity = 1.8f; preview.lights[0].transform.rotation = Quaternion.Euler(40, -60, 0);
                preview.lights[1].intensity = 1.2f; preview.lights[1].transform.rotation = Quaternion.Euler(30, 120, 0);
                preview.BeginStaticPreview(new Rect(0, 0, 1200, 750)); preview.Render(true);
                var image = preview.EndStaticPreview();
                try { File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); }
            }
            finally { preview.Cleanup(); }
        }
    }
}
