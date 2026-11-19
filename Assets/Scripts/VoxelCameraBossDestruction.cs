using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelCameraFollow
    {
        private const float MinimumBossDestructionReturnSeconds = .5f;
        private const float BossDestructionReturnPeakSpeed = 110f;
        public bool BossDestructionFocusComplete { get; private set; } = true;
        private bool bossDestructionFocusActive;
        private VoxelEnemyCar destructionFocusBoss;
        private Vector3 destructionFocusStart, destructionFocusLastPosition, destructionFocusOffset;
        private Quaternion destructionFocusHeading, destructionFocusStartRotation;
        private float destructionFocusElapsed, destructionFocusDuration, destructionFocusBlend, destructionFocusLookHeight;
        private float destructionFocusStartFov, destructionFocusFov;
        private float destructionReturnStartedAt, destructionReturnDuration;
        private double destructionFocusLastRealtime;

        public void BeginBossDestructionFocus(VoxelCarController player, VoxelEnemyCar boss, VoxelBossDefinition settings)
        {
            if (player == null || boss == null || settings == null) return;
            target = player.transform;
            destructionFocusBoss = boss;
            destructionFocusLastPosition = boss.transform.position;
            destructionFocusHeading = player.TrackPath != null ? player.TrackPath.Evaluate(boss.TrackDistance).rotation :
                Quaternion.Euler(0, boss.transform.eulerAngles.y, 0);
            destructionFocusStart = transform.position;
            destructionFocusStartRotation = transform.rotation;
            var camera = GetComponent<Camera>();
            destructionFocusStartFov = camera != null ? camera.fieldOfView : settings.destructionCameraFieldOfView;
            destructionFocusOffset = settings.destructionCameraOffset;
            destructionFocusLookHeight = settings.destructionCameraLookHeight;
            destructionFocusFov = settings.destructionCameraFieldOfView;
            destructionFocusDuration = Mathf.Max(0, settings.destructionCameraDuration);
            destructionFocusBlend = Mathf.Max(0, settings.destructionCameraBlendDuration);
            destructionFocusElapsed = 0;
            destructionFocusLastRealtime = Time.realtimeSinceStartupAsDouble;
            finishSequenceActive = false; FinishSequenceComplete = false;
            bossDestructionFocusActive = true; BossDestructionFocusComplete = false;
        }

        private void UpdateBossDestructionFocus()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float realSeconds = Mathf.Max(0, (float)(now - destructionFocusLastRealtime));
            destructionFocusLastRealtime = now;
            AdvanceBossDestructionFocus(realSeconds);
        }

        private void AdvanceBossDestructionFocus(float realSeconds)
        {
            if (VoxelPauseMenu.IsPaused || Time.timeScale <= 0) return;
            destructionFocusElapsed += Mathf.Max(0, realSeconds);
            if (destructionFocusBoss != null && destructionFocusBoss.gameObject.activeInHierarchy)
                destructionFocusLastPosition = destructionFocusBoss.transform.position;
            float blend = destructionFocusBlend <= 0 ? 1 : Mathf.SmoothStep(0, 1, destructionFocusElapsed / destructionFocusBlend);
            Vector3 position = destructionFocusLastPosition + destructionFocusHeading * destructionFocusOffset;
            Vector3 look = destructionFocusLastPosition + Vector3.up * destructionFocusLookHeight;
            transform.position = Vector3.Lerp(destructionFocusStart, position, blend);
            if ((look - transform.position).sqrMagnitude > .0001f)
                transform.rotation = Quaternion.Slerp(destructionFocusStartRotation, Quaternion.LookRotation(look - transform.position), blend);
            var camera = GetComponent<Camera>();
            if (camera != null) camera.fieldOfView = Mathf.Lerp(destructionFocusStartFov, destructionFocusFov, blend);

            float returnStart = Mathf.Max(0f, destructionFocusDuration - MinimumBossDestructionReturnSeconds);
            if (destructionFocusElapsed < returnStart)
            {
                ApplyShake();
                return;
            }

            if (!finishSequenceActive)
            {
                // Start the finish orbit close to the player. The return blend handles the distance from the wreck.
                var settings = Tuning;
                Quaternion heading = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
                Vector3 orbitStart = target.position + heading * (settings != null ? settings.chaseOffset : offset);
                Vector3 orbitLook = target.position + heading * Vector3.forward *
                    (settings != null ? settings.chaseLookAhead : lookAhead) + Vector3.up * .3f;
                StartFinishOrbit(orbitStart, Quaternion.LookRotation(orbitLook - orbitStart),
                    camera != null ? camera.fieldOfView : destructionFocusFov);

                destructionReturnStartedAt = destructionFocusElapsed;
                // SmoothStep peaks at 1.5 times the average speed between fixed endpoints.
                destructionReturnDuration = Mathf.Max(MinimumBossDestructionReturnSeconds,
                    Vector3.Distance(transform.position, orbitStart) * 1.5f / BossDestructionReturnPeakSpeed);
            }

            float returnProgress = (destructionFocusElapsed - destructionReturnStartedAt) / destructionReturnDuration;
            UpdateFinishSequence(Mathf.SmoothStep(0f, 1f, returnProgress));
            if (returnProgress < 1f) return;

            bossDestructionFocusActive = false;
            BossDestructionFocusComplete = true;
            destructionFocusBoss = null;
        }
    }
}
