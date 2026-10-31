using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelCarController
    {
        private float mineDamageRemainder;

        /// <summary>Mine-only protection. The normal armour, cooldown, debris and breakdown path still applies.</summary>
        public void ApplyMineDamage(Vector3 hitPoint, Vector3 impactDirection, string source = "Enemy mines")
        {
            if (IsDestroyed || Time.time < nextDamageTime) return;
            int original = damageVoxelsPerHit;
            float multiplier = VoxelUnderbodyArmorUpgradeState.MineDamageMultiplier(transform);
            if (multiplier >= 1f) { ApplyDamage(hitPoint, impactDirection, source); return; }
            float total = Mathf.Max(1, original) * multiplier + mineDamageRemainder;
            int count = Mathf.FloorToInt(total + .00001f);
            mineDamageRemainder = Mathf.Max(0, total - count);
            if (count == 0) { nextDamageTime = Time.time + .35f; return; }
            try { damageVoxelsPerHit = count; ApplyDamage(hitPoint, impactDirection, source); }
            finally { damageVoxelsPerHit = original; }
        }
    }
}
