using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelMainMenuPreview
    {
        [MenuItem("Tools/Voxel Racer/Main Menu/Render Artwork Preview")]
        public static void RenderExample()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Render from Edit Mode.");
            const string folder = "Temp/MainMenu/";
            Directory.CreateDirectory(folder);
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary main menu preview");
            SceneManager.MoveGameObjectToScene(root, scene);
            var previousEvents = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            try
            {
                var menu = root.AddComponent<VoxelMainMenuController>();
                menu.tuning = VoxelMainMenuTuning.Load();
                if (menu.tuning == null || menu.tuning.titleLogo == null) throw new InvalidOperationException("Main-menu logo missing.");
                menu.BuildArtwork(); menu.BuildMenuUi();
                var camera = new GameObject("Menu preview camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false); camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 31; camera.nearClipPlane = .1f; camera.farClipPlane = 10f;
                foreach (var canvas in root.GetComponentsInChildren<Canvas>())
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera; canvas.planeDistance = 1f;
                }
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                foreach (int width in new[] { 1920, 2400 }) Capture(camera, menu, width, folder);
                Debug.Log("PASS: main-menu logo present, no old title, correct aspect, no logo input interception and START retained at 16:9 and 20:9. Previews: " + folder);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var events in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                    if (!previousEvents.Contains(events)) Object.DestroyImmediate(events.gameObject);
            }
        }

        private static void Capture(Camera camera, VoxelMainMenuController menu, int width, string folder)
        {
            var rt = new RenderTexture(width, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(width, 1080, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt;
                Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render();
                var logo = menu.transform.Find("Main Menu UI/Road Spectre Logo/Logo Image").GetComponent<RawImage>();
                if (logo.texture != menu.tuning.titleLogo || logo.raycastTarget ||
                    Mathf.Abs(logo.rectTransform.rect.width / logo.rectTransform.rect.height - logo.texture.width / (float)logo.texture.height) > .001f)
                    throw new InvalidOperationException($"Logo missing, stretched or blocking input: texture={logo.texture}, rect={logo.rectTransform.rect}, aspect={logo.texture.width / (float)logo.texture.height}, raycast={logo.raycastTarget}.");
                if (menu.GetComponentsInChildren<Text>().Any(t => t.text.Contains("VOXEL RACER")))
                    throw new InvalidOperationException("Old title still visible.");
                var foreground = menu.transform.Find("Main Menu Artwork/Foreground Artwork Area/Muscle Car Foreground").GetComponent<RawImage>();
                var logoCorners = new Vector3[4]; var carCorners = new Vector3[4];
                logo.rectTransform.GetWorldCorners(logoCorners); foreground.rectTransform.GetWorldCorners(carCorners);
                // White roof begins about 29.2% down the supplied image; particles sit above it.
                float carHeight = carCorners[1].y - carCorners[0].y;
                float roofY = carCorners[1].y - carHeight * .292f;
                float roofGap = logoCorners[0].y - roofY;
                if (roofGap <= 0 || roofGap > carHeight * .06f)
                    throw new InvalidOperationException("Title must sit just above the white roof; particle overlap is allowed.");
                var start = menu.transform.Find("Main Menu UI/Start Button").GetComponent<Button>();
                if (!start.interactable || start.image.color != new Color(0, 0, 0, .7f))
                    throw new InvalidOperationException("START presentation changed.");
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, width, 1080), 0, 0); image.Apply();
                File.WriteAllBytes(folder + "RoadSpectre_" + width + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
            }
        }
    }
}
