using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    /// <summary>One combined crossed-card mesh per recycled road segment, with no per-plant objects.</summary>
    public sealed class VoxelDesertGroundCover : MonoBehaviour
    {
        public int ClumpCount { get; private set; }
        private Mesh generatedMesh;
        private struct Card
        {
            public Vector3[] vertices, normals;
            public Vector2[] uv;
            public int[] triangles;
            public Bounds bounds;
        }

        public static void Build(Transform segment, EndlessVoxelRoad road, float startDistance, System.Func<float, VoxelTrackPose> sample)
        {
            var tuning = road.trackDefinition != null ? road.trackDefinition.scenerySet?.groundCover : null;
            if (tuning == null || !tuning.enabled || tuning.material == null || tuning.variants.Length == 0 || tuning.clumpsPerSegment <= 0) return;
            var root = new GameObject("Clustered Desert Ground Cover", typeof(MeshFilter), typeof(MeshRenderer), typeof(VoxelDesertGroundCover));
            root.transform.SetParent(segment, false);
            root.transform.position = sample(road.segmentLength * .5f).position;
            var owner = root.GetComponent<VoxelDesertGroundCover>();
            var vertices = new List<Vector3>(tuning.clumpsPerSegment * 8); var normals = new List<Vector3>(tuning.clumpsPerSegment * 8);
            var uv = new List<Vector2>(tuning.clumpsPerSegment * 8); var colours = new List<Color32>(tuning.clumpsPerSegment * 8);
            var indices = new List<int>(tuning.clumpsPerSegment * 12);
            // Fetch asset arrays once per variant, not once per plant during chunk recycling.
            var cards = new Card[tuning.variants.Length];
            for (int i = 0; i < cards.Length; i++) if (tuning.variants[i] != null)
            {
                var mesh = tuning.variants[i];
                cards[i] = new Card { vertices = mesh.vertices, normals = mesh.normals, uv = mesh.uv, triangles = mesh.triangles, bounds = mesh.bounds };
            }
            var random = new System.Random(unchecked(tuning.seed ^ Mathf.RoundToInt(startDistance) * 73856093));
            float Next() => (float)random.NextDouble();
            for (int attempt = 0; attempt < tuning.clumpsPerSegment * 6 && owner.ClumpCount < tuning.clumpsPerSegment; attempt++)
            {
                var card = cards[random.Next(cards.Length)];
                if (card.vertices == null) continue;
                float scale = Mathf.Lerp(Mathf.Max(.1f, tuning.scaleRange.x), Mathf.Max(.1f, tuning.scaleRange.y), Next());
                float radius = card.bounds.extents.magnitude * scale;
                float min = road.roadWidth * .5f + tuning.roadClearance + radius;
                float max = Mathf.Min(road.groundWidth * .5f - radius, road.roadWidth * .5f + tuning.roadsideDistance);
                if (max <= min) continue;
                var pose = sample(Next() * road.segmentLength);
                var position = pose.position + pose.right * (Next() < .5f ? -1 : 1) * Mathf.Lerp(min, max, Next());
                if (Next() > tuning.Density(position)) continue;
                // Curved-road clearance is checked against the nearby actual centreline, not world Z.
                bool blocked = false;
                float clearance = road.roadWidth * .5f + tuning.roadClearance + radius;
                for (float d = 0; d <= road.segmentLength; d += 4)
                {
                    var delta = sample(d).position - position; delta.y = 0;
                    if (delta.sqrMagnitude < clearance * clearance) { blocked = true; break; }
                }
                if (blocked) continue;
                var matrix = root.transform.worldToLocalMatrix * Matrix4x4.TRS(position, Quaternion.Euler(0, Next() * 360, 0), Vector3.one * scale);
                int first = vertices.Count;
                var sourceVertices = card.vertices; var sourceNormals = card.normals; var sourceUv = card.uv;
                float shade = Mathf.Lerp(.86f, 1.08f, Next());
                for (int v = 0; v < sourceVertices.Length; v++)
                {
                    vertices.Add(matrix.MultiplyPoint3x4(sourceVertices[v]));
                    normals.Add(matrix.MultiplyVector(sourceNormals[v]).normalized); uv.Add(sourceUv[v]);
                    float baseShade = Mathf.Lerp(.72f, 1, Mathf.Clamp01(sourceVertices[v].y / Mathf.Max(.1f, card.bounds.max.y)));
                    colours.Add(new Color(shade * baseShade, shade * baseShade, shade * baseShade * .97f, 1));
                }
                foreach (int index in card.triangles) indices.Add(first + index);
                owner.ClumpCount++;
            }
            owner.generatedMesh = new Mesh { name = "Batched Desert Foliage", indexFormat = IndexFormat.UInt16 };
            owner.generatedMesh.SetVertices(vertices); owner.generatedMesh.SetNormals(normals); owner.generatedMesh.SetUVs(0, uv);
            owner.generatedMesh.SetColors(colours); owner.generatedMesh.SetTriangles(indices, 0); owner.generatedMesh.RecalculateBounds();
            root.GetComponent<MeshFilter>().sharedMesh = owner.generatedMesh;
            var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = tuning.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.SetPropertyBlock(tuning.DrawProperties());
        }
        private void OnDestroy()
        {
            if (generatedMesh == null) return;
            if (Application.isPlaying) Destroy(generatedMesh); else DestroyImmediate(generatedMesh);
        }
    }
}
