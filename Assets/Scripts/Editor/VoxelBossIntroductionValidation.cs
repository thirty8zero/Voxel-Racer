using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelBossIntroductionValidation
    {
        private static void Check(bool condition, string message) => VoxelBichoLocoValidation.Check(condition, message);
        private static void Equal(float actual, float expected, string message) => VoxelBichoLocoValidation.Equal(actual, expected, message);
        private static object Call(object target, string name, params object[] args) => VoxelBichoLocoValidation.Call(target, name, args);

        [MenuItem("Tools/Voxel Racer/Bosses/Validate Boss Introduction")]
        public static void Run()
        {
            Check(!Application.isPlaying && !VoxelBossIntroduction.IsShowing, "Run from idle Edit Mode.");
            var oldMission = VoxelMissionProgress.Active;
            var random = UnityEngine.Random.state;
            float scale = Time.timeScale, step = Time.fixedDeltaTime;
            bool audio = AudioListener.pause;
            var source = Resources.Load<VoxelBossDefinition>("Bosses/ElBichoLocoBoss");
            var van = Resources.Load<VoxelBossDefinition>("Bosses/RedVanBoss");
            string before = EditorJsonUtility.ToJson(source), vanBefore = EditorJsonUtility.ToJson(van);
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary Boss Introduction QA");
            SceneManager.MoveGameObjectToScene(root, scene);
            var definition = Object.Instantiate(source);
            var tuning = ScriptableObject.CreateInstance<VoxelMissionTuning>(); tuning.timeLimitSeconds = 60;
            var gunTuning = ScriptableObject.CreateInstance<VoxelGunTuning>();
            var boostTuning = ScriptableObject.CreateInstance<VoxelBoostTuning>();
            try
            {
                Check(definition.introductionPoster != null && definition.introductionPoster.name == "El Bicho Loco Boss Introduction",
                    "Authored Bicho poster is missing.");
                Check(van.introductionPoster != null && van.introductionPoster.name == "El Vandito Boss Introduction",
                    "Authored Vandito poster is missing.");
                Check(Resources.Load<VoxelTrackDefinition>("Tracks/TrackBoss01").boss == van,
                    "Vandito mission does not reference the poster-enabled definition.");
                var car = new GameObject("Player"); car.transform.SetParent(root.transform);
                var player = car.AddComponent<VoxelCarController>(); player.SetLaneLayout(4, 5);
                VoxelBichoLocoValidation.Property(player, "CurrentSpeed", 32f);
                var mission = root.AddComponent<VoxelMissionProgress>(); mission.Configure(tuning);
                var intro = root.AddComponent<VoxelBossIntroduction>();
                var gun = car.AddComponent<VoxelGunMount>(); gun.tuning = gunTuning;
                var boost = root.AddComponent<VoxelBoostController>();
                VoxelBichoLocoValidation.Property(boost, "Target", player);
                VoxelBichoLocoValidation.Property(boost, "Tuning", boostTuning);
                Check(gun.IsReady, "Baseline weapon is not ready.");
                foreach (var authored in new[] { source, van })
                foreach (float dt in new[] { 1f / 120, 1f / 60, 1f / 30, .25f })
                {
                    int callbacks = 0;
                    Check(intro.Begin(authored, player, mission, () => callbacks++), "Poster did not begin.");
                    Check(VoxelBossIntroduction.BlocksControls(player) && VoxelBossIntroduction.PausesMission(mission), "Reveal gates missing.");
                    Check(!gun.TryBeginShot(out _) && !boost.TryActivateBoost(), "Weapon/boost accepted input during reveal.");
                    gunTuning.projectileKind = VoxelProjectileKind.Missile;
                    Check(!gun.TryBeginShot(out _), "Missile accepted input during reveal.");
                    gunTuning.projectileKind = VoxelProjectileKind.Bullet;
                    float lane = player.TargetLaneOffset;
                    Call(player, "RequestLaneChange", 3);
                    Equal(player.TargetLaneOffset, lane, "Steering accepted reveal input");
                    float remaining = mission.RemainingTime;
                    mission.AdvanceBonusClock(20);
                    Equal(mission.RemainingTime, remaining, "Reveal consumed mission countdown");
                    float total = authored.introductionSlideInDuration + authored.introductionHoldDuration + authored.introductionSlideOutDuration;
                    float clock = 0;
                    while (clock + dt < total - .001f)
                    {
                        Call(intro, "Advance", dt); clock += dt;
                        Check(intro.IsPresenting && callbacks == 0, "Reveal completed early.");
                    }
                    Call(intro, "Advance", total - clock + .01f);
                    Check(!intro.IsPresenting && !VoxelBossIntroduction.IsShowing && callbacks == 1, "Completion/unlock must happen exactly once.");
                    Call(intro, "Advance", 10f); Check(callbacks == 1, "Repeated completion callback.");
                    Check(gun.IsReady, "Gun gate not released.");
                    mission.AdvanceBonusClock(.1f); Equal(mission.RemainingTime, remaining - .1f, "Countdown did not resume");
                }
                int canceledCallbacks = 0;
                intro.Begin(definition, player, mission, () => canceledCallbacks++);
                Call(intro, "Advance", definition.introductionSlideInDuration + .031f);
                var impact = (RectTransform)root.transform.Find("Boss Introduction Canvas/Sliding Poster/Impact Shake");
                Check(impact.anchoredPosition.sqrMagnitude > .01f, "Landing did not shake.");
                Call(intro, "Advance", definition.introductionShakeDuration + .05f);
                Equal(impact.anchoredPosition.magnitude, 0, "Landing shake did not settle");
                Capture(root, intro, scene, 1920, 1080, "16x9");
                Capture(root, intro, scene, 2400, 1080, "20x9");
                Capture(root, intro, scene, 1440, 1080, "4x3");
                intro.enabled = false;
                Call(intro, "OnDisable"); // Preview-scene MonoBehaviours do not run Edit Mode lifecycle callbacks.
                Check(!VoxelBossIntroduction.IsShowing && !VoxelBossIntroduction.PausesMission(mission) && canceledCallbacks == 0,
                    "Disable did not release gates or spawned boss after cancellation.");
                intro.enabled = true;
                intro.Begin(definition, player, mission, () => canceledCallbacks++);
                intro.Cancel(); Call(intro, "Advance", 100f);
                Check(canceledCallbacks == 0 && !VoxelBossIntroduction.IsShowing, "Cancel retained callback/input lock.");
                var poster = definition.introductionPoster; definition.introductionPoster = null;
                Check(!intro.Begin(definition, player, mission, () => canceledCallbacks++), "Missing poster did not skip cleanly.");
                definition.introductionPoster = poster;
                intro.Begin(van, player, mission, null);
                Call(intro, "Advance", van.introductionSlideInDuration + Mathf.Min(van.introductionHoldDuration * .5f,
                    van.introductionShakeDuration + .1f));
                Capture(root, intro, scene, 1920, 1080, "Vandito-16x9");
                intro.Cancel();
                Check(EditorJsonUtility.ToJson(source) == before && EditorJsonUtility.ToJson(van) == vanBefore, "Validation edited source bosses.");
                Equal(Time.timeScale, scale, "Reveal took global clock ownership"); Equal(Time.fixedDeltaTime, step, "Reveal changed physics clock");
                Check(AudioListener.pause == audio, "Reveal changed audio pause.");
                const string report = "PASS: authored Bicho and Vandito posters, Vandito mission binding; both configured sequences at 120/60/30 FPS and 250ms steps; steering/gun/missile/boost gates, countdown hold/resume, single completion, landing shake and settle, disable/cancel cleanup and empty-poster skip; 16:9/20:9/4:3 poster fit with no cropping/stretching and Vandito capture; source assets and clocks/audio/RNG preserved.";
                Directory.CreateDirectory("Temp/BossIntroduction"); File.WriteAllText("Temp/BossIntroduction/Validation.txt", report); Debug.Log(report);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("Temp/BossIntroduction"); File.WriteAllText("Temp/BossIntroduction/Validation.txt", "FAIL: " + error); throw;
            }
            finally
            {
                root.GetComponent<VoxelBossIntroduction>()?.Cancel();
                Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
                foreach (var asset in new Object[] { definition, tuning, gunTuning, boostTuning }) Object.DestroyImmediate(asset);
                typeof(VoxelMissionProgress).GetProperty("Active").SetValue(null, oldMission);
                UnityEngine.Random.state = random;
            }
        }

        private static void Capture(GameObject root, VoxelBossIntroduction intro, Scene scene, int width, int height, string name)
        {
            var go = new GameObject("Temporary Poster Camera"); SceneManager.MoveGameObjectToScene(go, scene);
            var camera = go.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
            camera.scene = scene;
            camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.transform.position = new Vector3(0, 0, -10);
            var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt;
            var canvas = root.GetComponentInChildren<Canvas>(true);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); Call(intro, "Advance", 0f); Canvas.ForceUpdateCanvases();
            var image = root.GetComponentInChildren<RawImage>();
            var rect = image.rectTransform.rect;
            Equal(rect.width / rect.height, image.texture.width / (float)image.texture.height, "Poster aspect ratio");
            var area = ((RectTransform)canvas.transform).rect;
            Check(rect.width <= area.width + 1 && rect.height <= area.height + 1, "Poster cropped outside viewport.");
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                Directory.CreateDirectory("Temp/BossIntroduction"); File.WriteAllBytes("Temp/BossIntroduction/Poster-" + name + ".png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; Object.DestroyImmediate(texture); camera.targetTexture = null;
                Object.DestroyImmediate(rt); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; Object.DestroyImmediate(go);
            }
        }
    }
}
