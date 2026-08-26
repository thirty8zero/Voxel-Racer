using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Temporarily fades a generated voxel hierarchy in, then restores its opaque materials.</summary>
    public sealed class VoxelFadeIn : MonoBehaviour
    {
        [Min(0.01f)] public float duration = 1f;

        private readonly List<RendererState> renderers = new();
        private float elapsed;
        private bool isFading;

        private sealed class RendererState
        {
            public MeshRenderer renderer;
            public Color baseColor;
            public MaterialPropertyBlock propertyBlock;
            public MaterialPropertyBlock opaquePropertyBlock;
            public Material opaqueMaterial;
            public Material fadeMaterial;
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                Restart();
        }

        private void Start()
        {
            if (Application.isPlaying && !isFading)
                Restart();
        }

        public void Restart()
        {
            if (!Application.isPlaying)
                return;

            RestoreOpaqueMaterials();
            CacheRenderers();
            elapsed = 0f;
            isFading = renderers.Count > 0;
            SetAlpha(0f);
        }

        private void Update()
        {
            if (!isFading)
                return;

            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            SetAlpha(alpha);
            if (alpha >= 1f)
            {
                RestoreOpaqueMaterials();
                isFading = false;
            }
        }

        private void CacheRenderers()
        {
            renderers.Clear();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty("_BaseColor"))
                    continue;

                Material fadeMaterial = new Material(material) { name = material.name + " (Fade In)" };
                fadeMaterial.SetFloat("_Surface", 1f);
                fadeMaterial.SetFloat("_Blend", 0f);
                fadeMaterial.SetFloat("_ZWrite", 0f);
                fadeMaterial.SetOverrideTag("RenderType", "Transparent");
                fadeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                fadeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                renderer.sharedMaterial = fadeMaterial;

                var existingProperties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(existingProperties);
                Color configuredColour = existingProperties.GetColor("_BaseColor");
                if (configuredColour.a <= 0f)
                    configuredColour = material.GetColor("_BaseColor");

                renderers.Add(new RendererState
                {
                    renderer = renderer,
                    baseColor = configuredColour,
                    propertyBlock = new MaterialPropertyBlock(),
                    opaquePropertyBlock = existingProperties,
                    opaqueMaterial = material,
                    fadeMaterial = fadeMaterial
                });
            }
        }

        private void SetAlpha(float alpha)
        {
            foreach (var state in renderers)
            {
                if (state.renderer == null)
                    continue;

                state.renderer.GetPropertyBlock(state.propertyBlock);
                Color colour = state.baseColor;
                colour.a = alpha;
                state.propertyBlock.SetColor("_BaseColor", colour);
                state.renderer.SetPropertyBlock(state.propertyBlock);
            }
        }

        private void RestoreOpaqueMaterials()
        {
            foreach (var state in renderers)
            {
                if (state.renderer != null)
                {
                    state.renderer.sharedMaterial = state.opaqueMaterial;
                    state.renderer.SetPropertyBlock(state.opaquePropertyBlock);
                }
                if (state.fadeMaterial != null)
                    Destroy(state.fadeMaterial);
            }
            renderers.Clear();
        }

        private void OnDestroy() => RestoreOpaqueMaterials();
    }
}
