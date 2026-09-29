using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelCarController
    {
        private float ploughDamageRemainder;

        /// <summary>Physical collisions only: bullets, mines and potholes keep their normal damage.</summary>
        public void ApplyCollisionDamage(Vector3 hitPoint, Vector3 impactDirection, string source, Vector3? playerToContact = null)
        {
            if (IsDestroyed || Time.time < nextDamageTime) return;
            int original = damageVoxelsPerHit;
            float reduced = VoxelPloughUpgradeState.PlayerDamage(Mathf.Max(1, original), transform, playerToContact ?? impactDirection);
            if (reduced >= Mathf.Max(1, original)) { ApplyDamage(hitPoint, impactDirection, source); return; }
            // Carry fractional voxels so small hits still receive the configured percentage over time.
            float total = reduced + ploughDamageRemainder;
            int count = Mathf.FloorToInt(total);
            ploughDamageRemainder = total - count;
            if (count == 0) { nextDamageTime = Time.time + .35f; return; }
            try { damageVoxelsPerHit = count; ApplyDamage(hitPoint, impactDirection, source); }
            finally { damageVoxelsPerHit = original; }
        }
    }
}
