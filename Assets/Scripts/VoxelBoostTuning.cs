using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Persistent settings for the player's single-charge speed boost.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Player/Boost Tuning", fileName = "VoxelBoostTuning")]
    public class VoxelBoostTuning : ScriptableObject
    {
        [Tooltip("Optional exhaust effect. Empty uses the standard boost flame.")]
        public ParticleSystem exhaustEffectPrefab;
        [Tooltip("Extra speed added to the player's normal top speed while boost is active.")]
        [Min(0f)] public float boostSpeed = 18f;
        [Tooltip("Seconds a fully charged boost remains active after one press.")]
        [Min(0.05f)] public float boostLength = 1.5f;
        [Tooltip("Seconds required to recharge an empty boost back to full.")]
        [Min(0.05f)] public float rechargeCooldownLength = 6f;

        [Header("Forward Movement")]
        [Tooltip("How many metres forward in its lane the car shifts while boost is active. The car smoothly returns to its normal position when boost ends.")]
        [Min(0f)] public float boostForwardOffset = 2f;
        [Tooltip("Seconds taken for the car to move forward at boost start and settle back after boost ends.")]
        [Min(0.01f)] public float boostForwardMovementDuration = 0.22f;
        [Tooltip("Easing used for the boosted forward movement and its return to the normal driving position.")]
        public VoxelEasingType boostForwardMovementEasing = VoxelEasingType.EaseInOutCubic;

        public static VoxelBoostTuning Load() => Resources.Load<VoxelBoostTuning>("Boost/DefaultBoostTuning");

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            boostSpeed = Mathf.Max(0f, boostSpeed);
            boostLength = Mathf.Max(0.05f, boostLength);
            rechargeCooldownLength = Mathf.Max(0.05f, rechargeCooldownLength);
            boostForwardOffset = Mathf.Max(0f, boostForwardOffset);
            boostForwardMovementDuration = Mathf.Max(0.01f, boostForwardMovementDuration);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
