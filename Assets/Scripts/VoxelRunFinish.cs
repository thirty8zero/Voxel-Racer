using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Waits for mission completion, then tells the player to coast into the end sequence.</summary>
    public sealed class VoxelRunFinish : MonoBehaviour
    {
        public VoxelCarController target;
        public bool HasFinished { get; private set; }
        public bool FinishCameraComplete => finishCamera == null || finishCamera.FinishSequenceComplete;

        private VoxelMissionProgress missionProgress;
        private VoxelCameraFollow finishCamera;

        public void Configure(VoxelCarController player, VoxelMissionProgress mission)
        {
            target = player;
            missionProgress = mission;
            HasFinished = false;
            finishCamera = null;
            SetFinishMarkerVisible(false);
        }

        private void Update()
        {
            if (target == null || target.IsDestroyed || HasFinished || missionProgress == null || !missionProgress.IsComplete)
                return;

            HasFinished = true;
            Camera camera = Camera.main;
            finishCamera = camera != null ? camera.GetComponent<VoxelCameraFollow>() : null;
            if (finishCamera != null)
                finishCamera.BeginFinishSequence(target);
            target.BeginFinishStop();
        }

        private void SetFinishMarkerVisible(bool visible)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
                renderer.enabled = visible;
        }
    }
}
