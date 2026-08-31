using UnityEngine;

namespace VoxelRacer
{
    public enum VoxelEasingType
    {
        Linear,
        EaseInQuad,
        EaseOutQuad,
        EaseInOutQuad,
        EaseInCubic,
        EaseOutCubic,
        EaseInOutCubic,
        EaseInQuart,
        EaseOutQuart,
        EaseInOutQuart,
        EaseInSine,
        EaseOutSine,
        EaseInOutSine,
        EaseInExpo,
        EaseOutExpo,
        EaseInOutExpo
    }

    /// <summary>Editable presentation profile shared by the driving camera.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Camera Tuning", fileName = "VoxelCameraTuning")]
    public sealed class VoxelCameraTuning : ScriptableObject
    {
        [Header("Chase View")]
        public Vector3 chaseOffset = new(-8.5f, 11f, -11f);
        [Min(0f)] public float chaseLookAhead = 14f;

        [Header("Finish View")]
        public Vector3 finishOffset = new(-4.2f, 2.5f, 5.5f);
        [Min(0f)] public float finishLookHeight = 0.9f;
        public float finishLookSideOffset = 3.4f;
        [Range(10f, 90f)] public float finishFieldOfView = 42f;
        [Min(0.01f)] public float finishSequenceDuration = 2.5f;

        [Header("Lane Change Camera")]
        [Min(0.01f)] public float laneChangeCameraDuration = 0.32f;
        public VoxelEasingType laneChangeCameraEasing = VoxelEasingType.EaseInOutCubic;

        [Header("Player Vehicle Impact Shake")]
        [Min(0f)] public float playerVehicleImpactShakeDuration = 0.24f;
        [Min(0f)] public float playerVehicleImpactShakePositionStrength = 0.32f;
        [Min(0f)] public float playerVehicleImpactShakeRotationDegrees = 2.4f;
        [Min(0f)] public float playerVehicleImpactShakeFrequency = 26f;

        [Header("Object Explosion Shake")]
        [Min(0f)] public float objectExplosionShakeDuration = 0.36f;
        [Min(0f)] public float objectExplosionShakePositionStrength = 0.58f;
        [Min(0f)] public float objectExplosionShakeRotationDegrees = 4f;
        [Min(0f)] public float objectExplosionShakeFrequency = 19f;

        public static VoxelCameraTuning Load() => Resources.Load<VoxelCameraTuning>("VoxelCameraTuning");
    }

    public static class VoxelEasing
    {
        public static float Evaluate(VoxelEasingType easing, float value)
        {
            float t = Mathf.Clamp01(value);
            return easing switch
            {
                VoxelEasingType.EaseInQuad => t * t,
                VoxelEasingType.EaseOutQuad => 1f - (1f - t) * (1f - t),
                VoxelEasingType.EaseInOutQuad => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f,
                VoxelEasingType.EaseInCubic => t * t * t,
                VoxelEasingType.EaseOutCubic => 1f - Mathf.Pow(1f - t, 3f),
                VoxelEasingType.EaseInOutCubic => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f,
                VoxelEasingType.EaseInQuart => t * t * t * t,
                VoxelEasingType.EaseOutQuart => 1f - Mathf.Pow(1f - t, 4f),
                VoxelEasingType.EaseInOutQuart => t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) * 0.5f,
                VoxelEasingType.EaseInSine => 1f - Mathf.Cos(t * Mathf.PI * 0.5f),
                VoxelEasingType.EaseOutSine => Mathf.Sin(t * Mathf.PI * 0.5f),
                VoxelEasingType.EaseInOutSine => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f,
                VoxelEasingType.EaseInExpo => t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f),
                VoxelEasingType.EaseOutExpo => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t),
                VoxelEasingType.EaseInOutExpo => t <= 0f ? 0f : t >= 1f ? 1f : t < 0.5f
                    ? Mathf.Pow(2f, 20f * t - 10f) * 0.5f
                    : (2f - Mathf.Pow(2f, -20f * t + 10f)) * 0.5f,
                _ => t
            };
        }
    }
}
