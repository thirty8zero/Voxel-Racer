using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
        private OpeningScenery openingScenery;
        private (VoxelScenerySet set, int revision, VoxelDesertFoliageTuning foliage, int foliageRevision,
            float radius, float roadWidth, float horizonCoverage) openingSettings;

        private uint PrepareOpeningScenery(Camera camera, VoxelScenerySet set, float radius)
        {
            uint revision = road.PathRevision;
            bool planning = road.HasOpeningPlan;
            if (!planning && openingScenery == null)
                return revision;

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
            CollectHorizonTiles(wanted, tileFocus, coverage + TileSize, road.EvaluateOpeningTarget);
            samples.Capture(tileFocus, coverage + TileSize + 64, road.EvaluateOpeningTarget);
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
                for (float distance = focus - reach; distance <= focus + reach; distance += 4f)
                    points.Add(evaluate(distance).position);
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
