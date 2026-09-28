using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelCarController
    {
        public bool IsOilSpinning => oilSpinElapsed < oilSpinDuration;
        private float oilSpinElapsed, oilSpinDuration, oilSpinStartOffset, oilSpinYaw;
        private int oilSpinDirection;
        private VoxelOilTireMarks oilTireMarks;

        /// <summary>Oil affects only this player controller; no health, cash or mission penalties.</summary>
        public bool TryStartOilSpin(float duration, float markLifetime)
        {
            if (IsDestroyed || IsOilSpinning || !drivingEnabled || finishingRun || TrackPath == null ||
                VoxelMissionProgress.Active?.IsComplete == true || VoxelMissionProgress.Active?.IsFailed == true)
                return false;

            int nearestLane = Mathf.Clamp(Mathf.RoundToInt(CurrentLaneOffset / laneWidth + (laneCount - 1) * .5f), 0, laneCount - 1);
            oilSpinDirection = nearestLane == 0 ? 1 : nearestLane == laneCount - 1 ? -1 : Random.value < .5f ? -1 : 1;
            previousLane = nearestLane;
            currentLane = Mathf.Clamp(nearestLane + oilSpinDirection, 0, laneCount - 1);
            oilSpinStartOffset = CurrentLaneOffset;
            oilSpinDuration = Mathf.Max(.2f, duration);
            oilSpinElapsed = 0f;
            oilSpinYaw = 0f;
            visualYaw = visualRoll = 0f;
            if (Application.isPlaying)
            {
                oilTireMarks = new GameObject("Oil tire marks").AddComponent<VoxelOilTireMarks>();
                oilTireMarks.Configure(this, markLifetime);
                oilTireMarks.Sample();
            }
            return true;
        }

        // Kept deterministic so editor validation can step the same motion used in gameplay.
        internal void AdvanceOilSpin(float seconds)
        {
            oilSpinElapsed = Mathf.Min(oilSpinDuration, oilSpinElapsed + Mathf.Max(0f, seconds));
            float progress = oilSpinDuration > 0f ? oilSpinElapsed / oilSpinDuration : 1f;
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            CurrentLaneOffset = Mathf.Lerp(oilSpinStartOffset, TargetLaneOffset, eased);
            oilSpinYaw = progress >= 1f ? 0f : oilSpinDirection * 360f * eased;
            visualYaw = visualRoll = 0f;
        }

        private void UpdateOilTireMarks()
        {
            if (oilTireMarks == null) return;
            oilTireMarks.Sample();
            if (!IsOilSpinning) oilTireMarks = null;
        }
    }
}
