using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelLoadingScreenPreview
    {
        public static string OutputFolder = "Temp/LoadingScreen";

        [MenuItem("Tools/Voxel Racer/Loading Screen/Render Example")]
        public static void RenderExample()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Render the example from Edit Mode.");
            var settings = VoxelLoadingScreenSettings.Load();
            if (settings == null) throw new InvalidOperationException("Missing Resources/LoadingScreenSettings.asset.");
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary loading screen preview");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                Directory.CreateDirectory(OutputFolder);
                var screen = root.AddComponent<VoxelLoadingScreen>();
                var image = settings.showImages ? settings.images?.FirstOrDefault(settings.IsEligible) : null;
                screen.Show(settings, image);
                var camera = new GameObject("Loading preview camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false); camera.enabled = false;
                camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.magenta; camera.cullingMask = 1 << 31;
                camera.nearClipPlane = .1f; camera.farClipPlane = 10f;
                var canvas = root.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
                foreach (var item in root.GetComponentsInChildren<Transform>()) item.gameObject.layer = 31;
                foreach (int width in new[] { 1920, 2400 })
                {
                    Capture(camera, screen, width, "Example_" + width + ".png");
                    ValidateLayout(canvas, width, image);
                }
                // Actual animation samples, available for an animated preview without simulating gameplay.
                for (int frame = 0; frame < 12; frame++)
                    Capture(camera, screen, 1280, "Jiggle_" + frame.ToString("D2") + ".png", frame / 12f);
                string report = "PASS (Edit Mode render): runtime loading UI at 16:9 and 20:9; opaque black backdrop and reserved top/bottom bars; white LOADING fully inside the footer; image aspect preserved; 12 unscaled jiggle samples. Example image: " + (image != null ? image.name : "none (plain black fallback)") + ". Temporary preview scene removed; source settings/images unchanged.";
                File.WriteAllText(Path.Combine(OutputFolder, "PreviewValidation.txt"), report); Debug.Log(report);
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void ValidateLayout(Canvas canvas, int width, Texture2D image)
        {
            var label = canvas.GetComponentInChildren<Text>();
            var bottom = (RectTransform)canvas.transform.Find("Bottom black bar");
            var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector3 local = bottom.InverseTransformPoint(corner);
                if (!bottom.rect.Contains(local)) throw new InvalidOperationException("LOADING left the bottom black bar at width " + width);
            }
            if (label.text != "LOADING" || label.color != Color.white) throw new InvalidOperationException("Loading label style changed.");
            var art = canvas.GetComponentInChildren<RawImage>();
            if (image != null && Mathf.Abs(art.rectTransform.rect.width / art.rectTransform.rect.height - image.width / (float)image.height) > .001f)
                throw new InvalidOperationException("Loading image stretched.");
        }

        private static void Capture(Camera camera, VoxelLoadingScreen screen, int width, string file, float time = 0f)
        {
            int height = width == 1280 ? 720 : 1080;
            var rt = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; screen.AnimateLabel(time);
                Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(OutputFolder, file), texture.EncodeToPNG());
                var pixels = texture.GetPixels32();
                foreach (int row in new[] { 0, height - 1 })
                    for (int x = 0; x < width; x++)
                    {
                        var pixel = pixels[row * width + x];
                        if (pixel.r > 1 || pixel.g > 1 || pixel.b > 1) throw new InvalidOperationException("Screen edge is not opaque black.");
                    }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture);
            }
        }

        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-voxelLoadingOutput");
            if (index >= 0 && index + 1 < args.Length) OutputFolder = args[index + 1];
            RenderExample();
        }
    }
}
