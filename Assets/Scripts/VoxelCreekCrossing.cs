using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    /// <summary>Owns the meshes for a single straight road section's creek.</summary>
    internal sealed class VoxelCreekCrossing : MonoBehaviour
    {
        internal const float GroundTop = -.145f;
        private const float RevealDuration = 1.6f;
        private static readonly int Reveal = Shader.PropertyToID("_CreekReveal");
        private MeshRenderer channelRenderer, coverRenderer;
        private Mesh coverMesh;
        private MaterialPropertyBlock revealProperties;
        private float revealElapsed;
        private float extent;
        private float reach;
        private Matrix4x4 worldToCreek;
        private readonly List<Section> sections = new();
        private Mesh channelMesh;
        private Mesh groundPatch;

        internal static VoxelCreekCrossing Create(Transform parent, VoxelTrackPose pose, float extent,
            float shoulder, float sectionLength, VoxelTrackDefinition track, System.Random random, System.Random levels)
        {
            var root = new GameObject("Creek Crossing");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var creek = root.AddComponent<VoxelCreekCrossing>();
            creek.extent = extent;
            creek.reach = Mathf.Min(8.5f, sectionLength * .34f);
            creek.worldToCreek = root.transform.worldToLocalMatrix;
            creek.Build(shoulder, track, random, levels);
            return creek;
        }

        internal bool Overlaps(Vector3 position, float radius)
        {
            Vector3 local = worldToCreek.MultiplyPoint3x4(position);
            if (Mathf.Abs(local.x) >= extent + radius || Mathf.Abs(local.z) >= reach + radius)
                return false;

            int first = 0, last = sections.Count - 1;
            while (first < last)
            {
                int middle = (first + last) / 2;
                if (sections[middle + 1].x < local.x - radius) first = middle + 1;
                else last = middle;
            }
            for (int i = first; i + 1 < sections.Count && sections[i].x <= local.x + radius; i++)
            {
                var a = sections[i];
                var b = sections[i + 1];
                if (local.z >= Mathf.Min(a.center - a.half, b.center - b.half) - radius &&
                    local.z <= Mathf.Max(a.center + a.half, b.center + b.half) + radius)
                    return true;
            }
            return false;
        }

        private void Build(float shoulder, VoxelTrackDefinition track, System.Random random, System.Random levels)
        {
            float Pick(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
            Color soil = track.groundColour;
            Color outerBank = Color.Lerp(soil, new Color(.67f, .31f, .16f), .55f);
            Color bed = Color.Lerp(new Color(.13f, .072f, .039f), soil * .42f, .25f);
            Color innerBank = Color.Lerp(outerBank, bed, .58f);
            Color shallowWater = new(.54f, .365f, .22f);
            Color deepWater = new(.37f, .235f, .135f);
            Color steel = new(.16f, .17f, .18f);
            float bedDepth = Mathf.Lerp(.90f, 2.15f, (float)levels.NextDouble());
            // Clamp the lowest part of the roll to zero so some crossings are completely dry.
            float fill = Mathf.Clamp01(Mathf.Lerp(-.2f, 1f, (float)levels.NextDouble()));
            float width = reach * Pick(.42f, .58f);
            float offset = Mathf.Round(reach * Pick(-.12f, .12f) * 5f) / 5f;
            float leftPhase = Pick(0f, 6.28f);
            float rightPhase = Pick(0f, 6.28f);
            float leftDepth = Pick(.76f, 1.22f);
            float rightDepth = Pick(.76f, 1.22f);
            float deepest = bedDepth * Mathf.Lerp(1f, Mathf.Max(1f, leftDepth, rightDepth),
                Mathf.Clamp01((extent - shoulder) / 7f));
            float waterDrop = deepest * (1f - fill);
            float leftBend = reach * Pick(.22f, .34f);
            float rightBend = reach * Pick(.22f, .34f);
            var land = new Surface();
            var features = new Surface();

            float detailLength = Mathf.Min(42f, extent - shoulder);
            int nearSlices = Mathf.CeilToInt(detailLength / 1.4f);
            int farSlices = Mathf.CeilToInt((extent - shoulder - detailLength) / 15f);
            var previous = Sample(-extent);
            sections.Add(previous);
            for (int i = farSlices - 1; i >= 0; i--)
                StepTo(Sample(-Mathf.Lerp(shoulder + detailLength, extent, (float)i / farSlices)));
            for (int i = nearSlices - 1; i >= 0; i--)
            {
                var next = Sample(-Mathf.Lerp(shoulder, shoulder + detailLength, (float)i / nearSlices));
                AddBankDetails(next, i + 1);
                StepTo(next);
            }
            AddSlice(Sample(shoulder));
            for (int i = 1; i <= nearSlices; i++)
            {
                var next = Sample(Mathf.Lerp(shoulder, shoulder + detailLength, (float)i / nearSlices));
                AddBankDetails(next, i + 1);
                StepTo(next);
            }
            for (int i = 1; i <= farSlices; i++)
                StepTo(Sample(Mathf.Lerp(shoulder + detailLength, extent, (float)i / farSlices)));

            float railHalfLength = width + .8f;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (shoulder - .16f);
                for (int post = 0; post <= 4; post++)
                {
                    float z = offset + Mathf.Lerp(-railHalfLength, railHalfLength, post / 4f);
                    features.Box(new Vector3(x, .51f, z), new Vector3(.13f, 1f, .15f), steel * .74f);
                    features.Box(new Vector3(x, .035f, z), new Vector3(.23f, .05f, .24f), steel * .67f);
                    features.Box(new Vector3(x, 1.02f, z), new Vector3(.15f, .055f, .17f), steel * 1.12f);
                }
                features.Box(new Vector3(x, .88f, offset), new Vector3(.09f, .17f, railHalfLength * 2f), steel);
                features.Box(new Vector3(x, .91f, offset), new Vector3(.115f, .045f, railHalfLength * 2f), steel * 1.18f);
                features.Box(new Vector3(x, .45f, offset), new Vector3(.075f, .105f, railHalfLength * 2f), steel * .86f);
            }

            groundPatch = AddMesh("Ground Around Creek", land, VoxelRacerBootstrap.GroundMaterial);
            channelMesh = AddMesh("Creek Banks, Water and Fences", features,
                Resources.Load<Material>("Scenery/CreekSurfaces"),
                track.nightTime ? new Color(.45f, .57f, .76f) : Color.white);
            return;

            Section Sample(float x)
            {
                float phase = x < 0f ? leftPhase : rightPhase;
                float bend = x < 0f ? leftBend : rightBend;
                float outward = Mathf.Max(0f, Mathf.Abs(x) - shoulder);
                float blend = Mathf.Clamp01(outward / 7f);
                float wiggle = Mathf.Sin(outward * .23f + phase) * bend * blend;
                float half = width * Mathf.Lerp(1f, .82f + .17f * Mathf.Sin(outward * .17f + phase), blend);
                float depth = bedDepth * Mathf.Lerp(1f, x < 0f ? leftDepth : rightDepth, blend);
                return new Section(x, Mathf.Round((offset + wiggle) * 2f) * .5f, Mathf.Round(half * 2f) * .5f, depth);
            }

            // Hold the profile across each voxel-length slice, then make the bend in a narrow step.
            void StepTo(Section next)
            {
                float seam = (previous.x + next.x) * .5f;
                if (previous.center != next.center || previous.half != next.half || previous.depth != next.depth)
                {
                    AddSlice(previous.AtX(seam - .025f));
                    AddSlice(next.AtX(seam + .025f));
                }
                AddSlice(next);
            }

            void AddBankDetails(Section section, int index)
            {
                if (Mathf.Abs(section.x) < shoulder + 1f || Mathf.Abs(section.x) > extent - 1f ||
                    section.half < 1f || index % 3 != 0)
                    return;

                float side = index % 2 == 0 ? -1f : 1f;
                Vector3 edge = section.At(side * .80f, .32f);
                var stone = new Color(.49f, .41f, .34f) * Pick(.86f, 1.14f);
                features.Box(edge + Vector3.up * .09f, new Vector3(Pick(.21f, .48f), .18f, Pick(.22f, .46f)), stone);
                if (fill == 0f || section.depth * .32f > waterDrop + .25f || index % 2 != 0)
                    return;
                for (int stalk = 0; stalk < 5; stalk++)
                {
                    float height = Pick(.23f, .67f);
                    var reed = new Color(.34f, .43f, .16f) * Pick(.85f, 1.15f);
                    var point = edge + new Vector3(Pick(-.25f, .25f), height * .5f, Pick(-.14f, .14f));
                    features.Box(point, new Vector3(.065f, height, .07f), reed);
                }
            }

            void AddSlice(Section next)
            {
                land.Band(previous.At(-1f, 0f), next.At(-1f, 0f),
                    new Vector3(next.x, GroundTop, -reach), new Vector3(previous.x, GroundTop, -reach), soil);
                land.Band(previous.At(1f, 0f), next.At(1f, 0f),
                    new Vector3(next.x, GroundTop, reach), new Vector3(previous.x, GroundTop, reach), soil);

                float shade = Pick(.88f, 1.11f);
                foreach (float side in new[] { -1f, 1f })
                {
                    Bank(next, side, 1f, 0f, .98f, .32f, outerBank * (shade * .77f));
                    Bank(next, side, .98f, .32f, .70f, .32f, outerBank * (shade * 1.12f));
                    Bank(next, side, .70f, .32f, .55f, 1f, innerBank * shade);
                }
                features.Band(previous.At(-.55f, 1f), next.At(-.55f, 1f),
                    next.At(.55f, 1f), previous.At(.55f, 1f), bed * shade);

                if (fill > 0f && Mathf.Max(previous.depth, next.depth) > waterDrop)
                    AddWater(previous, next);
                previous = next;
                sections.Add(next);
            }

            void Bank(Section next, float side, float outer, float top, float inner, float bottom, Color color) =>
                features.Band(previous.At(side * outer, top), next.At(side * outer, top),
                    next.At(side * inner, bottom), previous.At(side * inner, bottom), color);

            void AddWater(Section a, Section b)
            {
                if (a.depth <= waterDrop) a = Section.Lerp(a, b, Mathf.InverseLerp(a.depth, b.depth, waterDrop));
                if (b.depth <= waterDrop) b = Section.Lerp(b, a, Mathf.InverseLerp(b.depth, a.depth, waterDrop));
                float depth = (a.depth + b.depth) * .5f - waterDrop;
                Color water = Color.Lerp(shallowWater, deepWater, Mathf.Clamp01(depth / 1.4f));
                for (int cell = 0; cell < 6; cell++)
                {
                    float start = Mathf.Lerp(-1f, 1f, cell / 6f);
                    float end = Mathf.Lerp(-1f, 1f, (cell + 1) / 6f);
                    float shade = Pick(.96f, 1.055f) * (cell == 0 || cell == 5 ? 1.09f : 1f);
                    features.Band(WaterPoint(a, start), WaterPoint(b, start),
                        WaterPoint(b, end), WaterPoint(a, end), water * shade, water: true);
                }
            }

            Vector3 WaterPoint(Section section, float side)
            {
                float fraction = waterDrop / Mathf.Max(waterDrop, section.depth);
                float widthAtWater = fraction <= .32f ? Mathf.Lerp(1f, .98f, fraction / .32f) :
                    Mathf.Lerp(.70f, .55f, Mathf.InverseLerp(.32f, 1f, fraction));
                return new Vector3(section.x, GroundTop - waterDrop,
                    section.center + side * section.half * widthAtWater);
            }
        }

        internal void BeginReveal()
        {
            var ground = VoxelRacerBootstrap.GroundMaterial;
            if (!ground.HasProperty(Reveal))
                return;

            var cover = new Surface();
            for (int i = 1; i < sections.Count; i++)
            {
                var a = sections[i - 1];
                var b = sections[i];
                cover.Band(a.At(-1f, 0f), b.At(-1f, 0f), b.At(1f, 0f), a.At(1f, 0f), Color.white);
            }
            coverMesh = AddMesh("Creek Reveal Ground", cover, ground);
            coverRenderer = transform.Find("Creek Reveal Ground").GetComponent<MeshRenderer>();
            channelRenderer = transform.Find("Creek Banks, Water and Fences").GetComponent<MeshRenderer>();
            revealProperties ??= new MaterialPropertyBlock();
            ApplyReveal(0f);
        }

        private void Update()
        {
            if (coverRenderer == null)
                return;

            revealElapsed += Time.deltaTime;
            ApplyReveal(Mathf.SmoothStep(0f, 1f, revealElapsed / RevealDuration));
            if (revealElapsed < RevealDuration)
                return;

            channelRenderer.SetPropertyBlock(null);
            Destroy(coverRenderer.gameObject);
            Destroy(coverMesh);
            coverRenderer = null;
            coverMesh = null;
            revealProperties = null;
        }

        private void ApplyReveal(float progress)
        {
            revealProperties.SetFloat(Reveal, progress);
            channelRenderer.SetPropertyBlock(revealProperties);
            coverRenderer.SetPropertyBlock(revealProperties);
        }

        private readonly struct Section
        {
            public readonly float x, center, half, depth;
            public Section(float x, float center, float half, float depth)
            {
                this.x = x;
                this.center = center;
                this.half = half;
                this.depth = depth;
            }

            public Vector3 At(float side, float level) => new(x, GroundTop - depth * level, center + half * side);
            public Section AtX(float nextX) => new(nextX, center, half, depth);

            public static Section Lerp(Section a, Section b, float t) => new(Mathf.Lerp(a.x, b.x, t),
                Mathf.Lerp(a.center, b.center, t), Mathf.Lerp(a.half, b.half, t), Mathf.Lerp(a.depth, b.depth, t));
        }

        private Mesh AddMesh(string label, Surface surface, Material material, Color? tint = null)
        {
            var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            var mesh = new Mesh { name = label, hideFlags = HideFlags.DontSave };
            surface.Write(mesh, tint ?? Color.white);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return mesh;
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                Destroy(channelMesh);
                Destroy(groundPatch);
                if (coverMesh != null) Destroy(coverMesh);
            }
            else
            {
                DestroyImmediate(channelMesh);
                DestroyImmediate(groundPatch);
                if (coverMesh != null) DestroyImmediate(coverMesh);
            }
        }

        // Split the ground's top into convex pieces along each rectangular creek patch.
        // The patch supplies the ground around the channel, so the water can sit below it.
        internal static void CutGround(Mesh mesh, Transform ground, List<VoxelCreekCrossing> creeks)
        {
            var surface = new Surface();
            var polygons = new List<List<Vector3>>
            {
                new() { new(-.5f, .5f, -.5f), new(-.5f, .5f, .5f), new(.5f, .5f, .5f), new(.5f, .5f, -.5f) }
            };
            Matrix4x4 groundToWorld = ground.localToWorldMatrix;
            foreach (var creek in creeks)
            {
                var next = new List<List<Vector3>>();
                Matrix4x4 toCreek = creek.worldToCreek * groundToWorld;
                foreach (var polygon in polygons)
                {
                    List<Vector3> remaining = polygon;
                    for (int edge = 0; edge < 4 && remaining.Count >= 3; edge++)
                    {
                        var inside = new List<Vector3>();
                        var outside = new List<Vector3>();
                        Vector3 previous = remaining[^1];
                        float previousDistance = Distance(previous);
                        foreach (var point in remaining)
                        {
                            float distance = Distance(point);
                            if ((distance < 0f) != (previousDistance < 0f))
                            {
                                Vector3 crossing = Vector3.LerpUnclamped(previous, point, previousDistance / (previousDistance - distance));
                                inside.Add(crossing);
                                outside.Add(crossing);
                            }
                            (distance < 0f ? outside : inside).Add(point);
                            previous = point;
                            previousDistance = distance;
                        }
                        if (outside.Count >= 3) next.Add(outside);
                        remaining = inside;

                        float Distance(Vector3 vertex)
                        {
                            Vector3 local = toCreek.MultiplyPoint3x4(vertex);
                            return edge switch
                            {
                                0 => creek.extent + local.x,
                                1 => creek.extent - local.x,
                                2 => creek.reach + local.z,
                                _ => creek.reach - local.z
                            };
                        }
                    }
                }
                polygons = next;
            }
            foreach (var polygon in polygons) surface.Top(polygon);
            surface.Write(mesh);
        }

        private sealed class Surface
        {
            private readonly List<Vector3> vertices = new();
            private readonly List<int> triangles = new();
            private readonly List<Color> colors = new();
            private readonly List<Vector2> uv = new();

            private void Vertex(Vector3 point, Color color, bool water = false)
            {
                vertices.Add(point);
                color.a = water ? 1f : 0f;
                colors.Add(color);
                uv.Add(new Vector2(point.x + .5f, point.z + .5f));
            }

            public void Top(List<Vector3> polygon)
            {
                int start = vertices.Count;
                foreach (var point in polygon) Vertex(point, Color.white);
                for (int i = 2; i < polygon.Count; i++)
                {
                    triangles.Add(start);
                    triangles.Add(start + i - 1);
                    triangles.Add(start + i);
                }
            }

            public void Band(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, bool water = false)
            {
                if (Vector3.Cross(b - a, c - a).y < 0f)
                    Quad(a, d, c, b, color, water);
                else
                    Quad(a, b, c, d, color, water);
            }

            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, bool water = false)
            {
                int start = vertices.Count;
                Vertex(a, color, water); Vertex(b, color, water); Vertex(c, color, water); Vertex(d, color, water);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }

            public void Box(Vector3 center, Vector3 size, Color color)
            {
                Vector3 min = center - size * .5f;
                Vector3 max = center + size * .5f;
                Vector3 a = min, b = new(min.x, min.y, max.z), c = new(max.x, min.y, max.z), d = new(max.x, min.y, min.z);
                Vector3 up = Vector3.up * size.y;
                Quad(a + up, b + up, c + up, d + up, color * 1.14f);
                Quad(a, b, b + up, a + up, color * .88f);
                Quad(c, d, d + up, c + up, color);
                Quad(b, c, c + up, b + up, color * .94f);
                Quad(d, a, a + up, d + up, color * .84f);
            }

            public void Write(Mesh mesh, Color? tint = null)
            {
                if (tint.HasValue && tint.Value != Color.white)
                    for (int i = 0; i < colors.Count; i++)
                        colors[i] *= tint.Value;
                mesh.Clear();
                mesh.SetVertices(vertices);
                mesh.SetColors(colors);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
        }
    }
}
