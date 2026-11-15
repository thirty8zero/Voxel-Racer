using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Mission Briefing Settings", fileName = "MissionBriefingSettings")]
    public sealed class VoxelMissionBriefingSettings : ScriptableObject
    {
        [Header("Mission Entry")]
        public VoxelCarrierTruck carrierTruck;

        [Header("Backgrounds")]
        [Tooltip("Independent background textures. A track's Briefing Background Override takes priority.")]
        public Texture2D dayBackground, nightBackground;
        [Range(0, 1)] public float backgroundDim = .12f;
        [Header("Enemy Model Previews")]
        [Tooltip("Three-quarter renders of the actual models. Rebuild with Tools > Voxel Racer > Mission Briefing > Rebuild Model Previews.")]
        public List<VoxelMissionBriefingPreview> previews = new();
        public static VoxelMissionBriefingSettings Load() => Resources.Load<VoxelMissionBriefingSettings>("MissionBriefingSettings");
        public Texture2D BackgroundFor(VoxelTrackDefinition track) => track != null && track.briefingBackgroundOverride != null
            ? track.briefingBackgroundOverride : track != null && track.nightTime ? nightBackground : dayBackground;
        public Texture2D PreviewFor(UnityEngine.Object source)
            => PreviewRecordFor(source)?.StillImage;
        public VoxelMissionBriefingPreview PreviewRecordFor(UnityEngine.Object source)
        {
            foreach (var entry in previews)
                if (entry != null && entry.source == source) return entry;
            return null;
        }
    }

    [Serializable]
    public sealed class VoxelMissionBriefingPreview
    {
        public UnityEngine.Object source;
        public Texture2D image;
        [Tooltip("Optional illustrated portrait. Rebuilding prefab previews leaves this artwork assigned.")]
        public Texture2D portrait;
        [Tooltip("Optional transparent dish atlas over the illustrated portrait. Uses the same frame grid and timing.")]
        public Texture2D portraitDish;
        [Tooltip("Dish atlas placement in portrait coordinates, measured from the lower-left corner.")]
        public Rect portraitDishPlacement;
        [Tooltip("Optional baked animation. Frames run left to right, starting at the bottom row.")]
        public Texture2D animation;
        [Min(1)] public int columns = 1, rows = 1, frameCount = 1;
        [Min(0)] public float framesPerSecond;
        public Texture2D StillImage => portrait != null ? portrait : image;
        public Texture2D AnimationImage => portrait != null && portraitDish != null ? portraitDish : animation;
        public bool UsesPortraitDish => IsAnimated && AnimationImage == portraitDish;
        public bool IsAnimated => AnimationImage != null && columns > 0 && rows > 0 &&
            frameCount > 1 && frameCount <= columns * rows && framesPerSecond > 0;
        public float AspectRatio => IsAnimated && !UsesPortraitDish
            ? animation.width * (float)rows / (animation.height * columns)
            : StillImage != null ? StillImage.width / (float)StillImage.height : 1;
        public Rect FrameUv(float elapsed)
        {
            int frame = Mathf.FloorToInt(Mathf.Max(0, elapsed) * framesPerSecond) % frameCount;
            // Keep bilinear filtering inside each tile; no neighbouring-frame bleed.
            var atlas = AnimationImage;
            float insetX = .5f / atlas.width, insetY = .5f / atlas.height;
            return new Rect(frame % columns / (float)columns + insetX,
                frame / columns / (float)rows + insetY, 1f / columns - 2 * insetX, 1f / rows - 2 * insetY);
        }
    }
}
