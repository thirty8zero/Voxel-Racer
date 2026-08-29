using UnityEngine;

namespace VoxelRacer
{
    /// <summary>All data needed to present and generate one race track in the shared race scene.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Track Definition", fileName = "VoxelTrackDefinition")]
    public sealed class VoxelTrackDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Desert Track";
        public string raceSceneName = "SampleScene";

        [Header("Gameplay")]
        public VoxelRoadTuning roadTuning;
        public VoxelObstacleCarTuning obstacleCarTuning;
        public VoxelMissionTuning missionTuning;

        // Legacy storage, migrated into this track's embedded traffic tuning by
        // VoxelTrackDefinitionEditor. Keeping it prevents existing tracks losing their list.
        [HideInInspector]
        public VoxelStaticObstacleSpawnEntry[] staticObstacleSpawns;

        [Header("Optional Material Overrides")]
        [Tooltip("Leave an override empty to use its generated colour below.")]
        public Material skyboxMaterial;
        public Material roadMaterial;
        public Material groundMaterial;
        public Material shoulderMaterial;
        public Material roadLineMaterial;
        public Material cactusMaterial;
        public Material obstacleMaterial;

        [Header("Generated Material Colours")]
        public Color roadColour = new(0.10f, 0.12f, 0.16f);
        public Color groundColour = new(0.31f, 0.18f, 0.07f);
        public Color shoulderColour = new(0.72f, 0.38f, 0.15f);
        public Color roadLineColour = new(1f, 0.78f, 0.16f);
        public Color cactusColour = new(0.12f, 0.34f, 0.12f);
        public Color obstacleColour = new(0.34f, 0.17f, 0.07f);

        [Header("Ground Pixel Noise")]
        public bool groundPixelNoiseEnabled = true;
        [Min(0.05f)] public float groundNoisePixelSize = 0.75f;
        [Range(0f, 1f)] public float groundNoiseDensity = 0.6f;
        [Range(0f, 0.5f)] public float groundNoiseColourVariation = 0.1f;
        public int groundNoiseSeed = 317;

        [Header("Cactus Palette")]
        public Color[] cactusShades =
        {
            new Color(0.09f, 0.28f, 0.10f),
            new Color(0.12f, 0.34f, 0.12f),
            new Color(0.16f, 0.40f, 0.14f),
            new Color(0.18f, 0.33f, 0.10f)
        };

        [Header("Sky And Fog")]
        public Color skyTint = new(0.36f, 0.18f, 0.56f);
        public Color skyGroundColour = new(0.46f, 0.12f, 0.08f);
        [Min(0f)] public float atmosphereThickness = 1.25f;
        public bool fogEnabled = true;
        public Color fogColour = new(0.58f, 0.20f, 0.18f);
        [Min(0f)] public float fogStartDistance = 90f;
        [Min(0f)] public float fogEndDistance = 320f;

        [Header("Horizon Sun")]
        public bool horizonSunEnabled = true;
        [Min(1f)] public float sunDistanceAhead = 220f;
        public float sunHorizontalOffset = 12f;
        public float sunHorizonHeight = -80f;

        [Header("Horizon Mountains")]
        public bool horizonMountainsEnabled = true;
        [Min(20f)] public float mountainDistance = 170f;
        [Min(0.1f)] public float mountainScale = 1f;
        public float mountainBaseHeight = -45f;
        public float minimumMountainPeakHeight = 14f;
        public float maximumMountainPeakHeight = 42f;
        public Color mountainColour = new(0.20f, 0.08f, 0.07f);
        public int mountainSeed = 481;

        [Header("Additional Scenery")]
        [Tooltip("Optional decorative prefabs randomly placed beside generated road segments.")]
        public GameObject[] sceneryPrefabs;
        [Min(0)] public int minimumSceneryPerSegment;
        [Min(0)] public int maximumSceneryPerSegment;
        [Min(0.1f)] public float minimumSceneryScale = 0.8f;
        [Min(0.1f)] public float maximumSceneryScale = 1.25f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            maximumSceneryPerSegment = Mathf.Max(minimumSceneryPerSegment, maximumSceneryPerSegment);
            maximumSceneryScale = Mathf.Max(minimumSceneryScale, maximumSceneryScale);
            fogEndDistance = Mathf.Max(fogStartDistance, fogEndDistance);
            maximumMountainPeakHeight = Mathf.Max(minimumMountainPeakHeight, maximumMountainPeakHeight);
            mountainDistance = Mathf.Max(20f, Mathf.Min(mountainDistance, sunDistanceAhead - 5f));
            mountainScale = Mathf.Max(0.1f, mountainScale);
            groundNoisePixelSize = Mathf.Max(0.05f, groundNoisePixelSize);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
