using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Positions the finish strip and tells the player to coast down after crossing it.</summary>
    public sealed class VoxelRunFinish : MonoBehaviour
    {
        public VoxelCarController target;
        public VoxelRoadTuning tuning;

        public float FinishDistance => startDistance + CurrentRunLength;
        public float CurrentRunLength { get; private set; }
        public bool HasFinished { get; private set; }
        public bool FinishCameraComplete => finishCamera == null || finishCamera.FinishSequenceComplete;

        private float startDistance;
        private EndlessVoxelRoad path;
        private float appliedMinimumLength;
        private float appliedMaximumLength;
        private VoxelCameraFollow finishCamera;

        public void Configure(VoxelCarController player, VoxelRoadTuning roadTuning,
            EndlessVoxelRoad trackPath, float runStartDistance)
        {
            target = player;
            tuning = roadTuning;
            path = trackPath;
            startDistance = runStartDistance;
            HasFinished = false;
            finishCamera = null;
            SelectRunLength();
        }

        private void Start()
        {
            if (target != null && tuning != null && CurrentRunLength <= 0f)
                SelectRunLength();
        }

        private void Update()
        {
            if (target == null || tuning == null || HasFinished)
                return;

            if (!Mathf.Approximately(appliedMinimumLength, tuning.minimumRunLength) ||
                !Mathf.Approximately(appliedMaximumLength, tuning.maximumRunLength))
                SelectRunLength();

            UpdateFinishTransform();
            if (target.TrackDistance < FinishDistance)
                return;

            HasFinished = true;
            Camera camera = Camera.main;
            finishCamera = camera != null ? camera.GetComponent<VoxelCameraFollow>() : null;
            if (finishCamera != null)
                finishCamera.BeginFinishSequence(target);
            target.BeginFinishStop();
        }

        private void SelectRunLength()
        {
            if (tuning == null)
                return;

            appliedMinimumLength = Mathf.Max(25f, tuning.minimumRunLength);
            appliedMaximumLength = Mathf.Max(25f, tuning.maximumRunLength);
            float minimum = Mathf.Min(appliedMinimumLength, appliedMaximumLength);
            float maximum = Mathf.Max(appliedMinimumLength, appliedMaximumLength);
            CurrentRunLength = Mathf.Approximately(minimum, maximum) ? minimum : Random.Range(minimum, maximum);

            UpdateFinishTransform();
        }

        private void UpdateFinishTransform()
        {
            if (path == null)
                return;
            VoxelTrackPose pose = path.Evaluate(FinishDistance);
            transform.position = pose.position;
            transform.rotation = pose.rotation;
        }
    }
}
