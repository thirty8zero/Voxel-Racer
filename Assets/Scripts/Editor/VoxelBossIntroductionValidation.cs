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
                Check(definition.introductionPoster != null && definition.introductionPoster.name == "El Bicho Loco Boss Introduction v3",
                    "Authored Bicho poster is missing.");
                Check(van.introductionPoster != null && van.introductionPoster.name == "El Vandito Boss Introduction",
                    "Authored Vandito poster is missing.");
                Check(source.introductionSequence != null && van.introductionSequence != null &&
                    source.introductionSequence != van.introductionSequence &&
                    source.introductionSequence.PanelCount == 4 && van.introductionSequence.PanelCount == 4,
                    "Both bosses require independent four-panel sequence assets.");
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
                    float total = authored.IntroductionDuration;
                    float clock = 0;
                    while (clock + dt < total - .001f)
                    {
                        Call(intro, "Advance", dt); clock += dt;
                        Check(intro.IsPresenting && callbacks == 0, "Reveal completed early.");
                        var landingBackdrop = root.transform.Find("Boss Introduction Canvas/Sliding Poster/Landing Black Backdrop").GetComponent<Image>();
                        Check(landingBackdrop.enabled == (intro.Elapsed >= authored.introductionSequence.slideInDuration),
                            "Black backing must switch on at the first landing, including long frames.");
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
                Call(intro, "Advance", definition.IntroductionAssembledAt + .6f - intro.Elapsed);
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
                var sequence = definition.introductionSequence;
                definition.introductionSequence = null;
                // Older content without a sequence still uses its single poster.
                Check(intro.Begin(definition, player, mission, null), "Legacy poster fallback missing.");
                Equal(intro.Duration, definition.IntroductionDuration, "Legacy duration"); intro.Cancel();
                var poster = definition.introductionPoster; definition.introductionPoster = null;
                Check(!intro.Begin(definition, player, mission, () => canceledCallbacks++), "Missing poster did not skip cleanly.");
                definition.introductionSequence = sequence;
                Check(intro.Begin(definition, player, mission, null), "Layered sequence incorrectly needs a fallback poster."); intro.Cancel();
                definition.introductionPoster = poster;
                CheckPanelTimeline(root, intro, definition, player, mission);
                intro.Begin(van, player, mission, null);
                Call(intro, "Advance", van.IntroductionAssembledAt + .6f);
                Capture(root, intro, scene, 1920, 1080, "Vandito-16x9");
                intro.Cancel();
                foreach (var authored in new[] { source, van })
                    CheckLandingBackdrop(root, intro, authored, player, mission, scene, false);
                CheckLandingBackdrop(root, intro, source, player, mission, scene, true);
                Check(EditorJsonUtility.ToJson(source) == before && EditorJsonUtility.ToJson(van) == vanBefore, "Validation edited source bosses.");
                Equal(Time.timeScale, scale, "Reveal took global clock ownership"); Equal(Time.fixedDeltaTime, step, "Reveal changed physics clock");
                Check(AudioListener.pause == audio, "Reveal changed audio pause.");
                const string report = "PASS: independent four-layer Bicho/Vandito assets and mission binding; both sequences at 120/60/30 FPS and 250ms steps; left/right alternating entries, landing-to-entry delay, per-panel order independent of draw order, final hold and whole-poster exit; steering/gun/missile/boost gates, countdown hold/resume, single completion, shake/settle, disable/cancel cleanup, layer-only content and legacy/empty fallback; first-landing black backing, overscan, shake isolation, exit/restart cleanup and no exposed scene-edge pixels during exaggerated shake for both bosses and legacy posters at 16:9/20:9/4:3; source assets and clocks/audio/RNG preserved.";
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

        private static void CheckPanelTimeline(GameObject root, VoxelBossIntroduction intro, VoxelBossDefinition definition,
            VoxelCarController player, VoxelMissionProgress mission)
        {
            var original = definition.introductionSequence;
            var sequence = Object.Instantiate(original);
            definition.introductionSequence = sequence;
            try
            {
                foreach (var side in new[] { VoxelBossIntroSide.Left, VoxelBossIntroSide.Right })
                {
                    sequence.firstEntrySide = side;
                    int callbacks = 0;
                    intro.Begin(definition, player, mission, () => callbacks++);
                    var impact = root.transform.Find("Boss Introduction Canvas/Sliding Poster/Impact Shake");
                    var panels = impact.Cast<Transform>().Where(t => t.name.StartsWith("Panel ")).OrderBy(t => t.name).Cast<RectTransform>().ToArray();
                    Check(panels.Length == 4, "Expected four panel views.");
                    float sign = side == VoxelBossIntroSide.Left ? -1 : 1;
                    for (int i = 0; i < 4; i++)
                    {
                        Check(Mathf.Sign(panels[i].anchoredPosition.x) == (i % 2 == 0 ? sign : -sign), "Panel did not alternate entry side.");
                        Check(panels[i].gameObject.activeSelf == (i == 0), "Future panel visible early.");
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        float landing = i * (sequence.slideInDuration + sequence.delayBetweenPanels) + sequence.slideInDuration;
                        Call(intro, "Advance", landing - intro.Elapsed + .0001f);
                        Equal(panels[i].anchoredPosition.x, 0, "Panel failed to land");
                        Check(panels[i].gameObject.activeSelf, "Landed panel hidden.");
                        if (i < 3)
                        {
                            Call(intro, "Advance", sequence.delayBetweenPanels * .5f);
                            Check(!panels[i + 1].gameObject.activeSelf, "Next panel ignored the delay after landing.");
                        }
                    }
                    float beforeExit = sequence.AssembledAt + sequence.holdDuration - .01f;
                    Call(intro, "Advance", beforeExit - intro.Elapsed);
                    Equal(((RectTransform)impact.parent).anchoredPosition.x, 0, "Final hold ended early");
                    Call(intro, "Advance", sequence.slideOutDuration * .5f + .01f);
                    Check(Mathf.Sign(((RectTransform)impact.parent).anchoredPosition.x) == -sign, "Exit did not oppose first entry.");
                    Call(intro, "Advance", 100f); Check(callbacks == 1, "Timeline completion callback count.");
                }
                // Reorder entry rows without changing visual stacking. Skip unassigned rows.
                sequence.panels = new[] { original.panels[3], null, original.panels[0], original.panels[2], original.panels[1] };
                intro.Begin(definition, player, mission, null);
                var holder = root.transform.Find("Boss Introduction Canvas/Sliding Poster/Impact Shake");
                var first = holder.Cast<Transform>().Single(t => t.name.StartsWith("Panel 0 -"));
                Check(first.GetComponentInChildren<RawImage>(true).texture == original.panels[3].texture && first.GetSiblingIndex() == holder.childCount - 1,
                    "Entry order is tied to visual stacking.");
                Check(sequence.PanelCount == 4, "Missing row not skipped."); intro.Cancel();
                sequence.panels = Array.Empty<VoxelBossIntroductionPanel>();
                Check(intro.Begin(definition, player, mission, null), "Empty sequence did not fall back to poster.");
                Equal(intro.Duration, definition.introductionSlideInDuration + definition.introductionHoldDuration + definition.introductionSlideOutDuration, "Empty-sequence legacy timing");
                intro.Cancel();
            }
            finally { intro.Cancel(); definition.introductionSequence = original; Object.DestroyImmediate(sequence); }
        }

        private static void CheckLandingBackdrop(GameObject root, VoxelBossIntroduction intro, VoxelBossDefinition source,
            VoxelCarController player, VoxelMissionProgress mission, Scene scene, bool legacy)
        {
            var definition = Object.Instantiate(source);
            var sequence = Object.Instantiate(source.introductionSequence);
            definition.introductionSequence = legacy ? null : sequence;
            sequence.shakePixels = definition.introductionShakePixels = 100;
            sequence.shakeDuration = definition.introductionShakeDuration = .5f;
            try
            {
                intro.Begin(definition, player, mission, null);
                var backing = root.transform.Find("Boss Introduction Canvas/Sliding Poster/Landing Black Backdrop").GetComponent<Image>();
                var impact = (RectTransform)root.transform.Find("Boss Introduction Canvas/Sliding Poster/Impact Shake");
                float landing = legacy ? definition.introductionSlideInDuration : sequence.slideInDuration;
                Check(!backing.enabled, "Backing covered the scene before the first entry.");
                Call(intro, "Advance", landing - .001f);
                Check(!backing.enabled, "Backing appeared before the first panel landed.");
                Call(intro, "Advance", landing - intro.Elapsed);
                Check(backing.enabled && backing.color == Color.black && !backing.raycastTarget,
                    "First landing must reveal an opaque non-interactive black backing.");
                Check(backing.transform.parent == impact.parent && backing.transform.GetSiblingIndex() < impact.GetSiblingIndex(),
                    "Backing must sit behind the artwork outside its shake root.");
                var rect = backing.rectTransform;
                Check(rect.offsetMin.x <= -102 && rect.offsetMin.y <= -102 && rect.offsetMax.x >= 102 && rect.offsetMax.y >= 102,
                    "Backing does not overflow all four edges by the shake margin.");
                var position = rect.position;
                Call(intro, "Advance", .017f);
                Check(impact.anchoredPosition.sqrMagnitude > 1 && Vector3.Distance(position, rect.position) < .001f,
                    "Backing moved with the landing shake.");
                string name = legacy ? "Legacy" : source.name;
                Capture(root, intro, scene, 1920, 1080, name + "-Shake-16x9", true);
                Capture(root, intro, scene, 2400, 1080, name + "-Shake-20x9", true);
                Capture(root, intro, scene, 1440, 1080, name + "-Shake-4x3", true);
                float exitStart = intro.AssembledAt + (legacy ? definition.introductionHoldDuration : sequence.holdDuration);
                float exitDuration = legacy ? definition.introductionSlideOutDuration : sequence.slideOutDuration;
                Call(intro, "Advance", exitStart + exitDuration * .5f - intro.Elapsed);
                Check(backing.enabled && Mathf.Abs(((RectTransform)backing.transform.parent).anchoredPosition.x) > 1,
                    "Backing did not leave with the existing exit slide.");
                Call(intro, "Advance", 100f);
                Check(!backing.enabled && !backing.gameObject.activeInHierarchy, "Completion left a black overlay behind.");
                intro.Begin(definition, player, mission, null);
                Check(!backing.enabled, "Restart retained the previous black backing.");
                Call(intro, "Advance", landing + .017f);
                intro.Cancel();
                Check(!backing.enabled && !backing.gameObject.activeInHierarchy, "Cancellation left a black overlay behind.");
            }
            finally { intro.Cancel(); Object.DestroyImmediate(definition); Object.DestroyImmediate(sequence); }
        }

        private static void Capture(GameObject root, VoxelBossIntroduction intro, Scene scene, int width, int height, string name,
            bool checkSceneEdges = false)
        {
            var go = new GameObject("Temporary Poster Camera"); SceneManager.MoveGameObjectToScene(go, scene);
            var camera = go.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = checkSceneEdges ? Color.magenta : Color.gray;
            camera.scene = scene;
            camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.transform.position = new Vector3(0, 0, -10);
            var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt;
            var canvas = root.GetComponentInChildren<Canvas>(true);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); Call(intro, "Advance", 0f); Canvas.ForceUpdateCanvases();
            var area = ((RectTransform)canvas.transform).rect;
            foreach (var image in root.GetComponentsInChildren<RawImage>())
            {
                var rect = image.rectTransform.parent.name == "Artwork Frame" ? ((RectTransform)image.rectTransform.parent).rect : image.rectTransform.rect;
                Check(rect.width <= area.width + 1 && rect.height <= area.height + 1, "Poster cropped outside viewport.");
                bool fullScreen = image.rectTransform.parent.parent.name.Contains("Dark Background");
                if (fullScreen)
                { Equal(rect.width, area.width, "Background width"); Equal(rect.height, area.height, "Background height"); }
                else Equal(rect.width / rect.height, 1672f / 941f, "Artwork frame aspect ratio");
            }
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                if (checkSceneEdges)
                {
                    // A bright sentinel scene makes even a one-pixel uncovered viewport edge visible.
                    bool IsScene(Color32 pixel) => pixel.r > 250 && pixel.g < 5 && pixel.b > 250;
                    for (int x = 0; x < width; x++)
                        Check(!IsScene(texture.GetPixel(x, 0)) && !IsScene(texture.GetPixel(x, height - 1)), "Scene exposed along a horizontal edge during shake.");
                    for (int y = 0; y < height; y++)
                        Check(!IsScene(texture.GetPixel(0, y)) && !IsScene(texture.GetPixel(width - 1, y)), "Scene exposed along a vertical edge during shake.");
                    Check(texture.GetPixel(0, 0).maxColorComponent < .01f, "Exposed corner did not show the black backing.");
                }
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
