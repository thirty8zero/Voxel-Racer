using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
namespace VoxelRacer
{
    /// <summary>Collider-free missile queries for vehicles, crates and individual fuel drums.</summary>
    public sealed class VoxelMissileTarget : MonoBehaviour
    {
        private static readonly List<VoxelMissileTarget> Active = new();
        private MeshRenderer[] pieces;
        private VoxelEnemyCar enemy;
        private VoxelObstacleCar traffic;
        private VoxelObstacle crate;
        private VoxelFuelDrumObstacle drums;
        private Bounds localBounds;
        private bool ready;
        private bool membershipCached;
        private int lastPrepareFrame = -1;
        private VoxelSpawnScaleIn spawnGrowth;
        private VoxelBossEntrance bossEntrance;
        private float preparedGrowth;
        private Vector3 preparedBossScale;
        private static readonly ProfilerMarker prepareMarker = new("VoxelMissileTarget.PrepareBounds");
        private static readonly ProfilerMarker traceMarker = new("VoxelMissileTarget.Trace");
        private static readonly ProfilerMarker blastMarker = new("VoxelMissileTarget.Blast");
        public static void Register(GameObject model)
        {
            var target = model.GetComponent<VoxelMissileTarget>() ?? model.AddComponent<VoxelMissileTarget>();
            if (!Active.Contains(target)) Active.Add(target);
        }
        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        // Spread the first envelope setup over spawning, rather than making the
        // first missile discover every visible vehicle's voxels in one frame.
        private void Start() => Prepare();
        private void OnDisable() => Active.Remove(this);
        private void OnDestroy() => Active.Remove(this);
        private bool Alive => enemy != null ? enemy.CurrentHealth > 0 : traffic != null ? traffic.CurrentHealth > 0 :
            crate != null ? crate.IsRoadHazard : drums != null && drums.IsRoadHazard;
        private void Prepare()
        {
            if (ready || lastPrepareFrame == Time.frameCount &&
                (spawnGrowth == null || spawnGrowth.Growth == preparedGrowth) &&
                (bossEntrance == null || bossEntrance.transform.localScale == preparedBossScale)) return;
            using var profile = prepareMarker.Auto();
            lastPrepareFrame = Time.frameCount;
            if (!membershipCached)
            {
                enemy = GetComponent<VoxelEnemyCar>(); traffic = GetComponent<VoxelObstacleCar>();
                crate = GetComponent<VoxelObstacle>(); drums = GetComponentInParent<VoxelFuelDrumObstacle>();
                var geometry = new List<MeshRenderer>();
                foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.GetComponentInParent<VoxelEnemyHealthBar>() == null) geometry.Add(renderer);
                pieces = geometry.ToArray();
                spawnGrowth = GetComponentInParent<VoxelSpawnScaleIn>();
                bossEntrance = GetComponentInChildren<VoxelBossEntrance>(true);
                membershipCached = true;
            }
            bool first = true;
            var worldToLocal = transform.worldToLocalMatrix;
            foreach (var r in pieces)
            {
                if (r == null) continue;
                // Exact transformed AABB: read the native bounds/transform once
                // per renderer instead of crossing into Unity for every corner.
                Bounds source = r.localBounds;
                Matrix4x4 matrix = worldToLocal * r.transform.localToWorldMatrix;
                Vector3 extents = source.extents;
                Vector3 x = matrix.MultiplyVector(new Vector3(extents.x, 0, 0));
                Vector3 y = matrix.MultiplyVector(new Vector3(0, extents.y, 0));
                Vector3 z = matrix.MultiplyVector(new Vector3(0, 0, extents.z));
                var bounds = new Bounds(matrix.MultiplyPoint3x4(source.center), new Vector3(
                    Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                    Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                    Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2f);
                if (first) { localBounds = bounds; first = false; } else localBounds.Encapsulate(bounds);
            }
            // Growing visuals retain live-sized envelopes, refreshed once per
            // frame, even when several missiles sweep multiple short segments.
            ready = !first && localBounds.size.sqrMagnitude > 1f &&
                (spawnGrowth == null || spawnGrowth.Growth >= 1f) && (bossEntrance == null || !bossEntrance.enabled);
            preparedGrowth = spawnGrowth != null ? spawnGrowth.Growth : 1f;
            preparedBossScale = bossEntrance != null ? bossEntrance.transform.localScale : Vector3.one;
        }
        public static bool Trace(Vector3 start, Vector3 direction, float length, out VoxelMissileTarget target,
            out Transform voxel, out Vector3 point, out float distance, float laneHalfWidth = 0f)
        {
            using var profile = traceMarker.Auto();
            target = null; voxel = null; point = default; distance = length;
            foreach (var t in Active)
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                t.Prepare();
                if (!t.Alive) continue;
                // Solid envelopes retain contact after damage opens holes. Only vehicles
                // widen to cover edge-running missiles; props keep their actual bounds.
                Bounds hitBounds = t.localBounds;
                Vector3 extents = hitBounds.extents;
                if (t.enemy != null || t.traffic != null)
                    extents.x = Mathf.Max(extents.x, laneHalfWidth / Mathf.Max(.001f, t.transform.lossyScale.x));
                hitBounds.extents = extents;
                if (!Intersect(t.transform, hitBounds, start, direction, distance, out float d)) continue;
                distance = d; target = t; voxel = null;
                Vector3 localHit = t.transform.InverseTransformPoint(start + direction * d);
                point = t.transform.TransformPoint(t.localBounds.ClosestPoint(localHit));

            }
            return target != null;
        }
        private static bool Intersect(Transform t, Bounds b, Vector3 start, Vector3 direction, float length, out float distance)
        {
            var origin = t.InverseTransformPoint(start);
            var localDirection = t.InverseTransformVector(direction).normalized;
            distance = 0;
            if (b.Contains(origin)) return true;
            if (!b.IntersectRay(new Ray(origin, localDirection), out float localDistance)) return false;
            distance = Vector3.Dot(t.TransformPoint(origin + localDirection * localDistance) - start, direction);
            return distance >= 0 && distance <= length;
        }
        public static void Blast(Vector3 point, float radius, float damage, Vector3 direction,
            VoxelMissileTarget directTarget = null, Transform directVoxel = null)
        {
            using var profile = blastMarker.Auto();
            if (radius <= 0)
            {
                if (directTarget == null) return;
                directTarget.Prepare();
                if (directTarget.enemy != null) directTarget.enemy.TakeProjectileHit(directVoxel, damage, point, direction);
                else if (directTarget.traffic != null) directTarget.traffic.TakeProjectileHit(directVoxel, damage, point, direction);
                else directTarget.DamageObstacle(point, 0f, damage, direction, true);
                return;
            }
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var t = Active[i]; if (t == null || !t.isActiveAndEnabled) continue;
                t.Prepare(); if (!t.Alive) continue;
                if (t.enemy != null) t.enemy.TakeMissileBlast(point, radius, damage, direction, t.pieces, t == directTarget);
                else if (t.traffic != null) t.traffic.TakeMissileBlast(point, radius, damage, direction, t.pieces, t == directTarget);
                else t.DamageObstacle(point, radius, damage, direction, t == directTarget);
            }
        }

        private void DamageObstacle(Vector3 point, float radius, float damage, Vector3 direction, bool directImpact)
        {
            if (crate != null) crate.TakeMissileBlast(point, radius, damage, direction, pieces, directImpact);
            else if (drums != null) drums.TakeMissileBlast(transform, point, radius, damage, direction, pieces, directImpact);
        }
    }
}
