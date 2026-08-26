using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    /// <summary>A lightweight 2D mountain silhouette wrapped around the playable horizon.</summary>
    [ExecuteAlways]
    public sealed class VoxelHorizonMountains : MonoBehaviour
    {
        private const string MeshName = "Wrapped Horizon Mountain Card";
        private const int CardSegments = 96;
        private const int MountainPeaks = 24;

        public Transform target;
        public VoxelTrackDefinition trackDefinition;

        private float radius = 170f;
        private float verticalScale = 1f;
        private float verticalScaleMultiplier = 1f;
        private float baseHeight = -45f;
        private float minimumPeakHeight = 14f;
        private float maximumPeakHeight = 42f;
        private Color colour = new(0.20f, 0.08f, 0.07f);
        private int seed = 481;

        private float appliedRadius;
        private float appliedVerticalScale;
        private float appliedBaseHeight;
        private float appliedMinimumPeakHeight;
        private float appliedMaximumPeakHeight;
        private Color appliedColour;
        private int appliedSeed;

        public void Configure(Transform followTarget, VoxelTrackDefinition track,
            float scaleMultiplier = 1f)
        {
            target = followTarget;
            trackDefinition = track;
            verticalScaleMultiplier = Mathf.Max(0.1f, scaleMultiplier);
            verticalScale = verticalScaleMultiplier;
            ReadTrackSettings();
            Build();
            UpdatePosition();
        }

        private void Update()
        {
            if (trackDefinition != null && TrackSettingsChanged())
            {
                ReadTrackSettings();
                Build();
            }
            UpdatePosition();
        }

        private void ReadTrackSettings()
        {
            if (trackDefinition == null)
                return;
            radius = Mathf.Max(20f, Mathf.Min(trackDefinition.mountainDistance,
                trackDefinition.sunDistanceAhead - 5f));
            verticalScale = Mathf.Max(0.1f, trackDefinition.mountainScale) * verticalScaleMultiplier;
            baseHeight = trackDefinition.mountainBaseHeight;
            minimumPeakHeight = trackDefinition.minimumMountainPeakHeight;
            maximumPeakHeight = Mathf.Max(minimumPeakHeight, trackDefinition.maximumMountainPeakHeight);
            colour = trackDefinition.mountainColour;
            seed = trackDefinition.mountainSeed;
        }

        private bool TrackSettingsChanged()
        {
            float desiredRadius = Mathf.Max(20f, Mathf.Min(trackDefinition.mountainDistance,
                trackDefinition.sunDistanceAhead - 5f));
            return !Mathf.Approximately(appliedRadius, desiredRadius) ||
                !Mathf.Approximately(appliedVerticalScale,
                    Mathf.Max(0.1f, trackDefinition.mountainScale) * verticalScaleMultiplier) ||
                !Mathf.Approximately(appliedBaseHeight, trackDefinition.mountainBaseHeight) ||
                !Mathf.Approximately(appliedMinimumPeakHeight, trackDefinition.minimumMountainPeakHeight) ||
                !Mathf.Approximately(appliedMaximumPeakHeight, trackDefinition.maximumMountainPeakHeight) ||
                appliedColour != trackDefinition.mountainColour || appliedSeed != trackDefinition.mountainSeed;
        }

        private void Build()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter == null)
                filter = gameObject.AddComponent<MeshFilter>();
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = gameObject.AddComponent<MeshRenderer>();

            if (filter.sharedMesh != null && filter.sharedMesh.name == MeshName)
            {
                if (Application.isPlaying) Destroy(filter.sharedMesh); else DestroyImmediate(filter.sharedMesh);
            }
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.name == "Horizon Mountain Card")
            {
                if (Application.isPlaying) Destroy(renderer.sharedMaterial); else DestroyImmediate(renderer.sharedMaterial);
            }

            filter.sharedMesh = CreateMountainMesh();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = "Horizon Mountain Card", color = colour };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", colour);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;

            appliedRadius = radius;
            appliedVerticalScale = verticalScale;
            appliedBaseHeight = baseHeight;
            appliedMinimumPeakHeight = minimumPeakHeight;
            appliedMaximumPeakHeight = maximumPeakHeight;
            appliedColour = colour;
            appliedSeed = seed;
        }

        private Mesh CreateMountainMesh()
        {
            var random = new System.Random(seed);
            var peakHeights = new float[MountainPeaks];
            for (int peak = 0; peak < MountainPeaks; peak++)
                peakHeights[peak] = Mathf.Lerp(minimumPeakHeight, maximumPeakHeight, (float)random.NextDouble());

            var vertices = new Vector3[(CardSegments + 1) * 2];
            var triangles = new int[CardSegments * 6];
            for (int segment = 0; segment <= CardSegments; segment++)
            {
                float normalized = segment / (float)CardSegments;
                float peakPosition = normalized * MountainPeaks;
                int firstPeak = Mathf.FloorToInt(peakPosition) % MountainPeaks;
                int nextPeak = (firstPeak + 1) % MountainPeaks;
                float blend = peakPosition - Mathf.Floor(peakPosition);
                float peakHeight = Mathf.Lerp(peakHeights[firstPeak], peakHeights[nextPeak], blend);
                float angle = normalized * Mathf.PI * 2f;
                Vector3 radial = new(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
                int vertex = segment * 2;
                vertices[vertex] = radial + Vector3.up * (baseHeight * verticalScale);
                vertices[vertex + 1] = radial + Vector3.up * (peakHeight * verticalScale);

                if (segment == CardSegments)
                    continue;
                int triangle = segment * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 2;
            }

            var mesh = new Mesh { name = MeshName };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void UpdatePosition()
        {
            if (target == null)
                return;
            Vector3 position = target.position;
            position.y = 0f;
            transform.position = position;
            transform.rotation = Quaternion.identity;
        }
    }
}
