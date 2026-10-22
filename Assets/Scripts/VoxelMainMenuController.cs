using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Builds the title screen UI and starts a new mission with the selected car.</summary>
    public sealed class VoxelMainMenuController : MonoBehaviour
    {
        public string raceSceneName = "SampleScene";
        [Tooltip("Leave empty to load Resources/MainMenuTuning automatically.")]
        public VoxelMainMenuTuning tuning;

        private Transform featuredDisplayRoot;
        private Transform desertSceneryRoot;
        private bool isLoading;
        private Transform startButtonTransform;
        private Tween startButtonPulse;

        private void Awake()
        {
            tuning = tuning != null ? tuning : VoxelMainMenuTuning.Load();
            BuildFeaturedCars();
            BuildDesertScenery();
            BuildArtwork();
            BuildMenuUi();
        }

        public void BuildMenuUi()
        {
            tuning = tuning != null ? tuning : VoxelMainMenuTuning.Load();
            var existing = transform.Find("Main Menu UI") as RectTransform;
            RectTransform canvas = existing != null ? existing : VoxelMenuUi.CreateCanvas(transform, "Main Menu UI");
            BuildTitle(canvas);
            var existingButton = canvas.Find("Start Button");
            if (existingButton != null) { startButtonTransform = existingButton; return; }
            var startButton = VoxelMenuUi.CreateButton(canvas, "Start Button", "START", 132,
                new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(780f, 204f), StartMission);
            // Keep the label's existing layout so the large font stays unclipped.
            ((RectTransform)startButton.transform).sizeDelta = new Vector2(340f, 124f);
            startButton.image.color = new Color(0f, 0f, 0f, .7f);
            startButtonTransform = startButton.transform;
        }

        private void BuildTitle(RectTransform canvas)
        {
            var oldTitle = canvas.Find("Title");
            if (oldTitle != null) oldTitle.gameObject.SetActive(false);
            var existing = canvas.Find("Road Spectre Logo");
            if (existing == null)
            {
                var logoRoot = new GameObject("Road Spectre Logo", typeof(RectTransform));
                logoRoot.transform.SetParent(canvas, false);
                existing = logoRoot.transform;
            }
            var rect = (RectTransform)existing;
            rect.anchorMin = new Vector2(.05f, .62f); rect.anchorMax = new Vector2(.95f, .98f);
            rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            SetArtworkLayer(existing, "Logo Image", tuning != null ? tuning.titleLogo : null, AspectRatioFitter.AspectMode.FitInParent);
        }

        private void OnEnable()
        {
            if (startButtonTransform == null) return;
            startButtonPulse = startButtonTransform.DOScale(1.06f, .7f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void OnDisable()
        {
            startButtonPulse?.Kill();
            startButtonPulse = null;
            if (startButtonTransform != null) startButtonTransform.localScale = Vector3.one;
        }

        /// <summary>Non-interactive placeholder layers between the scene and the original menu UI.</summary>
        public void BuildArtwork()
        {
            tuning = tuning != null ? tuning : VoxelMainMenuTuning.Load();
            var titleCanvas = transform.Find("Main Menu UI") as RectTransform;
            if (titleCanvas != null) BuildTitle(titleCanvas);
            var existing = transform.Find("Main Menu Artwork");
            bool show = tuning != null && tuning.artworkEnabled && tuning.backgroundArtwork != null;
            if (!show)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }

            if (existing == null)
            {
                var root = new GameObject("Main Menu Artwork", typeof(RectTransform), typeof(Canvas));
                root.transform.SetParent(transform, false);
                existing = root.transform;
            }
            existing.gameObject.SetActive(true);
            var canvas = existing.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 99; // VoxelMenuUi's original title/button canvas is 100.
            SetArtworkLayer(existing, "Sunset Highway Background", tuning.backgroundArtwork, AspectRatioFitter.AspectMode.EnvelopeParent);
            var foregroundArea = existing.Find("Foreground Artwork Area") as RectTransform;
            if (foregroundArea == null)
            {
                foregroundArea = new GameObject("Foreground Artwork Area", typeof(RectTransform)).GetComponent<RectTransform>();
                foregroundArea.SetParent(existing, false);
                var previousForeground = existing.Find("Muscle Car Foreground");
                if (previousForeground != null) previousForeground.SetParent(foregroundArea, false);
            }
            // The logo ends just above the white roof; the particle area may extend behind it.
            foregroundArea.anchorMin = new Vector2(.08f, .02f);
            foregroundArea.anchorMax = new Vector2(.92f, .84f);
            foregroundArea.anchoredPosition = Vector2.zero; foregroundArea.sizeDelta = Vector2.zero;
            foregroundArea.SetAsLastSibling();
            SetArtworkLayer(foregroundArea, "Muscle Car Foreground", tuning.foregroundArtwork, AspectRatioFitter.AspectMode.FitInParent);
        }

        private static void SetArtworkLayer(Transform parent, string name, Texture2D texture, AspectRatioFitter.AspectMode aspectMode)
        {
            var existing = parent.Find(name);
            if (existing == null)
            {
                var layer = new GameObject(name, typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                layer.transform.SetParent(parent, false);
                existing = layer.transform;
            }
            existing.SetAsLastSibling();
            existing.gameObject.SetActive(texture != null);
            if (texture == null) return;
            var rect = (RectTransform)existing;
            rect.pivot = new Vector2(.5f, .5f);
            var image = existing.GetComponent<RawImage>();
            image.texture = texture;
            image.color = Color.white;
            image.raycastTarget = false;
            var fitter = existing.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = aspectMode;
            fitter.aspectRatio = texture.width / (float)texture.height;
        }

        public void BuildDesertScenery()
        {
            // Generated roots can survive scene saves/domain reloads while the field does not.
            // Remove every previous generated backdrop so a stale mountain wall cannot remain.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var previous = transform.GetChild(i);
                if (previous.name != "Main Menu Desert Scenery") continue;
                previous.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(previous.gameObject);
                else DestroyImmediate(previous.gameObject);
            }

            desertSceneryRoot = new GameObject("Main Menu Desert Scenery").transform;
            desertSceneryRoot.SetParent(transform, false);
            VoxelTrackDefinition track = VoxelRacerBootstrap.ActiveTrack;
            VoxelRacerBootstrap.PrepareTrackMaterials(track);
            if (track != null && track.skyboxMaterial != null)
                RenderSettings.skybox = track.skyboxMaterial;

            var surroundingGround = VoxelRacerBootstrap.CreateBlock("Main Menu Surrounding Ground",
                desertSceneryRoot, new Vector3(0f, -0.305f, -18f),
                new Vector3(120f, 0.25f, 120f), VoxelRacerBootstrap.GroundMaterial);
            surroundingGround.transform.SetAsFirstSibling();

            Camera menuCamera = Camera.main;
            VoxelMobilePostProcessing.Configure(menuCamera);
            if (tuning == null || tuning.showHorizonMountains)
            {
                var mountains = new GameObject("Main Menu Horizon Mountains")
                    .AddComponent<VoxelHorizonMountains>();
                mountains.transform.SetParent(desertSceneryRoot, false);
                mountains.Configure(menuCamera != null ? menuCamera.transform : transform, track,
                    tuning != null ? tuning.mountainScale : 1f, useTrackTimeOfDay: false);
                if (menuCamera != null)
                {
                    float mountainDistance = track != null ? track.mountainDistance : 170f;
                    menuCamera.farClipPlane = Mathf.Max(menuCamera.farClipPlane, mountainDistance + 40f);
                }
            }

            int cactusCount = tuning != null ? tuning.cactusCount : 24;
            float minimumScale = tuning != null ? tuning.minimumCactusScale : 0.55f;
            float maximumScale = tuning != null ? tuning.maximumCactusScale : 1.15f;
            int cactusSeed = tuning != null ? tuning.cactusSeed : 912;
            Random.State previousRandomState = Random.state;
            Random.InitState(cactusSeed);
            for (int index = 0; index < cactusCount; index++)
            {
                Vector3 position = ChooseCactusPosition();
                if (track != null && track.scenerySet != null)
                {
                    var entry = track.scenerySet.Choose();
                    if (entry != null)
                    {
                        float scale = VoxelScenerySet.ChooseScale(entry);
                        bool overlaps = false;
                        foreach (var other in desertSceneryRoot.GetComponentsInChildren<VoxelSceneryInstance>())
                            if (Vector3.Distance(position, other.transform.position) < entry.radius * scale + entry.spacing + other.radius) { overlaps = true; break; }
                        if (!overlaps) VoxelScenerySet.Spawn(entry, desertSceneryRoot, position, scale);
                    }
                    continue;
                }
                var cactus = new GameObject("Main Menu Voxel Cactus").transform;
                cactus.SetParent(desertSceneryRoot, false);
                cactus.localPosition = position;
                cactus.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                EndlessVoxelRoad.BuildRandomCactusVisual(cactus, minimumScale, maximumScale,
                    minimumScale, maximumScale, track != null ? track.cactusShades : null);
            }
            Random.state = previousRandomState;
        }

        private static Vector3 ChooseCactusPosition()
        {
            // The Menu Floor is 18 x 12 metres (x +/-9, z +/-6). Place every
            // cactus beyond that footprint on the surrounding desert instead.
            if (Random.value < 0.72f)
            {
                // Most scenery forms a broad range behind the display platform.
                return new Vector3(Random.Range(-13f, 13f), -0.13f, Random.Range(-16f, -7f));
            }

            // A smaller number fills the desert just beyond the platform's sides.
            float side = Random.value < 0.5f ? -1f : 1f;
            return new Vector3(side * Random.Range(9.75f, 14f), -0.13f, Random.Range(-9f, 3.5f));
        }

        private void Update()
        {
            if (featuredDisplayRoot != null)
                featuredDisplayRoot.Rotate(Vector3.up, 18f * Time.unscaledDeltaTime, Space.World);

            if (isLoading || VoxelLoadingScreen.IsLoading)
                return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                 keyboard.spaceKey.wasPressedThisFrame))
                StartMission();
        }

        /// <summary>Rebuilds the two display slots from the persistent featured-car list.</summary>
        public void BuildFeaturedCars()
        {
            DisableLegacyCarDisplays();

            // Editor previews and scene-saved displays can outlive the nonserialized
            // reference when entering Play Mode or reloading scripts.
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                Transform child = transform.GetChild(index);
                if (child != featuredDisplayRoot && child.name != "Featured Car Displays" && child.name != "Car Turntable")
                    continue;

                // Destroy is deferred until the end of the frame in Play Mode, so
                // hide the old display immediately to prevent a one-frame overlap.
                child.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
            featuredDisplayRoot = null;

            var definitions = GetFeaturedDefinitions();
            ConfigureDisplayAccessories(definitions.Count);
            if (definitions.Count == 0)
                return;

            featuredDisplayRoot = new GameObject("Featured Car Displays").transform;
            featuredDisplayRoot.SetParent(transform, false);
            VoxelCarTurntable.Create(transform, featuredDisplayRoot, definitions.Count == 1 ? 3.2f : 5.7f);
            for (int index = 0; index < definitions.Count; index++)
            {
                VoxelCarDefinition definition = definitions[index];
                GameObject display;
#if UNITY_EDITOR
                // Keep scene previews linked so model edits propagate without a rebuild.
                if (!Application.isPlaying)
                    display = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(
                        definition.visualPrefab, featuredDisplayRoot);
                else
#endif
                    display = Instantiate(definition.visualPrefab, featuredDisplayRoot);
                display.name = definition.displayName + " Main Menu Display";
                float x = definitions.Count == 1 ? 0f : index == 0 ? -2.65f : 2.65f;
                float yaw = definitions.Count == 1 ? 0f : index == 0 ? -25f : 25f;
                display.transform.localPosition = new Vector3(x, 0.18f, 0f);
                display.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                display.transform.localScale = Vector3.one * 0.92f;
            }
        }

        private List<VoxelCarDefinition> GetFeaturedDefinitions()
        {
            var featured = new List<VoxelCarDefinition>(2);
            if (tuning != null && tuning.featuredCars != null)
            {
                foreach (VoxelCarDefinition definition in tuning.featuredCars)
                {
                    if (definition == null || definition.visualPrefab == null || featured.Contains(definition))
                        continue;
                    featured.Add(definition);
                    if (featured.Count == 2)
                        break;
                }
            }

            if (featured.Count > 0)
                return featured;

            foreach (VoxelCarDefinition definition in VoxelCarSelectionState.LoadDefinitions())
            {
                if (definition == null || definition.visualPrefab == null)
                    continue;
                featured.Add(definition);
                if (featured.Count == 2)
                    break;
            }
            return featured;
        }

        private void DisableLegacyCarDisplays()
        {
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform child = transform.GetChild(index);
                if (child == featuredDisplayRoot || !child.name.EndsWith(" Display"))
                    continue;
                child.gameObject.SetActive(false);
            }
        }

        private void ConfigureDisplayAccessories(int displayCount)
        {
            var plinths = new List<Transform>();
            var spotlights = new List<Transform>();
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform child = transform.GetChild(index);
                if (child.name == "Car Display Plinth")
                    plinths.Add(child);
                else if (child.name.EndsWith(" Car Spotlight"))
                    spotlights.Add(child);
            }

            foreach (var plinth in plinths) plinth.gameObject.SetActive(false);
            ConfigureAccessoryList(spotlights, displayCount);
        }

        private static void ConfigureAccessoryList(List<Transform> accessories, int displayCount)
        {
            accessories.Sort((first, second) => first.localPosition.x.CompareTo(second.localPosition.x));
            for (int index = 0; index < accessories.Count; index++)
            {
                bool active = index < displayCount;
                accessories[index].gameObject.SetActive(active);
                if (!active)
                    continue;

                Vector3 position = accessories[index].localPosition;
                position.x = displayCount == 1 ? 0f : index == 0 ? -2.65f : 2.65f;
                accessories[index].localPosition = position;
            }
        }

        public void StartMission()
        {
            if (isLoading || VoxelLoadingScreen.IsLoading)
                return;
            VoxelCarDefinition selectedCar = VoxelCarSelectionState.GetSelectedOrDefault();
            if (selectedCar == null)
            {
                Debug.LogError("Cannot start mission because no selectable car is configured.");
                return;
            }

            VoxelCarSelectionState.Select(selectedCar);
            VoxelCarRunState.BeginNewRun(selectedCar);
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(
                "Assets/Scenes/" + raceSceneName + ".unity");
            if (buildIndex < 0)
            {
                Debug.LogError("Race scene is not enabled in Build Settings: " + raceSceneName);
                return;
            }
            isLoading = true;
            VoxelLoadingScreen.LoadScene(buildIndex);
        }
    }
}
