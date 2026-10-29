using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Cached damage membership: bodywork goes before the still-destructible wheels.</summary>
    internal sealed class VoxelVehicleDamageParts
    {
        private readonly HashSet<Transform> wheels = new();
        private readonly List<MeshRenderer> body = new();
        private readonly List<MeshRenderer> pieces = new();
        public int OriginalDestructibleCount { get; }
        private const float EnemyWreckRemainingFraction = .095f;

        public VoxelVehicleDamageParts(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.transform == root || renderer.GetComponentInParent<VoxelIndestructiblePart>() != null ||
                    renderer.GetComponentInParent<VoxelEnemyHealthBar>() != null) continue;
                pieces.Add(renderer);
                if (Active(renderer)) OriginalDestructibleCount++;
                bool wheel = false;
                for (var parent = renderer.transform; parent != null && parent != root; parent = parent.parent)
                    if (parent.name == VoxelVehicleDriveline.WheelName) { wheel = true; break; }
                if (wheel) wheels.Add(renderer.transform);
                else body.Add(renderer);
            }
        }

        public bool IsWheel(Transform part) => part != null && wheels.Contains(part);
        private static bool Active(MeshRenderer part) => part != null && part.enabled && part.gameObject.activeInHierarchy;
        public bool HasBody
        {
            get { foreach (var part in body) if (Active(part)) return true; return false; }
        }

        public void Sort(List<Transform> parts, Vector3 hitPoint) => parts.Sort((a, b) =>
        {
            int priority = IsWheel(a).CompareTo(IsWheel(b));
            return priority != 0 ? priority : (a.position - hitPoint).sqrMagnitude.CompareTo((b.position - hitPoint).sqrMagnitude);
        });

        /// <summary>Finish a non-boss enemy wreck without restoring damage or awarding extra combat points.</summary>
        public int StripEnemyWreck(Vector3 hitPoint, int maximumDebrisCount, System.Action<Transform> spawnDebris)
        {
            var remaining = new List<Transform>();
            foreach (var piece in pieces)
                if (Active(piece)) remaining.Add(piece.transform);
            int target = Mathf.FloorToInt(OriginalDestructibleCount * EnemyWreckRemainingFraction);
            int removeCount = Mathf.Max(0, remaining.Count - target);
            if (removeCount == 0) return 0;

            Sort(remaining, hitPoint);
            return DetachSorted(remaining, removeCount, maximumDebrisCount, spawnDebris);
        }

        public static int DetachSorted(List<Transform> remaining, int requestedCount, int maximumDebrisCount, System.Action<Transform> spawnDebris)
        {
            int removeCount = Mathf.Clamp(requestedCount, 0, remaining.Count);
            int debrisCount = Mathf.Clamp(maximumDebrisCount, 0, removeCount);
            int emitted = 0;
            for (int index = 0; index < removeCount; index++)
            {
                // Sample the whole removed area while keeping the existing physical-debris budget.
                if (emitted < debrisCount && index == (long)emitted * removeCount / debrisCount)
                {
                    spawnDebris(remaining[index]);
                    emitted++;
                }
                remaining[index].gameObject.SetActive(false);
            }
            return removeCount;
        }

        // A direct tyre shot still deals the normal vehicle health damage, but peels nearby
        // bodywork until it is stripped. Wheel pieces become eligible after the last body piece.
        public Transform PreferBody(Transform hit, Vector3 point)
        {
            if (!IsWheel(hit)) return hit;
            Transform closest = null; float distance = float.PositiveInfinity;
            foreach (var part in body)
            {
                if (!Active(part)) continue;
                float candidate = (part.transform.position - point).sqrMagnitude;
                if (candidate >= distance) continue;
                distance = candidate; closest = part.transform;
            }
            return closest != null ? closest : hit;
        }
    }
}
