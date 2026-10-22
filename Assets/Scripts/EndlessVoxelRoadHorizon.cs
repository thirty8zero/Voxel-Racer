using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    public sealed partial class EndlessVoxelRoad
    {
        [SerializeField, HideInInspector] private Mesh horizonMesh;
        [SerializeField, HideInInspector] private int horizonMeshOwner;
        private Transform horizonRoot;
        private MeshRenderer horizonRenderer;
        private float horizonStart = float.NaN, horizonEnd = float.NaN;
        private float horizonWidth;
        private int horizonLanes;
        private readonly List<Vector3> horizonVertices = new(2048);
        private readonly List<Vector2> horizonUV = new(2048);
        private readonly List<int>[] horizonTriangles = { new(2048), new(2048), new(2048) };
        private MaterialPropertyBlock horizonProperties;
        [SerializeField, HideInInspector] private Material[] horizonMaterials;
        [SerializeField, HideInInspector] private int horizonMaterialOwner;
        private const float HorizonUnderlay = .025f;

        private void RefreshHorizonRoad()
        {
            if (!horizonRoadEnabled || visibleSegments.Count == 0)
            {
                if (horizonRoot == null) horizonRoot = transform.Find("Distant Road");
                if (horizonRoot != null) horizonRoot.gameObject.SetActive(false);
                return;
            }
            // Overlap the last three chunks beneath the real surface, so a newly
            // recycled chunk can fade in without opening a hole in the distant road.
            float start = visibleSegments[Mathf.Max(0, visibleSegments.Count - 3)].startDistance;
            float focus = targetController != null ? targetController.TrackDistance : 0f;
            float end = Mathf.Ceil((focus + horizonRoadDistance) / segmentLength) * segmentLength;
            if (end <= visibleSegments[^1].EndDistance(segmentLength))
            {
                if (horizonRoot != null) horizonRoot.gameObject.SetActive(false);
                return;
            }
            bool owned = horizonMesh != null && horizonMeshOwner == GetInstanceID();
            if (owned && horizonRoot != null && horizonRoot.gameObject.activeSelf &&
                Mathf.Approximately(start, horizonStart) && Mathf.Approximately(end, horizonEnd) &&
                Mathf.Approximately(horizonWidth, roadWidth) && horizonLanes == laneCount) return;

            EnsurePathCovers(end + segmentLength);
            if (horizonRoot == null) horizonRoot = transform.Find("Distant Road");
            if (horizonRoot == null)
            {
                horizonRoot = new GameObject("Distant Road", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                horizonRoot.SetParent(transform, false);
                horizonRoot.gameObject.layer = gameObject.layer;
            }
            horizonRoot.gameObject.SetActive(true);
            if (!owned)
            {
                horizonMesh = new Mesh { name = "Distant Road Surface", hideFlags = HideFlags.DontSave };
                horizonMeshOwner = GetInstanceID();
                horizonMesh.MarkDynamic();
            }
            horizonRoot.GetComponent<MeshFilter>().sharedMesh = horizonMesh;
            horizonRenderer = horizonRoot.GetComponent<MeshRenderer>();
            UpdateHorizonMaterials();
            horizonRenderer.sharedMaterials = horizonMaterials;
            horizonRenderer.shadowCastingMode = ShadowCastingMode.Off;
            horizonRenderer.receiveShadows = false;
            horizonRenderer.lightProbeUsage = LightProbeUsage.Off;
            horizonRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            horizonProperties ??= new MaterialPropertyBlock();
            horizonProperties.SetFloat("_UseRoadUV", 1f);
            horizonRenderer.SetPropertyBlock(horizonProperties, 0);
            horizonVertices.Clear(); horizonUV.Clear();
            foreach (var triangles in horizonTriangles) triangles.Clear();
            float half = roadWidth * .5f;
            int first = Mathf.Max(0, Mathf.FloorToInt((start - pathSegments[0].startDistance) / segmentLength));
            for (int index = first; index < pathSegments.Count; index++)
            {
                var segment = pathSegments[index];
                if (segment.startDistance >= end) break;
                float length = Mathf.Min(segmentLength, end - segment.startDistance);
                int slices = Mathf.Max(1, Mathf.CeilToInt(length / 10f), Mathf.CeilToInt(Mathf.Abs(segment.turnAngle) / 5f));
                var a = EvaluateSegment(segment, 0f);
                for (int slice = 0; slice < slices; slice++)
                {
                    float d0 = length * slice / slices, d1 = length * (slice + 1) / slices;
                    var b = EvaluateSegment(segment, d1);
                    AddHorizonQuad(a, b, -half, half, -HorizonUnderlay,
                        segment.startDistance + d0, segment.startDistance + d1, 0);
                    AddHorizonQuad(a, b, -half - .55f, -half, .01f - HorizonUnderlay,
                        segment.startDistance + d0, segment.startDistance + d1, 1);
                    AddHorizonQuad(a, b, half, half + .55f, .01f - HorizonUnderlay,
                        segment.startDistance + d0, segment.startDistance + d1, 1);
                    a = b;
                }
                for (int lane = 1; lane < laneCount; lane++)
                for (float distance = 3f; distance + 1.4f < length; distance += 6f)
                {
                    float offset = -half + roadWidth * lane / Mathf.Max(1, laneCount);
                    AddHorizonMark(segment, distance, offset, .16f, 2.8f, .0375f - HorizonUnderlay);
                }
                if (VoxelRacerBootstrap.ShoulderMaterial.color != Color.white)
                    for (float distance = 2.5f; distance + .8f < length; distance += 5f)
                    {
                        AddHorizonMark(segment, distance, -half + .12f, .18f, 1.6f, .06f - HorizonUnderlay);
                        AddHorizonMark(segment, distance, half - .12f, .18f, 1.6f, .06f - HorizonUnderlay);
                    }
            }
            horizonMesh.Clear();
            horizonMesh.indexFormat = horizonVertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            horizonMesh.SetVertices(horizonVertices); horizonMesh.SetUVs(0, horizonUV);
            horizonMesh.subMeshCount = 3;
            for (int submesh = 0; submesh < 3; submesh++) horizonMesh.SetTriangles(horizonTriangles[submesh], submesh, false);
            horizonMesh.RecalculateNormals(); horizonMesh.RecalculateBounds();
            horizonStart = start; horizonEnd = end; horizonWidth = roadWidth; horizonLanes = laneCount;
        }

        private void AddHorizonMark(RoadSegment segment, float distance, float offset, float width, float length, float height)
        {
            AddHorizonQuad(EvaluateSegment(segment, distance - length * .5f), EvaluateSegment(segment, distance + length * .5f),
                offset - width * .5f, offset + width * .5f, height,
                segment.startDistance + distance - length * .5f, segment.startDistance + distance + length * .5f, 2);
        }

        private void UpdateHorizonMaterials()
        {
            // A Resources material keeps the fade shader available in player builds.
            var template = Resources.Load<Material>("Scenery/RoadHorizonSurface");
            var sources = new[] { VoxelRacerBootstrap.RoadMaterial, VoxelRacerBootstrap.ShoulderMaterial, VoxelRacerBootstrap.LineMaterial };
            if (horizonMaterials == null || horizonMaterialOwner != GetInstanceID())
            {
                horizonMaterials = new Material[3]; horizonMaterialOwner = GetInstanceID();
                for (int i = 0; i < 3; i++) horizonMaterials[i] = new Material(i == 0 ? template : sources[i])
                    { name = "Distant Road " + i, hideFlags = HideFlags.DontSave };
            }
            for (int i = 0; i < 3; i++)
            {
                var material = horizonMaterials[i]; var source = sources[i];
                material.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") :
                    source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
                if (i == 0)
                {
                    material.SetFloat("_Variation", source.HasProperty("_Variation") ? source.GetFloat("_Variation") : 0);
                    material.SetVector("_PatchSize", source.HasProperty("_PatchSize") ? source.GetVector("_PatchSize") : new Vector4(.65f, 7, 0, 0));
                    material.SetFloat("_HorizonFadeStart", horizonRoadDistance * .62f);
                    material.SetFloat("_HorizonFadeEnd", horizonRoadDistance * .94f);
                }
            }
        }

        private void AddHorizonQuad(VoxelTrackPose a, VoxelTrackPose b, float left, float right, float height,
            float d0, float d1, int material)
        {
            int first = horizonVertices.Count;
            float leftA = left, rightA = right, leftB = left, rightB = right;
            if (material > 0)
            {
                // Subtle widening of distant paint keeps it from disappearing
                // below a pixel. Keep the actual world-space road surface/depth.
                float focus = SceneryTrackDistance, centre = (left + right) * .5f;
                float halfA = Mathf.Max((right - left) * .5f, Mathf.Abs(d0 - focus) * .0006f);
                float halfB = Mathf.Max((right - left) * .5f, Mathf.Abs(d1 - focus) * .0006f);
                leftA = centre - halfA; rightA = centre + halfA;
                leftB = centre - halfB; rightB = centre + halfB;
            }
            horizonVertices.Add(horizonRoot.InverseTransformPoint(a.position + a.right * leftA + Vector3.up * height));
            horizonVertices.Add(horizonRoot.InverseTransformPoint(b.position + b.right * leftB + Vector3.up * height));
            horizonVertices.Add(horizonRoot.InverseTransformPoint(b.position + b.right * rightB + Vector3.up * height));
            horizonVertices.Add(horizonRoot.InverseTransformPoint(a.position + a.right * rightA + Vector3.up * height));
            horizonUV.Add(new Vector2(left, d0)); horizonUV.Add(new Vector2(left, d1));
            horizonUV.Add(new Vector2(right, d1)); horizonUV.Add(new Vector2(right, d0));
            var triangles = horizonTriangles[material];
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
        }

        private void OnDestroy()
        {
            if (horizonMaterials != null && horizonMaterialOwner == GetInstanceID())
                foreach (var material in horizonMaterials)
                    if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            // A preview clone can share the source reference until its first refresh.
            if (horizonMesh == null || horizonMeshOwner != GetInstanceID()) return;
            if (Application.isPlaying) Destroy(horizonMesh); else DestroyImmediate(horizonMesh);
        }
    }
}
