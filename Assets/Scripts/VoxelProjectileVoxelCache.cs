using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Immutable model membership with live transforms/visibility for bullet queries.</summary>
    internal sealed class VoxelProjectileVoxelCache
    {
        internal readonly struct Piece
        {
            public readonly MeshRenderer Renderer;
            public readonly Transform Transform;
            public readonly Bounds Bounds;
            public bool Active => Renderer != null && Transform != null && Transform.gameObject.activeInHierarchy;

            public Piece(MeshRenderer renderer, Bounds bounds)
            {
                Renderer = renderer;
                Transform = renderer.transform;
                Bounds = bounds;
            }
        }

        public readonly Piece[] Pieces;
        private readonly Transform root;
        private readonly float localRadius;

        public VoxelProjectileVoxelCache(Transform root, Func<MeshRenderer, bool> include, bool requireMesh = false)
        {
            this.root = root;
            var pieces = new List<Piece>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (include != null && !include(renderer)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (requireMesh && (filter == null || filter.sharedMesh == null)) continue;
                Bounds bounds = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : renderer.localBounds;
                pieces.Add(new Piece(renderer, bounds));

                // Triangle inequality over every pivot gives a conservative sphere
                // even while wheels, radar dishes and boss doors rotate. Use the
                // final entrance scale so caching at zero scale cannot hide a boss.
                float radius = (Abs(bounds.center) + bounds.extents).magnitude;
                for (var part = renderer.transform; part != root && part != null; part = part.parent)
                {
                    var entrance = part.GetComponent<VoxelBossEntrance>();
                    Vector3 scale = entrance != null ? entrance.FullScale : part.localScale;
                    radius = radius * MaxScale(scale) + part.localPosition.magnitude;
                }
                localRadius = Mathf.Max(localRadius, radius);
            }
            Pieces = pieces.ToArray();
        }

        // Exact traffic checks use a finite segment/sphere rejection before
        // touching individual voxels. The sphere is only a broad phase.
        public bool MayIntersect(Vector3 start, Vector3 direction, float length)
        {
            Vector3 offset = root.position - start;
            float along = Mathf.Clamp(Vector3.Dot(offset, direction), 0f, length);
            float radius = WorldRadius;
            return (offset - direction * along).sqrMagnitude <= radius * radius;
        }

        /// <summary>Nearest visible mesh surface within a finite world-space shot segment.</summary>
        public bool TryFindHit(Vector3 start, Vector3 direction, float length, out Vector3 point, out float distance)
        {
            point = default; distance = float.PositiveInfinity;
            if (length <= 0 || direction.sqrMagnitude < .000001f) return false;
            direction.Normalize();
            if (!MayIntersect(start, direction, length)) return false;
            foreach (var piece in Pieces)
            {
                if (!piece.Active || !piece.Renderer.enabled) continue;
                Vector3 localStart = piece.Transform.InverseTransformPoint(start);
                Vector3 localDirection = piece.Transform.InverseTransformVector(direction).normalized;
                float entry = 0;
                if (!piece.Bounds.Contains(localStart) && !piece.Bounds.IntersectRay(new Ray(localStart, localDirection), out entry)) continue;
                Vector3 hit = piece.Transform.TransformPoint(localStart + localDirection * entry);
                float along = Vector3.Dot(hit - start, direction);
                if (along < 0 || along > length || along >= distance) continue;
                point = hit; distance = along;
            }
            return !float.IsPositiveInfinity(distance);
        }

        // Rear-surface selection intentionally allows vertical peeling. Reject
        // only on the same forward/lateral axes used by the original selector.
        public bool MayReachSurface(Vector3 start, Vector3 direction, float length, float halfWidth)
        {
            float radius = WorldRadius;
            Vector3 offset = root.position - start;
            float along = Vector3.Dot(offset, direction);
            if (along < -radius || along > length + radius) return false;
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            return Mathf.Abs(Vector3.Dot(offset, right)) <= halfWidth + radius;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        private static float MaxScale(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));

        private float WorldRadius
        {
            get
            {
                // Frobenius norm bounds stretch even beneath a rotated,
                // non-uniformly scaled parent (lossyScale can underestimate shear).
                var m = root.localToWorldMatrix;
                float stretch = Mathf.Sqrt(m.m00 * m.m00 + m.m01 * m.m01 + m.m02 * m.m02 +
                    m.m10 * m.m10 + m.m11 * m.m11 + m.m12 * m.m12 +
                    m.m20 * m.m20 + m.m21 * m.m21 + m.m22 * m.m22);
                return localRadius * stretch;
            }
        }
    }
}
