using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Mission Briefing Settings", fileName = "MissionBriefingSettings")]
    public sealed class VoxelMissionBriefingSettings : ScriptableObject
    {
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
            => PreviewRecordFor(source)?.image;
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
        [Tooltip("Optional baked animation. Frames run left to right, starting at the bottom row.")]
        public Texture2D animation;
        [Min(1)] public int columns = 1, rows = 1, frameCount = 1;
        [Min(0)] public float framesPerSecond;
        public bool IsAnimated => animation != null && columns > 0 && rows > 0 &&
            frameCount > 1 && frameCount <= columns * rows && framesPerSecond > 0;
        public float AspectRatio => IsAnimated
            ? animation.width * (float)rows / (animation.height * columns)
            : image != null ? image.width / (float)image.height : 1;
        public Rect FrameUv(float elapsed)
        {
            int frame = Mathf.FloorToInt(Mathf.Max(0, elapsed) * framesPerSecond) % frameCount;
            // Keep bilinear filtering inside each tile; no neighbouring-frame bleed.
            float insetX = .5f / animation.width, insetY = .5f / animation.height;
            return new Rect(frame % columns / (float)columns + insetX,
                frame / columns / (float)rows + insetY, 1f / columns - 2 * insetX, 1f / rows - 2 * insetY);
        }
    }
}
