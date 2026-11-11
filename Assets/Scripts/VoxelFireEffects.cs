using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Small pooled fire bursts shared by player guns and roadside turret guns.</summary>
    public static class VoxelFireEffects
    {
        private const float MuzzleFireLifetime = 0.24f;
        private static readonly Queue<VoxelMuzzleFireInstance> MuzzlePool = new();
        private static readonly Queue<VoxelMuzzleFireInstance> MinigunMuzzlePool = new();
        private static ParticleSystem muzzlePrefab;
        private static Material minigunMuzzleMaterial;
        private static ParticleSystem boostExhaustPrefab;

        public static void PlayMuzzleFire(Transform muzzle, Vector3 direction, float forwardOffset = 0.06f)
            => PlayMuzzleFire(muzzle, direction, forwardOffset, false);

        internal static void PlayMinigunMuzzleFire(Transform muzzle, Vector3 direction)
            => PlayMuzzleFire(muzzle, direction, .025f, true);

        private static void PlayMuzzleFire(Transform muzzle, Vector3 direction, float forwardOffset, bool minigun)
        {
            if (!Application.isPlaying || muzzle == null)
                return;

            if (muzzlePrefab == null)
                muzzlePrefab = Resources.Load<ParticleSystem>("Effects/VoxelMuzzleFire");
            if (muzzlePrefab == null)
                return;

            var pool = minigun ? MinigunMuzzlePool : MuzzlePool;
            VoxelMuzzleFireInstance effect = null;
            while (pool.Count > 0 && effect == null)
                effect = pool.Dequeue();
            if (effect == null)
            {
                ParticleSystem particles = Object.Instantiate(muzzlePrefab);
                particles.name = minigun ? "Pooled Minigun Muzzle Fire" : "Pooled Voxel Muzzle Fire";
                if (minigun)
                    ConfigureMinigunFlash(particles);
                effect = particles.gameObject.AddComponent<VoxelMuzzleFireInstance>();
            }

            Vector3 fireDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            // Local simulation makes the very short muzzle fire follow a fast-moving
            // vehicle instead of being overtaken and appearing to travel backward.
            effect.transform.SetParent(muzzle, true);
            effect.transform.SetPositionAndRotation(muzzle.position + fireDirection * forwardOffset,
                Quaternion.LookRotation(fireDirection));
            // Pool entries can previously have been attached to another gun (or a
            // roadside turret). Explicitly preserve the prefab's world scale when
            // attaching to a moving muzzle so transform hierarchy changes cannot
            // accumulate scale or shear on long, turning tracks.
            effect.SetExpectedWorldScale(Vector3.one);
            effect.PlayFor(MuzzleFireLifetime, pool);
        }

        private static void ConfigureMinigunFlash(ParticleSystem particles)
        {
            var main = particles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(.075f, .155f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(7.8f, 10.8f);
            main.startColor = new Color(1f, .93f, .16f);

            var shape = particles.shape;
            shape.angle = 6f;
            shape.radius = .022f;
            var color = particles.colorOverLifetime;
            color.enabled = false;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.2f;
            if (minigunMuzzleMaterial == null)
                minigunMuzzleMaterial = Resources.Load<Material>("Effects/YellowVoxelImpactSpark");
            if (minigunMuzzleMaterial != null)
                renderer.sharedMaterial = minigunMuzzleMaterial;
        }

        /// <summary>Creates one locally simulated exhaust flame attached to the requested point.</summary>
        public static ParticleSystem CreateBoostExhaustFire(Transform vehicle, Vector3 worldPosition, Vector3 exhaustDirection, ParticleSystem overridePrefab = null)
        {
            if (vehicle == null)
                return null;
            if (boostExhaustPrefab == null && overridePrefab == null)
                boostExhaustPrefab = Resources.Load<ParticleSystem>("Effects/VoxelBoostExhaustFire");
            var prefab = overridePrefab != null ? overridePrefab : boostExhaustPrefab;
            if (prefab == null)
                return null;

            Vector3 direction = exhaustDirection.sqrMagnitude > 0.0001f
                ? exhaustDirection.normalized
                : -vehicle.forward;
            ParticleSystem effect = Object.Instantiate(prefab, worldPosition,
                Quaternion.LookRotation(direction, vehicle.up), vehicle);
            effect.name = "Boost Exhaust Fire";
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return effect;
        }

    }

    /// <summary>Returns a completed muzzle-flash particle system to the shared pool.</summary>
    public sealed class VoxelMuzzleFireInstance : MonoBehaviour
    {
        private ParticleSystem particles;
        private Queue<VoxelMuzzleFireInstance> pool;
        private float recycleAt;
        private Vector3 expectedWorldScale = Vector3.one;
#if UNITY_EDITOR
        private bool hasReportedScaleCorrection;
#endif

        /// <summary>Keeps a pooled effect visually at its prefab scale under any muzzle hierarchy.</summary>
        public void SetExpectedWorldScale(Vector3 value)
        {
            expectedWorldScale = value;
            MaintainWorldScale();
        }

        internal void PlayFor(float lifetime, Queue<VoxelMuzzleFireInstance> returnPool)
        {
            if (particles == null)
                particles = GetComponent<ParticleSystem>();
            pool = returnPool;
            gameObject.SetActive(true);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
            recycleAt = Time.time + lifetime;
        }

        private void Update()
        {
            if (Time.time < recycleAt)
                return;
            gameObject.SetActive(false);
            pool.Enqueue(this);
        }

        private void LateUpdate()
        {
            if (gameObject.activeInHierarchy)
                MaintainWorldScale();
        }

        private void MaintainWorldScale()
        {
            Transform parent = transform.parent;
            if (parent == null)
            {
                transform.localScale = expectedWorldScale;
                return;
            }

            Vector3 current = transform.lossyScale;
            bool needsCorrection = Mathf.Abs(current.x - expectedWorldScale.x) > 0.01f ||
                Mathf.Abs(current.y - expectedWorldScale.y) > 0.01f ||
                Mathf.Abs(current.z - expectedWorldScale.z) > 0.01f;
            if (!needsCorrection)
                return;

            Vector3 parentScale = parent.lossyScale;
            transform.localScale = new Vector3(
                expectedWorldScale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                expectedWorldScale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                expectedWorldScale.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
#if UNITY_EDITOR
            if (!hasReportedScaleCorrection)
            {
                hasReportedScaleCorrection = true;
                Debug.LogWarning($"Muzzle-fire scale corrected from {current} to {expectedWorldScale}.", this);
            }
#endif
        }
    }
}
