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
    public static class VoxelLoadingScreenValidation
    {
        private const string Pending = "VoxelRacer.LoadingScreenValidation";
        private static readonly string[] Destinations = { "CarSelect", "SampleScene", "RepairUpgrade", "MainMenu" };
        private static int destination, phase, wallet, trackIndex, instanceId;
        private static float scale, step;
        private static bool background, captured;
        private static double loadStartedAt;
        private static VoxelLoadingScreenSettings settings;
        private static Texture previousImage;
        private static string settingsBefore;
        private static bool sawRaceGate, sawJiggle;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Loading Screen/Validate Transitions in Play Mode")]
        public static void Run()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.GetActiveScene().name == "MainMenu", "Start from MainMenu in Edit Mode.");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-voxelValidationOutput");
            if (index >= 0 && index + 1 < args.Length) SessionState.SetString(Pending + ".Output", args[index + 1]);
            SessionState.SetBool(Pending + ".Batch", true); Run();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending + ".Batch", false))
            {
                SessionState.SetBool(Pending + ".Batch", false);
                EditorApplication.Exit(SessionState.GetBool(Pending + ".Success", false) ? 0 : 1); return;
            }
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                phase = destination = 0; captured = sawRaceGate = sawJiggle = false;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.ExitingPlayMode) Finish(false, "Interrupted before transitions completed.");
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        private static RawImage Image => VoxelLoadingScreen.Active.GetComponentInChildren<RawImage>(true);

        private static void Tick()
        {
            if (EditorApplication.isPaused && SessionState.GetBool(Pending + ".Batch", false)) EditorApplication.isPaused = false;
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            try
            {
                if (phase == 0)
                {
                    Check(VoxelLoadingScreen.Active != null, "Startup did not create a loading screen.");
                    wallet = VoxelCurrencyState.Balance; trackIndex = VoxelTrackProgressState.CurrentTrackIndex;
                    scale = Time.timeScale; step = Time.fixedDeltaTime; background = Application.runInBackground; captured = true;
                    Application.runInBackground = true;
                    instanceId = VoxelLoadingScreen.Active.GetInstanceID();
                    settings = VoxelLoadingScreenSettings.Load();
                    Check(settings != null, "Missing loading settings.");
                    settingsBefore = EditorJsonUtility.ToJson(settings);
                    CheckImagePool();
                    loadStartedAt = Time.realtimeSinceStartupAsDouble;
                    phase = 1;
                }
                else if (phase == 1)
                {
                    Check(Time.realtimeSinceStartupAsDouble - loadStartedAt < 30, "Startup loading screen did not finish.");
                    if (VoxelLoadingScreen.IsLoading) return;
                    previousImage = Image.texture;
                    StartNextLoad();
                }
                else if (phase == 2)
                {
                    double elapsed = Time.realtimeSinceStartupAsDouble - loadStartedAt;
                    Check(elapsed < 45, "Scene load timed out: " + Destinations[destination]);
                    if (VoxelLoadingScreen.IsLoading)
                    {
                        var screen = VoxelLoadingScreen.Active;
                        Check(screen != null && screen.GetInstanceID() == instanceId, "Loading overlay did not survive scene activation.");
                        var canvas = screen.GetComponentInChildren<Canvas>();
                        Check(canvas.sortingOrder == 30000 && canvas.GetComponent<GraphicRaycaster>() != null, "Loading overlay ordering/input blocker missing.");
                        Check(settings.showImages ? settings.IsEligible((Texture2D)Image.texture)
                            : Image.texture == null && !Image.gameObject.activeSelf, "Loading artwork visibility mismatch.");
                        var label = screen.GetComponentInChildren<Text>();
                        if (label.rectTransform.anchoredPosition.sqrMagnitude > .1f) sawJiggle = true;
                        var countdown = VoxelStartCountdown.Active;
                        if (countdown != null)
                        {
                            Check(!countdown.IsComplete && !countdown.IsTrafficSpawnWindowOpen && countdown.GameplayHudAlpha == 0,
                                "Race opening/countdown/traffic advanced under loading screen.");
                            sawRaceGate = true;
                        }
                        return;
                    }
                    Check(elapsed >= settings.minimumDisplaySeconds - .01f, "Fast load flashed instead of respecting its minimum duration.");
                    Check(SceneManager.GetActiveScene().name == Destinations[destination], "Wrong scene loaded.");
                    Check(VoxelCurrencyState.Balance == wallet && VoxelTrackProgressState.CurrentTrackIndex == trackIndex,
                        "Loader changed wallet or campaign position.");
                    if (settings.showImages && settings.images.Count(settings.IsEligible) > 1) Check(Image.texture != previousImage, "Adjacent loads repeated artwork despite alternatives.");
                    previousImage = Image.texture;
                    if (destination == 0) Check(Time.timeScale == 0f, "Loading at zero time scale changed the clock.");
                    Time.timeScale = scale;
                    destination++;
                    if (destination < Destinations.Length) StartNextLoad();
                    else
                    {
                        Check(sawJiggle && sawRaceGate, "Missing live jiggle or race-opening coverage.");
                        Check(EditorJsonUtility.ToJson(settings) == settingsBefore, "Loading edited its source settings.");
                        Finish(true, "PASS (Play Mode): startup overlay; asynchronous MainMenu -> CarSelect -> SampleScene -> RepairUpgrade -> MainMenu transitions; one persistent overlay, duplicate-request guard, real-time minimum/jiggle at timeScale 0, topmost input blocking, eligible/non-repeating random images, empty/excluded image handling, unchanged gameplay RNG/wallet/campaign position, race opening/traffic/HUD wait until loading ends, and source settings preserved.");
                    }
                }
            }
            catch (Exception error) { Finish(false, "FAIL: " + error); }
        }

        private static void StartNextLoad()
        {
            if (destination == 0) Time.timeScale = 0f;
            int buildIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + Destinations[destination] + ".unity");
            var departingRoots = SceneManager.GetActiveScene().GetRootGameObjects().Where(root => root.activeSelf).ToArray();
            loadStartedAt = Time.realtimeSinceStartupAsDouble;
            Check(VoxelLoadingScreen.LoadScene(buildIndex) && VoxelLoadingScreen.IsLoading, "Overlay was not shown immediately.");
            Check(departingRoots.All(root => root == null || !root.activeSelf), "Departing scene kept running after state capture/reset.");
            Check(!VoxelLoadingScreen.LoadScene(buildIndex), "Duplicate loading request accepted.");
            phase = 2;
        }

        private static void CheckImagePool()
        {
            var randomBefore = UnityEngine.Random.state;
            var random = new System.Random(7391);
            Texture2D prior = null;
            for (int i = 0; i < 40; i++)
            {
                var chosen = settings.ChooseImage(random, prior);
                Check(settings.showImages ? settings.IsEligible(chosen) : chosen == null, "Invalid image in initial loading pool.");
                if (settings.showImages && settings.images.Count(settings.IsEligible) > 1) Check(chosen != prior, "Random pool immediately repeated an image.");
                prior = chosen;
            }
            Check(UnityEngine.Random.state.Equals(randomBefore), "Image selection consumed gameplay RNG.");
            var copy = Object.Instantiate(settings);
            try
            {
                copy.showImages = true;
                var menu = VoxelMainMenuTuning.Load();
                var valid = settings.images.First(settings.IsEligible);
                copy.images = new[] { menu.backgroundArtwork, menu.foregroundArtwork, null, valid };
                for (int i = 0; i < 8; i++) Check(copy.ChooseImage(random) == valid, "Main-menu/null images were not excluded.");
                copy.showImages = false; Check(copy.ChooseImage(random) == null, "Disabled pictures must fall back to black.");
                copy.showImages = true;
                copy.images = Array.Empty<Texture2D>(); Check(copy.ChooseImage(random) == null, "Empty pool did not fall back to black.");
            }
            finally { Object.Destroy(copy); }
        }

        private static void Finish(bool success, string report)
        {
            EditorApplication.update -= Tick; SessionState.SetBool(Pending, false); SessionState.SetBool(Pending + ".Success", success);
            if (captured) { Time.timeScale = scale; Time.fixedDeltaTime = step; Application.runInBackground = background; }
            string path = SessionState.GetString(Pending + ".Output", "Temp/LoadingScreen/PlayValidation.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, report);
            if (success) Debug.Log(report); else Debug.LogError(report);
            EditorApplication.isPlaying = false;
        }
    }
}
