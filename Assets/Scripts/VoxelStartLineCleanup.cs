using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Removes the one-off start line once the player has driven well past it.</summary>
    public sealed class VoxelStartLineCleanup : MonoBehaviour
    {
        public VoxelCarController target;
        public float trackDistance;
        [Min(0f)] public float destroyBehindDistance = 10f;

        private void Update()
        {
            if (target != null && trackDistance < target.TrackDistance - destroyBehindDistance)
                Destroy(gameObject);
        }
    }
}
