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
    /// <summary>Repeatable comparison using the real road, player model and mobile profile.</summary>
    public static class VoxelLightingPreview
    {
        public static void CaptureBefore() => Capture("Before");
        [MenuItem("Tools/Voxel Racer/Lighting/Render Day and Night Preview")]
        public static void CaptureAfter() => Capture("After");

        public static void CaptureDioramaBefore(VolumeProfile profileOverride = null, Action<Light, VoxelTrackDefinition> lightingOverride = null)
        {
            Capture("Before", true, false, profileOverride, lightingOverride);
            Capture("Before", true, true, profileOverride, lightingOverride);
        }

        [MenuItem("Tools/Voxel Racer/Lighting/Render Diorama Day and Night Preview")]
        public static void CaptureDioramaAfter()
        {
            Capture("After", true, false);
            Capture("After", true, true);
        }

        [MenuItem("Tools/Voxel Racer/Lighting/Validate Diorama Focus")]
        public static void ValidateDioramaFocus()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Validate from Edit Mode.");
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var settings = VoxelPostProcessingSettings.Load();
            string settingsJson = EditorJsonUtility.ToJson(settings);
            string profileJson = EditorJsonUtility.ToJson(settings.profile);
            var stack = VolumeManager.instance.CreateStack();
            VolumeProfile ownedProfile = null;
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("Temporary focus validation"); root.layer = 31;
                var camera = root.AddComponent<Camera>(); camera.enabled = false;
                VoxelMobilePostProcessing.Configure(camera);
                var controller = root.GetComponent<VoxelMobilePostProcessing>();
                bool Blurring()
                {
                    VolumeManager.instance.Update(stack, root.transform, 1 << 31);
                    return stack.GetComponent<DepthOfField>().IsActive();
                }
                void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
                Check(!Blurring(), "Racing camera unexpectedly requests depth of field.");
                controller.SetDebriefFocus(true, 8f);
                Check(Blurring(), "Debrief camera did not enable distance blur.");
                var focus = stack.GetComponent<DepthOfField>();
                Check(focus.mode.value == DepthOfFieldMode.Gaussian && !focus.highQualitySampling.value &&
                    focus.gaussianStart.value >= 8f + settings.debriefSharpMargin - .01f && focus.gaussianMaxRadius.value <= 1f,
                    "Debrief focus lost the sharp car margin or low-cost settings.");
                ownedProfile = root.GetComponentsInChildren<Volume>().Single(v => v.priority == 11f).sharedProfile;
                for (int i = 0; i < 20; i++) { controller.Apply(); controller.SetDebriefFocus(true, 8f); }
                Check(root.GetComponentsInChildren<Volume>().Length == 2, "Repeated updates duplicated volumes.");
                settings.effectsEnabled = false; controller.Apply();
                Check(!Blurring() && !camera.GetUniversalAdditionalCameraData().renderPostProcessing, "Master switch left effects active.");
                settings.effectsEnabled = true; settings.debriefDepthOfField = false; controller.Apply();
                Check(!Blurring(), "Optional debrief switch left blur active.");
                settings.debriefDepthOfField = true; controller.Apply();
                Check(Blurring(), "Re-enabling debrief focus failed.");
                // Non-ExecuteAlways behaviours do not receive these callbacks in Edit
                // Mode. Exercise the actual callback bodies explicitly in this harness.
                controller.enabled = false; controller.SendMessage("OnDisable");
                Check(!Blurring(), "Disabled controller left blur active.");
                controller.enabled = true; controller.SendMessage("OnEnable");
                Check(Blurring(), "Re-enabled controller did not restore requested focus.");
                controller.SetDebriefFocus(false, 0); Check(!Blurring(), "Leaving debrief retained blur.");
                controller.SetDebriefFocus(true, 8f);
                controller.SendMessage("OnDestroy"); Object.DestroyImmediate(root);
                Check(ownedProfile == null, "Runtime focus profile leaked after camera destruction.");
                Check(EditorJsonUtility.ToJson(settings.profile) == profileJson, "Focus altered the authored shared profile.");
                Directory.CreateDirectory("Temp/DioramaComparison");
                File.WriteAllText("Temp/DioramaComparison/Validation.txt",
                    "PASS (Edit Mode, controlled lifecycle callbacks): actual VolumeManager stack has no DOF for racing; Gaussian far blur preserves the car's focus margin; low-quality sampling/radius cap; repeated Apply/focus updates do not duplicate volumes; master and optional blur switches, component disable/re-enable and return to racing clear/restore effects correctly; destruction callback releases the owned profile; shared authored profile unchanged. Temporary scene/settings restored. Device GPU timing not measured.");
                Debug.Log("PASS diorama focus and lifecycle validation.");
            }
            finally
            {
                EditorJsonUtility.FromJsonOverwrite(settingsJson, settings);
                VolumeManager.instance.DestroyStack(stack);
                EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(previousScene);
            }
        }

        private static void Capture(string version, bool diorama = false, bool? nightTrack = null,
            VolumeProfile profileOverride = null, Action<Light, VoxelTrackDefinition> lightingOverride = null)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Render from Edit Mode.");
            string folder = diorama ? "Temp/DioramaComparison/" : "Temp/LightingComparison/";
            Directory.CreateDirectory(folder);
            var previousScene = SceneManager.GetActiveScene();
            var random = UnityEngine.Random.state;
            var fields = typeof(VoxelRacerBootstrap).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(Material)).ToArray();
            var savedMaterials = fields.Select(f => f.GetValue(null)).ToArray();
            var previousMaterials = new HashSet<Material>(Resources.FindObjectsOfTypeAll<Material>());
            var previousMeshes = new HashSet<Mesh>(Resources.FindObjectsOfTypeAll<Mesh>());
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            VoxelTrackDefinition track = null;
            VoxelRoadTuning tuning = null;
            var lights = new List<Light>();
            var originalMaterials = new Dictionary<Material, Material>();
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach (var oldLight in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (oldLight.enabled) { oldLight.enabled = false; lights.Add(oldLight); }
                var source = Resources.Load<VoxelTrackDefinition>(nightTrack == true ? "Tracks/Track06" : "Tracks/Track01");
                string sourceJson = EditorJsonUtility.ToJson(source);
                track = Object.Instantiate(source);
                track.hideFlags = HideFlags.DontSave;
                tuning = Object.Instantiate(track.roadTuning);
                typeof(VoxelRacerBootstrap).GetMethod("PrepareTrackMaterials", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { track });
                UnityEngine.Random.InitState(173);
                var root = new GameObject("Temporary lighting comparison");
                var roadObject = new GameObject("Road"); roadObject.transform.SetParent(root.transform);
                var road = roadObject.AddComponent<EndlessVoxelRoad>();
                road.enabled = false; road.trackDefinition = track; road.SetTuning(tuning);
                road.randomizeTurnSeedEachRun = false; road.turnSeed = 173;
                road.BuildInitialRoad();
                var definition = Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
                var car = Object.Instantiate(definition.visualPrefab, root.transform);
                if (!diorama && version == "Before")
                {
                    // Preserve the original first-pass material values on preview-only clones.
                    foreach (var values in new[] { ("LongtailPaint", 0f, .22f), ("SpyCar2Alloy", 0f, .22f), ("CarMetalDetails", 0f, .62f) })
                    {
                        var material = Resources.Load<Material>("CarMaterials/" + values.Item1);
                        var copy = new Material(material);
                        copy.SetFloat("_Metallic", values.Item2); copy.SetFloat("_Smoothness", values.Item3);
                        originalMaterials.Add(material, copy);
                    }
                    foreach (var renderer in car.GetComponentsInChildren<Renderer>(true))
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m != null && originalMaterials.TryGetValue(m, out var copy) ? copy : m).ToArray();
                }
                car.transform.position = new Vector3(-1.5f, 0, 12);
                road.SetTarget(car.transform);
                var light = new GameObject("Main light").AddComponent<Light>();
                light.transform.SetParent(root.transform); light.type = LightType.Directional;
                light.shadows = LightShadows.Hard; light.shadowBias = .05f; light.shadowNormalBias = .4f;
                light.cullingMask = 1 << 31; RenderSettings.sun = light;
                var sun = new GameObject("Sun or moon").AddComponent<VoxelHorizonSun>(); sun.transform.SetParent(root.transform);
                var mountains = new GameObject("Mountains").AddComponent<VoxelHorizonMountains>(); mountains.transform.SetParent(root.transform);
                var camera = new GameObject("Comparison camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                // Volume caches its layer when enabled. Set this before Configure creates
                // the volume so an immediate Edit Mode render actually includes the grade.
                camera.gameObject.layer = 31;
                camera.enabled = false; camera.clearFlags = CameraClearFlags.Skybox;
                camera.farClipPlane = 500; camera.fieldOfView = 52; camera.allowHDR = true;
                camera.cullingMask = 1 << 31;
                camera.transform.position = car.transform.position + new Vector3(-8.5f, 6f, -11);
                camera.transform.LookAt(car.transform.position + new Vector3(0, .7f, 9));
                VoxelMobilePostProcessing.Configure(camera);
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                if (profileOverride != null)
                    camera.GetComponentsInChildren<Volume>().Single(v => v.priority == 10f).sharedProfile = profileOverride;
                var scenery = roadObject.AddComponent<VoxelDistantScenery>(); scenery.enabled = false;
                foreach (bool night in nightTrack.HasValue ? new[] { nightTrack.Value } : new[] { false, true })
                {
                    track.nightTime = night;
                    VoxelTrackEnvironment.ApplyLighting(light, track);
                    lightingOverride?.Invoke(light, track);
                    if (!diorama && version == "Before")
                    {
                        light.color = night ? track.nightLightColour : Color.white;
                        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                        RenderSettings.ambientLight = night ? track.nightAmbientColour : new Color(.55f, .62f, .72f);
                    }
                    VoxelTrackEnvironment.ApplyFog(track);
                    RenderSettings.skybox = VoxelTrackEnvironment.ResolveSkybox(track);
                    sun.Configure(car.transform, track); mountains.Configure(car.transform, track);
                    foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                    scenery.gameObject.layer = 31;
                    if (diorama)
                    {
                        var cameraTuning = VoxelCameraTuning.Load();
                        foreach (bool debrief in new[] { false, true })
                        {
                            camera.transform.position = car.transform.position + (debrief ? cameraTuning.finishOffset : cameraTuning.chaseOffset);
                            camera.transform.LookAt(car.transform.position + (debrief
                                ? Vector3.right * cameraTuning.finishLookSideOffset + Vector3.up * cameraTuning.finishLookHeight
                                : Vector3.forward * cameraTuning.chaseLookAhead + Vector3.up * .3f));
                            camera.fieldOfView = debrief ? cameraTuning.finishFieldOfView : 60f;
                            camera.GetComponent<VoxelMobilePostProcessing>().SetDebriefFocus(
                                debrief && version == "After", camera.transform.InverseTransformPoint(car.transform.position + Vector3.up * .9f).z);
                            scenery.SubmitDraws(camera);
                            Render(camera, folder + (night ? "Night" : "Day") + (debrief ? "Debrief" : "Chase") + version + ".png");
                            var stack = camera.GetUniversalAdditionalCameraData().volumeStack ?? VolumeManager.instance.stack;
                            var expected = profileOverride != null ? profileOverride : VoxelPostProcessingSettings.Load().profile;
                            if (expected.TryGet<ColorAdjustments>(out var colour) &&
                                Mathf.Abs(stack.GetComponent<ColorAdjustments>().contrast.value - colour.contrast.value) > .01f)
                                throw new InvalidOperationException("Comparison camera did not apply the expected colour-grading volume.");
                        }
                    }
                    else
                    {
                        // Draw the runtime instanced distant scenery for this exact camera.
                        scenery.SubmitDraws(camera);
                        Render(camera, folder + (night ? "Night" : "Day") + version + ".png");
                    }
                    Debug.Log($"Lighting preview: {version}, night={night}, ambient={RenderSettings.ambientMode}, key={light.color}, intensity={light.intensity}, renderers={root.GetComponentsInChildren<Renderer>().Length}.");
                }
                if (EditorJsonUtility.ToJson(source) != sourceJson) throw new InvalidOperationException("Preview changed source track.");
            }
            finally
            {
                foreach (var controller in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<VoxelMobilePostProcessing>(true)))
                    controller.ReleaseDebriefResources();
                var generatedMaterials = fields.Select(f => f.GetValue(null) as Material)
                    .Where(m => m != null && !previousMaterials.Contains(m) && !EditorUtility.IsPersistent(m)).ToArray();
                var generatedMeshes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                    .Select(f => f.sharedMesh).Where(m => m != null && !previousMeshes.Contains(m) && !EditorUtility.IsPersistent(m)).Distinct().ToArray();
                EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(previousScene);
                for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, savedMaterials[i]);
                foreach (var material in generatedMaterials) if (material != null) Object.DestroyImmediate(material);
                foreach (var mesh in generatedMeshes) if (mesh != null) Object.DestroyImmediate(mesh);
                foreach (var material in originalMaterials.Values) Object.DestroyImmediate(material);
                if (track != null) Object.DestroyImmediate(track);
                if (tuning != null) Object.DestroyImmediate(tuning);
                foreach (var light in lights) if (light != null) light.enabled = true;
                UnityEngine.Random.state = random;
            }
        }

        private static void Render(Camera camera, string path)
        {
            var target = new RenderTexture(1440, 810, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var texture = new Texture2D(1440, 810, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1440, 810), 0, 0); texture.Apply();
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
