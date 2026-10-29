using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelMissionProgress
    {
        public bool IsCompletionSlowMotionActive { get; private set; }
        private float completionPreviousTimeScale, completionPreviousFixedStep;
        private double completionSlowMotionEndsAt;
        private float completionAppliedScale;

        private void BeginCompletionSlowMotion()
        {
            if (!Application.isPlaying || IsCompletionSlowMotionActive || VoxelPlayerDeathScreen.IsShowing ||
                VoxelPauseMenu.IsPaused || Time.timeScale <= 0f) return;
            var boss = IsBossEncounter && missionBoss != null ? missionBoss.BossDefinition : null;
            bool bossOverride = boss != null && boss.overrideCompletionSlowMotion;
            float duration = bossOverride ? boss.completionSlowMotionDuration : Tuning != null ? Tuning.completionSlowMotionDuration : 1.5f;
            completionAppliedScale = Mathf.Clamp(bossOverride ? boss.completionSlowMotionTimeScale :
                Tuning != null ? Tuning.completionSlowMotionTimeScale : .25f, .01f, 1f);
            if (duration <= 0 || completionAppliedScale >= 1f) return;
            completionPreviousTimeScale = Time.timeScale;
            completionPreviousFixedStep = Time.fixedDeltaTime;
            completionSlowMotionEndsAt = Time.realtimeSinceStartupAsDouble + duration;
            IsCompletionSlowMotionActive = true;
            Time.timeScale = completionPreviousTimeScale * completionAppliedScale;
            // Preserve the normal real-time physics cadence while movement and effects slow down.
            Time.fixedDeltaTime = completionPreviousFixedStep * completionAppliedScale;
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
            if (Mathf.Approximately(Time.timeScale, completionPreviousTimeScale * completionAppliedScale))
                Time.timeScale = completionPreviousTimeScale;
            if (Mathf.Abs(Time.fixedDeltaTime - completionPreviousFixedStep * completionAppliedScale) < .000001f)
                Time.fixedDeltaTime = completionPreviousFixedStep;
        }
    }
}
