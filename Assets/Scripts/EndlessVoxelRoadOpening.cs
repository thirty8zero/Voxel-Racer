using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class EndlessVoxelRoad
    {
        private const float StationaryStartOffset = 5f;
        private int openingTurnIndex = -1;
        private float openingTargetHeading;
        private Vector3 openingTargetPosition;
        private float openingTurnBlend;
        private int OpeningStripSlices => Mathf.Max(2, Mathf.CeilToInt(maximumTurnAngle / curveDegreesPerSlice));
        internal bool HasOpeningPlan => openingTurnIndex >= 0;
        internal float OpeningSceneryFocus { get; private set; }
        internal VoxelTrackPose EvaluateOpeningTarget(float distance) => Evaluate(distance, true);

        internal void PlanOpeningTurns(float launchDistance, float sceneryFocus)
        {
            if (!straightOpening || openingTurnIndex >= 0)
                return;

            BuildInitialRoad();
            // The stationary start sits 5 m behind the origin. Its fourth chunk
            // is the first one allowed to turn, 1.5 chunk lengths past the origin.
            float earliestTurn = launchDistance + segmentLength * 1.5f + StationaryStartOffset;
            openingTurnIndex = Mathf.Max(3, Mathf.CeilToInt((earliestTurn - pathSegments[0].startDistance) / segmentLength));
            OpeningSceneryFocus = sceneryFocus;
            openingTargetHeading = 0f;
            straightSegmentsSinceTurn = minimumStraightSegmentsBetweenTurns;
            for (int i = openingTurnIndex; i < pathSegments.Count; i++)
                PlanOpeningTurn(pathSegments[i]);
            EnsurePathCovers(pathSegments[0].startDistance + (openingTurnIndex + 1) * segmentLength);
        }

        private void PlanOpeningTurn(RoadSegment segment)
        {
            if (segment.index == openingTurnIndex)
                openingTargetPosition = segment.startPosition;
            segment.openingStartPosition = openingTargetPosition;
            segment.openingStartHeading = openingTargetHeading;
            segment.openingTurnAngle = ChooseTurnAngle(segment.index, openingTargetHeading);
            openingTargetPosition = EvaluateArc(openingTargetPosition, openingTargetHeading,
                segment.openingTurnAngle, segmentLength).position;
            openingTargetHeading += segment.openingTurnAngle;
        }

        internal void BlendOpeningTurns(float progress)
        {
            if (openingTurnIndex < 0)
                return;

            float blend = Mathf.SmoothStep(0f, 1f, progress);
            if (openingTurnBlend == blend)
                return;

            openingTurnBlend = blend;
            bool finished = blend >= 1f;
            bool hasCurve = false;
            for (int i = openingTurnIndex; i < pathSegments.Count; i++)
            {
                RoadSegment previous = pathSegments[i - 1];
                RoadSegment segment = pathSegments[i];
                VoxelTrackPose end = EvaluateSegment(previous, segmentLength);
                segment.startPosition = end.position;
                segment.startHeading = previous.startHeading + previous.turnAngle;
                segment.turnAngle = segment.openingTurnAngle * blend;
                hasCurve |= segment.turnAngle != 0f;
                if (segment.visual == null)
                {
                    segment.openingVisual = null;
                    continue;
                }

                if (segment.openingVisual == null && hasCurve)
                    segment.openingVisual = new OpeningSegmentVisual(segment.visual);
                segment.openingVisual?.Update(this, segment, finished);
                if (finished)
                    segment.openingVisual = null;
            }

            horizonOpeningShapeDirty |= hasCurve;
            if (!finished)
                return;

            straightOpening = false;
            openingTurnIndex = -1;
            if (hasCurve)
                PathRevision++;
        }

        private sealed class OpeningSegmentVisual
        {
            private readonly List<VoxelRoadStripMesh> strips = new();
            private readonly List<(Transform transform, Vector3 position, Quaternion rotation)> placements = new();
            private readonly VoxelDesertGroundCover groundCover;
            private readonly VoxelTrackPose[] frames;

            public OpeningSegmentVisual(Transform root)
            {
                foreach (Transform child in root)
                {
                    if (child.TryGetComponent<VoxelRoadStripMesh>(out var strip))
                        strips.Add(strip);
                    else if (child.TryGetComponent<VoxelDesertGroundCover>(out var cover))
                        groundCover = cover;
                    else
                        placements.Add((child, child.position, child.rotation));
                }
                frames = new VoxelTrackPose[strips[0].SliceCount + 1];
            }

            public void Update(EndlessVoxelRoad road, RoadSegment segment, bool finished)
            {
                float length = road.segmentLength;
                float start = segment.startDistance;
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = road.EvaluateSegment(segment, i * length / (frames.Length - 1));
                foreach (var strip in strips)
                    strip.UpdateOpeningGeometry(frames, start, length, finished);
                foreach (var placement in placements)
                {
                    var pose = road.EvaluateSegment(segment, placement.position.z - start);
                    placement.transform.SetPositionAndRotation(
                        pose.position + pose.right * placement.position.x + Vector3.up * placement.position.y,
                        pose.rotation * placement.rotation);
                }
                if (groundCover != null)
                    groundCover.UpdateOpeningGeometry(start, distance => road.EvaluateSegment(segment, distance), finished);
            }
        }
    }
}
