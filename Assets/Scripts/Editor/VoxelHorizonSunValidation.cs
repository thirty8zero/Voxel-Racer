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
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary horizon sun preview");
            SceneManager.MoveGameObjectToScene(root, scene);
            Mesh mountainMesh = null, sunMesh = null;
            var material = Resources.Load<Material>("Scenery/DesertHorizonSun");
            try
            {
                var tracks = VoxelTrackSequence.Load().tracks;
                Check(tracks != null && tracks.Length > 0, "Missing mission sequence.");
                Check(material != null && !ShaderUtil.ShaderHasError(material.shader), "Missing or invalid sun material/shader.");
                float previousHeight = float.PositiveInfinity;
                foreach (var track in tracks)
                {
                    Check(track != null && track.horizonSunEnabled, "Sun disabled on a campaign mission.");
                    Check(track.sunHorizonHeight <= previousHeight, "Sun must lower through the mission sequence.");
                    previousHeight = track.sunHorizonHeight;
                }
                var target = new GameObject("Horizon anchor").transform; target.SetParent(root.transform);
                var mountains = new GameObject("Mountain artwork").AddComponent<VoxelHorizonMountains>();
                mountains.transform.SetParent(root.transform); mountains.Configure(target, tracks[0]);
                mountainMesh = mountains.GetComponent<MeshFilter>().sharedMesh;
                var sun = new GameObject("Yellow voxel sun").AddComponent<VoxelHorizonSun>();
                sun.transform.SetParent(root.transform); sun.Configure(target, tracks[0]);
                sunMesh = sun.GetComponent<MeshFilter>().sharedMesh;
                Check(sunMesh.vertexCount == 64 && sunMesh.triangles.Length == 96, "Sun geometry exceeded 32 triangles.");
                Check(sun.GetComponentsInChildren<MeshRenderer>().Length == 1 && sun.GetComponentsInChildren<Collider>().Length == 0,
                    "Sun must use one flat renderer and no physics.");
                foreach (var vertex in sunMesh.vertices) Check(vertex.z == 0, "Sun is not a flat 2D object.");
                sun.Build(); Check(sun.GetComponent<MeshFilter>().sharedMesh == sunMesh, "Rebuild duplicated sun geometry.");
                Vector3 initialPosition = sun.transform.position;
                target.position = new Vector3(75, 20, 350); target.rotation = Quaternion.Euler(85, 140, 90);
                sun.Configure(target, tracks[0]);
                Check(Vector3.Distance(sun.transform.position, initialPosition + new Vector3(75, 0, 350)) < .001f,
                    "Vehicle rotation/height moved the sun within the mountain artwork.");
                target.position = Vector3.zero; target.rotation = Quaternion.identity; sun.Configure(target, tracks[0]);

                var camera = new GameObject("Horizon preview camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform); camera.enabled = false; camera.scene = scene;
                camera.transform.position = new Vector3(0, 11, 0);
                camera.transform.rotation = Quaternion.Euler(0, tracks[0].sunAzimuthDegrees, 0);
                camera.fieldOfView = 20; camera.farClipPlane = 500; camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.gameObject.AddComponent<Skybox>().material = tracks[0].skyboxMaterial;
                foreach (var transform in root.GetComponentsInChildren<Transform>()) transform.gameObject.layer = 31;
                Directory.CreateDirectory("Temp/HorizonSun");
                int firstVisible = VisibleSunPixels(camera, sun, "Mission01.png");
                sun.Configure(target, tracks[tracks.Length - 1]);
                int lastVisible = VisibleSunPixels(camera, sun, "Mission06.png");
                Check(firstVisible > 100 && lastVisible > 0 && lastVisible < firstVisible,
                    "Sun should be visible in the gap and increasingly obscured by the mountains: " + firstVisible + "/" + lastVisible);
                mountains.GetComponent<MeshRenderer>().enabled = false;
                int unobscured = VisibleSunPixels(camera, sun, "SunSilhouette.png");
                Check(unobscured > lastVisible, "Mountain layer does not occlude the setting sun.");
                mountains.GetComponent<MeshRenderer>().enabled = true; sun.Configure(target, tracks[0]);
                camera.fieldOfView = 58;
                var tuning = VoxelCameraTuning.Load();
                camera.transform.position = tuning.chaseOffset;
                camera.transform.LookAt(new Vector3(0, .3f, tuning.chaseLookAhead));
                Capture(camera, "Temp/HorizonSun/RaceHorizon.png");
                Object.DestroyImmediate(sun.gameObject);
                Check(sunMesh == null && material != null, "Owned mesh cleanup removed the shared source material or leaked geometry.");
                string report = "PASS (Edit Mode): flat stepped yellow sun, one renderer/32 triangles/no colliders; idempotent mesh build; fixed mountain-relative heading through vehicle rotation, height and translation; enabled campaign tracks with descending heights; visible first/last mission gap renders and increasing mountain occlusion; normal chase horizon render; owned mesh released/shared material preserved. Temporary preview scene removed; source sky/mountain artwork and run progress unchanged. Visible sun pixels: " + firstVisible + " -> " + lastVisible + ".";
                File.WriteAllText("Temp/HorizonSun/Validation.txt", report); Debug.Log(report);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (mountainMesh != null) Object.DestroyImmediate(mountainMesh);
                EditorSceneManager.ClosePreviewScene(scene);
            }
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
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
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
