using System;
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
                        foreach (var style in new[] { VoxelScorePopup.Style.WeaponDamage, VoxelScorePopup.Style.RamDamage, VoxelScorePopup.Style.EnemyDestroyed })
                        {
                            int fontSize = style == VoxelScorePopup.Style.WeaponDamage ? 60 : style == VoxelScorePopup.Style.RamDamage ? 152 : 192;
                            var label = new GUIStyle(EditorStyles.label) { font = VoxelHudStyles.HudFont, fontSize = fontSize };
                            float height = label.CalcSize(new GUIContent("+1250")).y;
                            Call(popup, "BeginAtHealthBar", bar.transform.position, 1250, style, -1f, bar);
                            Check(!(bool)typeof(VoxelScorePopup).GetProperty("UsesHudSlot", Private).GetValue(popup), "Enemy number took the shared HUD slot");
                            foreach (float progress in new[] { 0f, .1f, .5f, .9f })
                            {
                                Vector3 origin = (Vector3)originField.GetValue(popup);
                                Vector2 center = (Vector2)Call(popup, "GetMissionFlightCenter", origin, height, progress);
                                float bottom = center.y + (height * .5f + 2f) * (1f - progress);
                                Check(bottom <= barRect.yMin - 5.9f * Mathf.Max(.1f, Screen.height / 1080f),
                                    name + " " + style + " covered its bar during hold/flight: bottom=" + bottom + ", bar top=" + barRect.yMin + ", progress=" + progress);
                            }
                            // Moving the source/camera upward must never move a bar through a held number.
                            Vector3 savedPosition = bar.transform.position;
                            bar.transform.position += camera.transform.up * 2f;
                            bar.TryGetScreenRect(camera, out Rect movingRect);
                            Vector2 movingCenter = (Vector2)Call(popup, "GetMissionFlightCenter", (Vector3)originField.GetValue(popup), height, .1f);
                            Check(movingCenter.y + (height * .5f + 2f) * .9f < movingRect.yMin, "Moving source overlapped a number");
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
                Directory.CreateDirectory("Temp/EnemyDamagePopups");
                string report = "PASS (Edit Mode): " + cases + " real-bar/popup cases across Black Interceptor, mine layer and Psycho Bug; 16:9/20:9; 12/35/80/140m; full/half/critical health; weapon, ram and destruction numbers. Projected renderer corners fit the pulse/frame envelope. Actual draw centres clear bars during holds, tally flights and source movement; hidden/dead bars retain origins; pooled popups release sources. Assets/game state/RNG restored.";
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
