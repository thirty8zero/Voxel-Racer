using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Persistent settings for the player's single-charge speed boost.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Player/Boost Tuning", fileName = "VoxelBoostTuning")]
    public sealed class VoxelBoostTuning : ScriptableObject
    {
        [Tooltip("Extra speed added to the player's normal top speed while boost is active.")]
        [Min(0f)] public float boostSpeed = 18f;
        [Tooltip("Seconds a fully charged boost remains active after one press.")]
        [Min(0.05f)] public float boostLength = 1.5f;
        [Tooltip("Seconds required to recharge an empty boost back to full.")]
        [Min(0.05f)] public float rechargeCooldownLength = 6f;

        public static VoxelBoostTuning Load() => Resources.Load<VoxelBoostTuning>("Boost/DefaultBoostTuning");

#if UNITY_EDITOR
        private void OnValidate()
        {
            boostSpeed = Mathf.Max(0f, boostSpeed);
            boostLength = Mathf.Max(0.05f, boostLength);
            rechargeCooldownLength = Mathf.Max(0.05f, rechargeCooldownLength);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
