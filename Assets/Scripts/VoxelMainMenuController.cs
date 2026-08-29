using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

        private void Awake()
        {
            tuning = tuning != null ? tuning : VoxelMainMenuTuning.Load();
            BuildFeaturedCars();
            BuildDesertScenery();

            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Main Menu UI");
            VoxelMenuUi.CreateText(canvas, "Title", "VOXEL RACER", 264, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(1600f, 250f)).color = Color.black;
            VoxelMenuUi.CreateButton(canvas, "Start Button", "START", 132,
                new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(780f, 204f), StartMission);
        }

        public void BuildDesertScenery()
        {
            if (desertSceneryRoot != null)
            {
                desertSceneryRoot.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(desertSceneryRoot.gameObject);
                else DestroyImmediate(desertSceneryRoot.gameObject);
            }

            desertSceneryRoot = new GameObject("Main Menu Desert Scenery").transform;
            desertSceneryRoot.SetParent(transform, false);
            VoxelTrackDefinition track = VoxelRacerBootstrap.ActiveTrack;
            VoxelRacerBootstrap.PrepareTrackMaterials(track);

            var surroundingGround = VoxelRacerBootstrap.CreateBlock("Main Menu Surrounding Ground",
                desertSceneryRoot, new Vector3(0f, -0.305f, -18f),
                new Vector3(120f, 0.25f, 120f), VoxelRacerBootstrap.GroundMaterial);
            surroundingGround.transform.SetAsFirstSibling();

            Camera menuCamera = Camera.main;
            if (tuning == null || tuning.showHorizonMountains)
            {
                var mountains = new GameObject("Main Menu Horizon Mountains")
                    .AddComponent<VoxelHorizonMountains>();
                mountains.transform.SetParent(desertSceneryRoot, false);
                mountains.Configure(menuCamera != null ? menuCamera.transform : transform, track,
                    tuning != null ? tuning.mountainScale : 1f);
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
                featuredDisplayRoot.Rotate(Vector3.up, 18f * Time.deltaTime, Space.World);

            if (isLoading)
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

            if (featuredDisplayRoot != null)
            {
                // Destroy is deferred until the end of the frame in Play Mode, so
                // hide the old display immediately to prevent a one-frame overlap.
                featuredDisplayRoot.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(featuredDisplayRoot.gameObject);
                else
                    DestroyImmediate(featuredDisplayRoot.gameObject);
            }

            var definitions = GetFeaturedDefinitions();
            ConfigureDisplayAccessories(definitions.Count);
            if (definitions.Count == 0)
                return;

            featuredDisplayRoot = new GameObject("Featured Car Displays").transform;
            featuredDisplayRoot.SetParent(transform, false);
            for (int index = 0; index < definitions.Count; index++)
            {
                VoxelCarDefinition definition = definitions[index];
                GameObject display = Instantiate(definition.visualPrefab, featuredDisplayRoot);
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

            ConfigureAccessoryList(plinths, displayCount);
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
            if (isLoading)
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
            SceneManager.LoadScene(buildIndex);
        }
    }
}
