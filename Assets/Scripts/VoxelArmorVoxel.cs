using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Extra durability, while still counting as one integrity voxel.</summary>
    public sealed class VoxelArmorVoxel : MonoBehaviour
    {
        public VoxelArmorTuning tuning;
        [SerializeField, HideInInspector] private int remainingHealth = -1;
        public int MaximumHealth => tuning != null ? Mathf.Max(1, tuning.voxelHitPoints) : 1;
        public int RemainingHealth => remainingHealth < 0 ? MaximumHealth : Mathf.Clamp(remainingHealth, 0, MaximumHealth);
        public bool NeedsRepair => RemainingHealth < MaximumHealth;

        public int AbsorbDamage(int damage)
        {
            int absorbed = Mathf.Min(Mathf.Max(0, damage), RemainingHealth);
            remainingHealth = RemainingHealth - absorbed;
            return absorbed;
        }

        public void RestoreHealth(int health) => remainingHealth = Mathf.Clamp(health, 0, MaximumHealth);
        public void RepairToFull() => remainingHealth = MaximumHealth;
    }
}
