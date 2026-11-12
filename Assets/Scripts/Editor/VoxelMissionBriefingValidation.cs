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
    public static class VoxelMissionBriefingValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        [MenuItem("Tools/Voxel Racer/Mission Briefing/Validate And Render All Levels")]
        public static void Run()
        {
            Check(!Application.isPlaying && !VoxelMissionBriefing.IsShowing, "Run from idle Edit Mode.");
            using var saved = new VoxelGarageUpgradeDockValidation.SavedState();
            var random = UnityEngine.Random.state;
            float clock = Time.timeScale; bool audio = AudioListener.pause;
            var events = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            var settings = VoxelMissionBriefingSettings.Load();
            Check(settings != null && settings.dayBackground != null && settings.nightBackground != null, "Missing independent day/night backgrounds.");
            var tracks = Resources.LoadAll<VoxelTrackDefinition>("Tracks").OrderBy(t => t.name).ToArray();
            var snapshots = tracks.ToDictionary(t => t, EditorJsonUtility.ToJson);
            string settingsBefore = EditorJsonUtility.ToJson(settings);
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary Mission Briefing QA"); SceneManager.MoveGameObjectToScene(root, scene);
            Directory.CreateDirectory("Temp/MissionBriefing");
            try
            {
                var view = root.AddComponent<VoxelMissionBriefing>();
                int balance = VoxelCurrencyState.Balance, missing = VoxelCarRunState.MissingVoxelCount, index = VoxelTrackProgressState.CurrentTrackIndex;
                foreach (var track in tracks)
                {
                    var enemies = VoxelMissionBriefingContent.Enemies(track);
                    Check(enemies.Select(e => e.Source).Distinct().Count() == enemies.Count, "Duplicate enemy in " + track.name);
                    Check(enemies.All(e => e.Model != null && settings.PreviewFor(e.Source) != null), "Unbaked enemy in " + track.name);
                    Check(settings.BackgroundFor(track) == (track.briefingBackgroundOverride != null ? track.briefingBackgroundOverride : track.nightTime ? settings.nightBackground : settings.dayBackground), "Background selection: " + track.name);
                    if (track.boss != null)
                        Check(enemies.Count == 2 && enemies.Any(e => e.Source == track.boss) && enemies.Any(e => e.Trait == "RADAR TARGET") &&
                            VoxelMissionBriefingContent.Objective(track).Contains("DEFEAT THE BOSS"), "Boss briefing must show radar/boss instead of score/ordinary traffic.");
                    else Check(VoxelMissionBriefingContent.Objective(track) == track.missionTuning.requiredPoints.ToString("N0"), "Incorrect score target in " + track.name);
                    int callbacks = 0;
                    view.Show(track, () => callbacks++);
                    Check(VoxelMissionBriefing.IsShowing && view.EnemyCount == enemies.Count, "Briefing did not open.");
                    Check(view.GetComponentsInChildren<Text>().Single(t => t.name == "Mission Name").text ==
                        VoxelTrackDefinition.ResolveDisplayName(track).ToUpperInvariant(), "Briefing must retain the authored mission name: " + track.name);
                    if (track.boss != null)
                    {
                        var labels = view.GetComponentsInChildren<Text>();
                        Check(labels.Count(t => t.text == "???") == 1 &&
                            labels.Where(t => t.name != "Mission Name").All(t => t.text.IndexOf(track.boss.bossName, StringComparison.OrdinalIgnoreCase) < 0),
                            "Boss encounter card must stay anonymous; the authored mission heading remains visible.");
                        Check(view.GetComponentsInChildren<RawImage>().All(i => i.texture != settings.PreviewFor(track.boss)),
                            "Boss model leaked into the briefing.");
                    }
                    var background = root.transform.Find("Mission Briefing Canvas/Background Image").GetComponent<RawImage>();
                    Check(background.texture == settings.BackgroundFor(track) && !background.raycastTarget, "Background is not a separate replaceable image.");
                    Capture(root, view, scene, track.name, 1920, 1080);
                    view.GoBack(); Check(callbacks == 0 && !VoxelMissionBriefing.IsShowing, "Back launched or retained overlay.");
                    view.Show(track, () => callbacks++); view.Accept(); view.Accept();
                    Check(callbacks == 1 && !VoxelMissionBriefing.IsShowing, "Start must invoke the launch exactly once.");
                }
                var day = tracks.First(t => t.name == "Track04");
                foreach (var size in new[] { new Vector2Int(2400, 1080), new Vector2Int(1440, 1080), new Vector2Int(1080, 1920) })
                { view.Show(day, null); Capture(root, view, scene, "Responsive", size.x, size.y); view.Close(); }
                var bossTrack = tracks.First(t => t.boss != null);
                CheckRadarAnimation(root, view, scene, bossTrack, settings);
                foreach (var width in new[] { 2400, 1440 })
                { view.Show(bossTrack, null); Capture(root, view, scene, "BossResponsive", width, 1080); view.Close(); }
                foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2400, 1080), new Vector2Int(1440, 1080), new Vector2Int(1080, 1920) })
                {
                    view.Show(bossTrack, null, alongsideRace: true);
                    Check(root.transform.Find("Mission Briefing Canvas/Safe Content/Briefing Panel")
                        .GetComponent<RectTransform>().rect.width == 900, "Carrier briefing width changed.");
                    Capture(root, view, scene, "CarrierResponsive", size.x, size.y);
                    view.Close();
                }
                CheckSyntheticContent(day, settings, view);
                Check(VoxelCurrencyState.Balance == balance && VoxelCarRunState.MissingVoxelCount == missing && VoxelTrackProgressState.CurrentTrackIndex == index,
                    "Briefing or Back changed wallet, damage or campaign index.");
                Check(tracks.All(t => EditorJsonUtility.ToJson(t) == snapshots[t]) && EditorJsonUtility.ToJson(settings) == settingsBefore,
                    "Preview validation changed authored tracks or settings.");
                Check(Time.timeScale == clock && AudioListener.pause == audio, "Briefing changed clocks or audio.");
                string report = "PASS (Edit Mode): all " + tracks.Length + " levels, anonymous boss card, authored titles and objectives, enemy eligibility and model previews, animated radar, background selection and override, empty pools, one-shot Start and Back, full-screen and carrier layouts at 16:9/20:9/4:3/portrait. Source assets, wallet, damage, campaign, clocks/audio/RNG preserved. Renders: Temp/MissionBriefing/.";
                File.WriteAllText("Temp/MissionBriefing/Validation.txt", report); Debug.Log(report);
            }
            catch (Exception error) { File.WriteAllText("Temp/MissionBriefing/Validation.txt", "FAIL: " + error); throw; }
            finally
            {
                root.GetComponent<VoxelMissionBriefing>()?.Close(); Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
                foreach (var e in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)) if (!events.Contains(e)) Object.DestroyImmediate(e.gameObject);
                UnityEngine.Random.state = random;
            }
        }

        private static void CheckRadarAnimation(GameObject root, VoxelMissionBriefing view,
            Scene scene, VoxelTrackDefinition track, VoxelMissionBriefingSettings settings)
        {
            var radar = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/RadarInterceptorTuning");
            var preview = settings.PreviewRecordFor(radar);
            Check(preview != null && preview.IsAnimated, "Radar briefing animation has not been baked.");
            float duration = preview.frameCount / preview.framesPerSecond;
            var dish = radar.modelPrefab.GetComponentInChildren<VoxelRadarDish>(true);
            Check(dish != null && Mathf.Abs(duration - 360f / Mathf.Abs(dish.degreesPerSecond)) < .001f,
                "Briefing dish must match the gameplay model's spin speed.");
            view.Show(track, null);
            var image = view.GetComponentsInChildren<RawImage>().Single(i => i.texture == preview.animation);
            Call(view, "AnimatePreviews", 0f); Rect first = image.uvRect;
            foreach (int quarter in new[] { 0, 1, 2, 3 })
            {
                Call(view, "AnimatePreviews", duration * quarter / 4);
                Check(quarter == 0 || image.uvRect != first, "Radar card did not advance its animation.");
                Capture(root, view, scene, "RadarSpin-" + quarter, 1920, 1080);
            }
            Call(view, "AnimatePreviews", duration);
            Check(image.uvRect == first, "Radar animation does not loop seamlessly.");
            view.Close(); view.Show(track, null);
            Check(view.GetComponentsInChildren<RawImage>().Single(i => i.texture == preview.animation).uvRect == first,
                "Reopening the briefing did not reset its animation.");
            view.Close();
        }

        private static void CheckSyntheticContent(VoxelTrackDefinition original, VoxelMissionBriefingSettings settings, VoxelMissionBriefing view)
        {
            var track = Object.Instantiate(original); var traffic = Object.Instantiate(original.obstacleCarTuning);
            var mission = Object.Instantiate(original.missionTuning); track.obstacleCarTuning = traffic; track.missionTuning = mission;
            try
            {
                track.briefingBackgroundOverride = settings.nightBackground;
                Check(settings.BackgroundFor(track) == settings.nightBackground, "Per-track independent image override ignored.");
                var vehicle = traffic.enemyVehicleSpawns.First(e => e.vehicle != null).vehicle;
                traffic.enemyVehicleSpawns = new[] { null, new VoxelEnemyVehicleSpawnEntry { vehicle = vehicle, spawnWeight = 0 },
                    new VoxelEnemyVehicleSpawnEntry { vehicle = vehicle, spawnWeight = 1 }, new VoxelEnemyVehicleSpawnEntry { vehicle = vehicle, spawnWeight = 2 } };
                traffic.obstacleCarSpawnChance = traffic.enemyCarSpawnChance = 0; traffic.enemyDroughtWaveLimit = 1;
                mission.roadsideTurretSpawnChance = 0;
                Check(VoxelMissionBriefingContent.Enemies(track).Count == 1, "Drought-eligible enemies, disabled rows or deduplication incorrect.");
                traffic.enemyDroughtWaveLimit = 0;
                Check(VoxelMissionBriefingContent.Enemies(track).Count == 0, "Disabled enemies still reported.");
                view.Show(track, null); Check(view.EnemyCount == 0, "Empty mission failed to render."); view.Close();
                VoxelMissionBriefing.UseStationaryOpeningOnRetry(original);
                Check(VoxelMissionBriefing.ConsumeStationaryRetry(original) && !VoxelMissionBriefing.ConsumeStationaryRetry(original), "Launch approval not consumed exactly once.");
                VoxelMissionBriefing.UseStationaryOpeningOnRetry(original);
                Check(!VoxelMissionBriefing.ConsumeStationaryRetry(track), "Launch approval accepted a different level.");
            }
            finally { view.Close(); Object.DestroyImmediate(track); Object.DestroyImmediate(traffic); Object.DestroyImmediate(mission); }
        }
        private static void Capture(GameObject root, VoxelMissionBriefing view, Scene scene, string label, int width, int height)
        {
            var go = new GameObject("Temporary Briefing Render Camera"); SceneManager.MoveGameObjectToScene(go, scene);
            var camera = go.AddComponent<Camera>(); camera.scene = scene; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta; camera.nearClipPlane = .1f; camera.farClipPlane = 10;
            var rt = new RenderTexture(width, height, 24); var previous = RenderTexture.active;
            var canvas = view.GetComponentInChildren<Canvas>(true);
            try
            {
                camera.targetTexture = rt; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                foreach (var t in canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                Canvas.ForceUpdateCanvases(); Call(view, "Layout"); Canvas.ForceUpdateCanvases(); camera.Render();
                var area = ((RectTransform)canvas.transform).rect;
                foreach (var text in view.GetComponentsInChildren<Text>())
                {
                    var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var local = canvas.transform.InverseTransformPoint(corner);
                        Check(local.x >= area.xMin - 1 && local.x <= area.xMax + 1 && local.y >= area.yMin - 1 && local.y <= area.yMax + 1,
                            "Text outside viewport: " + text.name + " at " + width + "x" + height);
                    }
                }
                foreach (var image in view.GetComponentsInChildren<RawImage>().Where(i => i.name == "Three Quarter Model"))
                    Check(image.texture != null && Mathf.Abs(image.rectTransform.rect.width / image.rectTransform.rect.height - image.GetComponent<AspectRatioFitter>().aspectRatio) < .005f,
                        "Enemy preview missing or stretched.");
                RenderTexture.active = rt;
                var imageTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
                try { imageTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0); imageTexture.Apply(); File.WriteAllBytes("Temp/MissionBriefing/" + label + "-" + width + "x" + height + ".png", imageTexture.EncodeToPNG()); }
                finally { Object.DestroyImmediate(imageTexture); }
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; camera.targetTexture = null;
                RenderTexture.active = previous; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
            }
        }
    }
}
