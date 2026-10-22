using UnityEngine;
using System.Collections.Generic;

namespace VoxelRacer
{
    /// <summary>Simple world-space health bar that faces the active camera.</summary>
    public sealed class VoxelEnemyHealthBar : MonoBehaviour
    {
        private const float BackgroundPadding = .08f;
        private const float EnemyOutlineThickness = .06f;
        private const float EnemyOutlineTransitionHealth = .5f;
        private static readonly Color EnemyOutlineYellow = new Color(1f, .92f, 0f);
        private Transform fill;
        private Renderer fillRenderer;
        private Material fillMaterial;
        private Material outlineMaterial;
        private float width;
        private Color fullColour;
        private Color emptyColour;
        private float criticalHealthPercent;
        private float criticalPulseSpeed;
        private float criticalPulseScale;
        private float healthPercent = 1f;
        private float fillHeight;
        private readonly List<Material> ownedMaterials = new();
        private readonly List<Renderer> barRenderers = new();
        private readonly List<Material> fadeMaterials = new();
        private const float VisibilityDistance = 150f;
        private const float VisibilityFadeSeconds = .4f;
        private Transform distanceTarget;
        public bool UsesDistanceVisibility { get; private set; }
        public float VisibilityAlpha { get; private set; } = 1f;

        public static VoxelEnemyHealthBar Create(Transform owner, VoxelEnemyVehicleTuning tuning, Transform player = null)
        {
            var bar = Create(owner, tuning.healthBarWidth, tuning.healthBarHeight, tuning.healthBarHeightOffset,
                tuning.healthBarFullColour, tuning.healthBarEmptyColour, tuning.criticalHealthPercent,
                tuning.criticalPulseSpeed, tuning.criticalPulseScale, outlined: true);
            if (player != null) bar.ConfigureDistanceVisibility(player);
            return bar;
        }

        private void ConfigureDistanceVisibility(Transform player)
        {
            distanceTarget = player;
            UsesDistanceVisibility = true;
            var lit = Resources.Load<Material>("Effects/HealthBarLitFade");
            var unlit = Resources.Load<Material>("Effects/HealthBarUnlitFade");
            foreach (var material in ownedMaterials)
                fadeMaterials.Add(new Material(material.shader.name.Contains("/Unlit") ? unlit : lit)
                    { name = material.name + " (Health Bar Fade)" });
            SetVisibility(0f);
        }

        private void TickVisibility(float deltaTime)
        {
            if (!UsesDistanceVisibility) return;
            Vector3 delta = distanceTarget != null ? transform.parent.position - distanceTarget.position : Vector3.positiveInfinity;
            delta.y = 0f;
            float target = delta.sqrMagnitude <= VisibilityDistance * VisibilityDistance ? 1f : 0f;
            float alpha = Mathf.MoveTowards(VisibilityAlpha, target, Mathf.Max(0f, deltaTime) / VisibilityFadeSeconds);
            if (alpha != VisibilityAlpha) SetVisibility(alpha);
        }

        private void SetVisibility(float alpha)
        {
            VisibilityAlpha = alpha;
            for (int i = 0; i < barRenderers.Count; i++)
            {
                var renderer = barRenderers[i];
                renderer.enabled = alpha > 0f;
                bool fading = alpha > 0f && alpha < 1f;
                if (fading)
                {
                    Color colour = ownedMaterials[i].GetColor("_BaseColor"); colour.a *= alpha;
                    fadeMaterials[i].SetColor("_BaseColor", colour);
                }
                renderer.sharedMaterial = fading ? fadeMaterials[i] : ownedMaterials[i];
            }
        }

        private void OnDisable() { if (UsesDistanceVisibility) SetVisibility(0f); }

        /// <summary>Creates the same billboard bar for non-vehicle damageable objects.</summary>
        public static VoxelEnemyHealthBar Create(Transform owner, float barWidth, float barHeight, float heightOffset,
            Color full, Color empty, float criticalPercent = 0.2f, float pulseSpeed = 7f, float pulseScale = 0.16f,
            bool outlined = false)
        {
            var root = new GameObject("Enemy Health Bar").AddComponent<VoxelEnemyHealthBar>();
            root.transform.SetParent(owner);
            root.transform.localPosition = Vector3.up * heightOffset;
            root.width = barWidth;
            root.fullColour = full;
            root.emptyColour = empty;
            root.criticalHealthPercent = criticalPercent;
            root.criticalPulseSpeed = pulseSpeed;
            root.criticalPulseScale = pulseScale;
            root.CreateVisual(barHeight);
            // Keep the black empty-health track in front of the frame so damage
            // never makes the exposed track look like remaining health.
            if (outlined)
                root.outlineMaterial = root.CreateBlock("Health Outline", root.transform, Vector3.forward * .04f,
                    new Vector3(root.width + BackgroundPadding + EnemyOutlineThickness * 2f,
                        root.fillHeight + BackgroundPadding + EnemyOutlineThickness * 2f, .04f),
                    EnemyOutlineYellow, true).GetComponent<Renderer>().sharedMaterial;
            root.SetHealth(1f);
            return root;
        }

        private void LateUpdate()
        {
            TickVisibility(VoxelPauseMenu.IsPaused ? 0f : Time.deltaTime);
            if (Camera.main != null)
                transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);

            float pulse = healthPercent <= criticalHealthPercent && healthPercent > 0f
                ? 1f + (Mathf.Sin(Time.time * criticalPulseSpeed) * 0.5f + 0.5f) * criticalPulseScale
                : 1f;
            fill.localScale = new Vector3(width * healthPercent * pulse, fillHeight * pulse, fill.localScale.z);
        }

        public void SetHealth(float value)
        {
            float normalized = Mathf.Clamp01(value);
            healthPercent = normalized;
            fill.localScale = new Vector3(width * normalized, fillHeight, fill.localScale.z);
            fill.localPosition = new Vector3(-(width - width * normalized) * 0.5f, 0f, -0.011f);
            Color colour = Color.Lerp(emptyColour, fullColour, normalized);
            // CreateBlock supplies a dedicated material for this bar. Reuse it
            // directly instead of asking Unity for another renderer.material clone.
            fillMaterial.color = colour;
            fillMaterial.SetColor("_BaseColor", colour);
            if (outlineMaterial != null)
            {
                // Keep the owned opaque material current even during a temporary spawn fade.
                Color outlineColour = Color.Lerp(Color.red, EnemyOutlineYellow,
                    Mathf.Clamp01(normalized / EnemyOutlineTransitionHealth));
                outlineMaterial.color = outlineColour;
                outlineMaterial.SetColor("_BaseColor", outlineColour);
            }
            if (UsesDistanceVisibility) SetVisibility(VisibilityAlpha);
        }

        private void CreateVisual(float height)
        {
            fillHeight = height;
            CreateBlock("Background", transform, Vector3.zero, new Vector3(width + BackgroundPadding, height + BackgroundPadding, 0.04f), Color.black);
            fill = CreateBlock("Fill", transform, Vector3.zero, new Vector3(width, height, 0.05f), fullColour).transform;
            fillRenderer = fill.GetComponent<Renderer>();
            fillMaterial = fillRenderer.sharedMaterial;
        }

        private GameObject CreateBlock(string blockName, Transform parent, Vector3 position, Vector3 scale, Color colour, bool unlit = false)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            if (Application.isPlaying) Destroy(block.GetComponent<BoxCollider>());
            else DestroyImmediate(block.GetComponent<BoxCollider>());
            var shader = unlit ? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") : null;
            var material = new Material(shader ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color = colour;
            material.SetColor("_BaseColor", colour);
            ownedMaterials.Add(material);
            var renderer = block.GetComponent<Renderer>();
            barRenderers.Add(renderer);
            renderer.sharedMaterial = material;
            if (unlit)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return block;
        }
        private void OnDestroy()
        {
            UsesDistanceVisibility = false;
            foreach (var material in ownedMaterials)
                if (material != null)
                {
                    if (Application.isPlaying) Destroy(material);
                    else DestroyImmediate(material);
                }
            ownedMaterials.Clear();
            foreach (var material in fadeMaterials)
                if (material != null)
                {
                    if (Application.isPlaying) Destroy(material);
                    else DestroyImmediate(material);
                }
            fadeMaterials.Clear();
        }
    }
}
