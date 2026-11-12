using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Owns the completion camera and moving carrier pickup.</summary>
    public sealed class VoxelRunFinish : MonoBehaviour
    {
        public VoxelCarController target;
        public bool HasFinished { get; private set; }
        public bool FinishCameraComplete => !waitingForBossCamera &&
            (finishCamera == null || !finishCamera.isActiveAndEnabled || finishCamera.FinishSequenceComplete);
        public bool ResultsReady => HasFinished && target != null && !target.IsDestroyed && FinishCameraComplete;

        private VoxelMissionProgress missionProgress;
        private VoxelCameraFollow finishCamera;
        private bool waitingForBossCamera;
        private VoxelEnemyCar heldBoss;
        private VoxelCarrierRecovery recovery;
        private float stopDuration;

        public void Configure(VoxelCarController player, VoxelMissionProgress mission)
        {
            target = player;
            missionProgress = mission;
            HasFinished = false;
            finishCamera = null;
            waitingForBossCamera = false; heldBoss = null;
            recovery = null;
            SetFinishMarkerVisible(false);
        }

        private void Update()
        {
            if (VoxelPauseMenu.IsPaused || VoxelLoadingScreen.IsLoading || target == null || target.IsDestroyed)
                return;

            if (recovery != null)
            {
                recovery.Advance(Time.deltaTime, FinishCameraComplete && !missionProgress.IsCompletionSlowMotionActive);
                if (recovery.TimedOut)
                {
                    recovery = null;
                    finishCamera.SetCarrierRecovery(null);
                    target.ReleaseCarrierRecovery();
                    ClearPlayerTravelLanes();
                    target.TrackPath.PreserveRoadForFinish();
                    target.BeginFinishStop(stopDuration);
                }
            }
            if (waitingForBossCamera)
            {
                if (finishCamera != null && finishCamera.isActiveAndEnabled && !finishCamera.BossDestructionFocusComplete) return;
                waitingForBossCamera = false; heldBoss = null;
                ClearPlayerTravelLanes();
                if (finishCamera != null) finishCamera.BeginFinishSequence(target);
                return;
            }
            if (HasFinished || missionProgress == null || !missionProgress.IsComplete)
                return;

            HasFinished = true;
            foreach (VoxelEnemyCar enemy in FindObjectsByType<VoxelEnemyCar>(FindObjectsSortMode.None))
                enemy.FinishMission();
            foreach (VoxelObstacleCar traffic in FindObjectsByType<VoxelObstacleCar>(FindObjectsSortMode.None))
                traffic.FinishMission();
            var boss = missionProgress.IsBossEncounter ? missionProgress.BossVehicle : null;
            var settings = boss != null ? boss.BossDefinition : null;
            Camera camera = Camera.main;
            finishCamera = camera != null ? camera.GetComponent<VoxelCameraFollow>() : null;
            waitingForBossCamera = settings != null && settings.focusCameraOnDestruction &&
                settings.destructionCameraDuration > 0 && finishCamera != null && finishCamera.isActiveAndEnabled;
            heldBoss = waitingForBossCamera ? boss : null;
            ClearPlayerTravelLanes();
            stopDuration = settings != null && settings.overrideFinishStopDuration ? settings.finishStopDuration : -1f;
            if (finishCamera != null && finishCamera.isActiveAndEnabled)
                recovery = VoxelCarrierRecovery.TryCreate(transform.parent != null ? transform.parent : transform, target);
            finishCamera?.SetCarrierRecovery(recovery);
            target.TrackPath?.PreserveRoadForFinish(recovery != null);
            if (recovery == null)
                target.BeginFinishStop(stopDuration);
            if (waitingForBossCamera)
                finishCamera.BeginBossDestructionFocus(target, boss, settings);
            else if (finishCamera != null)
                finishCamera.BeginFinishSequence(target);
        }

        /// <summary>Removes unavoidable hazards from the lane(s) the player occupies while coasting to the finish.</summary>
        private void ClearPlayerTravelLanes()
        {
            float laneTolerance = target.laneWidth * 0.45f;
            foreach (VoxelEnemyCar enemy in FindObjectsByType<VoxelEnemyCar>(FindObjectsSortMode.None))
                if (!enemy.IsLeavingMission) ClearIfInPlayerTravelLane(enemy, enemy.LaneOffset, laneTolerance);
            foreach (VoxelObstacleCar civilian in FindObjectsByType<VoxelObstacleCar>(FindObjectsSortMode.None))
                if (!civilian.IsLeavingMission) ClearIfInPlayerTravelLane(civilian, civilian.LaneOffset, laneTolerance);
            foreach (VoxelObstacle obstacle in FindObjectsByType<VoxelObstacle>(FindObjectsSortMode.None))
                ClearIfInPlayerTravelLane(obstacle, obstacle.LaneOffset, laneTolerance);
            foreach (VoxelPotholeObstacle pothole in FindObjectsByType<VoxelPotholeObstacle>(FindObjectsSortMode.None))
                ClearIfInPlayerTravelLane(pothole, pothole.LaneOffset, laneTolerance);
            foreach (VoxelFuelDrumObstacle drums in FindObjectsByType<VoxelFuelDrumObstacle>(FindObjectsSortMode.None))
                ClearIfInPlayerTravelLane(drums, drums.LaneOffset, laneTolerance);
            foreach (VoxelRoadMine mine in FindObjectsByType<VoxelRoadMine>(FindObjectsSortMode.None))
                ClearIfInPlayerTravelLane(mine, mine.LaneOffset, laneTolerance);
            foreach (VoxelOilSlickObstacle oil in FindObjectsByType<VoxelOilSlickObstacle>(FindObjectsSortMode.None))
                ClearIfInPlayerTravelLane(oil, oil.LaneOffset, laneTolerance);
        }

        private void ClearIfInPlayerTravelLane(MonoBehaviour objectToRemove, float objectLaneOffset, float laneTolerance)
        {
            if (heldBoss != null && objectToRemove == heldBoss) return;
            bool occupiesCurrentLane = Mathf.Abs(objectLaneOffset - target.CurrentLaneOffset) <= laneTolerance;
            bool occupiesDestinationLane = Mathf.Abs(objectLaneOffset - target.TargetLaneOffset) <= laneTolerance;
            if (!occupiesCurrentLane && !occupiesDestinationLane)
                return;

            objectToRemove.gameObject.SetActive(false);
            Destroy(objectToRemove.gameObject);
        }

        private void SetFinishMarkerVisible(bool visible)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
                renderer.enabled = visible;
        }
    }
}
