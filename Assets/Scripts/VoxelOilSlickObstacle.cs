using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    /// <summary>A non-solid, player-only hazard. Traffic and projectiles pass through it.</summary>
    public sealed class VoxelOilSlickObstacle : MonoBehaviour
    {
        public float LaneOffset { get; private set; }
        public float TrackDistance { get; private set; }
        public bool HasTriggered { get; private set; }
        private VoxelCarController target;
        private VoxelStaticObstacleDefinition definition;
        private Vector2 previousPlayer;
        private float halfWidth;
        private Mesh mesh;
        private static Material oilMaterial, sheenMaterial;

        public void Configure(VoxelCarController player, EndlessVoxelRoad road, VoxelStaticObstacleDefinition settings,
            float distance, float offset, float laneWidth)
        {
            target = player; definition = settings; TrackDistance = distance; LaneOffset = offset;
            halfWidth = laneWidth * .40f;
            previousPlayer = player != null ? player.CollisionTrackPosition : Vector2.zero;
            var pose = road.Evaluate(distance);
            transform.SetPositionAndRotation(pose.position + pose.right * offset + Vector3.up * .022f, pose.rotation);
            BuildVisuals();
        }

        private void LateUpdate()
        {
            if (target == null) { Destroy(gameObject); return; }
            CheckPlayerContact();
            if (TrackDistance < target.CollisionTrackPosition.y - 30f) Destroy(gameObject);
        }

        internal void CheckPlayerContact()
        {
            var current = target.CollisionTrackPosition;
            var center = new Vector2(LaneOffset, TrackDistance);
            if (!HasTriggered && VoxelVehicleCollision.Sweep(previousPlayer - center, current - center,
                new Vector2(halfWidth + .3f, 2.1f), out _) &&
                target.TryStartOilSpin(definition != null ? definition.oilSpinDuration : 1.1f,
                    definition != null ? definition.oilTireMarkLifetime : 8f))
                HasTriggered = true;
            previousPlayer = current;
        }

        private void BuildVisuals()
        {
            var vertices = new List<Vector3>();
            var oil = new List<int>();
            var sheen = new List<int>();
            AddBlob(vertices, oil, Vector3.zero, halfWidth, 1.55f, 32, .16f);
            AddBlob(vertices, oil, new Vector3(halfWidth * .75f, 0f, .9f), halfWidth * .3f, .42f, 12, .13f);
            AddBlob(vertices, oil, new Vector3(-halfWidth * .70f, 0f, -.95f), halfWidth * .23f, .32f, 12, .14f);
            // Muted, irregular reflections distinguish oil from the existing matte potholes.
            AddBlob(vertices, sheen, new Vector3(-halfWidth * .27f, .002f, .2f), halfWidth * .13f, .75f, 14, .17f);
            AddBlob(vertices, sheen, new Vector3(halfWidth * .28f, .002f, -.3f), halfWidth * .09f, .46f, 12, .12f);
            mesh = new Mesh { name = "Oil slick blob" };
            mesh.SetVertices(vertices); mesh.subMeshCount = 2;
            mesh.SetTriangles(oil, 0); mesh.SetTriangles(sheen, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            if (oilMaterial == null) oilMaterial = Material("Oil black", new Color(.012f, .014f, .018f), .82f);
            if (sheenMaterial == null) sheenMaterial = Material("Oil sheen", new Color(.045f, .05f, .065f), .9f);
            renderer.sharedMaterials = new[] { oilMaterial, sheenMaterial };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Material Material(string name, Color color, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            material.SetFloat("_Smoothness", smoothness);
            // Keep the puddle black under bright sky probes; authored streaks carry its oily sheen.
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            return material;
        }

        private static void AddBlob(List<Vector3> vertices, List<int> triangles, Vector3 center,
            float width, float depth, int count, float irregularity)
        {
            int first = vertices.Count;
            vertices.Add(center);
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                float radius = 1f + irregularity * Mathf.Sin(angle * 3f + .7f) + .06f * Mathf.Cos(angle * 7f);
                vertices.Add(center + new Vector3(Mathf.Cos(angle) * width * radius, 0f, Mathf.Sin(angle) * depth * radius));
                triangles.Add(first); triangles.Add(first + 1 + (i + 1) % count); triangles.Add(first + 1 + i);
            }
        }

        private void OnDestroy()
        {
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
