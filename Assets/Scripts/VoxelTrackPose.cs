using UnityEngine;

namespace VoxelRacer
{
    /// <summary>A sampled frame on the generated road centreline.</summary>
    public readonly struct VoxelTrackPose
    {
        public readonly Vector3 position;
        public readonly Vector3 forward;
        public readonly Vector3 right;
        public readonly Quaternion rotation;

        public VoxelTrackPose(Vector3 position, float headingDegrees)
        {
            this.position = position;
            rotation = Quaternion.Euler(0f, headingDegrees, 0f);
            forward = rotation * Vector3.forward;
            right = rotation * Vector3.right;
        }
    }
}
