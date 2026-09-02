using UnityEngine;

namespace VoxelRacer
{
    public sealed class VoxelCameraFollow : MonoBehaviour
    {
        [Header("Camera Tuning")]
        public VoxelCameraTuning tuning;

        [Header("Legacy Fallbacks")]
        public Transform target;
        public Vector3 offset = new(-8.5f, 11f, -11f);
        public float lookAhead = 14f;

        [Header("Finish View")]
        [Tooltip("Local-space front three-quarter camera offset used as the car slows after finishing.")]
        public Vector3 finishOffset = new(-4.2f, 2.5f, 5.5f);
        [Min(0f)] public float finishLookHeight = 0.9f;
        [Tooltip("Sideways look offset used to compose the stopped car on the right side of the screen.")]
        public float finishLookSideOffset = 3.4f;
        [Range(10f, 90f)] public float finishFieldOfView = 42f;
        [Min(0.01f)] public float finishSequenceDuration = 2.5f;

        public bool FinishSequenceComplete { get; private set; }

        private VoxelCarController finishTarget;
        private Vector3 finishStartOffset;
        private Quaternion finishStartRotation;
        private float finishStartFieldOfView;
        private float finishSequenceStartedAt;
        private bool finishSequenceActive;
        private bool laneCameraInitialised;
        private float displayedLaneOffset;
        private float laneChangeStartOffset;
        private float laneChangeTargetOffset;
        private float laneChangeStartedAt;
        private CameraShake impactShake;
        private CameraShake explosionShake;

        private VoxelCameraTuning Tuning => tuning != null ? tuning : VoxelCameraTuning.Load();

        public void SetTuning(VoxelCameraTuning value) => tuning = value;

        public void BeginFinishSequence(VoxelCarController car)
        {
            if (car == null || finishSequenceActive)
                return;

            target = car.transform;
            finishTarget = car;
            finishStartOffset = transform.position - target.position;
            finishStartRotation = transform.rotation;
            Camera camera = GetComponent<Camera>();
            finishStartFieldOfView = camera != null ? camera.fieldOfView : finishFieldOfView;
            finishSequenceStartedAt = Time.time;
            finishSequenceActive = true;
            FinishSequenceComplete = false;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || target == null)
                return;

            if (finishSequenceActive)
            {
                UpdateFinishSequence();
                return;
            }

            Quaternion roadHeading = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            Vector3 cameraTargetPosition = GetSmoothedLaneTargetPosition(roadHeading);
            Vector3 chaseOffset = Tuning != null ? Tuning.chaseOffset : offset;
            float chaseLookAhead = Tuning != null ? Tuning.chaseLookAhead : lookAhead;
            transform.position = cameraTargetPosition + roadHeading * chaseOffset;
            transform.rotation = Quaternion.LookRotation(cameraTargetPosition + target.forward * chaseLookAhead + Vector3.up * 0.3f - transform.position);
            ApplyShake();
        }

        private void UpdateFinishSequence()
        {
            VoxelCameraTuning settings = Tuning;
            float duration = settings != null ? settings.finishSequenceDuration : finishSequenceDuration;
            float progress = duration <= 0.001f ? 1f : Mathf.Clamp01((Time.time - finishSequenceStartedAt) / duration);
            float easedProgress = progress * progress * (3f - 2f * progress);

            Vector3 endOffset = Quaternion.Euler(0f, target.eulerAngles.y, 0f) *
                (settings != null ? settings.finishOffset : finishOffset);
            Vector3 startHorizontal = new Vector3(finishStartOffset.x, 0f, finishStartOffset.z);
            Vector3 endHorizontal = new Vector3(endOffset.x, 0f, endOffset.z);
            float startAngle = Mathf.Atan2(startHorizontal.x, startHorizontal.z) * Mathf.Rad2Deg;
            float endAngle = Mathf.Atan2(endHorizontal.x, endHorizontal.z) * Mathf.Rad2Deg;
            float angle = Mathf.LerpAngle(startAngle, endAngle, easedProgress) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(startHorizontal.magnitude, endHorizontal.magnitude, easedProgress);
            float height = Mathf.Lerp(finishStartOffset.y, endOffset.y, easedProgress);
            Vector3 orbitOffset = new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);

            transform.position = target.position + orbitOffset;
            Vector3 finishLookPoint = target.position + target.right *
                (settings != null ? settings.finishLookSideOffset : finishLookSideOffset) +
                Vector3.up * (settings != null ? settings.finishLookHeight : finishLookHeight);
            Quaternion endRotation = Quaternion.LookRotation(finishLookPoint - transform.position);
            transform.rotation = Quaternion.Slerp(finishStartRotation, endRotation, easedProgress);

            Camera camera = GetComponent<Camera>();
            if (camera != null)
                camera.fieldOfView = Mathf.Lerp(finishStartFieldOfView,
                    settings != null ? settings.finishFieldOfView : finishFieldOfView, easedProgress);

            ApplyShake();

            if (progress >= 1f)
                FinishSequenceComplete = true;
        }

        /// <summary>Shared shake for every successful player-damage event.</summary>
        public void ShakeFromPlayerDamage()
        {
            VoxelCameraTuning settings = Tuning;
            if (settings == null || settings.playerVehicleImpactShakeDuration <= 0f)
                return;

            StartShake(ref impactShake, settings.playerVehicleImpactShakeDuration,
                settings.playerVehicleImpactShakePositionStrength,
                settings.playerVehicleImpactShakeRotationDegrees,
                settings.playerVehicleImpactShakeFrequency);
        }

        /// <summary>Compatibility entry point retained for existing integrations.</summary>
        public void ShakeFromPlayerVehicleImpact() => ShakeFromPlayerDamage();

        public void ShakeFromObjectExplosion()
        {
            VoxelCameraTuning settings = Tuning;
            if (settings == null || settings.objectExplosionShakeDuration <= 0f)
                return;

            StartShake(ref explosionShake, settings.objectExplosionShakeDuration,
                settings.objectExplosionShakePositionStrength,
                settings.objectExplosionShakeRotationDegrees,
                settings.objectExplosionShakeFrequency);
        }

        private Vector3 GetSmoothedLaneTargetPosition(Quaternion roadHeading)
        {
            VoxelCarController car = target.GetComponent<VoxelCarController>();
            if (car == null)
                return target.position;

            float requestedLaneOffset = car.TargetLaneOffset;
            if (!laneCameraInitialised)
            {
                laneCameraInitialised = true;
                displayedLaneOffset = car.CurrentLaneOffset;
                laneChangeStartOffset = displayedLaneOffset;
                laneChangeTargetOffset = requestedLaneOffset;
                laneChangeStartedAt = Time.time;
            }
            else if (!Mathf.Approximately(requestedLaneOffset, laneChangeTargetOffset))
            {
                laneChangeStartOffset = displayedLaneOffset;
                laneChangeTargetOffset = requestedLaneOffset;
                laneChangeStartedAt = Time.time;
            }

            VoxelCameraTuning settings = Tuning;
            float duration = settings != null ? settings.laneChangeCameraDuration : 0.32f;
            VoxelEasingType easing = settings != null ? settings.laneChangeCameraEasing : VoxelEasingType.EaseInOutCubic;
            float progress = duration <= 0.001f ? 1f : (Time.time - laneChangeStartedAt) / duration;
            displayedLaneOffset = Mathf.Lerp(laneChangeStartOffset, laneChangeTargetOffset,
                VoxelEasing.Evaluate(easing, progress));

            Vector3 roadRight = roadHeading * Vector3.right;
            Vector3 roadCentre = target.position - roadRight * car.CurrentLaneOffset;
            return roadCentre + roadRight * displayedLaneOffset;
        }

        private void ApplyShake()
        {
            ApplyShake(ref impactShake);
            ApplyShake(ref explosionShake);
        }

        private static void StartShake(ref CameraShake shake, float duration, float positionStrength,
            float rotationDegrees, float frequency)
        {
            shake.active = true;
            shake.startedAt = Time.time;
            shake.duration = duration;
            shake.positionStrength = positionStrength;
            shake.rotationDegrees = rotationDegrees;
            shake.frequency = frequency;
            shake.seed = Random.value * 1000f;
        }

        private void ApplyShake(ref CameraShake shake)
        {
            if (!shake.active || shake.duration <= 0f)
                return;

            float progress = (Time.time - shake.startedAt) / shake.duration;
            if (progress >= 1f)
            {
                shake.active = false;
                return;
            }

            float strength = 1f - progress;
            float sampleTime = (Time.time - shake.startedAt) * shake.frequency;
            Vector3 noise = new(
                Mathf.PerlinNoise(shake.seed, sampleTime) * 2f - 1f,
                Mathf.PerlinNoise(shake.seed + 13.4f, sampleTime) * 2f - 1f,
                Mathf.PerlinNoise(shake.seed + 29.7f, sampleTime) * 2f - 1f);
            transform.position += transform.right * noise.x * shake.positionStrength * strength +
                transform.up * noise.y * shake.positionStrength * strength +
                transform.forward * noise.z * shake.positionStrength * 0.35f * strength;
            transform.rotation *= Quaternion.Euler(noise.y * shake.rotationDegrees * strength,
                noise.x * shake.rotationDegrees * strength, noise.z * shake.rotationDegrees * strength);
        }

        private struct CameraShake
        {
            public bool active;
            public float startedAt;
            public float duration;
            public float positionStrength;
            public float rotationDegrees;
            public float frequency;
            public float seed;
        }
    }
}
