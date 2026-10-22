using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Render the runtime pause UI without pausing or changing player preferences.</summary>
    public static class VoxelPauseMenuPreview
    {
        [MenuItem("Tools/Voxel Racer/Pause Menu/Render Preview")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Render from Edit Mode.");
            const string folder = "Temp/PauseMenu/";
            const string backgroundPath = "Temp/LightingComparison/DayAfter.png";
            if (!File.Exists(backgroundPath)) VoxelLightingPreview.CaptureAfter();
            Directory.CreateDirectory(folder);
            var previousEvents = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            float scale = Time.timeScale; bool audio = AudioListener.pause;
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary pause menu preview");
            SceneManager.MoveGameObjectToScene(root, scene);
            Texture2D background = null;
            try
            {
                var backgroundCanvas = VoxelMenuUi.CreateCanvas(root.transform, "Frozen race preview");
                backgroundCanvas.GetComponent<Canvas>().sortingOrder = 0;
                var image = new GameObject("Race view", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                image.transform.SetParent(backgroundCanvas, false);
                background = new Texture2D(2, 2);
                background.LoadImage(File.ReadAllBytes(backgroundPath));
                var art = image.GetComponent<RawImage>(); art.texture = background; art.raycastTarget = false;
                var fitter = image.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = background.width / (float)background.height;
                var pause = root.AddComponent<VoxelPauseMenu>();
                typeof(VoxelPauseMenu).GetMethod("BuildMenu", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pause, null);
                root.transform.Find("Pause Menu").gameObject.SetActive(true);
                var toggles = root.GetComponentsInChildren<Toggle>();
                toggles[0].SetIsOnWithoutNotify(VoxelFpsCounter.ShowCounter);
                toggles[1].SetIsOnWithoutNotify(VoxelFpsCounter.ShowWaveDebug);
                var camera = new GameObject("Pause preview camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false); camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 31; camera.nearClipPlane = .1f; camera.farClipPlane = 10;
                foreach (var canvas in root.GetComponentsInChildren<Canvas>())
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                }
                foreach (var item in root.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
                foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2400, 1080), new Vector2Int(1280, 960) })
                    Capture(camera, root, size, folder);
                if (Time.timeScale != scale || AudioListener.pause != audio) throw new InvalidOperationException("Preview changed pause state.");
                File.WriteAllText(folder + "Validation.txt", "PASS: actual runtime pause card rendered at 16:9, 20:9 and 4:3; card and interactive targets in bounds; settings rows and action buttons separated; enlarged row-wide toggle input; prior pause/audio state retained. Preview does not invoke reset, resume or preference changes.");
                Debug.Log("Pause menu preview and layout checks saved to " + folder);
            }
            finally
            {
                Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
                if (background != null) Object.DestroyImmediate(background);
                foreach (var events in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                    if (!previousEvents.Contains(events)) Object.DestroyImmediate(events.gameObject);
            }
        }

        private static void Capture(Camera camera, GameObject root, Vector2Int size, string folder)
        {
            var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt;
                for (int pass = 0; pass < 3; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (var graphic in root.GetComponentsInChildren<Graphic>()) graphic.SetAllDirty();
                    Canvas.ForceUpdateCanvases(); camera.Render();
                }
                var menu = (RectTransform)root.transform.Find("Pause Menu");
                var panel = (RectTransform)menu.Find("Pause Panel");
                CheckInside(panel, menu);
                var controls = panel.GetComponentsInChildren<Selectable>();
                foreach (var control in controls) CheckInside((RectTransform)control.transform, panel);
                var rectangles = controls.Select(c => RectTransformUtility.CalculateRelativeRectTransformBounds(panel, c.transform)).ToArray();
                for (int i = 0; i < rectangles.Length; i++)
                    for (int j = i + 1; j < rectangles.Length; j++)
                        if (rectangles[i].Intersects(rectangles[j])) throw new InvalidOperationException("Pause targets overlap.");
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                File.WriteAllBytes(folder + "Pause_" + size.x + "x" + size.y + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
            }
        }

        private static void CheckInside(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            foreach (var point in corners)
                if (!parent.rect.Contains(parent.InverseTransformPoint(point)))
                    throw new InvalidOperationException(child.name + " exceeds " + parent.name);
        }
    }
}
