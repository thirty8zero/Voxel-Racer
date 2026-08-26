using UnityEngine;

namespace VoxelRacer
{
    public sealed class VoxelCameraFollow : MonoBehaviour
    {
        [Header("Chase View")]
        public Transform target;
        public Vector3 offset = new(-8.5f, 11f, -11f);
        public float lookAhead = 14f;

        [Header("Finish View")]
        [Tooltip("Local-space front three-quarter camera offset used as the car slows after finishing.")]
        public Vector3 finishOffset = new(-4.2f, 2.5f, 5.5f);
        [Min(0f)] public float finishLookHeight = 0.9f;
        [Range(10f, 90f)] public float finishFieldOfView = 40f;

        public bool FinishSequenceComplete { get; private set; }

        private VoxelCarController finishTarget;
        private Vector3 finishStartOffset;
        private Quaternion finishStartRotation;
        private float finishStartFieldOfView;
        private float finishStartSpeed;
        private bool finishSequenceActive;

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
            finishStartSpeed = Mathf.Max(0.05f, car.CurrentSpeed);
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
            transform.position = target.position + roadHeading * offset;
            transform.rotation = Quaternion.LookRotation(target.position + target.forward * lookAhead + Vector3.up * 0.3f - transform.position);
        }

        private void UpdateFinishSequence()
        {
            float progress = finishTarget == null || finishTarget.CurrentSpeed <= 0.05f
                ? 1f
                : 1f - Mathf.Clamp01(finishTarget.CurrentSpeed / finishStartSpeed);
            float easedProgress = progress * progress * (3f - 2f * progress);

            Vector3 endOffset = Quaternion.Euler(0f, target.eulerAngles.y, 0f) * finishOffset;
            Vector3 startHorizontal = new Vector3(finishStartOffset.x, 0f, finishStartOffset.z);
            Vector3 endHorizontal = new Vector3(endOffset.x, 0f, endOffset.z);
            float startAngle = Mathf.Atan2(startHorizontal.x, startHorizontal.z) * Mathf.Rad2Deg;
            float endAngle = Mathf.Atan2(endHorizontal.x, endHorizontal.z) * Mathf.Rad2Deg;
            float angle = Mathf.LerpAngle(startAngle, endAngle, easedProgress) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(startHorizontal.magnitude, endHorizontal.magnitude, easedProgress);
            float height = Mathf.Lerp(finishStartOffset.y, endOffset.y, easedProgress);
            Vector3 orbitOffset = new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);

            transform.position = target.position + orbitOffset;
            Quaternion endRotation = Quaternion.LookRotation(
                target.position + Vector3.up * finishLookHeight - transform.position);
            transform.rotation = Quaternion.Slerp(finishStartRotation, endRotation, easedProgress);

            Camera camera = GetComponent<Camera>();
            if (camera != null)
                camera.fieldOfView = Mathf.Lerp(finishStartFieldOfView, finishFieldOfView, easedProgress);

            if (progress >= 1f)
                FinishSequenceComplete = true;
        }
    }
}
