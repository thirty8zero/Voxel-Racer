using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Grows spawned visuals around their own pivot without scaling a vehicle's driving root.</summary>
    public sealed class VoxelSpawnScaleIn : MonoBehaviour
    {
        [Min(.01f)] public float duration = 1f;
        private struct Visual
        {
            public Transform target, parent;
            public Vector3 position, scale;
        }
        private readonly List<Visual> visuals = new();
        private readonly List<MeshRenderer> renderers = new();
        private readonly HashSet<Transform> branches = new();
        private float elapsed;
        private bool growing;
        public float Growth { get; private set; } = 1f;

        public static void Play(GameObject spawned)
        {
            if (!Application.isPlaying || spawned == null) return;
            var effect = spawned.GetComponent<VoxelSpawnScaleIn>();
            if (effect == null) spawned.AddComponent<VoxelSpawnScaleIn>();
            else if (!effect.enabled) effect.enabled = true;
            else effect.Restart();
        }

        private void OnEnable() { if (Application.isPlaying) Restart(); }

        public void Restart()
        {
            Restore(); visuals.Clear(); branches.Clear();
            // The boss already owns its entrance scale animation.
            if (GetComponentInChildren<VoxelBossEntrance>(true) != null) return;
            if (GetComponent<MeshRenderer>() != null)
                branches.Add(transform); // Root-rendered static props, e.g. an oil slick.
            else
            {
                GetComponentsInChildren(true, renderers);
                foreach (var renderer in renderers)
                {
                    if (renderer.GetComponentInParent<VoxelEnemyHealthBar>() != null) continue;
                    Transform branch = renderer.transform;
                    while (branch.parent != null && branch.parent != transform) branch = branch.parent;
                    if (branch != transform) branches.Add(branch);
                }
            }
            foreach (var branch in branches)
                visuals.Add(new Visual { target = branch, parent = branch.parent,
                    position = branch.localPosition, scale = branch.localScale });
            elapsed = 0; growing = visuals.Count > 0;
            SetGrowth(0);
        }

        private void Update()
        {
            if (!growing || VoxelPauseMenu.IsPaused) return;
            Advance(Time.deltaTime);
        }

        private void Advance(float deltaTime)
        {
            elapsed += Mathf.Max(0, deltaTime);
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
            SetGrowth(Mathf.SmoothStep(0, 1, progress));
            if (progress >= 1) Restore();
        }

        private void SetGrowth(float factor)
        {
            Growth = factor;
            foreach (var visual in visuals)
            {
                // Destroyed or detached damage pieces belong to their debris controller now.
                if (visual.target == null || visual.target.parent != visual.parent) continue;
                visual.target.localScale = visual.scale * factor;
                if (visual.target != transform) visual.target.localPosition = visual.position * factor;
            }
        }

        private void Restore() { if (growing) SetGrowth(1); growing = false; }
        public void Complete() => Restore();
        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();
    }
}
