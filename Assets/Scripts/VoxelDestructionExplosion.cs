using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Spawns the shared, mobile-conscious 2D pixel-art destruction burst.</summary>
    public static class VoxelDestructionExplosion
    {
        private const string ResourcePath = "Effects/VoxelDestructionExplosion2D";
        private const float MinimumScale = 0.1f;
        private static GameObject prefab;

        public static void Play(Vector3 position, float scale = 1f)
        {
            if (prefab == null)
                prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
                return;

            GameObject effect = Object.Instantiate(prefab, position, Quaternion.identity);
            effect.name = "Voxel Destruction Explosion";
            // Preserve the prefab root as the global baseline, then apply the
            // destroyed object's tuning multiplier on top of it.
            effect.transform.localScale *= Mathf.Max(MinimumScale, scale);
            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
                particles.Play(true);
            Camera.main?.GetComponent<VoxelCameraFollow>()?.ShakeFromObjectExplosion();
            Object.Destroy(effect, 2.25f);
        }
    }
}
