using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelRepairUpgradeSceneController
    {
        private Matrix4x4 garageCameraTransitionFrom, garageCameraTargetProjection;
        private float garageCameraTransitionDuration, garageCameraTransitionElapsed;
        private bool garageCameraTransitionActive;
        private VoxelEasingType garageCameraTransitionEasing;

        private void BeginGarageCameraTransition()
        {
            // Editor preview tools render the settled layout; gameplay eases the
            // perspective framing, including its shifted upgrade composition.
            if (!Application.isPlaying) return;
            StartGarageCameraTransition(cameraTuning != null ? cameraTuning.cameraTransitionSeconds : .45f);
        }

        private void StartGarageCameraTransition(float duration)
        {
            if (workshopCamera == null) return;
            garageCameraTransitionFrom = workshopCamera.projectionMatrix;
            garageCameraTransitionDuration = Mathf.Max(0, duration);
            garageCameraTransitionElapsed = 0;
            garageCameraTransitionActive = duration > 0;
            garageCameraTransitionEasing = cameraTuning != null ? cameraTuning.cameraTransitionEasing : VoxelEasingType.SmoothStep;
        }

        private void ApplyGarageCameraProjection(Matrix4x4 target)
        {
            garageCameraTargetProjection = target;
            if (!garageCameraTransitionActive) { workshopCamera.projectionMatrix = target; return; }
            float t = VoxelEasing.Evaluate(garageCameraTransitionEasing, garageCameraTransitionElapsed / garageCameraTransitionDuration);
            var blended = new Matrix4x4();
            for (int i = 0; i < 4; i++)
                blended.SetColumn(i, Vector4.Lerp(garageCameraTransitionFrom.GetColumn(i), target.GetColumn(i), t));
            workshopCamera.projectionMatrix = blended;
        }

        private void AdvanceGarageCameraTransition(float unscaledDeltaTime)
        {
            if (!garageCameraTransitionActive || workshopCamera == null) return;
            garageCameraTransitionElapsed += Mathf.Max(0, unscaledDeltaTime);
            if (garageCameraTransitionElapsed >= garageCameraTransitionDuration) garageCameraTransitionActive = false;
            ApplyGarageCameraProjection(garageCameraTargetProjection);
        }
    }
}
