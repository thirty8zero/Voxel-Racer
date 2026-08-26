using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Ordered list of tracks used by the current race campaign.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Track Sequence", fileName = "VoxelTrackSequence")]
    public sealed class VoxelTrackSequence : ScriptableObject
    {
        public VoxelTrackDefinition[] tracks;
        [Tooltip("For the prototype, Next Race returns to Track 1 after the final configured track.")]
        public bool loopSequence = true;

        public static VoxelTrackSequence Load() => Resources.Load<VoxelTrackSequence>("VoxelTrackSequence");

#if UNITY_EDITOR
        private void OnValidate() => VoxelAssetSaveQueue.Request(this);
#endif
    }
}
