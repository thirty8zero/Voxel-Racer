using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelHorizonRoadPreview
    {
        public static void CaptureBefore() => Capture("Before", false);
        [MenuItem("Tools/Voxel Racer/Road Horizon/Render Before and After")]
        public static void Run() { CaptureBefore(); Capture("After", true); }
        public static void CaptureAfter() => Capture("After", true);

        private static void Capture(string version, bool horizon)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Render in Edit Mode.");
            const string folder = "Temp/RoadHorizon/";
            Directory.CreateDirectory(folder);
            var originalScene = SceneManager.GetActiveScene();
            var random = UnityEngine.Random.state;
            var fields = typeof(VoxelRacerBootstrap).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(Material)).ToArray();
            var materials = fields.Select(f => f.GetValue(null)).ToArray();
            var existingMaterials = new HashSet<Material>(Resources.FindObjectsOfTypeAll<Material>());
            var existingMeshes = new HashSet<Mesh>(Resources.FindObjectsOfTypeAll<Mesh>());
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var disabledLights = new List<Light>();
            VoxelTrackDefinition track = null;
            VoxelRoadTuning tuning = null;
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach (var existingLight in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (existingLight.enabled) { existingLight.enabled = false; disabledLights.Add(existingLight); }
                var source = Resources.Load<VoxelTrackDefinition>("Tracks/Track01");
                string trackJson = EditorJsonUtility.ToJson(source);
                string roadJson = EditorJsonUtility.ToJson(source.roadTuning);
                track = Object.Instantiate(source); tuning = Object.Instantiate(source.roadTuning);
                typeof(VoxelRacerBootstrap).GetMethod("PrepareTrackMaterials", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { track });
                var root = new GameObject("Temporary road horizon comparison");
                var roadObject = new GameObject("Road"); roadObject.transform.SetParent(root.transform);
                var road = roadObject.AddComponent<EndlessVoxelRoad>(); road.enabled = false;
                road.trackDefinition = track; road.SetTuning(tuning);
                road.randomizeTurnSeedEachRun = false; road.turnSeed = 173;
                // Reflection keeps CaptureBefore usable against the original road implementation.
                typeof(EndlessVoxelRoad).GetField("horizonRoadEnabled")?.SetValue(road, horizon);
                UnityEngine.Random.InitState(173);
                road.BuildInitialRoad();
                var definition = Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
                var model = Object.Instantiate(definition.visualPrefab, root.transform);
                var car = model.AddComponent<VoxelCarController>(); car.enabled = false;
                car.SetLaneLayout(road.laneCount, road.roadWidth / road.laneCount);
                car.SetTrack(road, 12f); road.SetTarget(car.transform);
                var light = new GameObject("Comparison sun").AddComponent<Light>();
                light.transform.SetParent(root.transform); light.type = LightType.Directional;
                light.shadows = LightShadows.Hard; light.shadowBias = .05f; light.shadowNormalBias = .4f;
                light.cullingMask = 1 << 31; RenderSettings.sun = light;
                var sun = new GameObject("Horizon sun or moon").AddComponent<VoxelHorizonSun>(); sun.transform.SetParent(root.transform);
                var mountains = new GameObject("Mountains").AddComponent<VoxelHorizonMountains>(); mountains.transform.SetParent(root.transform);
                var camera = new GameObject("Gameplay chase camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                camera.enabled = false; camera.clearFlags = CameraClearFlags.Skybox;
                camera.fieldOfView = 58; camera.farClipPlane = 1000; camera.allowHDR = true; camera.cullingMask = 1 << 31;
                var follow = camera.gameObject.AddComponent<VoxelCameraFollow>(); follow.enabled = false;
                follow.target = car.transform; follow.tuning = VoxelCameraTuning.Load();
                typeof(VoxelCameraFollow).GetMethod("UpdateFollow", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(follow, new object[] { 0f });
                VoxelMobilePostProcessing.Configure(camera);
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                var scenery = roadObject.AddComponent<VoxelDistantScenery>(); scenery.enabled = false;
                foreach (bool night in new[] { false, true })
                {
                    track.nightTime = night;
                    VoxelTrackEnvironment.ApplyLighting(light, track);
                    VoxelTrackEnvironment.ApplyFog(track);
                    RenderSettings.skybox = VoxelTrackEnvironment.ResolveSkybox(track);
                    sun.Configure(car.transform, track); mountains.Configure(car.transform, track);
                    foreach (var renderer in new[] { sun.GetComponent<MeshRenderer>(), mountains.GetComponent<MeshRenderer>() })
                    {
                        var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                        block.SetFloat("_HorizonBackground", horizon ? 1f : 0f); renderer.SetPropertyBlock(block);
                    }
                    foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                    scenery.SubmitDraws(camera);
                    Render(camera, folder + (night ? "Night" : "Day") + version + ".png");
                }
                if (EditorJsonUtility.ToJson(source) != trackJson || EditorJsonUtility.ToJson(source.roadTuning) != roadJson)
                    throw new InvalidOperationException("Comparison changed source track or road tuning.");
                File.WriteAllText(folder + version + "Capture.txt", "Seed 173; Track01; player at track distance 12; " +
                    "actual Camera Tuning chase pose; FOV 58; 1920x1080; existing mobile post-processing, lighting and fog; " +
                    "same detailed road/scenery. Distant road extension and background depth for mountain/sun artwork differ. " +
                    "Editor render, not a live gameplay screenshot.");
                Debug.Log("Road horizon " + version + " captures saved to " + folder);
            }
            finally
            {
                var generatedMaterials = fields.Select(f => f.GetValue(null) as Material)
                    .Where(m => m != null && !existingMaterials.Contains(m) && !EditorUtility.IsPersistent(m)).Distinct().ToArray();
                var generatedMeshes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                    .Select(f => f.sharedMesh).Where(m => m != null && !existingMeshes.Contains(m) && !EditorUtility.IsPersistent(m)).Distinct().ToArray();
                EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(originalScene);
                for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, materials[i]);
                foreach (var material in generatedMaterials) if (material != null) Object.DestroyImmediate(material);
                foreach (var mesh in generatedMeshes) if (mesh != null) Object.DestroyImmediate(mesh);
                if (track != null) Object.DestroyImmediate(track);
                if (tuning != null) Object.DestroyImmediate(tuning);
                foreach (var light in disabledLights) if (light != null) light.enabled = true;
                UnityEngine.Random.state = random;
            }
        }

        private static void Render(Camera camera, string path)
        {
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = active;
                rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
            }
        }
    }
}
