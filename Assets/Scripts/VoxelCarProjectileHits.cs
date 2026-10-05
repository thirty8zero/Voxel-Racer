using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelCarController
    {
        private VoxelProjectileVoxelCache hostileProjectileVoxels;

        internal bool TryFindHostileProjectileHit(Vector3 start, Vector3 direction, float length,
            out Vector3 point, out float distance)
        {
            point = default; distance = float.PositiveInfinity;
            if (IsDestroyed || !gameObject.activeInHierarchy) return false;
            // Authored player cars need no per-voxel physics colliders. Membership
            // is cached; damage, wheel rotation and car movement are read live.
            hostileProjectileVoxels ??= new VoxelProjectileVoxelCache(transform,
                renderer => renderer.transform != transform && renderer.GetComponentInParent<VoxelIndestructiblePart>() == null, true);
            return hostileProjectileVoxels.TryFindHit(start, direction, length, out point, out distance);
        }
    }
}
