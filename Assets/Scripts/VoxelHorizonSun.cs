using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    /// <summary>A flat, stepped yellow disc behind the mountains, independent of vehicle heading.</summary>
    [ExecuteAlways]
    public sealed class VoxelHorizonSun : MonoBehaviour
    {
        private const string DiscMeshName = "Stepped Horizon Sun Disc";
        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        public Transform target;
        public VoxelTrackDefinition trackDefinition;
        [Min(10f)] public float distanceAhead = 220f;
        [Range(0f, 360f)] public float azimuthDegrees = 45f;
        public float horizontalOffset;
        public float horizonHeight = 14f;
        [Min(1f)] public float diameter = 24f;
        public Color colour = new(1f, .84f, .12f);

        private Mesh generatedMesh;
        private MeshRenderer discRenderer;
        private MaterialPropertyBlock properties;
        private Color appliedColour;

        public void Configure(Transform followTarget, VoxelTrackDefinition track)
        {
            target = followTarget;
            trackDefinition = track;
            ReadTrackSettings();
            Build();
            UpdatePosition();
        }

        public void Build()
        {
            // Replace the legacy oversized two-disc sun without touching source assets.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var oldDisc = transform.GetChild(i).gameObject;
                if (oldDisc.name != "Sun Outer Glow" && oldDisc.name != "Sun Core") continue;
                oldDisc.SetActive(false);
                if (Application.isPlaying) Destroy(oldDisc); else DestroyImmediate(oldDisc);
            }
            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            if (filter.sharedMesh == null || filter.sharedMesh.name != DiscMeshName)
                filter.sharedMesh = CreateDiscMesh();
            generatedMesh = filter.sharedMesh;
            discRenderer = GetComponent<MeshRenderer>();
            if (discRenderer == null) discRenderer = gameObject.AddComponent<MeshRenderer>();
            discRenderer.sharedMaterial = Resources.Load<Material>("Scenery/DesertHorizonSun");
            discRenderer.shadowCastingMode = ShadowCastingMode.Off;
            discRenderer.receiveShadows = false;
            discRenderer.lightProbeUsage = LightProbeUsage.Off;
            discRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            properties ??= new MaterialPropertyBlock();
            ApplyColour();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            ReadTrackSettings();
            if (discRenderer == null || generatedMesh == null) Build();
            if (colour != appliedColour) ApplyColour();
            UpdatePosition();
        }

        private void ReadTrackSettings()
        {
            if (trackDefinition == null) return;
            distanceAhead = Mathf.Max(trackDefinition.sunDistanceAhead, trackDefinition.mountainDistance + 5f);
            azimuthDegrees = trackDefinition.sunAzimuthDegrees;
            horizontalOffset = trackDefinition.sunHorizontalOffset;
            horizonHeight = trackDefinition.sunHorizonHeight;
            diameter = trackDefinition.sunDiameter;
            colour = trackDefinition.sunColour;
        }

        private void ApplyColour()
        {
            properties.SetColor(BaseColour, colour);
            discRenderer.SetPropertyBlock(properties);
            appliedColour = colour;
        }

        private void UpdatePosition()
        {
            if (target == null) return;
            Vector3 direction = Quaternion.Euler(0f, azimuthDegrees, 0f) * Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            Vector3 origin = target.position;
            origin.y = 0f; // Match the mountain ring's centre; bumps/tumbling do not move the sun vertically.
            transform.position = origin + direction * distanceAhead + right * horizontalOffset + Vector3.up * horizonHeight;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.localScale = new Vector3(Mathf.Max(1f, diameter), Mathf.Max(1f, diameter), 1f);
        }

        private static Mesh CreateDiscMesh()
        {
            const int rows = 16;
            var vertices = new Vector3[rows * 4];
            var triangles = new int[rows * 6];
            for (int row = 0; row < rows; row++)
            {
                float y = row + .5f - rows * .5f;
                // Quantize the outline into a pixel circle, using one quad per horizontal strip.
                float halfWidth = Mathf.Round(Mathf.Sqrt(rows * rows * .25f - y * y)) / rows;
                float bottom = row / (float)rows - .5f;
                float top = (row + 1f) / rows - .5f;
                int vertex = row * 4, triangle = row * 6;
                vertices[vertex] = new Vector3(-halfWidth, bottom, 0f);
                vertices[vertex + 1] = new Vector3(-halfWidth, top, 0f);
                vertices[vertex + 2] = new Vector3(halfWidth, top, 0f);
                vertices[vertex + 3] = new Vector3(halfWidth, bottom, 0f);
                triangles[triangle] = vertex; triangles[triangle + 1] = vertex + 1; triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex; triangles[triangle + 4] = vertex + 2; triangles[triangle + 5] = vertex + 3;
            }
            var mesh = new Mesh { name = DiscMeshName, hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (generatedMesh == null) return;
            if (Application.isPlaying) Destroy(generatedMesh); else DestroyImmediate(generatedMesh);
        }
    }
}
