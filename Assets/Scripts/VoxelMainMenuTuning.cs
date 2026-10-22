using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Persistent selection of cars featured in the two Main Menu display slots.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Main Menu Tuning", fileName = "MainMenuTuning")]
    public sealed class VoxelMainMenuTuning : ScriptableObject
    {
        [Tooltip("Drag cars here in the order they should appear. The first two valid entries are displayed; one entry is centred.")]
        public VoxelCarDefinition[] featuredCars;

        [Header("Title Logo")]
        [Tooltip("Transparent title artwork fitted above the main-menu car and START button.")]
        public Texture2D titleLogo;

        [Header("Placeholder Artwork")]
        [Tooltip("Cover the 3D menu with the two artwork layers, below the original title and buttons.")]
        public bool artworkEnabled = true;
        [Tooltip("Fills the screen without stretching; excess image area is cropped.")]
        public Texture2D backgroundArtwork;
        [Tooltip("Transparent artwork fitted in the lower part of the screen, below the title logo.")]
        public Texture2D foregroundArtwork;

        [Header("Desert Scenery")]
        public bool showHorizonMountains = true;
        [Min(0.1f)] public float mountainScale = 1f;
        [Min(0)] public int cactusCount = 24;
        [Min(0.1f)] public float minimumCactusScale = 0.55f;
        [Min(0.1f)] public float maximumCactusScale = 1.15f;
        public int cactusSeed = 912;

        public static VoxelMainMenuTuning Load() =>
            Resources.Load<VoxelMainMenuTuning>("MainMenuTuning");

#if UNITY_EDITOR
        private bool refreshQueued;

        private void OnValidate()
        {
            maximumCactusScale = Mathf.Max(minimumCactusScale, maximumCactusScale);
            mountainScale = Mathf.Max(0.1f, mountainScale);
            VoxelAssetSaveQueue.Request(this);
            if (!Application.isPlaying || refreshQueued)
                return;

            refreshQueued = true;
            UnityEditor.EditorApplication.delayCall += RefreshLiveMainMenu;
        }

        private void RefreshLiveMainMenu()
        {
            refreshQueued = false;
            if (this == null)
                return;

            foreach (VoxelMainMenuController menu in
                     FindObjectsByType<VoxelMainMenuController>(FindObjectsSortMode.None))
                if (menu.tuning == this)
                {
                    menu.BuildFeaturedCars();
                    menu.BuildDesertScenery();
                    menu.BuildArtwork();
                }
        }
#endif
    }
}
