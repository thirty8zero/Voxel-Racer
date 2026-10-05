using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Desert Ground Cover", fileName = "DesertGroundCover")]
    public sealed class VoxelDesertFoliageTuning : ScriptableObject
    {
        [Header("Rendering")]
        public bool enabled = true;
        public Material material;
        [Tooltip("Shared crossed-card meshes with atlas UVs, authored by the desert detail builder.")]
        public Mesh[] variants = System.Array.Empty<Mesh>();
        [Min(10)] public float drawDistance = 150;
        [Min(1)] public float fadeStartDistance = 110;
        [Header("Roadside Patches")]
        [Range(0, 400)] public int clumpsPerSegment = 180;
        [Min(1)] public float roadsideDistance = 38;
        [Min(0)] public float roadClearance = 1.2f;
        public Vector2 scaleRange = new(.75f, 1.35f);
        [Header("Distribution")]
        public int seed = 28571;
        [Min(4)] public float patchSize = 24;
        [Range(0, 1)] public float patchCoverage = .55f;
        [Range(0, 1)] public float patchContrast = .95f;
        [Range(0, 100)] public int distantClumpsPerTile = 28;
        public int Revision { get; private set; }

        public float Density(Vector3 position)
        {
            float size = Mathf.Max(4, patchSize);
            float noise = Mathf.PerlinNoise(position.x / size + seed * .013f, position.z / size + seed * .021f);
            float threshold = Mathf.Lerp(.7f, .25f, patchCoverage);
            float patch = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(threshold - .08f, threshold + .16f, noise));
            return Mathf.Lerp(1, patch, patchContrast);
        }
        public MaterialPropertyBlock DrawProperties()
        {
            var block = new MaterialPropertyBlock();
            block.SetFloat("_DrawDistance", Mathf.Max(10, drawDistance));
            block.SetFloat("_FadeStart", Mathf.Clamp(fadeStartDistance, 0, drawDistance - 1));
            return block;
        }
#if UNITY_EDITOR
        private void OnValidate() { Revision++; VoxelAssetSaveQueue.Request(this); }
#endif
    }
}
