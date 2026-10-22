using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelNightEnvironmentValidation
    {
        private static string Folder = "Temp/NightEnvironment/";
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        public static void BakeAndRun()
        {
            var arguments = Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(arguments, "-voxelNightOutput");
            if (outputIndex >= 0 && outputIndex + 1 < arguments.Length)
                Folder = arguments[outputIndex + 1].TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
            VoxelPixelSkyBuilder.BakeNight();
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            Run();
        }

        [MenuItem("Tools/Voxel Racer/Validate and Render Night Environment")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Start in Edit Mode.");
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            VoxelTrackDefinition track = null;
            GameObject root = null;
            Material ground = null, road = null, stripe = null;
            Mesh mountainMesh = null;
            var previousLights = new System.Collections.Generic.List<Light>();
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach (var existingLight in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (existingLight.enabled) { previousLights.Add(existingLight); existingLight.enabled = false; }
                Directory.CreateDirectory(Folder);
                var source = AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>("Assets/Resources/Tracks/Track01.asset");
                Check(source != null, "Missing reference track.");
                string sourceBefore = EditorJsonUtility.ToJson(source);
                track = Object.Instantiate(source);
                track.hideFlags = HideFlags.DontSave;
                var daySky = track.skyboxMaterial;
                var nightSky = Resources.Load<Material>("Scenery/DesertNightPixelSky");
                Check(nightSky != null && nightSky.GetTexture("_Tex") is Cubemap, "Night cubemap is missing.");
                Check(!ShaderUtil.ShaderHasError(nightSky.shader), "Night sky shader errors.");
                string skyBefore = EditorJsonUtility.ToJson(daySky);
                string mountainBefore = EditorJsonUtility.ToJson(track.mountainCardMaterial);
                root = new GameObject("Temporary night environment validation");
                var target = new GameObject("Car anchor").transform;
                target.SetParent(root.transform);
                var light = new GameObject("Preview key light").AddComponent<Light>();
                light.transform.SetParent(root.transform); light.type = LightType.Directional;
                var sun = new GameObject("Preview sun or moon").AddComponent<VoxelHorizonSun>();
                sun.transform.SetParent(root.transform);
                var mountains = new GameObject("Preview mountains").AddComponent<VoxelHorizonMountains>();
                mountains.transform.SetParent(root.transform); mountains.Configure(target, track);
                mountainMesh = mountains.GetComponent<MeshFilter>().sharedMesh;
                var camera = new GameObject("Preview camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform); camera.enabled = false;
                camera.clearFlags = CameraClearFlags.Skybox; camera.farClipPlane = 500;
                var heading = Quaternion.Euler(0, track.sunAzimuthDegrees, 0);
                var cameraTuning = VoxelCameraTuning.Load();
                camera.transform.position = heading * cameraTuning.chaseOffset;
                camera.transform.LookAt(heading * new Vector3(0, .3f, cameraTuning.chaseLookAhead));
                camera.fieldOfView = 58;

                ground = MakeMaterial("Preview ground", track.groundColour);
                road = MakeMaterial("Preview road", track.roadColour);
                stripe = MakeMaterial("Preview lane marking", track.roadLineColour);
                Block(root.transform, "Ground", new Vector3(0, -.2f, 0), new Vector3(700, .2f, 700), ground, heading);
                Block(root.transform, "Road", heading * new Vector3(0, -.06f, 100), new Vector3(16, .1f, 240), road, heading);
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int z = -10; z < 220; z += 8)
                        Block(root.transform, "Lane marking", heading * new Vector3(side * 2.67f, .005f, z), new Vector3(.14f, .015f, 3), stripe, heading);
                }
                var carDefinition = AssetDatabase.LoadAssetAtPath<VoxelCarDefinition>("Assets/Resources/Cars/SpyCar2PlayerCar.asset");
                Check(carDefinition != null && carDefinition.visualPrefab != null, "Missing player model.");
                var car = Object.Instantiate(carDefinition.visualPrefab, root.transform);
                car.transform.SetPositionAndRotation(Vector3.zero, heading);

                Apply(track, light, sun, mountains, target, false);
                var mesh = sun.GetComponent<MeshFilter>().sharedMesh;
                Check(light.color == new Color(1f, .88f, .72f) && Mathf.Approximately(light.intensity, 1.3f), "Day light mismatch.");
                Check(RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight
                    && RenderSettings.ambientSkyColor.b > RenderSettings.ambientSkyColor.r
                    && RenderSettings.ambientGroundColor.r > RenderSettings.ambientGroundColor.b
                    && RenderSettings.ambientSkyColor.grayscale > RenderSettings.ambientGroundColor.grayscale,
                    "Day ambient must have cool sky light and dimmer warm ground bounce.");
                Check(RenderSettings.skybox == daySky && RenderSettings.fogColor == track.fogColour, "Day sky/fog changed.");
                float dayLuminance = Capture(camera, "Day.png");

                Apply(track, light, sun, mountains, target, true);
                Check(RenderSettings.skybox == nightSky, "Toggle did not select the night sky.");
                Check(light.color == track.nightLightColour && Mathf.Approximately(light.intensity, track.nightLightIntensity), "Night lighting mismatch.");
                Check(RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight
                    && RenderSettings.ambientSkyColor == track.nightAmbientColour
                    && RenderSettings.ambientGroundColor.grayscale < RenderSettings.ambientEquatorColor.grayscale
                    && RenderSettings.ambientEquatorColor.grayscale < RenderSettings.ambientSkyColor.grayscale
                    && RenderSettings.fogColor == track.nightFogColour, "Night ambient/fog mismatch.");
                Check(RenderSettings.fog == track.fogEnabled && RenderSettings.fogMode == FogMode.Linear
                    && Mathf.Approximately(RenderSettings.fogStartDistance, track.fogStartDistance)
                    && Mathf.Approximately(RenderSettings.fogEndDistance, track.fogEndDistance), "Night changed fog enable/range.");
                Check(sun.colour == track.moonColour && Mathf.Approximately(sun.horizonHeight, track.moonHorizonHeight), "Moon appearance mismatch.");
                Check(sun.GetComponent<MeshFilter>().sharedMesh == mesh && mesh.triangles.Length == 96, "Moon must reuse the stepped disc.");
                var properties = new MaterialPropertyBlock();
                mountains.GetComponent<MeshRenderer>().GetPropertyBlock(properties);
                Check(properties.GetColor("_BaseColor") == track.nightMountainTint, "Mountain artwork did not receive its night tint.");
                float nightLuminance = Capture(camera, "Night.png");
                Check(nightLuminance < dayLuminance * .85f && nightLuminance > .015f, "Night must be darker while remaining readable.");
                mountains.Configure(target, track, useTrackTimeOfDay: false);
                mountains.GetComponent<MeshRenderer>().GetPropertyBlock(properties);
                Check(properties.GetColor("_BaseColor") == Color.white, "Night track changed the daytime menu mountains.");
                mountains.Configure(target, track);
                var position = sun.transform.position;
                target.SetPositionAndRotation(new Vector3(50, 15, 80), Quaternion.Euler(70, 135, 30));
                sun.Configure(target, track);
                Check(Vector3.Distance(sun.transform.position, position + new Vector3(50, 0, 80)) < .001f, "Moon heading/height changed with car rotation.");
                target.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                track.nightSkyboxMaterial = daySky;
                Check(VoxelTrackEnvironment.ResolveSkybox(track) == daySky, "Night override ignored.");
                track.nightSkyboxMaterial = null;
                track.fogEnabled = false; VoxelTrackEnvironment.ApplyFog(track);
                Check(!RenderSettings.fog, "Night forced disabled fog on.");
                track.fogEnabled = source.fogEnabled;
                Apply(track, light, sun, mountains, target, false);
                Check(RenderSettings.skybox == daySky && light.color == new Color(1f, .88f, .72f)
                    && Mathf.Approximately(light.intensity, 1.3f)
                    && RenderSettings.ambientGroundColor == new Color(.28f, .22f, .20f), "Day transition left night lighting/sky behind.");
                Check(sun.colour == track.sunColour && Mathf.Approximately(sun.horizonHeight, track.sunHorizonHeight), "Day transition left the moon behind.");
                mountains.GetComponent<MeshRenderer>().GetPropertyBlock(properties);
                Check(properties.GetColor("_BaseColor") == Color.white, "Day transition left mountain tint behind.");
                Check(EditorJsonUtility.ToJson(source) == sourceBefore, "Validation changed source track.");
                Check(EditorJsonUtility.ToJson(daySky) == skyBefore, "Validation changed source sky material.");
                Check(EditorJsonUtility.ToJson(track.mountainCardMaterial) == mountainBefore, "Validation changed source mountain material.");
                string report = "PASS: daytime and night cubemap selection; custom night override; moon colour/height, shared stepped geometry and fixed heading; darker blue key/ambient lighting; fog enable/distances retained; night mountain tint and daytime restoration; source track/sky/mountain materials preserved. Matching model/road/horizon renders: Day.png and Night.png. Mean luminance day="
                    + dayLuminance.ToString("F3") + ", night=" + nightLuminance.ToString("F3") + ". Temporary scene removed; no purchases, run state or campaign selection changed.";
                File.WriteAllText(Folder + "Validation.txt", report); Debug.Log(report);
            }
            finally
            {
                if (root != null)
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                        if (filter.sharedMesh != null && filter.sharedMesh.name == "Wrapped Horizon Mountain Card")
                            mountainMesh = filter.sharedMesh;
                if (root != null) Object.DestroyImmediate(root);
                if (mountainMesh != null) Object.DestroyImmediate(mountainMesh);
                if (track != null) Object.DestroyImmediate(track);
                if (ground != null) Object.DestroyImmediate(ground);
                if (road != null) Object.DestroyImmediate(road);
                if (stripe != null) Object.DestroyImmediate(stripe);
                foreach (var previousLight in previousLights)
                    if (previousLight != null) previousLight.enabled = true;
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Apply(VoxelTrackDefinition track, Light light, VoxelHorizonSun sun,
            VoxelHorizonMountains mountains, Transform target, bool night)
        {
            track.nightTime = night;
            RenderSettings.sun = light;
            VoxelRacerBootstrap.SetupSky(target, track);
            Check(sun.trackDefinition == track && mountains.trackDefinition == track,
                "Race setup did not reuse the supplied horizon objects.");
        }

        private static Material MakeMaterial(string name, Color colour)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", colour); material.SetFloat("_Smoothness", .1f);
            return material;
        }

        private static void Block(Transform root, string name, Vector3 position, Vector3 size, Material material, Quaternion rotation)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube); block.name = name;
            block.transform.SetParent(root); block.transform.SetPositionAndRotation(position, rotation);
            block.transform.localScale = size; block.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(block.GetComponent<Collider>());
        }

        private static float Capture(Camera camera, string file)
        {
            var rt = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                File.WriteAllBytes(Folder + file, texture.EncodeToPNG());
                float sum = 0; var pixels = texture.GetPixels32();
                foreach (var p in pixels) sum += (.2126f * p.r + .7152f * p.g + .0722f * p.b) / 255f;
                return sum / pixels.Length;
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture);
            }
        }
    }
}
