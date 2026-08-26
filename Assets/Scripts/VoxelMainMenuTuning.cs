using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Persistent selection of cars featured in the two Main Menu display slots.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Main Menu Tuning", fileName = "MainMenuTuning")]
    public sealed class VoxelMainMenuTuning : ScriptableObject
    {
        [Tooltip("Drag cars here in the order they should appear. The first two valid entries are displayed; one entry is centred.")]
        public VoxelCarDefinition[] featuredCars;

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
                }
        }
#endif
    }
}
