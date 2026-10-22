using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    public sealed partial class VoxelDistantScenery
    {
        private readonly Dictionary<Vector2Int, List<Prop>> horizonTiles = new();
        private readonly Dictionary<Vector2Int, Bounds> horizonTileBounds = new();
        private readonly HashSet<Vector2Int> horizonWanted = new();
        private readonly Dictionary<Mesh, List<Matrix4x4>> horizonBatches = new();
        private MaterialPropertyBlock horizonFoliageProperties;
        private VoxelDesertFoliageTuning horizonFoliage;
        private int horizonRevision, horizonSeed, horizonDensity;
        private uint horizonPathRevision;
        private float horizonCoverage, horizonRoadWidth, horizonFocus = float.NaN;
        public int CachedHorizonClumpCount { get; private set; }
        public int VisibleHorizonClumpCount { get; private set; }
        public int VisibleHorizonBatchCount { get; private set; }

        private void PrepareHorizonFoliage(Camera camera, VoxelScenerySet set)
        {
            var foliage = set.groundCover;
            if (!road.horizonRoadEnabled || foliage == null || !foliage.enabled ||
                !foliage.horizonGroundCoverEnabled || foliage.horizonClumpsPerTile <= 0 ||
                foliage.material == null || foliage.variants == null || foliage.variants.Length == 0)
            { ClearHorizonFoliage(); return; }

            // A strip along the actual path, rather than expanding the expensive
            // 3D scenery's square tile cache to the whole horizon radius.
            float coverage = road.horizonRoadDistance;
            float focus = Mathf.Floor(road.SceneryTrackDistance / TileSize) * TileSize;
            bool changed = foliage != horizonFoliage || foliage.Revision != horizonRevision ||
                coverage != horizonCoverage || road.roadWidth != horizonRoadWidth ||
                road.turnSeed != horizonSeed || road.PathRevision != horizonPathRevision || foliage.horizonClumpsPerTile != horizonDensity;
            if (changed) { horizonTiles.Clear(); horizonTileBounds.Clear(); }
            if (changed || focus != horizonFocus)
            {
                horizonWanted.Clear();
                const float halfStrip = 144f;
                // Include rear coverage for the finish/orbit camera, too.
                for (float distance = focus - coverage; distance <= focus + coverage + TileSize; distance += TileSize * .5f)
                {
                    var pose = road.Evaluate(distance);
                    for (float side = -halfStrip; side <= halfStrip; side += TileSize * .5f)
                    {
                        Vector3 position = pose.position + pose.right * side;
                        horizonWanted.Add(new Vector2Int(Mathf.FloorToInt(position.x / TileSize), Mathf.FloorToInt(position.z / TileSize)));
                    }
                }
                expired.Clear();
                foreach (var key in horizonTiles.Keys) if (!horizonWanted.Contains(key)) expired.Add(key);
                foreach (var key in expired) { horizonTiles.Remove(key); horizonTileBounds.Remove(key); }
                // A continuous road clearance check also covers bends at tile joins.
                roadSamples.Clear();
                for (float distance = focus - coverage - 64; distance <= focus + coverage + 64; distance += 4)
                    roadSamples.Add(road.Evaluate(distance).position);
                foreach (var key in horizonWanted)
                    if (!horizonTiles.ContainsKey(key)) horizonTiles.Add(key, BuildHorizonTile(key, foliage));
                horizonFoliage = foliage; horizonRevision = foliage.Revision; horizonCoverage = coverage;
                horizonRoadWidth = road.roadWidth; horizonSeed = road.turnSeed; horizonDensity = foliage.horizonClumpsPerTile; horizonFocus = focus;
                horizonPathRevision = road.PathRevision;
                CachedHorizonClumpCount = 0;
                foreach (var tile in horizonTiles.Values) CachedHorizonClumpCount += tile.Count;
                horizonFoliageProperties = foliage.DrawProperties();
                horizonFoliageProperties.SetFloat("_FadeInStart", foliage.fadeStartDistance);
                horizonFoliageProperties.SetFloat("_FadeInEnd", foliage.drawDistance);
                horizonFoliageProperties.SetFloat("_FadeStart", coverage * .65f);
                horizonFoliageProperties.SetFloat("_DrawDistance", coverage * .95f);
            }
            foreach (var batch in horizonBatches.Values) batch.Clear();
            VisibleHorizonClumpCount = VisibleHorizonBatchCount = 0;
            float near = Mathf.Max(0, foliage.fadeStartDistance - 8);
            float far = Mathf.Min(camera.farClipPlane, coverage * .95f);
            foreach (var tile in horizonTiles)
            {
                if (tile.Value.Count == 0 || !GeometryUtility.TestPlanesAABB(planes, horizonTileBounds[tile.Key])) continue;
                foreach (var prop in tile.Value)
                {
                    float distance = (prop.bounds.center - camera.transform.position).sqrMagnitude;
                    if (distance < near * near || distance > far * far || !GeometryUtility.TestPlanesAABB(planes, prop.bounds)) continue;
                    if (!horizonBatches.TryGetValue(prop.mesh, out var batch))
                        horizonBatches.Add(prop.mesh, batch = new List<Matrix4x4>());
                    batch.Add(prop.matrix); VisibleHorizonClumpCount++;
                }
            }
            foreach (var batch in horizonBatches.Values) if (batch.Count > 0) VisibleHorizonBatchCount++;
        }

        private List<Prop> BuildHorizonTile(Vector2Int key, VoxelDesertFoliageTuning foliage)
        {
            var result = new List<Prop>(foliage.horizonClumpsPerTile);
            var random = new System.Random(unchecked(key.x * 73856093 ^ key.y * 19349663 ^ foliage.seed));
            float Next() => (float)random.NextDouble();
            for (int attempt = 0; attempt < foliage.horizonClumpsPerTile * 5 && result.Count < foliage.horizonClumpsPerTile; attempt++)
            {
                var mesh = foliage.variants[random.Next(foliage.variants.Length)];
                if (mesh == null) continue;
                float scale = Mathf.Lerp(Mathf.Max(.1f, foliage.scaleRange.x), Mathf.Max(.1f, foliage.scaleRange.y), Next());
                float footprint = mesh.bounds.extents.magnitude * scale;
                Vector3 position = new((key.x + Next()) * TileSize, 0, (key.y + Next()) * TileSize);
                if (Next() > foliage.Density(position)) continue;
                float clearance = road.roadWidth * .5f + foliage.roadClearance + footprint + 2;
                bool blocked = false;
                foreach (var sample in roadSamples)
                    if ((sample - position).sqrMagnitude < clearance * clearance) { blocked = true; break; }
                if (blocked) continue;
                var matrix = Matrix4x4.TRS(position, Quaternion.Euler(0, Next() * 360, 0), Vector3.one * scale);
                result.Add(new Prop { mesh = mesh, matrix = matrix,
                    bounds = new Bounds(matrix.MultiplyPoint3x4(mesh.bounds.center), Vector3.one * mesh.bounds.size.magnitude * scale) });
            }
            var bounds = new Bounds(new Vector3((key.x + .5f) * TileSize, 0, (key.y + .5f) * TileSize), Vector3.zero);
            foreach (var prop in result) bounds.Encapsulate(prop.bounds);
            horizonTileBounds[key] = bounds;
            return result;
        }

        private void SubmitHorizonFoliage(Camera camera)
        {
            if (horizonFoliage == null || horizonFoliageProperties == null) return;
            foreach (var batch in horizonBatches)
            for (int start = 0; start < batch.Value.Count; start += 1023)
            {
                int count = Mathf.Min(1023, batch.Value.Count - start);
                batch.Value.CopyTo(start, drawBuffer, 0, count);
                if (SystemInfo.supportsInstancing && horizonFoliage.material.enableInstancing)
                    Graphics.DrawMeshInstanced(batch.Key, 0, horizonFoliage.material, drawBuffer, count, horizonFoliageProperties,
                        ShadowCastingMode.Off, false, gameObject.layer, camera, LightProbeUsage.Off);
                else for (int i = 0; i < count; i++)
                    Graphics.DrawMesh(batch.Key, drawBuffer[i], horizonFoliage.material, gameObject.layer, camera, 0,
                        horizonFoliageProperties, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
            }
        }

        private void ClearHorizonFoliage()
        {
            horizonTiles.Clear(); horizonTileBounds.Clear(); horizonWanted.Clear(); horizonBatches.Clear();
            horizonFoliage = null; horizonFocus = float.NaN;
            CachedHorizonClumpCount = VisibleHorizonClumpCount = VisibleHorizonBatchCount = 0;
        }
    }
}
