using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelMissileHudValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Missile Cooldown HUD")]
        public static void Run()
        {
            if (Application.isPlaying) throw new Exception("Start this check in Edit Mode.");
            bool savedLeft = VoxelMissileUpgradeState.IsPurchased(false), savedRight = VoxelMissileUpgradeState.IsPurchased(true);
            var scene = EditorSceneManager.NewPreviewScene();
            var oldEvent = Object.FindFirstObjectByType<EventSystem>();
            var root = new GameObject("Temporary missile HUD validation");
            SceneManager.MoveGameObjectToScene(root, scene);
            var weapon = Object.Instantiate(VoxelMissileLauncherTuning.Load().weapon);
            var bullet = ScriptableObject.CreateInstance<VoxelGunTuning>();
            try
            {
                SetOwnership(false, false);
                weapon.useMissileCooldown = true; weapon.missileCooldownSeconds = 8;
                using (var tree = PropertyTree.Create(weapon))
                {
                    string before = EditorJsonUtility.ToJson(weapon);
                    tree.UpdateTree();
                    var properties = tree.EnumerateTree(true).ToArray();
                    foreach (string field in new[] { "useMissileCooldown", "missileCooldownSeconds" })
                    {
                        var property = properties.First(p => p.Name == field);
                        Check(property.Attributes.OfType<FoldoutGroupAttribute>().Any(a => a.GroupName == "Missile Cooldown") &&
                            property.Attributes.OfType<ShowIfAttribute>().Count() == 1,
                            "Cooldown must appear once in its own conditional inspector foldout: " + field);
                    }
                    Check(before == EditorJsonUtility.ToJson(weapon), "Inspector changed tuning values");
                }
                weapon.shotsPerSecond = .2f; weapon.ammunitionPerStage = 2;
                Check(weapon.SecondsPerShot == 8, "Explicit missile cooldown must override fire rate");
                weapon.useMissileCooldown = false;
                Check(weapon.SecondsPerShot == 5, "Disabled option must preserve legacy fire rate");
                weapon.useMissileCooldown = true;
                bullet.useMissileCooldown = true; bullet.missileCooldownSeconds = 8; bullet.shotsPerSecond = 4;
                Check(bullet.SecondsPerShot == .25f, "Bullet fire rate must stay unchanged");
                var car = root.AddComponent<VoxelCarController>(); car.SetDrivingEnabled(false);
                var left = MakeMount(root.transform, weapon, "Left");
                var right = MakeMount(root.transform, weapon, "Right");
                var controls = root.AddComponent<VoxelMobileControls>();
                if (root.GetComponentInChildren<Canvas>() == null) VoxelMissileValidation.Call(controls, "BuildHud");
                controls.Configure(car);
                var display = root.GetComponentInChildren<VoxelMissileButtonDisplay>(true);
                Check(!display.gameObject.activeSelf, "Missile button must start hidden without a purchase");
                display.GetComponent<IPointerDownHandler>().OnPointerDown(null);
                Check(!VoxelMobileControls.IsMissileHeld, "Unowned missile button must not accept held input");
                Equal(display.GetComponent<RectTransform>().sizeDelta.x * display.transform.localScale.x, 230f, "Larger overall button width");
                SetOwnership(true, false); VoxelMissileValidation.Call(controls, "UpdateMissileVisibility");
                Check(display.gameObject.activeSelf, "Left-only purchase must reveal button");
                display.GetComponent<IPointerDownHandler>().OnPointerDown(null);
                Check(VoxelMobileControls.IsMissileHeld, "Purchased missile input must work");
                SetOwnership(false, false); VoxelMissileValidation.Call(controls, "UpdateMissileVisibility");
                Check(!display.gameObject.activeSelf && !VoxelMobileControls.IsMissileHeld, "New run must hide button and clear held input");
                SetOwnership(false, true); VoxelMissileValidation.Call(controls, "UpdateMissileVisibility");
                Check(display.gameObject.activeSelf, "Right-only purchase must reveal button");
                SetOwnership(true, true); VoxelMissileValidation.Call(controls, "UpdateMissileVisibility");
                Check(display.gameObject.activeSelf, "Both purchases must keep button visible");
                Refresh(display);
                Check(display.IsReady && display.ChargePercent == 1, "New stage must show full ready face");
                Check(left.TryBeginShot(out _) && right.TryBeginShot(out _) && !left.TryBeginShot(out _), "Both sides must share configured timing independently and reject early shots");
                Refresh(display);
                Check(display.ChargePercent < .001f && !display.IsReady, "Firing must empty red face");
                SetProgress(left, .25f); SetProgress(right, .25f); Refresh(display);
                Equal(display.ChargePercent, .25f, "Quarter recharge");
                SetProgress(left, .5f); SetProgress(right, .5f); Refresh(display);
                Equal(display.ChargePercent, .5f, "Half recharge");
                SetProgress(left, 1f); SetProgress(right, .5f); Refresh(display);
                Check(display.IsReady && display.ChargePercent == 1, "One ready side must allow launch");
                Check(left.TryBeginShot(out _), "Ready launcher must fire again");
                SetProgress(left, 1f); Refresh(display);
                Check(!left.HasAmmunition && !display.IsReady, "Exhausted side must not appear ready");
                Equal(display.ChargePercent, .5f, "Remaining side drives recharge");
                right.enabled = false; Refresh(display);
                Check(display.ChargePercent == 0 && !display.IsReady, "Disabled and empty launchers must show empty");
                right.enabled = true; VoxelMissileValidation.Call(right, "OnEnable");
                SetProgress(right, 1f); Refresh(display);
                car.enabled = false; Refresh(display);
                Check(!display.IsReady && display.ChargePercent == 0, "Workshop/disabled car cannot show ready");
                car.enabled = true;

                var fill = display.transform.Find("Missile Cooldown Face").GetComponent<Image>();
                Check(fill.type == Image.Type.Filled && fill.fillMethod == Image.FillMethod.Radial360 &&
                    fill.fillClockwise && fill.fillOrigin == (int)Image.Origin360.Top && !fill.raycastTarget,
                    "Clock must refill clockwise from top without intercepting input");
                Check(display.GetComponentInChildren<Text>().text == "FIRE\nMISSILE", "Reference label missing");
                display.GetComponent<IPointerDownHandler>().OnPointerDown(null);
                Check(VoxelMobileControls.IsMissileHeld, "Missile button must keep hold input");
                display.GetComponent<IPointerUpHandler>().OnPointerUp(null);
                Check(!VoxelMobileControls.IsMissileHeld, "Pointer release must clear hold");
                VoxelMissileValidation.ValidateFireControls();

                var boost = root.AddComponent<VoxelBoostDisplay>();
                if (!root.GetComponentsInChildren<Canvas>().Any(c => c.name == "Boost HUD")) VoxelMissileValidation.Call(boost, "BuildHud");
                var ring = root.GetComponentsInChildren<Image>().First(i => i.name == "Boost Charge Ring");
                var text = root.GetComponentsInChildren<Text>().First(t => t.name == "Boost Label");
                Check(ring.color.b > ring.color.r && text.color.b > text.color.r, "Boost ring and label must be blue");
                var boostButtonRect = root.GetComponentsInChildren<Button>().First(b => b.name == "Boost Button").GetComponent<RectTransform>();
                Check(boostButtonRect.anchorMin == Vector2.zero &&
                    boostButtonRect.anchoredPosition == new Vector2(260, 630) &&
                    boostButtonRect.sizeDelta == new Vector2(220, 220) &&
                    ring.rectTransform.anchorMin == boostButtonRect.anchorMin &&
                    ring.rectTransform.anchoredPosition == boostButtonRect.anchoredPosition &&
                    text.transform.parent.name == "Boost Cap" &&
                    text.transform.IsChildOf(boostButtonRect),
                    "Boost must sit above the left brake, with its label on the moving cap");
                Check(ring.fillClockwise && ring.fillOrigin == (int)Image.Origin360.Bottom &&
                    Mathf.Abs(ring.fillAmount - 300f / 360f) < .001f &&
                    Mathf.Abs(Mathf.DeltaAngle(ring.rectTransform.localEulerAngles.z, -30f)) < .001f,
                    "Boost must use a 300-degree clockwise arc with a gap centred at the bottom");
                Render(root, display, right, ring);
                Directory.CreateDirectory("Temp/Missile/Hud");
                File.WriteAllText("Temp/Missile/Hud/Validation.txt", "PASS: 15% larger missile button, hidden without ownership, left/right/both purchased visibility, new-run hide and held-input reset, explicit/legacy cooldown, unchanged bullets, independent sides, ready/empty/quarter/half/full face, finite ammo, disabled car/mount, clock origin/direction, hold/release and existing pointer/focus gates, blue boost, rendered ready and recharge states. Edit Mode simulations.");
                Debug.Log(File.ReadAllText("Temp/Missile/Hud/Validation.txt"));
            }
            finally
            {
                SetOwnership(savedLeft, savedRight);
                Object.DestroyImmediate(root); Object.DestroyImmediate(weapon); Object.DestroyImmediate(bullet);
                EditorSceneManager.ClosePreviewScene(scene);
                if (oldEvent == null) { var e = Object.FindFirstObjectByType<EventSystem>(); if (e != null) Object.DestroyImmediate(e.gameObject); }
            }
        }

        private static void SetOwnership(bool left, bool right)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            typeof(VoxelMissileUpgradeState).GetField("leftPurchased", flags).SetValue(null, left);
            typeof(VoxelMissileUpgradeState).GetField("rightPurchased", flags).SetValue(null, right);
        }

        private static VoxelGunMount MakeMount(Transform root, VoxelGunTuning weapon, string name)
        {
            var obj = new GameObject(name); obj.transform.SetParent(root, false);
            var mount = obj.AddComponent<VoxelGunMount>(); mount.tuning = weapon;
            VoxelMissileValidation.Call(mount, "OnEnable"); return mount;
        }
        private static void Refresh(VoxelMissileButtonDisplay display) => VoxelMissileValidation.Call(display, "Refresh");
        private static void SetProgress(VoxelGunMount mount, float progress) => VoxelMissileValidation.Set(mount, "nextFireTime", Time.time + mount.tuning.SecondsPerShot * (1f - progress));
        private static void Equal(float a, float b, string message) => Check(Mathf.Abs(a - b) < .001f, message);
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

        private static void Render(GameObject root, VoxelMissileButtonDisplay display, VoxelGunMount mount, Image ring)
        {
            var cameraObj = new GameObject("HUD render camera"); cameraObj.transform.SetParent(root.transform);
            var camera = cameraObj.AddComponent<Camera>(); camera.enabled = false;
            camera.scene = root.scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(root.scene);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .055f);
            camera.cullingMask = 1 << 31;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>())
            {
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                canvas.GetComponent<CanvasGroup>().alpha = 1;
            }
            var mobileCanvas = display.transform.parent;
            foreach (Transform child in mobileCanvas) if (child != display.transform) child.gameObject.SetActive(false);
            Place(display.GetComponent<RectTransform>(), new Vector2(-200, 0), new Vector2(200, 200));
            var boostButton = root.GetComponentsInChildren<Button>().First(b => b.name == "Boost Button");
            Place(boostButton.GetComponent<RectTransform>(), new Vector2(200, 0), new Vector2(220, 220));
            Place(ring.rectTransform, new Vector2(200, 0), new Vector2(220, 220));
            var track = root.GetComponentsInChildren<Image>().First(i => i.name == "Boost Charge Track");
            Place(track.rectTransform, new Vector2(200, 0), new Vector2(220, 220));
            var boostDisplay = root.GetComponent<VoxelBoostDisplay>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
            Directory.CreateDirectory("Temp/Missile/Hud");
            var rt = new RenderTexture(800, 300, 24);
            var texture = new Texture2D(800, 300, TextureFormat.RGB24, false);
            var oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = rt;
                foreach (float progress in new[] { 1f, 0f, .25f, .5f, .75f })
                {
                    SetProgress(mount, progress); Refresh(display); ring.fillAmount = progress * (300f / 360f);
                    VoxelMissileValidation.Call(boostDisplay, "RefreshPresentation", progress, progress, progress >= .999f);
                    Canvas.ForceUpdateCanvases(); camera.Render(); camera.Render();
                    RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 800, 300), 0, 0); texture.Apply();
                    File.WriteAllBytes("Temp/Missile/Hud/Charge" + Mathf.RoundToInt(progress * 100) + ".png", texture.EncodeToPNG());
                }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = oldActive;
                rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture);
            }
        }
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
    }
}
