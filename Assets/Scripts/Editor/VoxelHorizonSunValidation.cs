using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelHorizonSunValidation
    {
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); }

        [MenuItem("Tools/Voxel Racer/Validate and Render Horizon Sun")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Start in Edit Mode.");
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var root = new GameObject("Temporary horizon sun preview");
            SceneManager.MoveGameObjectToScene(root, scene);
            Mesh mountainMesh = null, sunMesh = null;
            var material = Resources.Load<Material>("Scenery/DesertHorizonSun");
            VoxelTrackDefinition day = null, sunset = null;
            try
            {
                var tracks = VoxelTrackSequence.Load().tracks;
                Check(tracks != null && tracks.Length > 0, "Missing mission sequence.");
                Check(material != null && !ShaderUtil.ShaderHasError(material.shader), "Missing or invalid sun material/shader.");
                foreach (var track in tracks)
                {
                    Check(track != null && track.horizonSunEnabled, "Sun disabled on a campaign mission.");
                }
                // Campaigns can mix day/night tracks and place a boss between normal
                // missions. Test setting-sun occlusion with consistent cloned day fixtures.
                day = Object.Instantiate(tracks[0]); day.nightTime = false; day.sunHorizonHeight = 42f;
                sunset = Object.Instantiate(day); sunset.sunHorizonHeight = 0f;
                var target = new GameObject("Horizon anchor").transform; target.SetParent(root.transform);
                var mountains = new GameObject("Mountain artwork").AddComponent<VoxelHorizonMountains>();
                mountains.transform.SetParent(root.transform); mountains.Configure(target, day);
                mountainMesh = mountains.GetComponent<MeshFilter>().sharedMesh;
                var sun = new GameObject("Yellow voxel sun").AddComponent<VoxelHorizonSun>();
                sun.transform.SetParent(root.transform);
                foreach (var track in tracks)
                {
                    sun.Configure(target, track);
                    Check(Mathf.Abs(sun.horizonHeight - (track.nightTime ? track.moonHorizonHeight : track.sunHorizonHeight)) < .001f,
                        "Sun/moon does not read the individual track's height.");
                }
                sun.Configure(target, day);
                sunMesh = sun.GetComponent<MeshFilter>().sharedMesh;
                Check(sunMesh.vertexCount == 64 && sunMesh.triangles.Length == 96, "Sun geometry exceeded 32 triangles.");
                Check(sun.GetComponentsInChildren<MeshRenderer>().Length == 1 && sun.GetComponentsInChildren<Collider>().Length == 0,
                    "Sun must use one flat renderer and no physics.");
                foreach (var vertex in sunMesh.vertices) Check(vertex.z == 0, "Sun is not a flat 2D object.");
                sun.Build(); Check(sun.GetComponent<MeshFilter>().sharedMesh == sunMesh, "Rebuild duplicated sun geometry.");
                Vector3 initialPosition = sun.transform.position;
                target.position = new Vector3(75, 20, 350); target.rotation = Quaternion.Euler(85, 140, 90);
                sun.Configure(target, day);
                Check(Vector3.Distance(sun.transform.position, initialPosition + new Vector3(75, 0, 350)) < .001f,
                    "Vehicle rotation/height moved the sun within the mountain artwork.");
                target.position = Vector3.zero; target.rotation = Quaternion.identity; sun.Configure(target, day);

                var camera = new GameObject("Horizon preview camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform); camera.enabled = false;
                camera.transform.position = new Vector3(0, 11, 0);
                camera.transform.rotation = Quaternion.Euler(0, tracks[0].sunAzimuthDegrees, 0);
                camera.fieldOfView = 20; camera.farClipPlane = 500; camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.Skybox;
                RenderSettings.skybox = day.skyboxMaterial;
                foreach (var transform in root.GetComponentsInChildren<Transform>()) transform.gameObject.layer = 31;
                Directory.CreateDirectory("Temp/HorizonSun");
                int firstVisible = VisibleSunPixels(camera, sun, "Mission01.png");
                sun.Configure(target, sunset);
                int lastVisible = VisibleSunPixels(camera, sun, "Mission06.png");
                mountains.GetComponent<MeshRenderer>().enabled = false;
                int unobscured = VisibleSunPixels(camera, sun, "SunSilhouette.png");
                Check(firstVisible > 100 && lastVisible < firstVisible,
                    "Sun should be visible in the gap and increasingly obscured by the mountains: " + firstVisible + "/" + lastVisible + "; unobscured=" + unobscured);
                Check(unobscured > lastVisible, "Mountain layer does not occlude the setting sun.");
                mountains.GetComponent<MeshRenderer>().enabled = true; sun.Configure(target, day);
                CheckWorldInFront(camera, mountains);
                camera.fieldOfView = 58;
                var tuning = VoxelCameraTuning.Load();
                camera.transform.position = tuning.chaseOffset;
                camera.transform.LookAt(new Vector3(0, .3f, tuning.chaseLookAhead));
                Capture(camera, "Temp/HorizonSun/RaceHorizon.png");
                Object.DestroyImmediate(sun.gameObject);
                Check(sunMesh == null && material != null, "Owned mesh cleanup removed the shared source material or leaked geometry.");
                string report = "PASS (Edit Mode): flat stepped yellow sun, one renderer/32 triangles/no colliders; idempotent mesh build; fixed mountain-relative heading through vehicle rotation, height and translation; individual campaign day/moon heights; consistent day fixtures show increasing mountain occlusion of a setting sun; world geometry beyond the mountain ring stays in front; normal chase horizon render; owned mesh released/shared material preserved. Temporary preview scene removed; source sky/mountain artwork and run progress unchanged. Visible sun pixels: " + firstVisible + " -> " + lastVisible + ".";
                File.WriteAllText("Temp/HorizonSun/Validation.txt", report); Debug.Log(report);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (mountainMesh != null) Object.DestroyImmediate(mountainMesh);
                if (day != null) Object.DestroyImmediate(day);
                if (sunset != null) Object.DestroyImmediate(sunset);
                EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(previousScene);
            }
        }

        private static void CheckWorldInFront(Camera camera, VoxelHorizonMountains mountains)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.layer = 31;
            quad.transform.position = camera.transform.position + camera.transform.forward * 350;
            quad.transform.rotation = camera.transform.rotation;
            quad.transform.localScale = Vector3.one * 120;
            var green = new Material(Shader.Find("Voxel Racer/Horizon Sun"));
            green.SetColor("_BaseColor", Color.green); green.SetFloat("_HorizonBackground", 0);
            quad.GetComponent<MeshRenderer>().sharedMaterial = green;
            var renderer = mountains.GetComponent<MeshRenderer>();
            try
            {
                var withMountains = Capture(camera, null);
                renderer.enabled = false;
                var withoutMountains = Capture(camera, null);
                int greenPixels = 0;
                for (int i = 0; i < withoutMountains.Length; i++)
                    if (withoutMountains[i].g > 240 && withoutMountains[i].r < 10 && withoutMountains[i].b < 10)
                    {
                        greenPixels++;
                        Check(withMountains[i].g > 240 && withMountains[i].r < 10 && withMountains[i].b < 10,
                            "Mountain artwork hides world geometry beyond its physical ring.");
                    }
                Check(greenPixels > 100, "Foreground depth fixture was not rendered.");
            }
            finally { renderer.enabled = true; Object.DestroyImmediate(quad); Object.DestroyImmediate(green); }
        }

        private static int VisibleSunPixels(Camera camera, VoxelHorizonSun sun, string file)
        {
            var renderer = sun.GetComponent<MeshRenderer>(); renderer.enabled = false;
            var background = Capture(camera, null); renderer.enabled = true;
            var pixels = Capture(camera, "Temp/HorizonSun/" + file);
            int visible = 0;
            for (int i = 0; i < pixels.Length; i++)
                if (Mathf.Abs(pixels[i].r - background[i].r) + Mathf.Abs(pixels[i].g - background[i].g) +
                    Mathf.Abs(pixels[i].b - background[i].b) > 20) visible++;
            return visible;
        }

        private static Color32[] Capture(Camera camera, string file)
        {
            var rt = new RenderTexture(1200, 500, 24);
            var texture = new Texture2D(1200, 500, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1200, 500), 0, 0); texture.Apply();
                if (file != null) File.WriteAllBytes(file, texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture);
            }
        }
    }
}
