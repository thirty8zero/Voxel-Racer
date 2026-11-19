using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Checks the real projected bars and popup draw centres, including moving sources and pooling.</summary>
    public static class VoxelEnemyDamagePopupValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Call(object owner, string name, params object[] args) =>
            owner.GetType().GetMethod(name, Private).Invoke(owner, args);
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        [MenuItem("Tools/Voxel Racer/Validate Enemy Damage Popup Placement")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Run this check in Edit Mode.");
            ValidateWeaponColours();
            ValidateMissionSpacing(960, 540);
            ValidateMissionSpacing(1280, 720);
            ValidateMissionSpacing(2400, 1080);
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var random = UnityEngine.Random.state;
            var oldCamera = Camera.main;
            using var saved = new VoxelGarageUpgradeDockValidation.SavedState();
            VoxelPsychoBugValidation.Fixture fixture = null;
            try
            {
                if (oldCamera != null) oldCamera.tag = "Untagged";
                VoxelRacerBootstrap.ReloadGeneratedMaterials();
                fixture = new VoxelPsychoBugValidation.Fixture();
                var cameraObject = new GameObject("Temporary popup camera");
                cameraObject.transform.SetParent(fixture.Root.transform, false);
                var camera = cameraObject.AddComponent<Camera>(); camera.tag = "MainCamera";
                Check(Camera.main == camera, "Validation camera is not the active main camera");
                var popupObject = new GameObject("Temporary enemy popup");
                popupObject.transform.SetParent(fixture.Root.transform, false);
                var popup = popupObject.AddComponent<VoxelScorePopup>();
                var originField = typeof(VoxelScorePopup).GetField("lockedScreenPosition", Private);
                int cases = 0;
                foreach (string name in new[] { "BlackInterceptorTuning", "BI_MineLayerTuning", "PsychoBugEnemyTuning" })
                {
                    EditorUtility.CopySerialized(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/" + name), fixture.EnemyTuning);
                    foreach (float aspect in new[] { 16f / 9f, 20f / 9f })
                    foreach (float distance in new[] { 12f, 35f, 80f, 140f })
                    foreach (float health in new[] { 1f, .5f, .05f })
                    {
                        fixture.Spawn(0f, distance);
                        var bar = fixture.Enemy.GetComponentInChildren<VoxelEnemyHealthBar>();
                        bar.SetHealth(health);
                        Vector3 player = fixture.Player.transform.position;
                        camera.aspect = aspect; camera.fieldOfView = 60;
                        camera.transform.position = player + new Vector3(0f, 12f, -16f);
                        camera.transform.LookAt(player + new Vector3(0f, 0f, 28f));
                        Call(bar, "LateUpdate");
                        Check(bar.TryGetScreenRect(camera, out Rect barRect), "Visible bar did not project");
                        CheckProjectedGeometry(bar, camera, barRect);
                        foreach (var style in new[] { VoxelScorePopup.Style.WeaponDamage, VoxelScorePopup.Style.FuelDrumDamage, VoxelScorePopup.Style.RamDamage, VoxelScorePopup.Style.EnemyDestroyed })
                        {
                            float scale = Mathf.Max(.1f, Screen.height / 1080f);
                            bool destruction = style == VoxelScorePopup.Style.EnemyDestroyed;
                            float destructionScale = Mathf.Min(1f, scale);
                            int fontSize = destruction ? Mathf.RoundToInt(192f * destructionScale) :
                                style == VoxelScorePopup.Style.WeaponDamage ? 60 : style == VoxelScorePopup.Style.FuelDrumDamage ? 48 : 152;
                            float outline = destruction ? Mathf.Max(1f, 2f * destructionScale) : 2f;
                            var label = new GUIStyle { font = VoxelHudStyles.HudFont, fontSize = fontSize };
                            float height = label.CalcSize(new GUIContent("+1250")).y;
                            Call(popup, "BeginAtHealthBar", bar.transform.position, 1250, style, -1f, bar);
                            Check(!(bool)typeof(VoxelScorePopup).GetProperty("UsesHudSlot", Private).GetValue(popup), "Enemy number took the shared HUD slot");
                            foreach (float progress in new[] { 0f, .1f, .5f, .9f })
                            {
                                Vector3 origin = (Vector3)originField.GetValue(popup);
                                Vector2 center = (Vector2)Call(popup, "GetMissionFlightCenter", origin, height, progress);
                                float bottom = center.y + (height * .5f + outline) * (1f - progress);
                                Check(bottom <= barRect.yMin - 5.9f * scale,
                                    name + " " + style + " covered its bar during hold/flight: bottom=" + bottom + ", bar top=" + barRect.yMin + ", progress=" + progress);
                            }
                            // Moving the source/camera upward must never move a bar through a held number.
                            Vector3 savedPosition = bar.transform.position;
                            bar.transform.position += camera.transform.up * 2f;
                            bar.TryGetScreenRect(camera, out Rect movingRect);
                            Vector2 movingCenter = (Vector2)Call(popup, "GetMissionFlightCenter", (Vector3)originField.GetValue(popup), height, .1f);
                            Check(movingCenter.y + (height * .5f + outline) * .9f < movingRect.yMin, "Moving source overlapped a number");
                            bar.transform.position = savedPosition;
                            // Lethal hits retain a finite captured origin after the bar disappears.
                            bar.gameObject.SetActive(false);
                            Vector2 captured = (Vector2)Call(popup, "GetMissionFlightCenter", (Vector3)originField.GetValue(popup), height, .5f);
                            Check(!float.IsNaN(captured.x) && !float.IsNaN(captured.y), "Destroyed source lost its popup origin");
                            bar.gameObject.SetActive(true);
                            Call(popup, "OnDisable");
                            Check(typeof(VoxelScorePopup).GetField("sourceHealthBar", Private).GetValue(popup) == null, "Pooled popup retained its source");
                            cases++;
                        }
                    }
                }
                var repeatedHitBar = fixture.Enemy.GetComponentInChildren<VoxelEnemyHealthBar>();
                ValidateRepeatedDamage(fixture.Root.transform, camera, repeatedHitBar, VoxelScorePopup.Style.RamDamage, 38);
                ValidateRepeatedDamage(fixture.Root.transform, camera, repeatedHitBar, VoxelScorePopup.Style.WeaponDamage, 38);
                ValidateRepeatedDamage(fixture.Root.transform, camera, null, VoxelScorePopup.Style.WeaponDamage, -38);
                foreach (int points in new[] { 1, 9, -1, -9 })
                    ValidateCompactDamage(fixture.Root.transform, camera, points > 0 ? repeatedHitBar : null, points);
                Directory.CreateDirectory("Temp/EnemyDamagePopups");
                string report = "PASS (Edit Mode): " + cases + " real-bar/popup cases across Black Interceptor, mine layer and Psycho Bug; 16:9/20:9; 12/35/80/140m; full/half/critical health; weapon, ram and destruction numbers. Actual draw centres clear moving and pulsing bars during holds and tally flights. Repeated +38 ram and weapon hits and -38 HUD damage sit side by side, keep their holds, retain their slots after neighbours expire, and release slots on pooling. Bursts of 24 single-digit +1/+9/-1/-9 voxel hits keep launch-slot movement within 32 height-scaled pixels, preserve holds and bar clearance, remain stable after neighbours expire and reset compact layout on pooling. Mixed-width labels fit screen edges and use an upper row when necessary at 540p/720p/1080p. Default versus upgraded weapon identities and pooled colour reset verified. Assets/game state/RNG restored.";
                File.WriteAllText("Temp/EnemyDamagePopups/Validation.txt", report); Debug.Log(report);
            }
            finally
            {
                fixture?.Dispose();
                if (oldCamera != null) oldCamera.tag = "MainCamera";
                UnityEngine.Random.state = random;
                SceneManager.SetActiveScene(active); EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateWeaponColours()
        {
            var basic = Resources.Load<VoxelGunTuning>("Weapons/BasicHoodGunTuning");
            Check(basic.DamagePopupSource == VoxelScorePopup.WeaponSource.DefaultGun, "Starter gun lost its white damage identity.");
            foreach (string name in new[] { "LongBarrelHoodGunTuning", "MinigunTuning", "MissileWeaponTuning" })
            {
                var source = Resources.Load<VoxelGunTuning>("Weapons/" + name).DamagePopupSource;
                int level = Resources.Load<VoxelGunTuning>("Weapons/" + name).upgradeLevel;
                Check(source == (VoxelScorePopup.WeaponSource)Mathf.Clamp(level, 1, 4), name + " did not use its authored tier.");
            }
            var temporary = ScriptableObject.CreateInstance<VoxelGunTuning>();
            try
            {
                for (int level = 1; level <= 4; level++)
                {
                    temporary.upgradeLevel = level;
                    Check(temporary.DamagePopupSource == (VoxelScorePopup.WeaponSource)level,
                        "A custom gun did not carry its rarity to damage feedback.");
                }
            }
            finally { Object.DestroyImmediate(temporary); }
        }

        private static void ValidateCompactDamage(Transform root, Camera camera, VoxelEnemyHealthBar bar, int points)
        {
            const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
            var type = typeof(VoxelScorePopup);
            var active = (List<VoxelScorePopup>)type.GetField("ActiveMissionPopups", Static).GetValue(null);
            var origin = type.GetField("lockedScreenPosition", Private);
            var needsSlot = type.GetProperty("NeedsMissionSlot", Private);
            var elapsed = type.GetField("elapsed", Private);
            var penalties = (Dictionary<VoxelScorePopup.Style, GUIStyle>)type.GetField("PenaltyStyles", Static).GetValue(null);
            penalties.TryGetValue(VoxelScorePopup.Style.WeaponDamage, out var savedPenalty);
            var label = new GUIStyle
            {
                font = points < 0 ? Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001") : VoxelHudStyles.HudFont,
                fontSize = 60, alignment = TextAnchor.MiddleCenter
            };
            Vector2 size = label.CalcSize(new GUIContent(points > 0 ? $"+{points}" : points.ToString()));
            float scale = Mathf.Max(.1f, Screen.height / 1080f);
            var burst = new GameObject("Compact voxel damage burst");
            burst.transform.SetParent(root, false);
            var popups = new List<VoxelScorePopup>();
            Vector3 position = bar != null ? bar.transform.position : camera.transform.position + camera.transform.forward * 20f;
            try
            {
                if (points < 0) penalties[VoxelScorePopup.Style.WeaponDamage] = label;
                for (int hit = 0; hit < 24; hit++)
                {
                    var popup = new GameObject("Single-digit hit").AddComponent<VoxelScorePopup>();
                    popup.transform.SetParent(burst.transform, false);
                    popups.Add(popup);
                    Call(popup, "BeginAtHealthBar", position, points, VoxelScorePopup.Style.WeaponDamage, -1f, bar);
                    active.Add(popup);
                    Vector2 requested = (Vector2)Call(popup, "GetMissionFlightCenter", (Vector3)origin.GetValue(popup), size.y, 0f);
                    if (bar != null && bar.TryGetScreenRect(camera, out Rect barRect))
                        Check(Mathf.Abs(requested.x - barRect.center.x) <= 12f * scale + .01f,
                            "Single-digit damage retained excessive initial scatter.");
                    if ((bool)needsSlot.GetValue(popup)) Call(popup, "PlaceMissionLabel", origin.GetValue(popup), size);
                    Vector2 held = (Vector2)Call(popup, "GetMissionFlightCenter", (Vector3)origin.GetValue(popup), size.y, 0f);
                    Check(Mathf.Abs(held.x - requested.x) <= 32f * scale + .01f && Mathf.Abs(held.y - requested.y) < .01f,
                        "A single-digit burst fanned out beyond its compact launch area.");
                    if (bar != null && bar.TryGetScreenRect(camera, out Rect envelope))
                        Check(held.y + size.y * .5f + 2f <= envelope.yMin - 5.9f * scale,
                            "Compact damage covered its source health bar.");
                    foreach (var previous in popups)
                        Check((float)elapsed.GetValue(previous) == 0f, "Compact damage cut another number's hold short.");
                }
                var last = popups[popups.Count - 1];
                Vector2 before = (Vector2)Call(last, "GetMissionFlightCenter", (Vector3)origin.GetValue(last), size.y, 0f);
                Call(popups[0], "OnDisable");
                Vector2 after = (Vector2)Call(last, "GetMissionFlightCenter", (Vector3)origin.GetValue(last), size.y, 0f);
                Check(before == after, "Compact damage moved after a neighbour expired.");
                type.GetField("weaponSource", Private).SetValue(last, VoxelScorePopup.WeaponSource.OtherWeapon);
                Call(last, "BeginAtHealthBar", position, 38, VoxelScorePopup.Style.WeaponDamage, -1f, bar);
                Check(!(bool)type.GetField("compactVoxelDamage", Private).GetValue(last), "Pooling applied compact spacing to a larger number.");
                Check((VoxelScorePopup.WeaponSource)type.GetField("weaponSource", Private).GetValue(last) == VoxelScorePopup.WeaponSource.DefaultGun,
                    "A pooled popup retained the previous weapon's colour.");
            }
            finally
            {
                foreach (var popup in popups) Call(popup, "OnDisable");
                if (savedPenalty != null) penalties[VoxelScorePopup.Style.WeaponDamage] = savedPenalty;
                else penalties.Remove(VoxelScorePopup.Style.WeaponDamage);
                Object.DestroyImmediate(burst);
            }
        }

        private static void ValidateRepeatedDamage(Transform root, Camera camera, VoxelEnemyHealthBar bar,
            VoxelScorePopup.Style style, int points)
        {
            const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
            var type = typeof(VoxelScorePopup);
            var active = (List<VoxelScorePopup>)type.GetField("ActiveMissionPopups", Static).GetValue(null);
            var origin = type.GetField("lockedScreenPosition", Private);
            var elapsed = type.GetField("elapsed", Private);
            var needsSlot = type.GetProperty("NeedsMissionSlot", Private);
            var hasSlot = type.GetField("hasMissionSlot", Private);
            var penalties = (Dictionary<VoxelScorePopup.Style, GUIStyle>)type.GetField("PenaltyStyles", Static).GetValue(null);
            penalties.TryGetValue(style, out var savedPenalty);
            var label = new GUIStyle
            {
                font = points < 0 ? Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001") : VoxelHudStyles.HudFont,
                fontSize = style == VoxelScorePopup.Style.RamDamage ? 152 : 60,
                alignment = TextAnchor.MiddleCenter
            };
            Vector2 size = label.CalcSize(new GUIContent(points > 0 ? $"+{points}" : points.ToString()));
            var pair = new GameObject("Repeated damage popups");
            pair.transform.SetParent(root, false);
            var first = new GameObject("First hit").AddComponent<VoxelScorePopup>();
            var second = new GameObject("Second hit").AddComponent<VoxelScorePopup>();
            first.transform.SetParent(pair.transform, false);
            second.transform.SetParent(pair.transform, false);
            Vector3 position = bar != null ? bar.transform.position : camera.transform.position + camera.transform.forward * 20f;
            try
            {
                // OnGUI normally creates this style. Provide the same font and size for this Edit Mode fixture.
                if (points < 0) penalties[style] = label;
                Call(first, "BeginAtHealthBar", position, points, style, -1f, bar);
                active.Add(first);
                Call(second, "BeginAtHealthBar", position, points, style, -1f, bar);
                active.Add(second);
                origin.SetValue(second, origin.GetValue(first));
                Check((float)elapsed.GetValue(first) == 0f && (float)elapsed.GetValue(second) == 0f,
                    "A repeated damage number cut the first hold short.");
                Check((bool)needsSlot.GetValue(second), "A repeated damage number did not request spacing.");
                if ((bool)needsSlot.GetValue(first)) Call(first, "PlaceMissionLabel", origin.GetValue(first), size);
                Call(second, "PlaceMissionLabel", origin.GetValue(second), size);
                foreach (float progress in new[] { 0f, .2f, .55f })
                {
                    var firstCenter = (Vector2)Call(first, "GetMissionFlightCenter", origin.GetValue(first), size.y, progress);
                    var secondCenter = (Vector2)Call(second, "GetMissionFlightCenter", origin.GetValue(second), size.y, progress);
                    Check(Mathf.Abs(firstCenter.y - secondCenter.y) < .01f &&
                        Mathf.Abs(firstCenter.x - secondCenter.x) >= (size.x + 4f) * (1f - progress) - .01f,
                        $"Repeated {points} labels overlapped instead of sitting side by side at progress {progress}.");
                }
                var before = (Vector2)Call(second, "GetMissionFlightCenter", origin.GetValue(second), size.y, 0f);
                Call(first, "OnDisable");
                var after = (Vector2)Call(second, "GetMissionFlightCenter", origin.GetValue(second), size.y, 0f);
                Check(before == after, "A held popup moved when its neighbour expired.");
                Call(second, "BeginAtHealthBar", position, points, style, -1f, bar);
                Check(!(bool)hasSlot.GetValue(second), "A pooled damage popup kept its previous slot.");
            }
            finally
            {
                Call(first, "OnDisable"); Call(second, "OnDisable");
                if (savedPenalty != null) penalties[style] = savedPenalty; else penalties.Remove(style);
                Object.DestroyImmediate(pair);
            }
        }

        private static void ValidateMissionSpacing(float width, float height)
        {
            var findSlot = typeof(VoxelScorePopup).GetMethod("FindMissionSlot", BindingFlags.Static | BindingFlags.NonPublic);
            var label = new GUIStyle
            {
                font = VoxelHudStyles.HudFont,
                fontSize = Mathf.RoundToInt(192f * height / 1080f),
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            Vector2 size = label.CalcSize(new GUIContent("+75"));
            var desired = new Rect(width - size.x, height * .5f, size.x, size.y);
            var voxelHit = new Rect(desired.center.x - 28f, desired.yMax - 48f, 56f, 48f);
            var occupied = new List<Rect> { voxelHit };
            Rect first = (Rect)findSlot.Invoke(null, new object[] { desired, occupied, width });
            Check(first.y == desired.y && first.xMin >= 0f && first.xMax <= width && !first.Overlaps(voxelHit),
                "Destruction number did not fit beside the voxel hit at the screen edge");
            occupied.Add(first);
            Rect second = (Rect)findSlot.Invoke(null, new object[] { desired, occupied, width });
            Check(second.y == desired.y && second.xMin >= 0f && second.xMax <= width &&
                !second.Overlaps(first) && !second.Overlaps(voxelHit), "Simultaneous destruction numbers overlapped");

            occupied.Clear();
            occupied.Add(new Rect(0f, desired.y, width, desired.height));
            Rect upper = (Rect)findSlot.Invoke(null, new object[] { desired, occupied, width });
            Check(upper.yMin >= 0f && upper.yMax <= desired.yMin, "A full row did not move the destruction number up");
        }

        private static void CheckProjectedGeometry(VoxelEnemyHealthBar bar, Camera camera, Rect envelope)
        {
            foreach (var renderer in bar.GetComponentsInChildren<Renderer>())
            {
                Bounds bounds = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    Vector3 point = camera.WorldToScreenPoint(renderer.transform.TransformPoint(local));
                    Check(point.x >= envelope.xMin - .01f && point.x <= envelope.xMax + .01f &&
                        Screen.height - point.y >= envelope.yMin - .01f && Screen.height - point.y <= envelope.yMax + .01f,
                        "Popup envelope did not include the visible frame/fill");
                }
            }
        }
    }
}
