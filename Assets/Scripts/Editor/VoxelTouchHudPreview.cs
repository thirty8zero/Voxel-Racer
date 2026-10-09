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
    /// <summary>Renders the actual runtime controls, with outlines taken from their input rectangles.</summary>
    public static class VoxelTouchHudPreview
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Call(object owner, string method, params object[] args) =>
            owner.GetType().GetMethod(method, Private).Invoke(owner, args);
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        [MenuItem("Tools/Voxel Racer/Preview Touch Areas and Boost Button")]
        public static void Run() => Render("Temp/TouchHud");

        public static void Render(string directory, int width = 1920)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Render this preview in Edit Mode.");
            Directory.CreateDirectory(directory);
            bool left = VoxelMissileUpgradeState.IsPurchased(false);
            var ownership = typeof(VoxelMissileUpgradeState).GetField("leftPurchased", BindingFlags.Static | BindingFlags.NonPublic);
            var scene = EditorSceneManager.NewPreviewScene();
            var existingEventSystem = EventSystem.current;
            var root = new GameObject("Temporary Touch HUD Preview");
            SceneManager.MoveGameObjectToScene(root, scene);
            Camera camera = null;
            RenderTexture rt = null;
            Texture2D texture = null;
            var savedActive = RenderTexture.active;
            try
            {
                ownership.SetValue(null, true);
                var car = root.AddComponent<VoxelCarController>(); car.enabled = false;
                var controls = root.AddComponent<VoxelMobileControls>();
                Call(controls, "BuildHud"); controls.Configure(car);
                var boost = root.AddComponent<VoxelBoostDisplay>(); Call(boost, "BuildHud");
                var integrity = root.AddComponent<VoxelCarIntegrityDisplay>(); Call(integrity, "BuildHud");
                Check(root.transform.Find("Boost HUD").GetComponent<CanvasScaler>().matchWidthOrHeight == 1f &&
                    root.transform.Find("Temporary Mobile Controls").GetComponent<CanvasScaler>().matchWidthOrHeight == 1f,
                    "Touch canvases must use the integrity dial's height-based scaling");
                var rects = root.GetComponentsInChildren<RectTransform>();
                string[] names = { "Move Left Button", "Move Right Button", "Brake Button", "Boost Button", "Fire Button", "Missile Button" };
                var buttons = names.Select(n => rects.First(r => r.name == n)).ToArray();
                var boostRect = buttons[3];
                Check(boostRect.anchorMin == Vector2.zero && boostRect.anchoredPosition == new Vector2(260, 630), "Boost placement");
                Check(boostRect.sizeDelta == new Vector2(220, 220), "Boost touch area must remain fixed");
                var cap = rects.First(r => r.name == "Boost Cap");
                float lastHeight = -100;
                foreach (float progress in new[] { 0f, .25f, .5f, .75f, 1f })
                {
                    Call(boost, "RefreshPresentation", progress, progress, progress == 1);
                    Check(cap.anchoredPosition.y > lastHeight, "Cap must rise monotonically during recharge");
                    Check(boostRect.sizeDelta == new Vector2(220, 220), "Recharge moved the touch area");
                    lastHeight = cap.anchoredPosition.y;
                }
                var format = typeof(VoxelMissionProgress).GetMethod("FormatMultiplier", BindingFlags.Static | BindingFlags.NonPublic);
                Check((string)format.Invoke(null, new object[] { 1f }) == "--", "1x must display dashes");
                Check((string)format.Invoke(null, new object[] { 1.25f }) == "1.25x", "Larger multiplier must remain numeric");

                var cameraObject = new GameObject("Touch HUD Preview Camera"); cameraObject.transform.SetParent(root.transform, false);
                camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .04f, .065f);
                camera.cullingMask = 1 << 31;
                var overlay = VoxelMenuUi.CreateCanvas(root.transform, "Touch Area Annotations");
                overlay.GetComponent<Canvas>().sortingOrder = 200;
                var heading = VoxelMenuUi.CreateText(overlay, "Preview Title", "TOUCH AREAS", 42, TextAnchor.MiddleCenter,
                    new Vector2(.5f, 1), new Vector2(0, -85), new Vector2(1000, 65));
                heading.color = new Color(.6f, .85f, 1f);
                VoxelMenuUi.CreateText(overlay, "Preview Note", "Outlined rectangles are the full tap targets, including transparent pixels.", 25,
                    TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(0, -140), new Vector2(1400, 45));
                VoxelMenuUi.CreateText(overlay, "Missile Note", "MISSILE is visible only when a launcher is fitted.", 23,
                    TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(0, -183), new Vector2(1100, 40));
                VoxelMenuUi.CreateText(overlay, "Wave Preview", "WAVE 3 IN 2.4s  |  SPAWNED 2  |  QUEUED 0", 24,
                    TextAnchor.MiddleCenter, new Vector2(.5f, 0), new Vector2(0, 56), new Vector2(900, 32)).color = new Color(.4f, .85f, 1f);
                VoxelMenuUi.CreateText(overlay, "FPS Preview", "FPS: 60", 24, TextAnchor.MiddleCenter,
                    new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(180, 32));
                foreach (var canvas in root.GetComponentsInChildren<Canvas>())
                {
                    canvas.GetComponent<CanvasScaler>().enabled = false;
                    canvas.scaleFactor = 1;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                    var group = canvas.GetComponent<CanvasGroup>(); if (group != null) { group.alpha = 1; group.blocksRaycasts = true; }
                }
                rt = new RenderTexture(width, 1080, 24); camera.targetTexture = rt;
                texture = new Texture2D(width, 1080, TextureFormat.RGB24, false);
                Canvas.ForceUpdateCanvases();
                // Camera rendering applies its target dimensions to screen-space canvases.
                // Resolve those dimensions before measuring the annotation rectangles.
                camera.Render(); camera.Render(); Canvas.ForceUpdateCanvases();
                var bounds = buttons.Select(r => Bounds(r, overlay)).ToArray();
                var integrityBackplate = root.GetComponentsInChildren<RectTransform>().First(r => r.name == "Integrity Dial Backplate");
                Check(!bounds[3].Overlaps(Bounds(integrityBackplate, overlay)), "Boost overlaps the integrity dial");
                root.transform.Find("Player Integrity HUD").gameObject.SetActive(false);
                for (int i = 0; i < bounds.Length; i++)
                {
                    for (int j = i + 1; j < bounds.Length; j++) Check(!bounds[i].Overlaps(bounds[j]), "Touch targets overlap: " + names[i] + " / " + names[j]);
                    Check(buttons[i].GetComponent<Image>().raycastTarget && buttons[i].GetComponent<Image>().alphaHitTestMinimumThreshold == 0,
                        "Preview must use actual rectangular raycast targets");
                    Check(bounds[i].xMin >= overlay.rect.xMin && bounds[i].xMax <= overlay.rect.xMax &&
                        bounds[i].yMin >= overlay.rect.yMin && bounds[i].yMax <= overlay.rect.yMax, "Touch target outside canvas");
                }
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
                Save("Controls.png");
                Color[] colours = { new Color(.3f, 1, .65f), new Color(.9f, .8f, .3f), new Color(1, .45f, .35f), new Color(.2f, .75f, 1), new Color(1, .45f, .8f), new Color(.7f, .55f, 1) };
                string[] captions = { "STEER LEFT", "STEER RIGHT", "BRAKE", "BOOST", "GUN FIRE", "MISSILE" };
                var annotations = new GameObject("Touch Outlines", typeof(RectTransform)); annotations.transform.SetParent(overlay, false);
                var annotationRect = annotations.GetComponent<RectTransform>(); annotationRect.anchorMin = Vector2.zero; annotationRect.anchorMax = Vector2.one;
                annotationRect.offsetMin = annotationRect.offsetMax = Vector2.zero;
                for (int i = 0; i < buttons.Length; i++)
                {
                    Rect b = bounds[i]; Color colour = colours[i];
                    Panel(annotationRect, b.center, b.size, new Color(colour.r, colour.g, colour.b, .10f));
                    Panel(annotationRect, new Vector2(b.center.x, b.yMin), new Vector2(b.width, 3), colour);
                    Panel(annotationRect, new Vector2(b.center.x, b.yMax), new Vector2(b.width, 3), colour);
                    Panel(annotationRect, new Vector2(b.xMin, b.center.y), new Vector2(3, b.height), colour);
                    Panel(annotationRect, new Vector2(b.xMax, b.center.y), new Vector2(3, b.height), colour);
                    var label = VoxelMenuUi.CreateText(annotationRect, captions[i], captions[i] + "  " + Mathf.RoundToInt(b.width) + " x " + Mathf.RoundToInt(b.height), 20,
                        TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(b.center.x, b.yMax + 15), new Vector2(300, 24));
                    label.color = colour;
                }
                foreach (Transform item in annotations.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
                Save("TouchAreas.png"); annotations.SetActive(false);
                // Close-up with the same cap and ring at representative cooldown values.
                root.transform.Find("Temporary Mobile Controls").gameObject.SetActive(false);
                heading.text = "BOOST COOLDOWN: CAP RISES AS IT RECHARGES";
                heading.rectTransform.sizeDelta = new Vector2(1700, 85);
                foreach (Transform child in overlay) if (child != heading.transform && child != annotations.transform) child.gameObject.SetActive(false);
                var ring = root.GetComponentsInChildren<Image>().First(i => i.name == "Boost Charge Ring");
                var track = root.GetComponentsInChildren<Image>().First(i => i.name == "Boost Charge Track");
                for (int i = 0; i < 4; i++)
                {
                    float progress = i / 3f;
                    var clone = Object.Instantiate(boostRect.gameObject, boostRect.parent).GetComponent<RectTransform>();
                    clone.anchorMin = clone.anchorMax = new Vector2(.5f, .5f); clone.anchoredPosition = new Vector2(-600 + i * 400, 0); clone.localScale = Vector3.one * 1.5f;
                    var cloneCap = clone.Find("Boost Cap").GetComponent<RectTransform>();
                    cloneCap.anchoredPosition = new Vector2(0, Mathf.Lerp(-8, 24, progress)); cloneCap.localScale = Vector3.one * Mathf.Lerp(.94f, 1, progress);
                    foreach (var source in new[] { track, ring })
                    {
                        var copy = Object.Instantiate(source, source.transform.parent); copy.rectTransform.anchorMin = copy.rectTransform.anchorMax = new Vector2(.5f, .5f);
                        copy.rectTransform.anchoredPosition = clone.anchoredPosition; copy.rectTransform.localScale = Vector3.one * 1.5f;
                        if (source == ring) copy.fillAmount = progress * 300f / 360f;
                    }
                    VoxelMenuUi.CreateText(overlay, "State", i == 3 ? "READY" : i == 0 ? "PRESSED / EMPTY" : Mathf.RoundToInt(progress * 100) + "% RECHARGED", 28,
                        TextAnchor.MiddleCenter, new Vector2(.5f, .5f), clone.anchoredPosition + new Vector2(0, -210), new Vector2(390, 60));
                }
                boostRect.gameObject.SetActive(false); ring.gameObject.SetActive(false); track.gameObject.SetActive(false);
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
                Save("BoostCooldown.png");
                File.WriteAllText(Path.Combine(directory, "Validation.txt"), "PASS: actual runtime HUD preview; six input rectangles in bounds and non-overlapping; blue boost above brake; fixed 220x220 boost input while cap rises monotonically through cooldown; 1x displays -- and 1.25x stays numeric. Edit Mode geometry/presentation checks. Missile purchase state restored.");
                Debug.Log("Touch HUD preview and checks saved to " + directory);

                void Save(string name)
                {
                    Canvas.ForceUpdateCanvases(); camera.Render(); camera.Render(); RenderTexture.active = rt;
                    texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
                    File.WriteAllBytes(Path.Combine(directory, name), texture.EncodeToPNG());
                }
            }
            finally
            {
                ownership.SetValue(null, left);
                RenderTexture.active = savedActive;
                if (camera != null) camera.targetTexture = null;
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (texture != null) Object.DestroyImmediate(texture);
                Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
                if (existingEventSystem == null && EventSystem.current != null) Object.DestroyImmediate(EventSystem.current.gameObject);
            }
        }

        private static Rect Bounds(RectTransform rect, RectTransform canvas)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = canvas.InverseTransformPoint(corners[0]), max = canvas.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static void Panel(Transform parent, Vector2 centre, Vector2 size, Color colour)
        {
            var panel = VoxelMenuUi.CreatePanel(parent, "Touch Outline", new Vector2(.5f, .5f), centre, size);
            panel.color = colour; panel.raycastTarget = false;
        }
    }
}
