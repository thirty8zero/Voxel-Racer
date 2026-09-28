using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    /// <summary>One bounded mesh per spin, sampled at the actual rotating wheels, with no colliders.</summary>
    public sealed class VoxelOilTireMarks : MonoBehaviour
    {
        private const int MaximumSamples = 160;
        private readonly List<Transform> wheels = new();
        private readonly List<Vector3> vertices = new();
        private readonly List<int> triangles = new();
        private VoxelCarController car;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock properties;
        private static Material material;
        private int sampleCount;
        private float lifetime, lastSampleTime, lastEmissionTime;

        public void Configure(VoxelCarController player, float seconds)
        {
            car = player; lifetime = Mathf.Clamp(seconds, 1f, 20f);
            foreach (var child in player.GetComponentsInChildren<Transform>())
                if (child.name == "Voxel Wheel" && wheels.Count < 4) wheels.Add(child);
            mesh = new Mesh { name = "Oil spin tire ribbons" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Oil tire marks" };
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            properties = new MaterialPropertyBlock();
            lastEmissionTime = Time.time;
            lastSampleTime = -1f;
        }

        public void Sample()
        {
            if (car == null || car.IsDestroyed || sampleCount >= MaximumSamples || Time.time - lastSampleTime < .016f) return;
            int count = wheels.Count;
            if (count == 0) return;
            float height = car.TrackPath.Evaluate(car.CollisionTrackPosition.y).position.y + .028f;
            for (int i = 0; i < count; i++)
            {
                Vector3 point = wheels[i] != null ? wheels[i].position : car.transform.position;
                point.y = height;
                Vector3 across = car.transform.right * .11f;
                // Build each strip perpendicular to its travel, avoiding collapsed ribbons mid-spin.
                if (sampleCount > 0)
                {
                    // Previous sample's same wheel is exactly one full wheel-set behind this pair.
                    int previous = vertices.Count - count * 2;
                    var direction = point - (vertices[previous] + vertices[previous + 1]) * .5f;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > .00001f) across = Vector3.Cross(Vector3.up, direction.normalized) * .11f;
                }
                int index = vertices.Count;
                vertices.Add(point - across); vertices.Add(point + across);
                if (sampleCount > 0)
                {
                    int previous = index - count * 2;
                    triangles.Add(previous); triangles.Add(index); triangles.Add(previous + 1);
                    triangles.Add(previous + 1); triangles.Add(index); triangles.Add(index + 1);
                }
            }
            sampleCount++;
            lastSampleTime = lastEmissionTime = Time.time;
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }

        private void Update()
        {
            float age = Time.time - lastEmissionTime;
            if (age >= lifetime || (car != null && Vector3.Distance(car.transform.position, mesh.bounds.center) > 220f))
            { Destroy(gameObject); return; }
            float alpha = .85f * Mathf.Clamp01((lifetime - age) / Mathf.Min(2f, lifetime));
            properties.SetColor("_BaseColor", new Color(.008f, .009f, .012f, alpha));
            meshRenderer.SetPropertyBlock(properties);
        }

        private void OnDestroy()
        {
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
