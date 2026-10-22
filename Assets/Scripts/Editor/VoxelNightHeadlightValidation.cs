using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelNightHeadlightValidation
    {
        private const string Folder = "Temp/NightHeadlights/";
        [MenuItem("Tools/Voxel Racer/Lighting/Select Night Headlight Settings")]
        public static void SelectSettings() => Selection.activeObject = VoxelNightHeadlightSettings.Load();

        [MenuItem("Tools/Voxel Racer/Lighting/Validate and Render Night Headlights")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Start from Edit Mode.");
            Directory.CreateDirectory(Folder);
            var previousScene = SceneManager.GetActiveScene();
            var random = UnityEngine.Random.state;
            var fields = typeof(VoxelRacerBootstrap).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(Material)).ToArray();
            var saved = fields.Select(f => f.GetValue(null)).ToArray();
            var oldMaterials = new HashSet<Material>(Resources.FindObjectsOfTypeAll<Material>());
            var oldMeshes = new HashSet<Mesh>(Resources.FindObjectsOfTypeAll<Mesh>());
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            VoxelTrackDefinition track = null;
            VoxelRoadTuning roadTuning = null;
            var oldLights = new List<Light>();
            try
            {
                SceneManager.SetActiveScene(scene);
                var settings = VoxelNightHeadlightSettings.Load();
                Check(settings != null && settings.headlightsEnabled, "Headlight settings missing or disabled.");
                Check(settings.beamMaterial.enableInstancing && !ShaderUtil.ShaderHasError(settings.beamMaterial.shader), "Beam shader/instancing invalid.");
                Check(settings.lampMaterial.IsKeywordEnabled("_EMISSION"), "Build-referenced lamp emission is disabled.");
                Check(settings.enemyLampMaterial != null && settings.enemyLampMaterial.IsKeywordEnabled("_EMISSION"), "Enemy red lamp emission is disabled.");
                foreach (var old in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (old.enabled) { old.enabled = false; oldLights.Add(old); }
                var source = Resources.Load<VoxelTrackDefinition>("Tracks/Track01");
                string originalTrack = EditorJsonUtility.ToJson(source);
                track = Object.Instantiate(source); track.nightTime = true;
                roadTuning = Object.Instantiate(source.roadTuning);
                roadTuning.randomizeTurnSeedEachRun = false; roadTuning.turnSeed = 173;
                roadTuning.turnChancePerSegment = 0;
                typeof(VoxelRacerBootstrap).GetMethod("PrepareTrackMaterials", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { track });
                UnityEngine.Random.InitState(173);
                var root = new GameObject("Temporary Night Headlight Preview");
                var roadObject = Child(root.transform, "Road");
                var road = roadObject.AddComponent<EndlessVoxelRoad>(); road.enabled = false;
                road.trackDefinition = track; road.SetTuning(roadTuning); road.BuildInitialRoad();
                var playerModel = Object.Instantiate(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab, root.transform);
                var player = playerModel.AddComponent<VoxelCarController>(); player.enabled = false;
                player.SetTrack(road, 12); road.SetTarget(player.transform);
                Check(VoxelNightHeadlights.Attach(player.gameObject, track) == null, "Player received night headlights.");
                var key = Child(root.transform, "Night key").AddComponent<Light>(); key.type = LightType.Directional;
                key.shadows = LightShadows.Hard; key.cullingMask = 1 << 31; RenderSettings.sun = key;
                VoxelTrackEnvironment.ApplyLighting(key, track); VoxelTrackEnvironment.ApplyFog(track);
                RenderSettings.skybox = VoxelTrackEnvironment.ResolveSkybox(track);
                Child(root.transform, "Moon").AddComponent<VoxelHorizonSun>().Configure(player.transform, track);
                Child(root.transform, "Mountains").AddComponent<VoxelHorizonMountains>().Configure(player.transform, track);
                var camera = Child(root.transform, "Headlight preview camera").AddComponent<Camera>(); camera.enabled = false;
                camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.Skybox;
                camera.farClipPlane = 1000; camera.fieldOfView = 58; camera.allowHDR = true;
                var follow = camera.gameObject.AddComponent<VoxelCameraFollow>(); follow.enabled = false;
                follow.target = player.transform; follow.tuning = VoxelCameraTuning.Load();
                VoxelMissileValidation.Call(follow, "UpdateFollow", 0f);
                VoxelMobilePostProcessing.Configure(camera);
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                Layer(root);
                int models = ValidateModels(root.transform, player, road, camera, track);
                var cars = new List<VoxelObstacleCar>();
                foreach (var setup in new[] { ("CivilianBeetleTuning", 32f, -5.33f, false),
                    ("CivilianVanTuning", 42f, 0f, true), ("CivilianMercedesWagonTuning", 59f, 5.33f, false) })
                {
                    var traffic = Child(root.transform, "Preview " + setup.Item1).AddComponent<VoxelObstacleCar>();
                    // Explicit pool clone keeps this a civilian while selecting the preview model.
                    var tuning = Object.Instantiate(VoxelObstacleCarTuning.Load());
                    tuning.civilianVehiclePool = new[] { Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/" + setup.Item1) };
                    traffic.Configure(player, tuning, setup.Item4, road, setup.Item2, setup.Item3, 0);
                    traffic.enabled = false; cars.Add(traffic);
                }
                var enemy = Child(root.transform, "Preview Interceptor").AddComponent<VoxelEnemyCar>();
                enemy.Configure(player, VoxelObstacleCarTuning.Load(), Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning"), road, 28f, 5.33f);
                enemy.enabled = false;
                Check(enemy.GetComponent<VoxelNightHeadlights>().IsEnemyVehicle &&
                    enemy.GetComponentsInChildren<MeshRenderer>().Any(r => r.sharedMaterial == settings.enemyLampMaterial), "Enemy headlights are not red.");
                Check(cars.All(c => !c.GetComponent<VoxelNightHeadlights>().IsEnemyVehicle &&
                    c.GetComponentsInChildren<MeshRenderer>().Any(r => r.sharedMaterial == settings.lampMaterial)), "Civilian headlights lost their yellow lamps.");
                var radar = Child(root.transform, "Radar headlight check").AddComponent<VoxelObstacleCar>();
                radar.Configure(player, VoxelObstacleCarTuning.Load(), true, road, 85, 0, 0,
                    Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/RadarInterceptorTuning"));
                Check(radar.IsEnemyTraffic && radar.GetComponent<VoxelNightHeadlights>().IsEnemyVehicle &&
                    radar.GetComponentsInChildren<MeshRenderer>().Any(r => r.sharedMaterial == settings.enemyLampMaterial), "Radar car did not receive red enemy headlights.");
                Object.DestroyImmediate(radar.gameObject);
                Layer(root);
                ValidateBeamClearance();
                int lightsBefore = root.GetComponentsInChildren<Light>(true).Length;
                Render(camera, Folder + "NightChase.png");
                Check(VoxelNightHeadlights.LastBeamCount == 8, "Four traffic vehicles did not submit exactly eight cards.");
                Check(VoxelNightHeadlights.LastBatchCount == (SystemInfo.supportsInstancing ? 1 : 8), "Beam cards did not share the expected batch.");
                var close = Object.Instantiate(camera, root.transform);
                close.transform.position = new Vector3(-10, 7, 50);
                close.transform.LookAt(new Vector3(0, .5f, 34));
                Render(close, Folder + "NightClose.png");
                foreach (var side in new[] { -1f, 1f })
                {
                    var profile = Object.Instantiate(camera, root.transform);
                    profile.transform.position = new Vector3(side * 24f, 3f, 34f);
                    profile.transform.LookAt(new Vector3(0, .65f, 38));
                    Render(profile, Folder + (side < 0 ? "NightLeft.png" : "NightRight.png"));
                }
                Check(root.GetComponentsInChildren<Light>(true).Length == lightsBefore, "Headlights added realtime lights.");
                Check(EditorJsonUtility.ToJson(source) == originalTrack, "Headlight preview edited source track.");
                foreach (var car in cars) Object.DestroyImmediate(car.tuning);
                File.WriteAllText(Folder + "Validation.txt", "PASS: " + models + " authored traffic/enemy/boss models; night-only and player exclusion; front lamps only; exactly two cards per intact vehicle and one shared instanced batch; destroyed lamp/vehicle and disabled-owner gates; no extra renderers/colliders/lights or projectile membership; source prefabs/materials/track and RNG preserved. NightChase/NightClose are isolated editor renders with the mobile post-processing profile, not a target-phone performance benchmark.");
                Debug.Log(File.ReadAllText(Folder + "Validation.txt"));
            }
            catch (Exception error) { File.WriteAllText(Folder + "Validation.txt", "FAIL: " + error); throw; }
            finally
            {
                var materials = Resources.FindObjectsOfTypeAll<Material>().Where(m => !oldMaterials.Contains(m) && !EditorUtility.IsPersistent(m)).ToArray();
                var meshes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                    .Select(f => f.sharedMesh).Where(m => m != null && !oldMeshes.Contains(m) && !EditorUtility.IsPersistent(m)).Distinct().ToArray();
                EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previousScene);
                for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, saved[i]);
                foreach (var material in materials) if (material != null) Object.DestroyImmediate(material);
                foreach (var mesh in meshes) if (mesh != null) Object.DestroyImmediate(mesh);
                if (track != null) Object.DestroyImmediate(track);
                if (roadTuning != null) Object.DestroyImmediate(roadTuning);
                foreach (var light in oldLights) if (light != null) light.enabled = true;
                UnityEngine.Random.state = random;
            }
        }

        private static void ValidateBeamClearance()
        {
            var build = typeof(VoxelNightHeadlights).GetMethod("BuildBeamMatrix", BindingFlags.Static | BindingFlags.NonPublic);
            int samples = 0;
            foreach (float yaw in new[] { 0f, 65f, 180f, 275f })
            foreach (float growth in new[] { .05f, .5f, 1f })
            foreach (float lampHeight in new[] { .49f, .95f, 2.5f })
            foreach (var view in new[] { new Vector3(-8, 6, -18), new Vector3(8, 6, -18),
                new Vector3(-24, 3, 0), new Vector3(24, 3, 0), new Vector3(0, 5, 24), new Vector3(0, 20, 0) })
            {
                var rotation = Quaternion.Euler(0, yaw, 0);
                Vector3 ground = new Vector3(120, 4, -70);
                Vector3 origin = ground + Vector3.up * (lampHeight * growth);
                Vector3 forward = rotation * Vector3.forward;
                var matrix = (Matrix4x4)build.Invoke(null, new object[] { origin, forward, Vector3.up, ground,
                    ground + rotation * view, 14f * growth, 2f * growth });
                Check((matrix.MultiplyPoint3x4(Vector3.zero) - origin).sqrMagnitude < .000001f, "Clearance adjustment moved the lamp anchor.");
                var axis = (Vector3)matrix.GetColumn(2);
                Check(Mathf.Abs(Vector3.Dot(axis, forward) - 14f * growth) < .0001f, "Clearance adjustment changed horizontal reach.");
                // The shader's visible triangle widens from 2.5% to 100% of the card.
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f, width = Mathf.Lerp(.025f, 1f, t);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 point = matrix.MultiplyPoint3x4(new Vector3(side * width, 0, t));
                        Check(point.y >= ground.y - .0001f, "Camera-facing triangle intersects the road.");
                        if (i == 10) Check(point.y >= ground.y + .0999f, "Far edge lost its road clearance.");
                    }
                }
                samples++;
            }
            File.WriteAllText(Folder + "RoadClearanceValidation.txt", "PASS: " + samples + " camera/heading/growth/lamp-height cases; both visible triangle edges stay above the road from lamp to far end; far edge clears by 0.1m, lamp anchor and horizontal reach preserved. No additional geometry or draw batches.");
        }

        private static int ValidateModels(Transform root, VoxelCarController player, EndlessVoxelRoad road,
            Camera camera, VoxelTrackDefinition track)
        {
            int count = 0;
            var tunings = Resources.LoadAll<VoxelEnemyVehicleTuning>("EnemyVehicles").Where(t => t.modelPrefab != null).ToList();
            var bossTunings = new List<VoxelEnemyVehicleTuning>();
            foreach (var boss in Resources.LoadAll<VoxelBossDefinition>("Bosses"))
            {
                if (boss.bossPrefab == null) continue;
                var tuning = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>();
                tuning.modelPrefab = boss.bossPrefab; tuning.name = boss.name;
                tunings.Add(tuning); bossTunings.Add(tuning);
            }
            try
            {
                foreach (var tuning in tunings)
                {
                    string before = EditorJsonUtility.ToJson(tuning.modelPrefab);
                    var go = Child(root, "Headlight check " + tuning.name);
                    var car = go.AddComponent<VoxelEnemyCar>();
                    car.Configure(player, VoxelObstacleCarTuning.Load(), tuning, road, 40, 0); car.enabled = false;
                    Layer(go);
                    int renderers = go.GetComponentsInChildren<MeshRenderer>(true).Length;
                    int colliders = go.GetComponentsInChildren<Collider>(true).Length;
                    var effect = go.GetComponent<VoxelNightHeadlights>();
                    Check(effect != null && effect.BoundLensCount >= 2, tuning.name + " has no front lamp binding.");
                    VoxelNightHeadlights.SubmitDraws(camera, false);
                    Check(VoxelNightHeadlights.LastBeamCount == 2, tuning.name + " did not produce two headlight cards.");
                    var lit = go.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.sharedMaterial == VoxelNightHeadlightSettings.Load().enemyLampMaterial).ToArray();
                    Check(effect.IsEnemyVehicle && lit.Length >= 2, "Enemy/boss model did not bind its red lamps.");
                    Check(lit.All(r => r.transform.position.z > go.transform.position.z && !r.name.ToLowerInvariant().Contains("plate")), "Non-front lamp recoloured.");
                    foreach (var lens in lit.Where(r => go.transform.InverseTransformPoint(r.transform.position).x < 0)) lens.gameObject.SetActive(false);
                    VoxelNightHeadlights.SubmitDraws(camera, false);
                    Check(VoxelNightHeadlights.LastBeamCount == 1, "Destroyed headlamp still casts a beam.");
                    foreach (var lens in lit) lens.gameObject.SetActive(true);
                    VoxelMissileValidation.Set(car, "hasBeenRammed", true);
                    VoxelNightHeadlights.SubmitDraws(camera, false);
                    Check(VoxelNightHeadlights.LastBeamCount == 0, "Wreck still casts beams.");
                    VoxelMissileValidation.Set(car, "hasBeenRammed", false);
                    effect.enabled = false;
                    VoxelMissileValidation.Call(effect, "OnDisable");
                    VoxelNightHeadlights.SubmitDraws(camera, false);
                    Check(VoxelNightHeadlights.LastBeamCount == 0, "Disabled headlights still render.");
                    Check(go.GetComponentsInChildren<MeshRenderer>(true).Length == renderers && go.GetComponentsInChildren<Collider>(true).Length == colliders,
                        "Beam cards changed damage/collision mesh membership.");
                    Check(EditorJsonUtility.ToJson(tuning.modelPrefab) == before, "Changed authored prefab.");
                    Object.DestroyImmediate(go); count++;
                }
                track.nightTime = false;
                var day = Child(root, "Daylight binding check");
                Check(VoxelNightHeadlights.Attach(day, track) == null && day.GetComponent<VoxelNightHeadlights>() == null, "Daytime headlights created.");
                Object.DestroyImmediate(day); track.nightTime = true;
                return count;
            }
            finally { foreach (var tuning in bossTunings) Object.DestroyImmediate(tuning); }
        }

        private static GameObject Child(Transform parent, string name)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
        private static void Layer(GameObject root)
        { foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31; }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Render(Camera camera, string path)
        {
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGBHalf);
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                VoxelNightHeadlights.SubmitDraws(camera);
                camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(texture);
            }
        }
    }
}
