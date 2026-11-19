using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelDistantScenery
    {
        private sealed class OpeningScenery
        {
            public readonly uint SourceRevision;
            public readonly Dictionary<Vector2Int, Tile> Tiles = new();
            public readonly Dictionary<Vector2Int, Tile> HorizonTiles = new();
            public IEnumerator Build;

            public OpeningScenery(uint sourceRevision) => SourceRevision = sourceRevision;
        }

        private const double OpeningWorkMilliseconds = 1.5;
        private static readonly ProfilerMarker OpeningSceneryMarker = new("VoxelDistantScenery.PrepareOpeningScenery");
        private OpeningScenery openingScenery;
        internal bool IsOpeningSceneryReady => openingScenery == null;
        private (VoxelScenerySet set, int revision, VoxelDesertFoliageTuning foliage, int foliageRevision,
            float radius, float roadWidth, float horizonCoverage) openingSettings;

        private uint PrepareOpeningScenery(Camera camera, VoxelScenerySet set, float radius)
        {
            uint revision = road.PathRevision;
            bool planning = road.HasOpeningPlan;
            if (!planning && openingScenery == null)
                return revision;

            using var sample = OpeningSceneryMarker.Auto();
            var foliage = set.groundCover;
            float coverage = HasHorizonFoliage(foliage) ? road.horizonRoadDistance : 0f;
            var settings = (set, set.Revision, foliage, foliage != null ? foliage.Revision : 0,
                radius, road.roadWidth, coverage);
            if (openingScenery != null && (!settings.Equals(openingSettings) ||
                revision != openingScenery.SourceRevision + (planning ? 0u : 1u)))
                openingScenery = null;

            if (planning && openingScenery == null)
            {
                float focus = road.OpeningSceneryFocus;
                // The launch lane is straight. Account for forward travel and allow one
                // extra row on either side for the pullback from the carrier camera.
                Vector3 eyeAtOne = camera.transform.position + Vector3.forward * (focus - road.SceneryTrackDistance);
                var center = new Vector2Int(Mathf.FloorToInt(eyeAtOne.x / TileSize), Mathf.FloorToInt(eyeAtOne.z / TileSize));
                openingSettings = settings;
                openingScenery = new OpeningScenery(revision);
                openingScenery.Build = BuildOpeningScenery(openingScenery, set, radius, center, focus, coverage);
                // Keep the first countdown frame clear of tile work. The existing caches can draw this frame.
                return revision;
            }
            if (openingScenery == null)
                return revision;

            long began = Stopwatch.GetTimestamp();
            while (openingScenery.Build != null)
            {
                if (!openingScenery.Build.MoveNext())
                    openingScenery.Build = null;
                if (Stopwatch.GetTimestamp() - began >= Stopwatch.Frequency * (OpeningWorkMilliseconds / 1000))
                    break;
            }

            // On a slow frame, keep drawing the current cache until the time-sliced
            // replacement is ready. Never finish the pending tiles in one frame.
            if (planning || openingScenery.Build != null)
                return openingScenery.SourceRevision;

            tiles = openingScenery.Tiles;
            horizonTiles = openingScenery.HorizonTiles;
            openingScenery = null;
            lastPathRevision = horizonPathRevision = revision;
            lastTile = new Vector2Int(int.MinValue, int.MinValue);
            horizonFocus = float.NaN;
            return revision;
        }

        private IEnumerator BuildOpeningScenery(OpeningScenery opening, VoxelScenerySet set, float radius,
            Vector2Int center, float focus, float coverage)
        {
            var samples = new RoadSamples();
            samples.Capture(focus, radius * 2 + 64, road.EvaluateOpeningTarget);
            foreach (var key in SquareTiles(center, SceneryTileExtent(radius) + 1))
            {
                opening.Tiles.Add(key, BuildTile(key, set, samples));
                yield return null;
            }
            if (coverage <= 0f)
                yield break;

            float tileFocus = HorizonTileFocus(focus);
            var wanted = new HashSet<Vector2Int>();
            CollectHorizonTiles(wanted, tileFocus, coverage + TileSize, road.EvaluateOpeningTarget,
                set.groundCover.horizonGapFillClumpsPerTile > 0);
            samples.Capture(tileFocus, HorizonSampleReach(coverage + TileSize), road.EvaluateOpeningTarget);
            foreach (var key in wanted)
            {
                opening.HorizonTiles.Add(key, BuildHorizonTile(key, set.groundCover, samples));
                yield return null;
            }
        }

        private sealed class RoadSamples
        {
            private readonly List<Vector3> points = new();

            public void Capture(float focus, float reach, Func<float, VoxelTrackPose> evaluate)
            {
                points.Clear();
                // Tiles occupy world space, whereas focus is distance along the road.
                // On bends, a fixed arc-length window ends inside the tile cache and
                // leaves future road sections unprotected when cached plants are placed.
                Vector3 centre = evaluate(focus).position;
                points.Add(centre);
                float limit = Mathf.Max(4f, reach) * 20f;
                for (int direction = -1; direction <= 1; direction += 2)
                for (float offset = 4f; offset <= limit; offset += 4f)
                {
                    Vector3 position = evaluate(focus + offset * direction).position;
                    points.Add(position);
                    if ((position.z - centre.z) * direction >= reach) break;
                }
                points.Sort((a, b) => a.z.CompareTo(b.z));
            }

            public bool WithinClearance(Vector3 position, float radius)
            {
                // Only samples within radius along Z can affect placement, including on a bend.
                int first = 0, end = points.Count;
                while (first < end)
                {
                    int middle = (first + end) / 2;
                    if (position.z - points[middle].z > radius) first = middle + 1;
                    else end = middle;
                }
                float radiusSquared = radius * radius;
                for (int i = first; i < points.Count; i++)
                {
                    Vector3 delta = points[i] - position;
                    if (delta.z > radius) break;
                    if (delta.sqrMagnitude < radiusSquared) return true;
                }
                return false;
            }
        }
    }
}
