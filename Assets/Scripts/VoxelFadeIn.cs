using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace VoxelRacer
{
    /// <summary>Temporarily fades a generated voxel hierarchy in, then restores its opaque materials.</summary>
    public sealed class VoxelFadeIn : MonoBehaviour
    {
        [Min(0.01f)] public float duration = 1f;

        private readonly List<RendererState> renderers = new();
        private float elapsed;
        private bool isFading;
        private static readonly Dictionary<Material, SharedFadeMaterial> sharedFadeMaterials = new();
        private static readonly ProfilerMarker setupMarker = new ProfilerMarker("VoxelFadeIn.Setup");

        private sealed class SharedFadeMaterial
        {
            public Material material;
            public int users;
        }

        private struct RendererState
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
            using var profile = setupMarker.Auto();
            renderers.Clear();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty("_BaseColor"))
                    continue;

                Material fadeMaterial = AcquireFadeMaterial(material);
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

        private static Material AcquireFadeMaterial(Material source)
        {
            if (!sharedFadeMaterials.TryGetValue(source, out var shared))
            {
                var material = new Material(source) { name = source.name + " (Fade In)" };
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                shared = new SharedFadeMaterial { material = material };
                sharedFadeMaterials.Add(source, shared);
            }
            shared.users++;
            return shared.material;
        }

        private static void ReleaseFadeMaterial(Material source)
        {
            if (!sharedFadeMaterials.TryGetValue(source, out var shared) || --shared.users > 0) return;
            sharedFadeMaterials.Remove(source);
            if (shared.material != null)
            {
                if (Application.isPlaying) Destroy(shared.material);
                else DestroyImmediate(shared.material);
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
                ReleaseFadeMaterial(state.opaqueMaterial);
            }
            renderers.Clear();
        }

        private void OnDisable()
        {
            RestoreOpaqueMaterials();
            isFading = false;
        }

        private void OnDestroy() => RestoreOpaqueMaterials();
    }
}
