using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelMissionProgress
    {
        private const float CompletionSlowMotionScale = .25f;
        private const float CompletionSlowMotionDuration = 1.5f;
        public bool IsCompletionSlowMotionActive { get; private set; }
        private float completionPreviousTimeScale, completionPreviousFixedStep;
        private double completionSlowMotionEndsAt;

        private void BeginCompletionSlowMotion()
        {
            if (!Application.isPlaying || IsCompletionSlowMotionActive || VoxelPlayerDeathScreen.IsShowing ||
                VoxelPauseMenu.IsPaused || Time.timeScale <= 0f) return;
            completionPreviousTimeScale = Time.timeScale;
            completionPreviousFixedStep = Time.fixedDeltaTime;
            completionSlowMotionEndsAt = Time.realtimeSinceStartupAsDouble + CompletionSlowMotionDuration;
            IsCompletionSlowMotionActive = true;
            Time.timeScale = completionPreviousTimeScale * CompletionSlowMotionScale;
            // Preserve the normal real-time physics cadence while movement and effects slow down.
            Time.fixedDeltaTime = completionPreviousFixedStep * CompletionSlowMotionScale;
        }

        private void TickCompletionSlowMotion()
        {
            if (IsCompletionSlowMotionActive && Time.realtimeSinceStartupAsDouble >= completionSlowMotionEndsAt)
                CancelCompletionSlowMotion();
        }

        /// <summary>Release the completion clock before another sequence (such as player death) takes ownership.</summary>
        public void CancelCompletionSlowMotion()
        {
            if (!IsCompletionSlowMotionActive) return;
            IsCompletionSlowMotionActive = false;
            if (Mathf.Approximately(Time.timeScale, completionPreviousTimeScale * CompletionSlowMotionScale))
                Time.timeScale = completionPreviousTimeScale;
            if (Mathf.Abs(Time.fixedDeltaTime - completionPreviousFixedStep * CompletionSlowMotionScale) < .000001f)
                Time.fixedDeltaTime = completionPreviousFixedStep;
        }
    }
}
