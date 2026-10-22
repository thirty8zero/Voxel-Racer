using System;
using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Loading Screen Settings", fileName = "LoadingScreenSettings")]
    public sealed class VoxelLoadingScreenSettings : ScriptableObject
    {
        [Header("Loading Images")]
        [Tooltip("Show random pictures while loading. Turn off for black with LOADING, keeping the image list for later.")]
        public bool showImages = true;
        [Tooltip("One image is chosen per load. Add/remove entries here; main-menu artwork and empty entries are always skipped.")]
        public Texture2D[] images = Array.Empty<Texture2D>();

        [Header("Presentation")]
        [Tooltip("Minimum real seconds to show the screen, avoiding a flash during fast loads. Zero removes the minimum.")]
        [Min(0f)] public float minimumDisplaySeconds = .65f;
        [Range(.05f, .25f)] public float topBarFraction = .10f;
        [Range(.12f, .3f)] public float bottomBarFraction = .18f;
        [Range(36, 120)] public int loadingFontSize = 88;

        public bool IsEligible(Texture2D image)
        {
            if (image == null) return false;
            var menu = VoxelMainMenuTuning.Load();
            return menu == null || (image != menu.backgroundArtwork && image != menu.foregroundArtwork && image != menu.titleLogo);
        }

        public Texture2D ChooseImage(System.Random random, Texture2D previous = null)
        {
            if (!showImages || images == null) return null;
            bool hasAlternative = false;
            foreach (var image in images)
                if (image != previous && IsEligible(image)) { hasAlternative = true; break; }
            Texture2D selected = null;
            int count = 0;
            foreach (var image in images)
            {
                if (!IsEligible(image) || (hasAlternative && image == previous)) continue;
                if (random.Next(++count) == 0) selected = image;
            }
            return selected;
        }

        public static VoxelLoadingScreenSettings Load() => Resources.Load<VoxelLoadingScreenSettings>("LoadingScreenSettings");

#if UNITY_EDITOR
        private void OnValidate()
        {
            minimumDisplaySeconds = Mathf.Max(0f, minimumDisplaySeconds);
            topBarFraction = Mathf.Clamp(topBarFraction, .05f, .25f);
            bottomBarFraction = Mathf.Clamp(bottomBarFraction, .12f, .3f);
            loadingFontSize = Mathf.Clamp(loadingFontSize, 36, 120);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
