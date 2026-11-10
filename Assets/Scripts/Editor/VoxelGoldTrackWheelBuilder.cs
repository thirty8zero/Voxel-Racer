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
        private const int Rubber = 0, Alloy = 1, Hardware = 2, White = 3;

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
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterials = new[]
                {
                    Resources.Load<Material>(Palette + "PerformanceTyreRubber"), gold,
                    Resources.Load<Material>(Palette + "PerformanceWheelGraphite"),
                    Resources.Load<Material>(Palette + "PerformanceTyreWhite")
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
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

            // Profiles travel from the inboard face to the outside along the tyre's crown.
            Profile(Rubber, 24, (-.146f, .338f), (-.146f, .429f), (-.119f, .461f), (-.086f, .475f),
                (-.041f, .475f), (-.030f, .467f), (-.017f, .475f), (.017f, .475f), (.030f, .467f),
                (.041f, .475f), (.086f, .475f), (.119f, .461f), (.146f, .429f), (.146f, .338f));

            // A full-depth barrel and a stepped lip leave the ten spoke openings visible.
            Profile(Alloy, 30, (-.136f, .313f), (-.136f, .349f), (.117f, .349f), (.143f, .350f),
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

            // Match the 13-degree pitch, radius and face dimensions of the original Sports Wheel tabs.
            foreach (int side in new[] { -1, 1 })
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
                    Vector3 back = Vector3.right * (side < 0 ? -.151f : .145f);
                    Vector3 front = Vector3.right * (side < 0 ? -.145f : .151f);
                    Quad(White, a + front, b + front, c + front, d + front);
                    Quad(White, d + back, c + back, b + back, a + back);
                    Quad(White, b + back, b + front, a + front, a + back);
                    Quad(White, c + back, c + front, b + front, b + back);
                    Quad(White, d + back, d + front, c + front, c + back);
                    Quad(White, a + back, a + front, d + front, d + back);
                }

            // The white jaw sits behind the spokes and deepens across the outer edge of the brake disc.
            const float caliperAngle = 270;
            var innerJaw = SpokeSection(.174f, caliperAngle, .052f, -.032f, .013f);
            var innerShoulder = SpokeSection(.202f, caliperAngle, .091f, -.032f, .024f);
            var outerShoulder = SpokeSection(.248f, caliperAngle, .088f, -.034f, .024f);
            var bridge = SpokeSection(.267f, caliperAngle, .079f, -.091f, .020f);
            var crown = SpokeSection(.285f, caliperAngle, .058f, -.087f, .009f);
            Join(White, innerJaw, innerShoulder);
            Join(White, innerShoulder, outerShoulder);
            Join(White, outerShoulder, bridge);
            Join(White, bridge, crown);
            for (int i = 1; i < crown.Length - 1; i++)
            {
                Face(White, innerJaw[0], innerJaw[i + 1], innerJaw[i]);
                Face(White, crown[0], crown[i], crown[i + 1]);
            }

            mesh.Clear();
            mesh.name = "GoldTrackWheelMesh";
            mesh.SetVertices(vertices);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

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
    }
}
