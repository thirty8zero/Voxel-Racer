using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Simple world-space health bar that faces the active camera.</summary>
    public sealed class VoxelEnemyHealthBar : MonoBehaviour
    {
        private Transform fill;
        private Renderer fillRenderer;
        private float width;
        private Color fullColour;
        private Color emptyColour;
        private float criticalHealthPercent;
        private float criticalPulseSpeed;
        private float criticalPulseScale;
        private float healthPercent = 1f;
        private float fillHeight;

        public static VoxelEnemyHealthBar Create(Transform owner, VoxelEnemyVehicleTuning tuning)
        {
            var root = new GameObject("Enemy Health Bar").AddComponent<VoxelEnemyHealthBar>();
            root.transform.SetParent(owner);
            root.transform.localPosition = Vector3.up * tuning.healthBarHeightOffset;
            root.width = tuning.healthBarWidth;
            root.fullColour = tuning.healthBarFullColour;
            root.emptyColour = tuning.healthBarEmptyColour;
            root.criticalHealthPercent = tuning.criticalHealthPercent;
            root.criticalPulseSpeed = tuning.criticalPulseSpeed;
            root.criticalPulseScale = tuning.criticalPulseScale;
            root.CreateVisual(tuning.healthBarHeight);
            root.SetHealth(1f);
            return root;
        }

        private void LateUpdate()
        {
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
            fillRenderer.material.color = colour;
            fillRenderer.material.SetColor("_BaseColor", colour);
        }

        private void CreateVisual(float height)
        {
            fillHeight = height;
            CreateBlock("Background", transform, Vector3.zero, new Vector3(width + 0.08f, height + 0.08f, 0.04f), Color.black);
            fill = CreateBlock("Fill", transform, Vector3.zero, new Vector3(width, height, 0.05f), fullColour).transform;
            fillRenderer = fill.GetComponent<Renderer>();
        }

        private static GameObject CreateBlock(string blockName, Transform parent, Vector3 position, Vector3 scale, Color colour)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            Destroy(block.GetComponent<BoxCollider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color = colour;
            material.SetColor("_BaseColor", colour);
            block.GetComponent<Renderer>().material = material;
            return block;
        }
    }
}
