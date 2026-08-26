using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelRacer
{
    /// <summary>
    /// Creates the first playable visual prototype when the sample scene starts.
    /// It deliberately uses simple cubes so the car's voxel layout is easy to replace
    /// with detachable damage voxels later.
    /// </summary>
    public static class VoxelRacerBootstrap
    {
        private const float RoadWidth = 12f;
        private const float RoadLength = 160f;
        private const int LaneCount = 4;

        private static Material roadMaterial;
        private static Material paintMaterial;
        private static Material glassMaterial;
        private static Material tyreMaterial;
        private static Material hubMaterial;
        private static Material lineMaterial;
        private static Material shoulderMaterial;
        private static Material groundMaterial;
        private static Material cactusMaterial;
        private static Material obstacleMaterial;
        private static Material obstacleCarPaintMaterial;
        private static Material obstacleCarTrimMaterial;
        private static Material obstacleCarGlassMaterial;
        private static Material startLineMaterial;
        private static Material carTailLightMaterial;
        private static Material carMetalDetailMaterial;
        private static Material carAccentMaterial;
        private static Material longtailPaintMaterial;
        private static Material formulaOrangeMaterial;
        private static Material formulaWhiteMaterial;
        private static Material formulaBlackMaterial;
        private static Material finishDarkMaterial;
        private static Material carHeadlightMaterial;
        private static VoxelTrackDefinition activeTrack;

        public static VoxelTrackDefinition ActiveTrack => activeTrack != null
            ? activeTrack
            : VoxelTrackProgressState.CurrentTrack;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CreatePrototype();
        }

        private static void CreatePrototype()
        {
            if (Object.FindFirstObjectByType<VoxelRacerPrototypeMarker>() != null ||
                Object.FindFirstObjectByType<VoxelRacerShowcase>() != null ||
                Object.FindFirstObjectByType<VoxelMainMenuController>() != null ||
                Object.FindFirstObjectByType<VoxelCarSelectSceneController>() != null ||
                Object.FindFirstObjectByType<VoxelRepairUpgradeSceneController>() != null)
                return;

            var root = new GameObject("Voxel Racer Prototype");
            root.AddComponent<VoxelRacerPrototypeMarker>();
            BuildPrototype(root.transform);
        }

        public static void BuildPrototype(Transform root)
        {
            activeTrack = VoxelTrackProgressState.CurrentTrack;
            CreateMaterials(activeTrack);
            var existing = root.Find("Generated Environment");
            if (existing != null)
            {
                var environmentMarker = existing.GetComponent<VoxelRacerGeneratedEnvironment>();
                if (environmentMarker != null && environmentMarker.layoutVersion == 24)
                {
                    var existingCar = existing.Find("Player Voxel Car");
                    var existingRoad = existing.GetComponent<EndlessVoxelRoad>();
                    if (existingCar != null && existingRoad != null)
                    {
                        ConfigureCarTuning(existingCar);
                        ConfigureRoadTuning(existingRoad);
                        existingRoad.RebuildSegmentCache();
                        existingRoad.SetTarget(existingCar);
                        SetupGameplay(existing, existingCar, existingRoad);
                        SetupCamera(existingCar);
                        SetupSky(existingCar, activeTrack);
                    }
                    return;
                }

                // Replace the previous one-off road prototype with the drivable version.
                if (Application.isPlaying)
                    Object.Destroy(existing.gameObject);
                else
                    Object.DestroyImmediate(existing.gameObject);
            }

            var generated = new GameObject("Generated Environment").transform;
            generated.SetParent(root);
            generated.localPosition = Vector3.zero;
            generated.localRotation = Quaternion.identity;
            generated.localScale = Vector3.one;
            generated.gameObject.AddComponent<VoxelRacerGeneratedEnvironment>();

            var road = BuildRoad(generated);
            var car = BuildCar(generated);
            road.SetTarget(car);
            BuildStartLine(generated, car, road);
            BuildFinishLine(generated, road);
            SetupGameplay(generated, car, road);
            SetupCamera(car);
            SetupLighting();
            SetupSky(car, activeTrack);
        }

        private static EndlessVoxelRoad BuildRoad(Transform parent)
        {
            var road = parent.gameObject.AddComponent<EndlessVoxelRoad>();
            road.trackDefinition = activeTrack;
            ConfigureRoadTuning(road);
            road.BuildInitialRoad();
            return road;
        }

        private static void ConfigureRoadTuning(EndlessVoxelRoad road)
        {
            VoxelRoadTuning roadTuning = activeTrack != null && activeTrack.roadTuning != null
                ? activeTrack.roadTuning
                : VoxelRoadTuning.Load();
            road.trackDefinition = activeTrack;
            road.SetTuning(roadTuning);
        }

        private static Transform BuildCar(Transform parent)
        {
            var car = new GameObject("Player Voxel Car").transform;
            car.SetParent(parent);
            car.position = new Vector3(-1.5f, 0f, -5f);

            VoxelCarDefinition selectedCar = VoxelCarSelectionState.GetSelectedOrDefault();
            if (selectedCar != null && selectedCar.visualPrefab != null)
            {
                var visual = Object.Instantiate(selectedCar.visualPrefab, car);
                visual.name = selectedCar.displayName + " Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
            }
            else
            {
                BuildCarVisuals(car);
            }
            car.gameObject.AddComponent<VoxelCarController>();
            ConfigureCarTuning(car, selectedCar);
            car.GetComponent<VoxelCarController>().ResetIntegrityBaseline();
            VoxelCarRunState.Apply(car.GetComponent<VoxelCarController>(), selectedCar);
            return car;
        }

        public static void RebuildCarVisuals(Transform car)
        {
            for (int index = car.childCount - 1; index >= 0; index--)
            {
                var child = car.GetChild(index).gameObject;
                if (Application.isPlaying)
                    Object.Destroy(child);
                else
                    Object.DestroyImmediate(child);
            }

            BuildCarVisuals(car);
        }

        private static void BuildCarVisuals(Transform car)
        {
            CreateDetailedPlayerCarVisuals(car);
        }

        /// <summary>The preserved original player-car model before the detail pass.</summary>
        public static void CreateOriginalPlayerCarVisuals(Transform car)
        {
            EnsureMaterials();
            // Chassis: a deliberately chunky arrangement of individually named blocks.
            CreateCarVoxelVolume("Chassis", car, new Vector3(0f, 0.42f, 0f), new Vector3(2.25f, 0.55f, 3.9f), paintMaterial);
            CreateCarVoxelVolume("Front Bumper", car, new Vector3(0f, 0.45f, 2.05f), new Vector3(2.05f, 0.38f, 0.35f), paintMaterial);
            CreateCarVoxelVolume("Rear Bumper", car, new Vector3(0f, 0.42f, -2.0f), new Vector3(2.05f, 0.32f, 0.28f), paintMaterial);
            CreateCarVoxelVolume("Bonnet", car, new Vector3(0f, 0.84f, 1.05f), new Vector3(2.05f, 0.33f, 1.45f), paintMaterial);
            CreateCarVoxelVolume("Cabin", car, new Vector3(0f, 1.22f, -0.48f), new Vector3(1.78f, 0.85f, 1.55f), glassMaterial);
            CreateCarVoxelVolume("Roof", car, new Vector3(0f, 1.72f, -0.48f), new Vector3(1.86f, 0.18f, 1.62f), paintMaterial);

            CreateCarVoxelVolume("Left Headlight", car, new Vector3(-0.68f, 0.74f, 1.8f), new Vector3(0.46f, 0.2f, 0.07f), carHeadlightMaterial);
            CreateCarVoxelVolume("Right Headlight", car, new Vector3(0.68f, 0.74f, 1.8f), new Vector3(0.46f, 0.2f, 0.07f), carHeadlightMaterial);

            CreateWheel(car, -1.18f, 1.22f, true);
            CreateWheel(car, 1.18f, 1.22f, true);
            CreateWheel(car, -1.18f, -1.25f, false);
            CreateWheel(car, 1.18f, -1.25f, false);
            CreateDrivetrain(car);
        }

        /// <summary>Adds a rally-style detail pass while retaining independently damageable voxels.</summary>
        public static void AddDetailedPlayerCarVisuals(Transform car)
        {
            // Wider fender brows and low side skirts strengthen the silhouette around the wheels.
            CreateCarVoxelVolume("Front Left Fender", car, new Vector3(-1.13f, 0.72f, 1.22f), new Vector3(0.20f, 0.35f, 0.90f), paintMaterial);
            CreateCarVoxelVolume("Front Right Fender", car, new Vector3(1.13f, 0.72f, 1.22f), new Vector3(0.20f, 0.35f, 0.90f), paintMaterial);
            CreateCarVoxelVolume("Rear Left Fender", car, new Vector3(-1.13f, 0.72f, -1.25f), new Vector3(0.20f, 0.35f, 0.90f), paintMaterial);
            CreateCarVoxelVolume("Rear Right Fender", car, new Vector3(1.13f, 0.72f, -1.25f), new Vector3(0.20f, 0.35f, 0.90f), paintMaterial);
            CreateCarVoxelVolume("Left Side Skirt", car, new Vector3(-1.16f, 0.25f, -0.02f), new Vector3(0.18f, 0.16f, 2.90f), paintMaterial);
            CreateCarVoxelVolume("Right Side Skirt", car, new Vector3(1.16f, 0.25f, -0.02f), new Vector3(0.18f, 0.16f, 2.90f), paintMaterial);

            // Body-coloured pillars divide the original glass cabin into readable windows.
            CreateCarVoxelVolume("Left A Pillar", car, new Vector3(-0.82f, 1.34f, 0.28f), new Vector3(0.15f, 0.78f, 0.15f), paintMaterial);
            CreateCarVoxelVolume("Right A Pillar", car, new Vector3(0.82f, 1.34f, 0.28f), new Vector3(0.15f, 0.78f, 0.15f), paintMaterial);
            CreateCarVoxelVolume("Left B Pillar", car, new Vector3(-0.85f, 1.34f, -0.50f), new Vector3(0.15f, 0.78f, 0.15f), paintMaterial);
            CreateCarVoxelVolume("Right B Pillar", car, new Vector3(0.85f, 1.34f, -0.50f), new Vector3(0.15f, 0.78f, 0.15f), paintMaterial);
            CreateCarVoxelVolume("Left C Pillar", car, new Vector3(-0.82f, 1.34f, -1.22f), new Vector3(0.15f, 0.78f, 0.15f), paintMaterial);
            CreateCarVoxelVolume("Right C Pillar", car, new Vector3(0.82f, 1.34f, -1.22f), new Vector3(0.15f, 0.78f, 0.15f), paintMaterial);

            CreateBlock("Left Mirror", car, new Vector3(-1.08f, 1.28f, 0.22f), new Vector3(0.30f, 0.18f, 0.28f), paintMaterial);
            CreateBlock("Right Mirror", car, new Vector3(1.08f, 1.28f, 0.22f), new Vector3(0.30f, 0.18f, 0.28f), paintMaterial);
            CreateBlock("Left Door Handle", car, new Vector3(-1.15f, 1.00f, -0.62f), new Vector3(0.07f, 0.08f, 0.30f), carMetalDetailMaterial);
            CreateBlock("Right Door Handle", car, new Vector3(1.15f, 1.00f, -0.62f), new Vector3(0.07f, 0.08f, 0.30f), carMetalDetailMaterial);

            // Front and bonnet detailing gives the car a clear face at gameplay distance.
            CreateCarVoxelVolume("Front Grille", car, new Vector3(0f, 0.54f, 2.24f), new Vector3(1.25f, 0.30f, 0.08f), tyreMaterial);
            CreateCarVoxelVolume("Left Front Intake", car, new Vector3(-0.78f, 0.48f, 2.24f), new Vector3(0.35f, 0.20f, 0.08f), tyreMaterial);
            CreateCarVoxelVolume("Right Front Intake", car, new Vector3(0.78f, 0.48f, 2.24f), new Vector3(0.35f, 0.20f, 0.08f), tyreMaterial);
            CreateCarVoxelVolume("Left Bonnet Vent", car, new Vector3(-0.48f, 1.02f, 1.10f), new Vector3(0.28f, 0.06f, 0.55f), tyreMaterial);
            CreateCarVoxelVolume("Right Bonnet Vent", car, new Vector3(0.48f, 1.02f, 1.10f), new Vector3(0.28f, 0.06f, 0.55f), tyreMaterial);
            CreateCarVoxelVolume("Bonnet Race Stripe", car, new Vector3(0f, 1.035f, 1.05f), new Vector3(0.28f, 0.04f, 1.35f), carAccentMaterial);
            CreateCarVoxelVolume("Roof Race Stripe", car, new Vector3(0f, 1.825f, -0.48f), new Vector3(0.28f, 0.04f, 1.50f), carAccentMaterial);

            // Rear lamps, exhaust hardware, diffuser and spoiler identify the back immediately.
            CreateCarVoxelVolume("Left Tail Light", car, new Vector3(-0.70f, 0.65f, -2.16f), new Vector3(0.48f, 0.24f, 0.08f), carTailLightMaterial);
            CreateCarVoxelVolume("Right Tail Light", car, new Vector3(0.70f, 0.65f, -2.16f), new Vector3(0.48f, 0.24f, 0.08f), carTailLightMaterial);
            CreateCarVoxelVolume("Rear Diffuser", car, new Vector3(0f, 0.26f, -2.17f), new Vector3(1.40f, 0.18f, 0.14f), tyreMaterial);
            CreateBlock("Left Exhaust", car, new Vector3(-0.66f, 0.20f, -2.28f), new Vector3(0.22f, 0.18f, 0.22f), carMetalDetailMaterial);
            CreateBlock("Right Exhaust", car, new Vector3(0.66f, 0.20f, -2.28f), new Vector3(0.22f, 0.18f, 0.22f), carMetalDetailMaterial);
            CreateBlock("Left Spoiler Support", car, new Vector3(-0.55f, 0.94f, -1.82f), new Vector3(0.16f, 0.38f, 0.16f), tyreMaterial);
            CreateBlock("Right Spoiler Support", car, new Vector3(0.55f, 0.94f, -1.82f), new Vector3(0.16f, 0.38f, 0.16f), tyreMaterial);
            CreateCarVoxelVolume("Rear Spoiler", car, new Vector3(0f, 1.16f, -1.82f), new Vector3(1.90f, 0.15f, 0.45f), paintMaterial);
        }

        public static void CreateDetailedPlayerCarVisuals(Transform car)
        {
            CreateOriginalPlayerCarVisuals(car);
            AddDetailedPlayerCarVisuals(car);
        }

        /// <summary>Builds a low-roof, rounded long-tail coupe derived from the Original car.</summary>
        public static void CreateLongtailPlayerCarVisuals(Transform car)
        {
            EnsureMaterials();

            // Three progressively narrower layers soften the rectangular silhouette.
            CreateCarVoxelVolume("Longtail Lower Body", car, new Vector3(0f, 0.31f, -0.32f),
                new Vector3(2.08f, 0.24f, 4.42f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Main Body", car, new Vector3(0f, 0.52f, -0.34f),
                new Vector3(2.24f, 0.38f, 4.08f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Rounded Shoulder", car, new Vector3(0f, 0.76f, -0.42f),
                new Vector3(1.92f, 0.22f, 3.72f), longtailPaintMaterial);

            // Short stepped nose and bonnet, substantially reduced from the Original coupe.
            CreateCarVoxelVolume("Longtail Short Bonnet", car, new Vector3(0f, 0.91f, 1.18f),
                new Vector3(1.78f, 0.22f, 1.02f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Bonnet Crown", car, new Vector3(0f, 1.04f, 1.15f),
                new Vector3(1.42f, 0.10f, 0.82f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Rounded Nose", car, new Vector3(0f, 0.65f, 1.82f),
                new Vector3(1.90f, 0.42f, 0.30f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Lower Nose", car, new Vector3(0f, 0.44f, 1.92f),
                new Vector3(1.62f, 0.20f, 0.22f), longtailPaintMaterial);

            // A compressed, much lower cabin with stepped glass and roof widths.
            CreateCarVoxelVolume("Longtail Low Cabin", car, new Vector3(0f, 1.08f, -0.30f),
                new Vector3(1.64f, 0.52f, 1.22f), glassMaterial);
            CreateCarVoxelVolume("Longtail Cabin Lower Rim", car, new Vector3(0f, 0.86f, -0.32f),
                new Vector3(1.82f, 0.18f, 1.38f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Low Roof", car, new Vector3(0f, 1.39f, -0.34f),
                new Vector3(1.46f, 0.14f, 1.02f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Roof Crown", car, new Vector3(0f, 1.48f, -0.36f),
                new Vector3(1.12f, 0.08f, 0.76f), longtailPaintMaterial);

            // Pillars divide the glass while accentuating the lower roofline.
            CreateCarVoxelVolume("Longtail Left A Pillar", car, new Vector3(-0.72f, 1.13f, 0.18f),
                new Vector3(0.13f, 0.48f, 0.14f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Right A Pillar", car, new Vector3(0.72f, 1.13f, 0.18f),
                new Vector3(0.13f, 0.48f, 0.14f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Left B Pillar", car, new Vector3(-0.73f, 1.13f, -0.85f),
                new Vector3(0.14f, 0.48f, 0.14f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Right B Pillar", car, new Vector3(0.73f, 1.13f, -0.85f),
                new Vector3(0.14f, 0.48f, 0.14f), longtailPaintMaterial);

            // Long rear deck and layered tail shift the visual weight behind the cabin.
            CreateCarVoxelVolume("Longtail Rear Deck", car, new Vector3(0f, 0.91f, -1.30f),
                new Vector3(1.82f, 0.26f, 1.68f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Rear Deck Crown", car, new Vector3(0f, 1.07f, -1.30f),
                new Vector3(1.44f, 0.10f, 1.38f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Tapered Tail", car, new Vector3(0f, 0.69f, -2.32f),
                new Vector3(1.88f, 0.42f, 0.52f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Lower Tail", car, new Vector3(0f, 0.42f, -2.48f),
                new Vector3(1.58f, 0.22f, 0.28f), longtailPaintMaterial);

            // Fuller stepped wheel shoulders add curvature without covering the tyres.
            CreateCarVoxelVolume("Longtail Front Left Shoulder", car, new Vector3(-1.02f, 0.74f, 0.96f),
                new Vector3(0.24f, 0.34f, 0.86f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Front Right Shoulder", car, new Vector3(1.02f, 0.74f, 0.96f),
                new Vector3(0.24f, 0.34f, 0.86f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Rear Left Shoulder", car, new Vector3(-1.03f, 0.75f, -1.52f),
                new Vector3(0.26f, 0.36f, 1.02f), longtailPaintMaterial);
            CreateCarVoxelVolume("Longtail Rear Right Shoulder", car, new Vector3(1.03f, 0.75f, -1.52f),
                new Vector3(0.26f, 0.36f, 1.02f), longtailPaintMaterial);

            // Front detailing.
            CreateCarVoxelVolume("Longtail Front Bumper", car, new Vector3(0f, 0.39f, 2.08f),
                new Vector3(1.96f, 0.18f, 0.16f), carMetalDetailMaterial);
            CreateCarVoxelVolume("Longtail Front Grille", car, new Vector3(0f, 0.58f, 2.075f),
                new Vector3(0.72f, 0.22f, 0.08f), tyreMaterial);
            CreateCarVoxelVolume("Longtail Left Headlight", car, new Vector3(-0.61f, 0.75f, 2.00f),
                new Vector3(0.42f, 0.22f, 0.10f), carHeadlightMaterial);
            CreateCarVoxelVolume("Longtail Right Headlight", car, new Vector3(0.61f, 0.75f, 2.00f),
                new Vector3(0.42f, 0.22f, 0.10f), carHeadlightMaterial);

            // Rear detailing and small chrome exhausts.
            CreateCarVoxelVolume("Longtail Rear Bumper", car, new Vector3(0f, 0.37f, -2.70f),
                new Vector3(1.98f, 0.18f, 0.16f), carMetalDetailMaterial);
            CreateCarVoxelVolume("Longtail Left Tail Light", car, new Vector3(-0.61f, 0.72f, -2.61f),
                new Vector3(0.44f, 0.24f, 0.10f), carTailLightMaterial);
            CreateCarVoxelVolume("Longtail Right Tail Light", car, new Vector3(0.61f, 0.72f, -2.61f),
                new Vector3(0.44f, 0.24f, 0.10f), carTailLightMaterial);
            CreateBlock("Longtail Left Exhaust", car, new Vector3(-0.48f, 0.22f, -2.77f),
                new Vector3(0.20f, 0.16f, 0.18f), carMetalDetailMaterial);
            CreateBlock("Longtail Right Exhaust", car, new Vector3(0.48f, 0.22f, -2.77f),
                new Vector3(0.20f, 0.16f, 0.18f), carMetalDetailMaterial);

            // Side details sit on both faces so they remain readable while rotating.
            CreateBlock("Longtail Left Door Handle", car, new Vector3(-1.13f, 0.98f, -0.38f),
                new Vector3(0.07f, 0.08f, 0.30f), carMetalDetailMaterial);
            CreateBlock("Longtail Right Door Handle", car, new Vector3(1.13f, 0.98f, -0.38f),
                new Vector3(0.07f, 0.08f, 0.30f), carMetalDetailMaterial);
            CreateBlock("Longtail Left Mirror", car, new Vector3(-1.04f, 1.13f, 0.13f),
                new Vector3(0.28f, 0.16f, 0.24f), longtailPaintMaterial);
            CreateBlock("Longtail Right Mirror", car, new Vector3(1.04f, 1.13f, 0.13f),
                new Vector3(0.28f, 0.16f, 0.24f), longtailPaintMaterial);

            CreateWheel(car, -1.18f, 0.96f, true);
            CreateWheel(car, 1.18f, 0.96f, true);
            CreateWheel(car, -1.18f, -1.58f, false);
            CreateWheel(car, 1.18f, -1.58f, false);
            CreateLongtailDrivetrain(car);
        }

        /// <summary>Builds an original open-wheel Formula-style player car from damageable voxels.</summary>
        public static void CreateFormulaPlayerCarVisuals(Transform car)
        {
            EnsureMaterials();

            // Low central tub and floor establish the long, narrow single-seater silhouette.
            CreateCarVoxelVolume("Formula Floor", car, new Vector3(0f, 0.24f, -0.05f),
                new Vector3(1.18f, 0.18f, 4.35f), formulaBlackMaterial);
            CreateCarVoxelVolume("Formula Central Tub", car, new Vector3(0f, 0.52f, -0.15f),
                new Vector3(0.94f, 0.52f, 3.15f), formulaOrangeMaterial);

            // Stepped nose sections taper toward the front like a voxel wedge.
            CreateCarVoxelVolume("Formula Nose Base", car, new Vector3(0f, 0.58f, 1.45f),
                new Vector3(0.82f, 0.42f, 1.05f), formulaOrangeMaterial);
            CreateCarVoxelVolume("Formula Nose Middle", car, new Vector3(0f, 0.52f, 2.12f),
                new Vector3(0.62f, 0.34f, 0.70f), formulaOrangeMaterial);
            CreateCarVoxelVolume("Formula Nose Tip", car, new Vector3(0f, 0.45f, 2.62f),
                new Vector3(0.42f, 0.24f, 0.42f), formulaWhiteMaterial);
            CreateCarVoxelVolume("Formula Nose Stripe", car, new Vector3(0f, 0.805f, 1.55f),
                new Vector3(0.32f, 0.055f, 1.25f), formulaWhiteMaterial);

            // Separate sidepods leave clear air around the exposed wheels.
            CreateCarVoxelVolume("Formula Left Sidepod", car, new Vector3(-0.73f, 0.55f, -0.28f),
                new Vector3(0.52f, 0.52f, 1.55f), formulaOrangeMaterial);
            CreateCarVoxelVolume("Formula Right Sidepod", car, new Vector3(0.73f, 0.55f, -0.28f),
                new Vector3(0.52f, 0.52f, 1.55f), formulaOrangeMaterial);
            CreateCarVoxelVolume("Formula Left Sidepod Top", car, new Vector3(-0.69f, 0.86f, -0.24f),
                new Vector3(0.42f, 0.14f, 1.20f), formulaWhiteMaterial);
            CreateCarVoxelVolume("Formula Right Sidepod Top", car, new Vector3(0.69f, 0.86f, -0.24f),
                new Vector3(0.42f, 0.14f, 1.20f), formulaWhiteMaterial);
            CreateCarVoxelVolume("Formula Left Intake", car, new Vector3(-0.74f, 0.66f, 0.52f),
                new Vector3(0.36f, 0.28f, 0.10f), formulaBlackMaterial);
            CreateCarVoxelVolume("Formula Right Intake", car, new Vector3(0.74f, 0.66f, 0.52f),
                new Vector3(0.36f, 0.28f, 0.10f), formulaBlackMaterial);

            // Cockpit opening, seat and roll hoop make the open cabin readable from above.
            CreateCarVoxelVolume("Formula Cockpit Left Rim", car, new Vector3(-0.40f, 0.92f, 0.12f),
                new Vector3(0.20f, 0.20f, 1.08f), formulaWhiteMaterial);
            CreateCarVoxelVolume("Formula Cockpit Right Rim", car, new Vector3(0.40f, 0.92f, 0.12f),
                new Vector3(0.20f, 0.20f, 1.08f), formulaWhiteMaterial);
            CreateCarVoxelVolume("Formula Cockpit Front Rim", car, new Vector3(0f, 0.91f, 0.67f),
                new Vector3(0.62f, 0.18f, 0.18f), formulaWhiteMaterial);
            CreateCarVoxelVolume("Formula Seat", car, new Vector3(0f, 0.83f, -0.02f),
                new Vector3(0.48f, 0.30f, 0.72f), formulaBlackMaterial);
            CreateBlock("Formula Roll Hoop Left", car, new Vector3(-0.27f, 1.17f, -0.49f),
                new Vector3(0.14f, 0.58f, 0.16f), formulaBlackMaterial);
            CreateBlock("Formula Roll Hoop Right", car, new Vector3(0.27f, 1.17f, -0.49f),
                new Vector3(0.14f, 0.58f, 0.16f), formulaBlackMaterial);
            CreateBlock("Formula Roll Hoop Top", car, new Vector3(0f, 1.43f, -0.49f),
                new Vector3(0.66f, 0.14f, 0.16f), formulaBlackMaterial);
            CreateBlock("Formula Driver Helmet", car, new Vector3(0f, 1.13f, -0.08f),
                new Vector3(0.45f, 0.42f, 0.42f), formulaWhiteMaterial);
            CreateBlock("Formula Helmet Visor", car, new Vector3(0f, 1.16f, 0.145f),
                new Vector3(0.34f, 0.14f, 0.055f), formulaBlackMaterial);

            // Engine cover steps upward behind the cockpit and narrows toward the rear.
            CreateCarVoxelVolume("Formula Engine Cover", car, new Vector3(0f, 0.78f, -1.18f),
                new Vector3(0.92f, 0.66f, 1.38f), formulaOrangeMaterial);
            CreateCarVoxelVolume("Formula Engine Spine", car, new Vector3(0f, 1.13f, -1.13f),
                new Vector3(0.38f, 0.28f, 1.28f), formulaWhiteMaterial);
            CreateCarVoxelVolume("Formula Rear Taper", car, new Vector3(0f, 0.54f, -2.02f),
                new Vector3(0.70f, 0.44f, 0.62f), formulaOrangeMaterial);

            // Broad front and rear aero surfaces complete the characteristic profile.
            CreateCarVoxelVolume("Formula Front Wing", car, new Vector3(0f, 0.26f, 2.92f),
                new Vector3(3.15f, 0.14f, 0.48f), formulaBlackMaterial);
            CreateCarVoxelVolume("Formula Front Wing Orange Centre", car, new Vector3(0f, 0.35f, 2.92f),
                new Vector3(1.15f, 0.08f, 0.38f), formulaOrangeMaterial);
            CreateBlock("Formula Front Left Endplate", car, new Vector3(-1.55f, 0.40f, 2.92f),
                new Vector3(0.12f, 0.42f, 0.52f), formulaWhiteMaterial);
            CreateBlock("Formula Front Right Endplate", car, new Vector3(1.55f, 0.40f, 2.92f),
                new Vector3(0.12f, 0.42f, 0.52f), formulaWhiteMaterial);
            CreateBlock("Formula Rear Wing Left Support", car, new Vector3(-0.46f, 1.16f, -2.17f),
                new Vector3(0.15f, 0.92f, 0.15f), formulaBlackMaterial);
            CreateBlock("Formula Rear Wing Right Support", car, new Vector3(0.46f, 1.16f, -2.17f),
                new Vector3(0.15f, 0.92f, 0.15f), formulaBlackMaterial);
            CreateCarVoxelVolume("Formula Rear Wing", car, new Vector3(0f, 1.58f, -2.17f),
                new Vector3(2.82f, 0.20f, 0.58f), formulaOrangeMaterial);
            CreateCarVoxelVolume("Formula Rear Wing White Stripe", car, new Vector3(0f, 1.70f, -2.17f),
                new Vector3(2.40f, 0.055f, 0.44f), formulaWhiteMaterial);
            CreateBlock("Formula Rear Left Endplate", car, new Vector3(-1.42f, 1.47f, -2.17f),
                new Vector3(0.12f, 0.72f, 0.62f), formulaBlackMaterial);
            CreateBlock("Formula Rear Right Endplate", car, new Vector3(1.42f, 1.47f, -2.17f),
                new Vector3(0.12f, 0.72f, 0.62f), formulaBlackMaterial);

            CreateFormulaWheel(car, -1.32f, 1.55f, true, false);
            CreateFormulaWheel(car, 1.32f, 1.55f, true, false);
            CreateFormulaWheel(car, -1.34f, -1.55f, false, true);
            CreateFormulaWheel(car, 1.34f, -1.55f, false, true);
            CreateFormulaDrivetrain(car);
        }

        private static void CreateFormulaWheel(Transform parent, float x, float z, bool isFrontWheel, bool isRearWheel)
        {
            var steeringPivot = new GameObject(isFrontWheel ? "Front Wheel Steering" : "Rear Wheel Mount").transform;
            steeringPivot.SetParent(parent);
            steeringPivot.localPosition = new Vector3(x, isRearWheel ? 0.48f : 0.43f, z);

            // Keep this exact name so VoxelCarController applies wheel spin.
            var wheel = new GameObject("Voxel Wheel").transform;
            wheel.SetParent(steeringPivot);
            wheel.localPosition = Vector3.zero;
            wheel.localRotation = Quaternion.identity;
            wheel.localScale = Vector3.one;
            float width = isRearWheel ? 0.62f : 0.46f;
            float height = isRearWheel ? 0.82f : 0.70f;
            float depth = isRearWheel ? 0.84f : 0.72f;
            CreateBlock("Formula Wheel Centre", wheel, Vector3.zero,
                new Vector3(width, height * 0.70f, depth), formulaBlackMaterial);
            CreateBlock("Formula Wheel Top", wheel, new Vector3(0f, height * 0.42f, 0f),
                new Vector3(width, height * 0.22f, depth * 0.70f), formulaBlackMaterial);
            CreateBlock("Formula Wheel Bottom", wheel, new Vector3(0f, -height * 0.42f, 0f),
                new Vector3(width, height * 0.22f, depth * 0.70f), formulaBlackMaterial);
            CreateBlock("Formula Wheel Front", wheel, new Vector3(0f, 0f, depth * 0.43f),
                new Vector3(width, height * 0.56f, depth * 0.18f), formulaBlackMaterial);
            CreateBlock("Formula Wheel Rear", wheel, new Vector3(0f, 0f, -depth * 0.43f),
                new Vector3(width, height * 0.56f, depth * 0.18f), formulaBlackMaterial);
            CreateBlock("Formula Wheel Hub", wheel, new Vector3(x > 0f ? width * 0.53f : -width * 0.53f, 0f, 0f),
                new Vector3(0.07f, height * 0.42f, height * 0.42f), hubMaterial);
            wheel.gameObject.AddComponent<VoxelWheelIntegrity>();
        }

        private static void CreateFormulaDrivetrain(Transform car)
        {
            var frontAxle = CreateBlock("Formula Front Axle", car, new Vector3(0f, 0.43f, 1.55f),
                new Vector3(2.82f, 0.12f, 0.12f), formulaBlackMaterial);
            frontAxle.AddComponent<VoxelIndestructiblePart>();
            var rearAxle = CreateBlock("Formula Rear Axle", car, new Vector3(0f, 0.48f, -1.55f),
                new Vector3(2.92f, 0.14f, 0.14f), formulaBlackMaterial);
            rearAxle.AddComponent<VoxelIndestructiblePart>();
            var driveline = CreateBlock("Formula Driveline", car, new Vector3(0f, 0.34f, 0f),
                new Vector3(0.14f, 0.14f, 3.22f), formulaBlackMaterial);
            driveline.AddComponent<VoxelIndestructiblePart>();
        }

        private static void CreateDrivetrain(Transform car)
        {
            var frontAxle = CreateBlock("Front Axle", car, new Vector3(0f, 0.39f, 1.22f), new Vector3(2.55f, 0.16f, 0.16f), tyreMaterial);
            frontAxle.AddComponent<VoxelIndestructiblePart>();
            var rearAxle = CreateBlock("Rear Axle", car, new Vector3(0f, 0.39f, -1.25f), new Vector3(2.55f, 0.16f, 0.16f), tyreMaterial);
            rearAxle.AddComponent<VoxelIndestructiblePart>();
            var driveline = CreateBlock("Driveline", car, new Vector3(0f, 0.39f, -0.015f), new Vector3(0.16f, 0.16f, 2.62f), tyreMaterial);
            driveline.AddComponent<VoxelIndestructiblePart>();
        }

        private static void CreateLongtailDrivetrain(Transform car)
        {
            var frontAxle = CreateBlock("Longtail Front Axle", car, new Vector3(0f, 0.39f, 0.96f),
                new Vector3(2.55f, 0.16f, 0.16f), tyreMaterial);
            frontAxle.AddComponent<VoxelIndestructiblePart>();
            var rearAxle = CreateBlock("Longtail Rear Axle", car, new Vector3(0f, 0.39f, -1.58f),
                new Vector3(2.55f, 0.16f, 0.16f), tyreMaterial);
            rearAxle.AddComponent<VoxelIndestructiblePart>();
            var driveline = CreateBlock("Longtail Driveline", car, new Vector3(0f, 0.39f, -0.31f),
                new Vector3(0.16f, 0.16f, 2.70f), tyreMaterial);
            driveline.AddComponent<VoxelIndestructiblePart>();
        }

        private static void ConfigureCarTuning(Transform car, VoxelCarDefinition definition = null)
        {
            definition ??= VoxelCarSelectionState.GetSelectedOrDefault();
            VoxelCarTuning tuning = definition != null && definition.tuning != null
                ? definition.tuning
                : VoxelCarTuning.Load();
            car.GetComponent<VoxelCarController>().SetTuning(tuning);
        }

        private static void CreateCarVoxelVolume(string partName, Transform parent, Vector3 centre, Vector3 size, Material material)
        {
            // 0.25 produces 896 body voxels (about 920 including the wheels).
            const float targetVoxelSize = 0.25f;
            int xCount = Mathf.CeilToInt(size.x / targetVoxelSize);
            int yCount = Mathf.CeilToInt(size.y / targetVoxelSize);
            int zCount = Mathf.CeilToInt(size.z / targetVoxelSize);
            Vector3 voxelSize = new(size.x / xCount, size.y / yCount, size.z / zCount);
            var part = new GameObject(partName + " Voxels").transform;
            part.SetParent(parent);
            part.localPosition = centre;

            for (int x = 0; x < xCount; x++)
            for (int y = 0; y < yCount; y++)
            for (int z = 0; z < zCount; z++)
            {
                Vector3 localPosition = new(
                    (x - (xCount - 1) * 0.5f) * voxelSize.x,
                    (y - (yCount - 1) * 0.5f) * voxelSize.y,
                    (z - (zCount - 1) * 0.5f) * voxelSize.z);
                CreateBlock(partName + " Voxel", part, localPosition, voxelSize, material);
            }
        }

        private static void CreateWheel(Transform parent, float x, float z, bool isFrontWheel)
        {
            // Five blocks make a low-poly octagonal tyre profile when viewed from the side.
            // Each block stays independently addressable for future damage effects.
            var steeringPivot = new GameObject(isFrontWheel ? "Front Wheel Steering" : "Rear Wheel Mount").transform;
            steeringPivot.SetParent(parent);
            steeringPivot.localPosition = new Vector3(x, 0.39f, z);
            var wheel = new GameObject("Voxel Wheel").transform;
            wheel.SetParent(steeringPivot);
            wheel.localPosition = Vector3.zero;
            CreateBlock("Wheel Centre", wheel, Vector3.zero, new Vector3(0.46f, 0.52f, 0.82f), tyreMaterial);
            CreateBlock("Wheel Top", wheel, new Vector3(0f, 0.31f, 0f), new Vector3(0.46f, 0.22f, 0.58f), tyreMaterial);
            CreateBlock("Wheel Bottom", wheel, new Vector3(0f, -0.31f, 0f), new Vector3(0.46f, 0.22f, 0.58f), tyreMaterial);
            CreateBlock("Wheel Front", wheel, new Vector3(0f, 0f, 0.36f), new Vector3(0.46f, 0.42f, 0.20f), tyreMaterial);
            CreateBlock("Wheel Rear", wheel, new Vector3(0f, 0f, -0.36f), new Vector3(0.46f, 0.42f, 0.20f), tyreMaterial);

            float outerSide = x > 0f ? 0.25f : -0.25f;
            CreateBlock("Wheel Hub", wheel, new Vector3(outerSide, 0f, 0f), new Vector3(0.06f, 0.30f, 0.30f), hubMaterial);
            wheel.gameObject.AddComponent<VoxelWheelIntegrity>();
        }

        internal static GameObject CreateBlock(string blockName, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(parent);
            block.transform.localPosition = localPosition;
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            return block;
        }

        private static void SetupCamera(Transform target)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }

            camera.transform.position = new Vector3(-10f, 11f, -16f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(-1.5f, 0.3f, 10f) - camera.transform.position);
            camera.fieldOfView = 58f;
            camera.backgroundColor = new Color(0.48f, 0.75f, 0.92f);

            var follow = camera.GetComponent<VoxelCameraFollow>();
            if (follow == null)
                follow = camera.gameObject.AddComponent<VoxelCameraFollow>();
            follow.target = target;
        }

        private static void SetupGameplay(Transform environment, Transform car, EndlessVoxelRoad road)
        {
            car.GetComponent<VoxelCarController>().EnsureIntegrityBaseline();
            var spawner = environment.GetComponent<VoxelObstacleSpawner>();
            if (spawner == null)
                spawner = environment.gameObject.AddComponent<VoxelObstacleSpawner>();

            spawner.SetTarget(car.GetComponent<VoxelCarController>());
            spawner.obstacleCarTuning = activeTrack != null && activeTrack.obstacleCarTuning != null
                ? activeTrack.obstacleCarTuning
                : VoxelObstacleCarTuning.Load();
            car.GetComponent<VoxelCarController>().SetLaneLayout(road.laneCount, road.roadWidth / road.laneCount);
            spawner.laneCount = road.laneCount;
            spawner.laneWidth = road.roadWidth / road.laneCount;

            var runFinish = environment.GetComponentInChildren<VoxelRunFinish>();
            if (runFinish != null)
            {
                runFinish.Configure(car.GetComponent<VoxelCarController>(), road.tuning, road,
                    car.GetComponent<VoxelCarController>().TrackDistance);
                spawner.SetRunFinish(runFinish);

                var postRaceContinue = environment.GetComponent<VoxelPostRaceContinue>();
                if (postRaceContinue == null)
                    postRaceContinue = environment.gameObject.AddComponent<VoxelPostRaceContinue>();
                postRaceContinue.Configure(runFinish);
            }

            var repairButton = environment.GetComponent<VoxelRepairButton>();
            if (repairButton == null)
                repairButton = environment.gameObject.AddComponent<VoxelRepairButton>();
            repairButton.target = car.GetComponent<VoxelCarController>();

            var integrityDisplay = environment.GetComponent<VoxelCarIntegrityDisplay>();
            if (integrityDisplay == null)
                integrityDisplay = environment.gameObject.AddComponent<VoxelCarIntegrityDisplay>();
            integrityDisplay.target = car.GetComponent<VoxelCarController>();

            var countdown = environment.GetComponent<VoxelStartCountdown>();
            if (countdown == null)
                countdown = environment.gameObject.AddComponent<VoxelStartCountdown>();
            countdown.Prepare(car.GetComponent<VoxelCarController>());
            spawner.SetStartCountdown(countdown);

            var selectionScreen = environment.GetComponent<VoxelCarSelectionScreen>();
            if (selectionScreen != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(selectionScreen);
                else
                    Object.DestroyImmediate(selectionScreen);
            }
            if (Application.isPlaying)
                countdown.BeginCountdown();

            var speedometer = environment.GetComponent<VoxelSpeedometer>();
            if (speedometer == null)
                speedometer = environment.gameObject.AddComponent<VoxelSpeedometer>();
            speedometer.target = car.GetComponent<VoxelCarController>();
        }

        private static void BuildStartLine(Transform parent, Transform car, EndlessVoxelRoad road)
        {
            var controller = car.GetComponent<VoxelCarController>();
            float startLineDistance = controller.TrackDistance + 2f;
            VoxelTrackPose pose = road.Evaluate(startLineDistance);
            var startLine = CreateBlock("White Start Line", parent, pose.position + Vector3.up * 0.025f,
                new Vector3(road.roadWidth - 0.35f, 0.04f, 0.65f), startLineMaterial);
            startLine.transform.rotation = pose.rotation;
            var cleanup = startLine.AddComponent<VoxelStartLineCleanup>();
            cleanup.target = controller;
            cleanup.trackDistance = startLineDistance;
        }

        private static void BuildFinishLine(Transform parent, EndlessVoxelRoad road)
        {
            var finishRoot = new GameObject("Chequered Finish Line").transform;
            finishRoot.SetParent(parent);
            finishRoot.localPosition = Vector3.zero;
            finishRoot.gameObject.AddComponent<VoxelRunFinish>();

            int columns = Mathf.Max(8, road.laneCount * 4);
            const int rows = 4;
            float squareWidth = road.roadWidth / columns;
            const float squareDepth = 0.5f;
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                float x = -road.roadWidth * 0.5f + squareWidth * (column + 0.5f);
                float z = (row - (rows - 1) * 0.5f) * squareDepth;
                Material material = (row + column) % 2 == 0 ? startLineMaterial : finishDarkMaterial;
                CreateBlock("Finish Checker", finishRoot, new Vector3(x, 0.025f, z),
                    new Vector3(squareWidth - 0.025f, 0.04f, squareDepth - 0.025f), material);
            }
        }

        internal static void SetupLighting()
        {
            var light = Object.FindFirstObjectByType<Light>();
            if (light == null)
            {
                light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            light.intensity = 1.3f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.62f, 0.72f);
        }

        internal static void SetupSky(Transform target, VoxelTrackDefinition track = null)
        {
            track ??= ActiveTrack;
            if (track != null && track.skyboxMaterial != null)
            {
                RenderSettings.skybox = track.skyboxMaterial;
            }
            else
            {
                var skybox = new Material(Shader.Find("Skybox/Procedural"));
                skybox.SetColor("_SkyTint", track != null ? track.skyTint : new Color(0.36f, 0.18f, 0.56f));
                skybox.SetColor("_GroundColor", track != null ? track.skyGroundColour : new Color(0.46f, 0.12f, 0.08f));
                skybox.SetFloat("_AtmosphereThickness", track != null ? track.atmosphereThickness : 1.25f);
                skybox.SetFloat("_SunDisk", 0f);
                skybox.SetFloat("_SunSize", 0.06f);
                skybox.SetFloat("_SunSizeConvergence", 3f);
                RenderSettings.skybox = skybox;
            }

            RenderSettings.fog = track == null || track.fogEnabled;
            RenderSettings.fogColor = track != null ? track.fogColour : new Color(0.58f, 0.20f, 0.18f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = track != null ? track.fogStartDistance : 90f;
            RenderSettings.fogEndDistance = track != null ? track.fogEndDistance : 320f;

            var horizonSun = Object.FindFirstObjectByType<VoxelHorizonSun>();
            bool sunEnabled = track == null || track.horizonSunEnabled;
            if (!sunEnabled)
            {
                if (horizonSun != null)
                    horizonSun.gameObject.SetActive(false);
            }
            else
            {
                if (horizonSun == null)
                    horizonSun = new GameObject("Horizon Sun").AddComponent<VoxelHorizonSun>();
                horizonSun.gameObject.SetActive(true);
                horizonSun.target = target;
                horizonSun.distanceAhead = track != null ? track.sunDistanceAhead : 220f;
                horizonSun.horizontalOffset = track != null ? track.sunHorizontalOffset : 12f;
                horizonSun.horizonHeight = track != null ? track.sunHorizonHeight : -80f;
                horizonSun.transform.localScale = Vector3.one * 10f;
                horizonSun.Build();
            }

            var horizonMountains = Object.FindFirstObjectByType<VoxelHorizonMountains>(FindObjectsInactive.Include);
            bool mountainsEnabled = track == null || track.horizonMountainsEnabled;
            if (!mountainsEnabled)
            {
                if (horizonMountains != null)
                    horizonMountains.gameObject.SetActive(false);
            }
            else
            {
                if (horizonMountains == null)
                    horizonMountains = new GameObject("Wrapped Horizon Mountains").AddComponent<VoxelHorizonMountains>();
                horizonMountains.gameObject.SetActive(true);
                horizonMountains.Configure(target, track);
            }
        }

        private static void CreateMaterials(VoxelTrackDefinition track = null)
        {
            roadMaterial = ResolveTrackMaterial(track != null ? track.roadMaterial : null,
                "Road", track != null ? track.roadColour : new Color(0.10f, 0.12f, 0.16f));
            paintMaterial = LoadCarMaterial("CarMaterials/CarPaint", "Car Paint", new Color(0.08f, 0.55f, 0.95f));
            glassMaterial = LoadCarMaterial("CarMaterials/CarGlass", "Windows", new Color(0.04f, 0.15f, 0.25f));
            tyreMaterial = LoadCarMaterial("CarMaterials/CarTyres", "Tyres", new Color(0.025f, 0.03f, 0.045f));
            hubMaterial = LoadCarMaterial("CarMaterials/CarHubs", "Wheel Hubs", new Color(0.68f, 0.76f, 0.84f));
            lineMaterial = ResolveTrackMaterial(track != null ? track.roadLineMaterial : null,
                "Road Lines", track != null ? track.roadLineColour : new Color(1f, 0.78f, 0.16f));
            shoulderMaterial = ResolveTrackMaterial(track != null ? track.shoulderMaterial : null,
                "Shoulders", track != null ? track.shoulderColour : new Color(0.72f, 0.38f, 0.15f));
            groundMaterial = CreateGroundMaterial(track);
            cactusMaterial = ResolveTrackMaterial(track != null ? track.cactusMaterial : null,
                "Cactus", track != null ? track.cactusColour : new Color(0.12f, 0.34f, 0.12f));
            obstacleMaterial = ResolveTrackMaterial(track != null ? track.obstacleMaterial : null,
                "Obstacle", track != null ? track.obstacleColour : new Color(0.34f, 0.17f, 0.07f));
            obstacleCarPaintMaterial = MakeMaterial("Traffic Car Paint", new Color(0.80f, 0.07f, 0.10f));
            obstacleCarTrimMaterial = MakeMaterial("Traffic Car Trim", new Color(1.0f, 0.62f, 0.08f));
            obstacleCarGlassMaterial = MakeMaterial("Traffic Car Windows", new Color(0.06f, 0.04f, 0.10f));
            startLineMaterial = MakeMaterial("Start Line", Color.white);
            carHeadlightMaterial = LoadCarMaterial("CarMaterials/CarHeadlights", "Car Headlights", new Color(1f, 0.78f, 0.16f));
            carTailLightMaterial = LoadCarMaterial("CarMaterials/CarTailLights", "Car Tail Lights", new Color(0.95f, 0.04f, 0.03f));
            carMetalDetailMaterial = LoadCarMaterial("CarMaterials/CarMetalDetails", "Car Metal Details", new Color(0.72f, 0.78f, 0.86f));
            carAccentMaterial = LoadCarMaterial("CarMaterials/CarAccent", "Car Accent Stripe", new Color(1.0f, 0.30f, 0.05f));
            longtailPaintMaterial = LoadCarMaterial("CarMaterials/LongtailPaint", "Longtail Paint", new Color(0.04f, 0.42f, 0.30f));
            formulaOrangeMaterial = LoadCarMaterial("CarMaterials/FormulaOrange", "Formula Orange", new Color(0.95f, 0.20f, 0.035f));
            formulaWhiteMaterial = LoadCarMaterial("CarMaterials/FormulaWhite", "Formula White", new Color(0.95f, 0.95f, 0.92f));
            formulaBlackMaterial = LoadCarMaterial("CarMaterials/FormulaBlack", "Formula Black", new Color(0.018f, 0.022f, 0.028f));
            finishDarkMaterial = MakeMaterial("Finish Line Dark", new Color(0.015f, 0.018f, 0.025f));
        }

        internal static void PrepareTrackMaterials(VoxelTrackDefinition track = null)
        {
            CreateMaterials(track);
        }

        private static Material ResolveTrackMaterial(Material materialOverride, string materialName, Color colour)
        {
            return materialOverride != null ? materialOverride : MakeMaterial(materialName, colour);
        }

        private static Material CreateGroundMaterial(VoxelTrackDefinition track)
        {
            if (track != null && track.groundMaterial != null)
                return track.groundMaterial;

            Color colour = track != null ? track.groundColour : new Color(0.31f, 0.18f, 0.07f);
            bool noiseEnabled = track == null || track.groundPixelNoiseEnabled;
            Shader shader = noiseEnabled ? Shader.Find("Voxel Racer/Ground Pixel Noise") : null;
            if (shader == null)
                return MakeMaterial("Ground", colour);

            var material = new Material(shader) { name = "Ground Pixel Noise" };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_PixelSize", track != null ? track.groundNoisePixelSize : 0.75f);
            material.SetFloat("_NoiseDensity", track != null ? track.groundNoiseDensity : 0.6f);
            material.SetFloat("_ColourVariation", track != null ? track.groundNoiseColourVariation : 0.1f);
            material.SetFloat("_NoiseSeed", track != null ? track.groundNoiseSeed : 317f);
            return material;
        }

        private static Material MakeMaterial(string materialName, Color colour)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = materialName, color = colour };
            // Keep generated voxel materials opaque so overlapping road and car geometry
            // writes to the depth buffer correctly during the brightness fade-in.
            material.SetFloat("_Smoothness", 0.15f);
            return material;
        }

        private static Material LoadCarMaterial(string resourcesPath, string materialName, Color colour)
        {
            Material material = Resources.Load<Material>(resourcesPath);
            return material != null ? material : MakeMaterial(materialName, colour);
        }

        private static void EnsureMaterials()
        {
            if (paintMaterial == null || glassMaterial == null || tyreMaterial == null)
                CreateMaterials(ActiveTrack);
        }

        public static void ReloadGeneratedMaterials() => CreateMaterials(ActiveTrack);

        internal static Material RoadMaterial => roadMaterial;
        internal static Material LineMaterial => lineMaterial;
        internal static Material ShoulderMaterial => shoulderMaterial;
        internal static Material GroundMaterial => groundMaterial;
        internal static Material CactusMaterial => cactusMaterial;
        internal static Material ObstacleMaterial => obstacleMaterial;
        internal static Material ObstacleCarPaintMaterial => obstacleCarPaintMaterial;

        /// <summary>Builds a distinct pickup-like traffic car with approximately 500 detachable voxels.</summary>
        internal static void CreateObstacleCarVisuals(Transform car)
        {
            CreateObstacleCarVoxelVolume("Traffic Chassis", car, new Vector3(0f, 0.40f, 0f), new Vector3(2.25f, 0.50f, 3.75f), obstacleCarPaintMaterial);
            CreateObstacleCarVoxelVolume("Traffic Bonnet", car, new Vector3(0f, 0.78f, 1.12f), new Vector3(2.10f, 0.35f, 1.35f), obstacleCarPaintMaterial);
            CreateObstacleCarVoxelVolume("Traffic Cab", car, new Vector3(0f, 1.20f, -0.45f), new Vector3(1.75f, 0.72f, 1.42f), obstacleCarGlassMaterial);
            CreateObstacleCarVoxelVolume("Traffic Roof", car, new Vector3(0f, 1.65f, -0.45f), new Vector3(1.92f, 0.20f, 1.55f), obstacleCarPaintMaterial);
            CreateObstacleCarVoxelVolume("Traffic Bed Rails", car, new Vector3(0f, 0.86f, -1.48f), new Vector3(2.15f, 0.32f, 0.82f), obstacleCarTrimMaterial);
            CreateObstacleCarVoxelVolume("Traffic Front Bumper", car, new Vector3(0f, 0.43f, 2.02f), new Vector3(2.10f, 0.24f, 0.28f), obstacleCarTrimMaterial);
            CreateObstacleCarVoxelVolume("Traffic Rear Bumper", car, new Vector3(0f, 0.40f, -2.00f), new Vector3(2.10f, 0.22f, 0.25f), obstacleCarTrimMaterial);
            CreateObstacleWheel(car, -1.17f, 1.18f);
            CreateObstacleWheel(car, 1.17f, 1.18f);
            CreateObstacleWheel(car, -1.17f, -1.22f);
            CreateObstacleWheel(car, 1.17f, -1.22f);
        }

        /// <summary>Builds a long-haul semi with a separate cab and box trailer, using detachable voxels.</summary>
        internal static void CreateObstacleSemiTrailerVisuals(Transform truck)
        {
            CreateObstacleCarVoxelVolume("Semi Cab Chassis", truck, new Vector3(0f, 0.42f, 2.55f), new Vector3(2.30f, 0.52f, 2.75f), obstacleCarPaintMaterial);
            CreateObstacleCarVoxelVolume("Semi Bonnet", truck, new Vector3(0f, 0.82f, 3.35f), new Vector3(2.15f, 0.38f, 1.15f), obstacleCarPaintMaterial);
            CreateObstacleCarVoxelVolume("Semi Cab", truck, new Vector3(0f, 1.42f, 2.10f), new Vector3(1.92f, 1.12f, 1.35f), obstacleCarGlassMaterial);
            CreateObstacleCarVoxelVolume("Semi Cab Roof", truck, new Vector3(0f, 2.08f, 2.10f), new Vector3(2.08f, 0.20f, 1.50f), obstacleCarPaintMaterial);
            CreateObstacleCarVoxelVolume("Semi Grille", truck, new Vector3(0f, 0.75f, 4.00f), new Vector3(1.70f, 0.36f, 0.16f), obstacleCarTrimMaterial);
            CreateObstacleCarVoxelVolume("Semi Trailer", truck, new Vector3(0f, 1.35f, -1.65f), new Vector3(2.55f, 1.85f, 5.80f), obstacleCarPaintMaterial, 0.45f);
            CreateObstacleCarVoxelVolume("Semi Trailer Stripe", truck, new Vector3(0f, 1.15f, -1.65f), new Vector3(2.59f, 0.17f, 5.86f), obstacleCarTrimMaterial, 0.45f);
            CreateObstacleCarVoxelVolume("Semi Rear Bumper", truck, new Vector3(0f, 0.45f, -4.63f), new Vector3(2.35f, 0.24f, 0.24f), obstacleCarTrimMaterial);
            CreateObstacleWheel(truck, -1.18f, 3.05f);
            CreateObstacleWheel(truck, 1.18f, 3.05f);
            CreateObstacleWheel(truck, -1.28f, -2.70f);
            CreateObstacleWheel(truck, 1.28f, -2.70f);
            CreateObstacleWheel(truck, -1.28f, -3.85f);
            CreateObstacleWheel(truck, 1.28f, -3.85f);
        }

        private static void CreateObstacleCarVoxelVolume(string partName, Transform parent, Vector3 centre, Vector3 size, Material material, float targetVoxelSize = 0.30f)
        {
            int xCount = Mathf.CeilToInt(size.x / targetVoxelSize);
            int yCount = Mathf.CeilToInt(size.y / targetVoxelSize);
            int zCount = Mathf.CeilToInt(size.z / targetVoxelSize);
            Vector3 voxelSize = new(size.x / xCount, size.y / yCount, size.z / zCount);
            var part = new GameObject(partName + " Voxels").transform;
            part.SetParent(parent);
            part.localPosition = centre;
            for (int x = 0; x < xCount; x++)
            for (int y = 0; y < yCount; y++)
            for (int z = 0; z < zCount; z++)
                CreateBlock(partName + " Voxel", part, new Vector3((x - (xCount - 1) * 0.5f) * voxelSize.x, (y - (yCount - 1) * 0.5f) * voxelSize.y, (z - (zCount - 1) * 0.5f) * voxelSize.z), voxelSize, material);
        }

        private static void CreateObstacleWheel(Transform parent, float x, float z)
        {
            var wheel = new GameObject("Obstacle Voxel Wheel").transform;
            wheel.SetParent(parent);
            wheel.localPosition = new Vector3(x, 0.40f, z);
            CreateBlock("Traffic Wheel Centre", wheel, Vector3.zero, new Vector3(0.46f, 0.52f, 0.82f), tyreMaterial);
            CreateBlock("Traffic Wheel Top", wheel, new Vector3(0f, 0.31f, 0f), new Vector3(0.46f, 0.22f, 0.58f), tyreMaterial);
            CreateBlock("Traffic Wheel Bottom", wheel, new Vector3(0f, -0.31f, 0f), new Vector3(0.46f, 0.22f, 0.58f), tyreMaterial);
            CreateBlock("Traffic Wheel Front", wheel, new Vector3(0f, 0f, 0.36f), new Vector3(0.46f, 0.42f, 0.20f), tyreMaterial);
            CreateBlock("Traffic Wheel Rear", wheel, new Vector3(0f, 0f, -0.36f), new Vector3(0.46f, 0.42f, 0.20f), tyreMaterial);
            CreateBlock("Traffic Wheel Hub", wheel, new Vector3(x > 0f ? 0.25f : -0.25f, 0f, 0f), new Vector3(0.06f, 0.30f, 0.30f), obstacleCarTrimMaterial);
        }

    }

    public sealed class VoxelRacerPrototypeMarker : MonoBehaviour { }
    public sealed class VoxelRacerGeneratedEnvironment : MonoBehaviour
    {
        public int layoutVersion = 24;
    }
}
