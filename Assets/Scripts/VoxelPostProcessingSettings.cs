using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Post Processing Settings")]
    public sealed class VoxelPostProcessingSettings : ScriptableObject
    {
        [Tooltip("Master switch for the game cameras. Off bypasses the entire post-processing pass.")]
        public bool effectsEnabled = true;
        public VolumeProfile profile;
        public static VoxelPostProcessingSettings Load() => Resources.Load<VoxelPostProcessingSettings>("MobilePostProcessingSettings");
    }
}
