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
        {
            foreach (var entry in previews)
                if (entry != null && entry.source == source) return entry.image;
            return null;
        }
    }

    [Serializable]
    public sealed class VoxelMissionBriefingPreview
    {
        public UnityEngine.Object source;
        public Texture2D image;
    }
}
