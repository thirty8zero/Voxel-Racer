using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class EndlessVoxelRoad
    {
        private const int CreekGapSections = 5;
        private float CreekChance => trackDefinition != null ? Mathf.Clamp(trackDefinition.creekCrossingChancePercent, 0f, 100f) : 0f;
        private float appliedCreekChance;
        private int firstCreekSection = 3;
        private int? creekLevelSeed;
        private bool creekGroundDirty = true;
        private Mesh creekGroundMesh;
        private MeshFilter creekGroundFilter;
        private readonly List<VoxelCreekCrossing> visibleCreeks = new();

        private void ExcludeStagedCreekSections()
        {
            if (visibleSegments.Count > 0)
                firstCreekSection = Mathf.Max(firstCreekSection, visibleSegments[^1].index + 1);
        }

        private VoxelCreekCrossing CreateCreekCrossing(Transform segment, RoadSegment path)
        {
            if (straightOpening || !ResolveCreekSection(path.index))
                return null;

            float shoulder = roadWidth * .5f + .55f;
            float sceneryEdge = groundWidth * .5f;
            // The visible ground can continue beyond the authored scenery area.
            float extent = horizonRoadEnabled ? Mathf.Max(sceneryEdge, horizonRoadDistance + segmentLength * 2f) : sceneryEdge;
            var random = CreekRandom(path.index);
            // Keep the original shape rolls in place while depth and water use their own seed.
            random.NextDouble(); // Spawn roll.
            random.NextDouble(); // Previous bed depth roll.
            random.NextDouble(); // Previous water level roll.

            // One seed per road instance keeps a crossing stable when its meshes stream back in.
            creekLevelSeed ??= System.Guid.NewGuid().GetHashCode();
            var levels = new System.Random(unchecked(creekLevelSeed.Value ^ (path.index + 1) * 73856093));

            var crossing = VoxelCreekCrossing.Create(segment, EvaluateSegment(path, segmentLength * .5f),
                extent, shoulder, segmentLength, trackDefinition, random, levels);
            creekGroundDirty = true;
            return crossing;
        }

        internal bool IsCreekSection(float distance)
        {
            if (straightOpening || CreekChance <= 0f)
                return false;
            EnsurePathCovers(distance + 10f);
            float relativeDistance = distance - pathSegments[0].startDistance;
            return relativeDistance >= 0f && ResolveCreekSection(Mathf.FloorToInt(relativeDistance / segmentLength));
        }

        private bool ResolveCreekSection(int index)
        {
            // Ahead-of-player spawns can query sections before their meshes stream in.
            // Resolve earlier rolls first so the five-section gap is independent of query order.
            int firstUnresolved = index;
            while (firstUnresolved > 0 && !pathSegments[firstUnresolved - 1].hasCreek.HasValue)
                firstUnresolved--;
            float chance = CreekChance;
            for (int i = firstUnresolved; i <= index; i++)
            {
                var path = pathSegments[i];
                path.hasCreek ??= i >= firstCreekSection && path.turnAngle == 0f &&
                    segmentLength >= 14f && groundWidth * .5f >= roadWidth * .5f + .55f + 4f &&
                    chance > 0f && CreekRandom(i).NextDouble() * 100d < chance && HasCreekClearance(i);
            }
            return pathSegments[index].hasCreek.Value;
        }

        private System.Random CreekRandom(int index)
        {
            int seed = Application.isPlaying && randomizeTurnSeedEachRun ? randomizedTurnSeedForRun : turnSeed;
            return new System.Random(unchecked(seed ^ (index + 1) * 73856093 ^ 42661));
        }

        private bool HasCreekClearance(int index)
        {
            // Road sections retain their decisions after their visual meshes are recycled.
            for (int previous = Mathf.Max(0, index - CreekGapSections); previous < index; previous++)
                if (pathSegments[previous].hasCreek == true)
                    return false;
            return true;
        }

        internal bool IsNearVisibleCreek(Vector3 position, float radius)
        {
            foreach (var creek in visibleCreeks)
                if (creek != null && creek.Overlaps(position, radius))
                    return true;
            return false;
        }

        private void RefreshCreekGround() => UpdateContinuousGroundPosition();

        private void RefreshCreekGround(Vector3 position)
        {
            if (creekGroundDirty)
            {
                visibleCreeks.Clear();
                foreach (var segment in visibleSegments)
                    if (segment.creek != null)
                        visibleCreeks.Add(segment.creek);
            }

            if (creekGroundFilter == null)
                creekGroundFilter = continuousGround.GetComponent<MeshFilter>();
            if (visibleCreeks.Count == 0)
            {
                continuousGround.position = position;
                if (creekGroundMesh != null && creekGroundFilter.sharedMesh == creekGroundMesh)
                    creekGroundFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                creekGroundDirty = false;
                return;
            }

            // The cutouts are fixed in world space. Snap the oversized ground box in XZ so the
            // mesh only needs rebuilding when a creek streams or the box moves to a new anchor.
            float step = Mathf.Min(32f, continuousGround.localScale.x * .05f);
            var anchor = new Vector3(Mathf.Round(position.x / step) * step, position.y,
                Mathf.Round(position.z / step) * step);
            if (!creekGroundDirty && continuousGround.position == anchor && creekGroundFilter.sharedMesh == creekGroundMesh)
                return;

            continuousGround.position = anchor;
            creekGroundMesh ??= new Mesh { name = "Ground with Creek Cutouts", hideFlags = HideFlags.DontSave };
            VoxelCreekCrossing.CutGround(creekGroundMesh, continuousGround, visibleCreeks);
            creekGroundFilter.sharedMesh = creekGroundMesh;
            creekGroundDirty = false;
        }

        private void ReleaseCreekGround()
        {
            if (creekGroundMesh == null) return;
            if (Application.isPlaying) Destroy(creekGroundMesh); else DestroyImmediate(creekGroundMesh);
        }
    }
}
