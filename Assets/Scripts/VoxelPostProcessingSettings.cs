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

        [Header("Debrief Background Focus")]
        [Tooltip("Optional Gaussian distance blur during the finish-camera sequence only. Racing, garage and menu cameras keep their normal sharp view.")]
        public bool debriefDepthOfField = true;
        [Tooltip("Keep this many metres behind the car's camera-space centre sharp before distance blur starts.")]
        [Min(0)] public float debriefSharpMargin = 4f;
        [Tooltip("Metres from blur start to maximum background softness.")]
        [Min(1)] public float debriefBlurRange = 40f;
        [Range(0, 1)] public float debriefBlurRadius = .7f;
        public static VoxelPostProcessingSettings Load() => Resources.Load<VoxelPostProcessingSettings>("MobilePostProcessingSettings");
    }
}
