using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelGoldTrackWheelBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Upgrades/GoldTrackWheel.prefab";
        private const string MeshPath = "Assets/Prefabs/Upgrades/GoldTrackWheelMesh.asset";
        private const string TuningPath = "Assets/Resources/Upgrades/GoldTrackWheelTuning.asset";
        private const string MaterialPath = "Assets/Resources/CarMaterials/GoldTrackWheelAlloy.mat";
        private const string Palette = "CarMaterials/";
        private const int Rubber = 0, Alloy = 1, Hardware = 2, White = 3, Black = 4;
        private const float OuterFace = .151f;
        private const float WidthMultiplier = 1.5f;
        private const float AddedWidth = 2f * OuterFace * (WidthMultiplier - 1f);

        [MenuItem("Tools/Voxel Racer/Build Gold Track Wheels")]
        public static void Build()
        {
            var gold = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (gold == null)
            {
                gold = new Material(Resources.Load<Material>(Palette + "PerformanceWheelGraphite"));
                gold.SetColor("_BaseColor", new Color32(217, 180, 109, 255));
                gold.SetFloat("_Metallic", .36f);
                gold.SetFloat("_Smoothness", .38f);
                AssetDatabase.CreateAsset(gold, MaterialPath);
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool createMesh = mesh == null;
            if (createMesh) mesh = new Mesh();
            BuildMesh(mesh);
            if (createMesh) AssetDatabase.CreateAsset(mesh, MeshPath);
            else
            {
                EditorUtility.SetDirty(mesh);
                AssetDatabase.SaveAssetIfDirty(mesh);
            }

            var root = new GameObject("GoldTrackWheel");
            try
            {
                // The mounting helper uses this offset to keep the inboard envelope in the stock arch.
                root.transform.localPosition = Vector3.right * AddedWidth;
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterials = new[]
                {
                    Resources.Load<Material>(Palette + "PerformanceTyreRubber"), gold,
                    Resources.Load<Material>(Palette + "PerformanceWheelGraphite"),
                    Resources.Load<Material>(Palette + "PerformanceTyreWhite"),
                    Resources.Load<Material>(Palette + "FormulaBlack")
                };
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var tuning = AssetDatabase.LoadAssetAtPath<VoxelPerformanceWheelTuning>(TuningPath);
                if (tuning == null)
                {
                    tuning = Object.Instantiate(VoxelPerformanceWheelTuning.Load());
                    tuning.displayName = "GOLD TRACK WHEELS";
                    tuning.upgradeLevel = 2;
                    AssetDatabase.CreateAsset(tuning, TuningPath);
                }
                tuning.wheelPrefab = prefab;
                EditorUtility.SetDirty(tuning);
                AssetDatabase.SaveAssetIfDirty(tuning);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildMesh(Mesh mesh)
        {
            var vertices = new List<Vector3>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };

            // Step the shoulders and stagger the two rows of tread blocks around a recessed centre channel.
            var tyre = new (float x, float radius)[]
            {
                (-OuterFace, .338f), (-OuterFace, .429f), (-.123f, .429f), (-.123f, .449f),
                (-.100f, .449f), (-.100f, .461f), (-.010f, .461f), (-.010f, .451f),
                (.010f, .451f), (.010f, .441f), (.123f, .441f), (.123f, .429f),
                (.146f, .429f), (.146f, .338f)
            };
            for (int i = 0; i < tyre.Length; i++) tyre[i].x = Widen(tyre[i].x);
            Profile(Rubber, 24, tyre);
            for (int block = 0; block < 20; block++)
            {
                TreadBlock(-.103f, -.018f, block * 18f, .443f, .475f);
                // The stepped outer shoulder leaves room for the Spy Car 2 arch at full lock.
                TreadBlock(.018f, .103f, block * 18f + 9f, .435f, .455f);
            }

            // A full-depth barrel and a stepped lip leave the ten spoke openings visible.
            Profile(Alloy, 30, (Widen(-.136f), .313f), (Widen(-.136f), .349f), (.117f, .349f), (.143f, .350f),
                (.151f, .342f), (.151f, .330f), (.141f, .318f), (.109f, .311f));
            Profile(Hardware, 20, (-.074f, .084f), (-.074f, .238f), (-.065f, .253f), (-.035f, .253f),
                (-.027f, .243f), (-.027f, .083f));
            Profile(Alloy, 20, (-.090f, .0f), (-.090f, .079f), (.043f, .105f), (.085f, .118f),
                (.110f, .098f), (.116f, .081f), (.116f, .0f));
            Profile(Hardware, 20, (.113f, .0f), (.113f, .078f), (.119f, .079f), (.125f, .069f), (.125f, .0f));
            Profile(Alloy, 8, (.124f, .0f), (.124f, .024f), (.127f, .020f), (.127f, .0f));

            for (int pair = 0; pair < 5; pair++)
                for (int branch = -1; branch <= 1; branch += 2)
                {
                    float baseAngle = pair * 72 + 18;
                    // Spokes fan apart at the roots and sweep up into the lip at the tips.
                    var root = SpokeSection(.075f, baseAngle + branch * 9, .024f, .032f, .096f);
                    var shoulder = SpokeSection(.133f, baseAngle + branch * 14, .025f, .025f, .084f);
                    var blade = SpokeSection(.270f, baseAngle + branch * 17, .016f, .054f, .103f);
                    var tip = SpokeSection(.338f, baseAngle + branch * 17, .019f, .088f, .143f);
                    Join(Alloy, root, shoulder);
                    Join(Alloy, shoulder, blade);
                    Join(Alloy, blade, tip);
                }

            // Keep the 13-degree pitch on the outward-facing sidewall.
            foreach (int arc in new[] { 0, 180 })
                for (int tab = -2; tab <= 2; tab++)
                {
                    float angle = arc + tab * 13f;
                    Vector3 radial = Polar(0, 1, angle * Mathf.Deg2Rad);
                    Vector3 tangent = Vector3.Cross(Vector3.right, radial);
                    Vector3 centre = radial * .398f;
                    Vector3 halfWidth = tangent * .0335f, halfHeight = radial * .0225f;
                    var a = centre - halfHeight - halfWidth;
                    var b = centre + halfHeight - halfWidth;
                    var c = centre + halfHeight + halfWidth;
                    var d = centre - halfHeight + halfWidth;
                    Vector3 back = Vector3.right * .145f;
                    Vector3 front = Vector3.right * OuterFace;
                    Quad(White, a + front, b + front, c + front, d + front);
                    Quad(White, d + back, c + back, b + back, a + back);
                    Quad(White, b + back, b + front, a + front, a + back);
                    Quad(White, c + back, c + front, b + front, b + back);
                    Quad(White, d + back, d + front, c + front, c + back);
                    Quad(White, a + back, a + front, d + front, d + back);
                }

            // Double the silhouette facing the spokes while keeping the rotor and spoke clearances along X.
            const float caliperFace = .024f;
            var innerJaw = CaliperSection(.174f, .052f, -.032f, .013f);
            var innerShoulder = CaliperSection(.202f, .091f, -.032f, caliperFace);
            var outerShoulder = CaliperSection(.248f, .088f, -.034f, caliperFace);
            var bridge = CaliperSection(.267f, .079f, -.091f, .020f);
            var crown = CaliperSection(.285f, .058f, -.087f, .009f);
            Join(White, innerJaw, innerShoulder);
            Join(White, innerShoulder, outerShoulder);
            Join(White, outerShoulder, bridge);
            Join(White, bridge, crown);
            for (int i = 1; i < crown.Length - 1; i++)
            {
                Face(White, innerJaw[0], innerJaw[i + 1], innerJaw[i]);
                Face(White, crown[0], crown[i], crown[i + 1]);
            }

            // 制動 (braking), read top to bottom. The angular strokes share the wheel's baked mesh.
            Mark(.050f,
                (2, 1, 1, 4), (1.5f, 2.5f, 5.5f, 2.5f), (.5f, 4.5f, 6, 4.5f),
                (3.5f, .5f, 3.5f, 9.5f), (1.2f, 8.5f, 1.2f, 6), (1.2f, 6, 5.7f, 6),
                (5.7f, 6, 5.7f, 8.5f), (5.7f, 8.5f, 4.7f, 8.5f), (7.3f, 2, 7.3f, 7),
                (9.5f, .8f, 9.5f, 9.3f), (9.5f, 9.3f, 8.2f, 9.3f));
            Mark(-.006f,
                (1.2f, 1.4f, 5.4f, .6f), (.5f, 2.9f, 6.2f, 2.9f), (3.4f, 1.2f, 3.4f, 8.8f),
                (1.1f, 4.1f, 5.7f, 4.1f), (1.1f, 4.1f, 1.1f, 6.5f), (5.7f, 4.1f, 5.7f, 6.5f),
                (1.1f, 5.3f, 5.7f, 5.3f), (1.1f, 6.5f, 5.7f, 6.5f), (1, 7.8f, 5.8f, 7.8f),
                (.6f, 9.4f, 5.9f, 8.6f), (6.5f, 3.2f, 9.5f, 3.2f), (9.5f, 3.2f, 9.3f, 8.6f),
                (9.3f, 8.6f, 8.7f, 9.3f), (8.7f, 9.3f, 7.9f, 9.3f), (8, .7f, 7.8f, 5.7f),
                (7.8f, 5.7f, 7.2f, 7.6f), (7.2f, 7.6f, 6.1f, 9.4f));

            mesh.Clear();
            mesh.name = "GoldTrackWheelMesh";
            mesh.SetVertices(vertices);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            Vector3[] CaliperSection(float radius, float halfWidth, float back, float front)
            {
                const float rotorEdge = .253f;
                var section = SpokeSection(radius, 270f, halfWidth, back, front);
                for (int i = 0; i < section.Length; i++)
                {
                    section[i].y *= 1.6f;
                    section[i].z = (section[i].z + rotorEdge) * 1.25f - rotorEdge;
                }
                return section;
            }

            void Mark(float top, params (float u0, float v0, float u1, float v1)[] strokes)
            {
                const float cell = .0046f;
                foreach (var (u0, v0, u1, v1) in strokes)
                {
                    var start = new Vector2(u0, v0);
                    var end = new Vector2(u1, v1);
                    var direction = end - start;
                    var halfStroke = new Vector2(-direction.y, direction.x).normalized * .49f;
                    Quad(Black, Point(start - halfStroke), Point(end - halfStroke),
                        Point(end + halfStroke), Point(start + halfStroke));
                }

                Vector3 Point(Vector2 grid) => new Vector3(caliperFace + .0008f,
                    top - grid.y * cell, -.218f + (grid.x - 5f) * cell);
            }

            void TreadBlock(float inboard, float outboard, float angle, float baseRadius, float treadRadius)
            {
                float start = angle * Mathf.Deg2Rad, end = (angle + 12f) * Mathf.Deg2Rad;
                float x0 = Widen(inboard), x1 = Widen(outboard);
                var a = Polar(x0, treadRadius, start);
                var b = Polar(x0, treadRadius, end);
                var c = Polar(x1, treadRadius, end);
                var d = Polar(x1, treadRadius, start);
                var e = Polar(x0, baseRadius, start);
                var f = Polar(x0, baseRadius, end);
                var g = Polar(x1, baseRadius, end);
                var h = Polar(x1, baseRadius, start);
                Quad(Rubber, a, b, c, d);
                Quad(Rubber, e, f, b, a);
                Quad(Rubber, d, c, g, h);
                Quad(Rubber, a, d, h, e);
                Quad(Rubber, b, f, g, c);
            }

            void Profile(int material, int sides, params (float x, float radius)[] profile)
            {
                for (int p = 0; p < profile.Length; p++)
                {
                    var from = profile[p];
                    var to = profile[(p + 1) % profile.Length];
                    for (int i = 0; i < sides; i++)
                    {
                        float angle = i * 2 * Mathf.PI / sides;
                        float next = ((i + 1) % sides) * 2 * Mathf.PI / sides;
                        var a = Polar(from.x, from.radius, angle);
                        var b = Polar(from.x, from.radius, next);
                        var c = Polar(to.x, to.radius, next);
                        var d = Polar(to.x, to.radius, angle);
                        if (from.radius == 0 && to.radius > 0) Face(material, a, c, d);
                        else if (to.radius == 0 && from.radius > 0) Face(material, a, b, d);
                        else if (from.radius > 0 && to.radius > 0) Quad(material, a, b, c, d);
                    }
                }
            }

            Vector3[] SpokeSection(float radius, float angle, float halfWidth, float back, float front)
            {
                float radians = angle * Mathf.Deg2Rad;
                Vector3 centre = Polar(0, radius, radians);
                Vector3 tangent = Vector3.Cross(Vector3.right, Polar(0, 1, radians));
                const float bevel = .005f;
                return new[] { Point(-halfWidth + bevel, back), Point(halfWidth - bevel, back),
                    Point(halfWidth, back + bevel), Point(halfWidth, front - bevel),
                    Point(halfWidth - bevel, front), Point(-halfWidth + bevel, front),
                    Point(-halfWidth, front - bevel), Point(-halfWidth, back + bevel) };

                Vector3 Point(float offset, float x) => centre + offset * tangent + x * Vector3.right;
            }

            void Join(int material, Vector3[] from, Vector3[] to)
            {
                for (int i = 0; i < from.Length; i++)
                {
                    int next = (i + 1) % from.Length;
                    Quad(material, from[i], from[next], to[next], to[i]);
                }
            }

            void Quad(int material, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                var indices = triangles[material];
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
            }

            void Face(int material, Vector3 a, Vector3 b, Vector3 c)
            {
                var indices = triangles[material];
                indices.Add(vertices.Count); indices.Add(vertices.Count + 1); indices.Add(vertices.Count + 2);
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
            }
        }

        private static Vector3 Polar(float x, float radius, float radians) =>
            new Vector3(x, radius * Mathf.Cos(radians), radius * Mathf.Sin(radians));

        private static float Widen(float x) => OuterFace + (x - OuterFace) * WidthMultiplier;
    }
}
