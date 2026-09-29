using System.Collections.Generic;
using UnityEngine;
namespace VoxelRacer
{
    /// <summary>Collider-free, nearest-surface missile queries for all traffic and boss models.</summary>
    public sealed class VoxelMissileTarget : MonoBehaviour
    {
        private static readonly List<VoxelMissileTarget> Active = new();
        private MeshRenderer[] pieces;
        private VoxelEnemyCar enemy;
        private VoxelObstacleCar traffic;
        private Bounds localBounds;
        private bool ready;
        public static void Register(GameObject car)
        {
            var target = car.GetComponent<VoxelMissileTarget>() ?? car.AddComponent<VoxelMissileTarget>();
            if (!Active.Contains(target)) Active.Add(target);
        }
        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);
        private void OnDestroy() => Active.Remove(this);
        private bool Alive => enemy != null ? enemy.CurrentHealth > 0 : traffic != null && traffic.CurrentHealth > 0;
        private void Prepare()
        {
            if (ready) return;
            enemy = GetComponent<VoxelEnemyCar>(); traffic = GetComponent<VoxelObstacleCar>();
            pieces = GetComponentsInChildren<MeshRenderer>(true);
            bool first = true;
            foreach (var r in pieces)
            {
                if (r.GetComponentInParent<VoxelEnemyHealthBar>() != null) continue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = r.localBounds.center + Vector3.Scale(r.localBounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (first) { localBounds = new Bounds(p, Vector3.zero); first = false; } else localBounds.Encapsulate(p);
                }
            }
            // Do not cache the boss's zero-scale entrance; it will be queried again as it grows.
            ready = !first && localBounds.size.sqrMagnitude > 1f && GetComponentInChildren<VoxelBossEntrance>()?.enabled != true;
        }
        public static bool Trace(Vector3 start, Vector3 direction, float length, out VoxelMissileTarget target,
            out Transform voxel, out Vector3 point, out float distance)
        {
            target = null; voxel = null; point = default; distance = length;
            foreach (var t in Active)
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                t.Prepare();
                if (!t.Alive || !Intersect(t.transform, t.localBounds, start, direction, length, out _)) continue;
                foreach (var r in t.pieces)
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.GetComponentInParent<VoxelEnemyHealthBar>() != null) continue;
                    if (!Intersect(r.transform, r.localBounds, start, direction, distance, out float d)) continue;
                    distance = d; point = start + direction * d; target = t; voxel = r.transform;
                }
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
            if (radius <= 0)
            {
                if (directTarget == null) return;
                directTarget.Prepare();
                if (directTarget.enemy != null) directTarget.enemy.TakeProjectileHit(directVoxel, damage, point, direction);
                else directTarget.traffic?.TakeProjectileHit(directVoxel, damage, point, direction);
                return;
            }
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var t = Active[i]; if (t == null || !t.isActiveAndEnabled) continue;
                t.Prepare(); if (!t.Alive) continue;
                if (t.enemy != null) t.enemy.TakeMissileBlast(point, radius, damage, direction, t.pieces);
                else t.traffic.TakeMissileBlast(point, radius, damage, direction, t.pieces);
            }
        }
    }
}
