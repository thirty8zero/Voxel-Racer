using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Persistent shared settings for the endless road and its terrain.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Road Tuning", fileName = "VoxelRoadTuning")]
    public sealed class VoxelRoadTuning : ScriptableObject
    {
        public int laneCount = 4;
        public float roadWidth = 12f;
        public float groundWidth = 80f;
        public int minimumCactiPerSegment = 3;
        public int maximumCactiPerSegment = 8;
        [Min(0.1f)] public float minimumCactusHeightScale = 0.75f;
        [Min(0.1f)] public float maximumCactusHeightScale = 1.5f;
        [Min(0.1f)] public float minimumCactusWidthScale = 0.8f;
        [Min(0.1f)] public float maximumCactusWidthScale = 1.25f;
        public float segmentLength = 30f;
        public int segmentCount = 8;
        public float recycleBehindDistance = 45f;

        [Range(0f, 1f)] public float turnChancePerSegment = 0.35f;
        [Min(1f)] public float minimumTurnAngle = 12f;
        [Min(1f)] public float maximumTurnAngle = 32f;
        [Min(0)] public int minimumStraightSegmentsBetweenTurns = 1;
        [Min(1f)] public float maximumTrackHeading = 65f;
        [Range(1f, 15f)] public float curveDegreesPerSlice = 5f;
        public int turnSeed = 173;

#if UNITY_EDITOR
        private bool roadRefreshQueued;
#endif

        public static VoxelRoadTuning Load() => Resources.Load<VoxelRoadTuning>("VoxelRoadTuning");

        public void ApplyTo(EndlessVoxelRoad road)
        {
            road.laneCount = laneCount;
            road.roadWidth = roadWidth;
            road.groundWidth = groundWidth;
            road.minimumCactiPerSegment = minimumCactiPerSegment;
            road.maximumCactiPerSegment = maximumCactiPerSegment;
            road.minimumCactusHeightScale = minimumCactusHeightScale;
            road.maximumCactusHeightScale = maximumCactusHeightScale;
            road.minimumCactusWidthScale = minimumCactusWidthScale;
            road.maximumCactusWidthScale = maximumCactusWidthScale;
            road.segmentLength = segmentLength;
            road.segmentCount = segmentCount;
            road.recycleBehindDistance = recycleBehindDistance;
            road.turnChancePerSegment = turnChancePerSegment;
            road.minimumTurnAngle = minimumTurnAngle;
            road.maximumTurnAngle = maximumTurnAngle;
            road.minimumStraightSegmentsBetweenTurns = minimumStraightSegmentsBetweenTurns;
            road.maximumTrackHeading = maximumTrackHeading;
            road.curveDegreesPerSlice = curveDegreesPerSlice;
            road.turnSeed = turnSeed;
        }

        public void CopyFrom(EndlessVoxelRoad road)
        {
            laneCount = road.laneCount;
            roadWidth = road.roadWidth;
            groundWidth = road.groundWidth;
            minimumCactiPerSegment = road.minimumCactiPerSegment;
            maximumCactiPerSegment = road.maximumCactiPerSegment;
            minimumCactusHeightScale = road.minimumCactusHeightScale;
            maximumCactusHeightScale = road.maximumCactusHeightScale;
            minimumCactusWidthScale = road.minimumCactusWidthScale;
            maximumCactusWidthScale = road.maximumCactusWidthScale;
            segmentLength = road.segmentLength;
            segmentCount = road.segmentCount;
            recycleBehindDistance = road.recycleBehindDistance;
            turnChancePerSegment = road.turnChancePerSegment;
            minimumTurnAngle = road.minimumTurnAngle;
            maximumTurnAngle = road.maximumTurnAngle;
            minimumStraightSegmentsBetweenTurns = road.minimumStraightSegmentsBetweenTurns;
            maximumTrackHeading = road.maximumTrackHeading;
            curveDegreesPerSlice = road.curveDegreesPerSlice;
            turnSeed = road.turnSeed;
        }

        private void OnValidate()
        {
            maximumCactiPerSegment = Mathf.Max(minimumCactiPerSegment, maximumCactiPerSegment);
            maximumCactusHeightScale = Mathf.Max(minimumCactusHeightScale, maximumCactusHeightScale);
            maximumCactusWidthScale = Mathf.Max(minimumCactusWidthScale, maximumCactusWidthScale);
            maximumTurnAngle = Mathf.Max(minimumTurnAngle, maximumTurnAngle);
            maximumTrackHeading = Mathf.Max(maximumTurnAngle, maximumTrackHeading);
#if UNITY_EDITOR
            VoxelAssetSaveQueue.Request(this);
            QueueRoadRefresh();
#endif
        }

#if UNITY_EDITOR
        private void QueueRoadRefresh()
        {
            if (roadRefreshQueued)
                return;

            roadRefreshQueued = true;
            UnityEditor.EditorApplication.delayCall += RefreshRoadsWhenEditorIsIdle;
        }

        private void RefreshRoadsWhenEditorIsIdle()
        {
            roadRefreshQueued = false;
            foreach (var road in FindObjectsByType<EndlessVoxelRoad>(FindObjectsSortMode.None))
                if (road.tuning == this)
                    road.SetTuning(this);
        }
#endif
    }
}
