using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Spawns the shared, mobile-conscious 2D pixel-art destruction burst.</summary>
    public static class VoxelDestructionExplosion
    {
        private const string ResourcePath = "Effects/VoxelDestructionExplosion2D";
        private const float MinimumScale = 0.1f;
        private static GameObject prefab;

        public static void Play(Vector3 position, float scale = 1f, bool shakeCamera = true)
        {
            Spawn(position, scale, shakeCamera, true);
        }

        /// <summary>A local burst with an absolute size, independent of the shared prefab's global baseline.</summary>
        public static void PlayAtWorldScale(Vector3 position, float scale, bool shakeCamera = false)
        {
            Spawn(position, scale, shakeCamera, false);
        }

        private static void Spawn(Vector3 position, float scale, bool shakeCamera, bool inheritPrefabScale)
        {
            if (prefab == null)
                prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
                return;

            GameObject effect = Object.Instantiate(prefab, position, Quaternion.identity);
            effect.name = "Voxel Destruction Explosion";
            var systems = effect.GetComponentsInChildren<ParticleSystem>(true);
            if (!inheritPrefabScale)
                foreach (var particles in systems) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // Preserve the prefab root as the global baseline, then apply the
            // destroyed object's tuning multiplier on top of it.
            effect.transform.localScale = (inheritPrefabScale ? effect.transform.localScale : Vector3.one) * Mathf.Max(MinimumScale, scale);
            foreach (ParticleSystem particles in systems)
                particles.Play(true);
            if (shakeCamera)
                Camera.main?.GetComponent<VoxelCameraFollow>()?.ShakeFromObjectExplosion();
            Object.Destroy(effect, 2.25f);
        }
    }
}
