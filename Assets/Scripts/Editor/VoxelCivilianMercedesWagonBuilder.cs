using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Hollow S123-inspired estate using the standard destructible civilian model conventions.</summary>
    public static class VoxelCivilianMercedesWagonBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Cars/CivilianMercedesWagon.prefab";
        public const string TuningPath = "Assets/Resources/EnemyVehicles/CivilianMercedesWagonTuning.asset";
        public const int PieceBudget = 650;
        private static Material paint, glass, rubber, chrome, lamp, red, amber;

        [MenuItem("Tools/Voxel Racer/Build Civilian Mercedes Wagon")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Build outside Play Mode.");
            Material Shared(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CarMaterials/CivilianHatch" + name + ".mat");
            paint = PaintMaterial();
            glass = Shared("Glass"); rubber = Shared("Trim"); chrome = Shared("Rim");
            lamp = Shared("Lamp"); red = Shared("TailLamp"); amber = Shared("Indicator");
            var root = new GameObject("Civilian Mercedes Wagon");
            try
            {
                root.AddComponent<VoxelTrafficPaint>().bodyMaterial = paint;
                foreach (int side in new[] { -1, 1 })
                {
                    // Only exterior panels. The cabin and lower shell are hollow.
                    for (int row = 0; row < 4; row++)
                        for (int col = 0; col < 16; col++)
                        {
                            float z = -2.26f + (col + .5f) * 4.52f / 16;
                            float y = .50f + row * .18f;
                            if (new[] { -1.50f, 1.45f }.Any(w => Mathf.Abs(z - w) < .50f &&
                                y < .42f + Mathf.Sqrt(Mathf.Max(0, .53f * .53f - (z - w) * (z - w))))) continue;
                            Box(root.transform, "Side body voxel", new Vector3(side * 1.0f, y, z), new Vector3(.16f, .18f, 4.52f / 16), paint);
                        }
                    for (int row = 0; row < 4; row++)
                    {
                        float back = Back(row), front = Front(row), step = (front - back) / 12;
                        for (int col = 0; col < 12; col++)
                        {
                            // Three large side windows separated by the wagon's four pillars.
                            bool pillar = col == 0 || col == 4 || col == 8 || col == 11;
                            Box(root.transform, pillar ? "Cabin pillar voxel" : "Side window voxel",
                                new Vector3(side * (1.0f - row * .025f), 1.195f + row * .14f, back + (col + .5f) * step),
                                new Vector3(.14f, .14f, step), pillar ? paint : glass);
                        }
                    }
                    foreach (float z in new[] { .90f, -.10f, -1.14f })
                        Box(root.transform, "Door seam", new Vector3(side * 1.085f, .82f, z), new Vector3(.012f, .50f, .014f), rubber);
                    foreach (float z in new[] { .06f, -.97f })
                        Box(root.transform, "Chrome door handle", new Vector3(side * 1.10f, 1.04f, z), new Vector3(.035f, .045f, .19f), chrome);
                    for (int col = 0; col < 6; col++)
                    {
                        float z = -2.26f + (col + .5f) * 4.52f / 6;
                        Box(root.transform, "Black side moulding", new Vector3(side * 1.085f, .72f, z), new Vector3(.025f, .07f, 4.52f / 6), rubber);
                        Box(root.transform, "Chrome beltline", new Vector3(side * 1.086f, 1.125f, z), new Vector3(.024f, .035f, 4.52f / 6), chrome);
                    }
                    Box(root.transform, "Mirror mount", new Vector3(side * 1.08f, 1.25f, .79f), new Vector3(.16f, .05f, .10f), rubber);
                    Box(root.transform, "Chrome mirror", new Vector3(side * 1.23f, 1.28f, .76f), new Vector3(.20f, .14f, .22f), chrome);
                    Box(root.transform, "Mirror glass", new Vector3(side * 1.23f, 1.28f, .64f), new Vector3(.15f, .095f, .02f), glass);
                    foreach (float z in new[] { -1.50f, 1.45f })
                    {
                        Wheel(root.transform, side, z);
                        float[] arch = { -.54f, -.45f, -.25f, 0f, .25f, .45f, .54f };
                        for (int edge = 0; edge < arch.Length - 1; edge++)
                        {
                            Vector3 Point(float dz) => new Vector3(side * 1.087f, .42f + Mathf.Sqrt(.55f * .55f - dz * dz), z + dz);
                            Vector3 a = Point(arch[edge]), b = Point(arch[edge + 1]);
                            var lip = Box(root.transform, "Painted wheel arch lip", (a + b) * .5f,
                                new Vector3(.035f, .045f, Vector3.Distance(a, b) + .025f), paint);
                            lip.localRotation = Quaternion.LookRotation(b - a);
                        }
                    }
                    // Thin rails and four mounts leave the roof visible between them.
                    Box(root.transform, "Chrome roof rail", new Vector3(side * .79f, 1.88f, -.76f), new Vector3(.045f, .05f, 2.30f), chrome);
                    foreach (float z in new[] { -1.70f, .18f })
                        Box(root.transform, "Roof rail mount", new Vector3(side * .79f, 1.80f, z), new Vector3(.055f, .12f, .065f), rubber);
                }
                foreach (int end in new[] { -1, 1 })
                    for (int row = 0; row < 4; row++)
                        for (int col = 0; col < 8; col++)
                            Box(root.transform, "End body voxel", new Vector3((col - 3.5f) * .27f, .50f + row * .18f, end * 2.26f), new Vector3(.27f, .18f, .16f), paint);
                for (int row = 0; row < 4; row++)
                    for (int col = 0; col < 8; col++)
                    {
                        float step = (2.16f - row * .05f) / 8;
                        foreach (int end in new[] { -1, 1 })
                            Box(root.transform, end == 1 ? "Windscreen voxel" : "Rear screen voxel",
                                new Vector3((col - 3.5f) * step, 1.195f + row * .14f, end == 1 ? Front(row) : Back(row)),
                                new Vector3(step, .14f, .24f), col == 0 || col == 7 ? paint : glass);
                    }
                for (int x = 0; x < 8; x++)
                {
                    for (int z = 0; z < 3; z++)
                        Box(root.transform, "Bonnet voxel", new Vector3((x - 3.5f) * .27f, 1.16f, .78f + (z + .5f) * 1.56f / 3), new Vector3(.27f, .06f, 1.56f / 3), paint);
                    for (int z = 0; z < 5; z++)
                        Box(root.transform, "Roof voxel", new Vector3((x - 3.5f) * 2.05f / 8, 1.725f, -2.08f + (z + .5f) * 2.72f / 5), new Vector3(2.05f / 8, .08f, 2.72f / 5), paint);
                }
                foreach (int end in new[] { -1, 1 })
                {
                    for (int x = 0; x < 6; x++)
                        Box(root.transform, "Black bumper voxel", new Vector3((x - 2.5f) * .38f, .59f, end * 2.39f), new Vector3(.38f, .17f, .18f), rubber);
                    Box(root.transform, "Chrome bumper cap", new Vector3(0, .69f, end * 2.41f), new Vector3(2.20f, .04f, .16f), chrome);
                    Box(root.transform, "Number plate", new Vector3(0, end == 1 ? .58f : .94f, end * (end == 1 ? 2.49f : 2.35f)), new Vector3(.47f, .15f, .025f), lamp);
                    Box(root.transform, "Plate inset", new Vector3(0, end == 1 ? .58f : .94f, end * (end == 1 ? 2.505f : 2.365f)), new Vector3(.30f, .035f, .015f), rubber);
                }
                // Upright chrome grille with narrow mesh bars and the characteristic centre rib.
                Box(root.transform, "Grille inset", new Vector3(0, .93f, 2.353f), new Vector3(.98f, .47f, .025f), rubber);
                foreach (int sign in new[] { -1, 1 })
                {
                    Box(root.transform, "Grille chrome horizontal", new Vector3(0, .93f + sign * .245f, 2.375f), new Vector3(1.02f, .035f, .04f), chrome);
                    Box(root.transform, "Grille chrome vertical", new Vector3(sign * .51f, .93f, 2.375f), new Vector3(.035f, .47f, .04f), chrome);
                    Box(root.transform, "Headlamp surround", new Vector3(sign * .78f, .95f, 2.35f), new Vector3(.48f, .32f, .04f), chrome);
                    Box(root.transform, "Rectangular headlamp", new Vector3(sign * .73f, .95f, 2.38f), new Vector3(.34f, .26f, .025f), lamp);
                    Box(root.transform, "Amber corner indicator", new Vector3(sign * .99f, .95f, 2.375f), new Vector3(.14f, .28f, .035f), amber);
                    for (int row = 0; row < 3; row++)
                        Box(root.transform, "Ribbed rear lamp", new Vector3(sign * .82f, .84f + row * .085f, -2.355f), new Vector3(.40f, .08f, .035f), row == 2 ? amber : row == 0 ? red : lamp);
                }
                for (int row = 0; row < 5; row++)
                    Box(root.transform, "Chrome grille slat", new Vector3(0, .755f + row * .087f, 2.376f), new Vector3(.95f, .012f, .025f), chrome);
                Box(root.transform, "Chrome grille centre rib", new Vector3(0, .93f, 2.395f), new Vector3(.035f, .47f, .025f), chrome);
                Badge(root.transform, "Hood star", new Vector3(0, 1.255f, 2.25f), .13f);
                Badge(root.transform, "Tailgate star", new Vector3(0, 1.08f, -2.355f), .12f);
                Box(root.transform, "Tailgate handle", new Vector3(0, 1.13f, -2.36f), new Vector3(.30f, .035f, .025f), chrome);
                foreach (int sign in new[] { -1, 1 })
                    Box(root.transform, "Tailgate seam", new Vector3(sign * .69f, .91f, -2.35f), new Vector3(.014f, .41f, .015f), rubber);
                var wiper = Box(root.transform, "Rear window wiper", new Vector3(-.12f, 1.34f, Back(1) - .14f), new Vector3(.015f, .36f, .018f), rubber);
                wiper.localRotation = Quaternion.Euler(0, 0, -55f);
                Box(root.transform, "Exhaust tip", new Vector3(.70f, .36f, -2.39f), new Vector3(.10f, .08f, .23f), chrome);
                VoxelCivilianDrivelineBuilder.AddStructure(root);
                int count = root.GetComponentsInChildren<MeshRenderer>().Length;
                if (count > PieceBudget) throw new InvalidOperationException("Mercedes wagon exceeds mobile piece budget: " + count);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var tuning = AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>(TuningPath);
                if (tuning == null)
                {
                    tuning = Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning"));
                    tuning.name = "CivilianMercedesWagonTuning"; tuning.displayName = "Civilian Mercedes Wagon";
                    tuning.collisionHalfWidth = 1.45f; tuning.collisionHalfLength = 2.70f;
                    AssetDatabase.CreateAsset(tuning, TuningPath);
                }
                tuning.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                EditorUtility.SetDirty(tuning); Register(tuning); AssetDatabase.SaveAssets();
                Debug.Log("Civilian Mercedes wagon built: " + count + " pieces including three protected driveline pieces.");
            }
            finally { Object.DestroyImmediate(root); }
            Render();
        }

        private static float Front(int row) => .90f - row * .13f;
        private static float Back(int row) => -2.20f + row * .08f;

        private static void Register(VoxelEnemyVehicleTuning vehicle)
        {
            var defaults = new[] { "TrafficCarTuning", "CivilianVanTuning", "CivilianBeetleTuning", "CivilianKombiTuning", "CivilianJettaTuning" }
                .Select(n => Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/" + n)).Where(v => v != null).ToArray();
            foreach (string guid in AssetDatabase.FindAssets("t:VoxelTrackDefinition").Concat(AssetDatabase.FindAssets("t:VoxelObstacleCarTuning")).Distinct())
                foreach (var traffic in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<VoxelObstacleCarTuning>())
                {
                    var existing = traffic.civilianVehiclePool;
                    if (existing != null && existing.Contains(vehicle)) continue;
                    traffic.civilianVehiclePool = (existing != null && existing.Length > 0 ? existing : defaults).Concat(new[] { vehicle }).ToArray();
                    EditorUtility.SetDirty(traffic);
                }
        }

        private static Material PaintMaterial()
        {
            const string path = "Assets/Resources/CarMaterials/MercedesWagonIvoryPaint.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(.83f, .84f, .78f)); material.SetFloat("_Smoothness", .28f);
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material; return go.transform;
        }

        private static void Badge(Transform parent, string name, Vector3 centre, float size)
        {
            for (int spoke = 0; spoke < 3; spoke++)
            {
                float angle = spoke * 120f;
                var ray = Quaternion.Euler(0, 0, angle) * Vector3.up;
                var part = Box(parent, name, centre + ray * size * .25f, new Vector3(.017f, size * .5f, .025f), chrome);
                part.localRotation = Quaternion.Euler(0, 0, angle);
            }
        }

        private static void Wheel(Transform parent, int side, float z)
        {
            var wheel = new GameObject("Obstacle Voxel Wheel").transform;
            wheel.SetParent(parent, false); wheel.localPosition = new Vector3(side * 1.055f, .42f, z);
            int[] widths = { 3, 5, 7, 7, 7, 5, 3 };
            for (int row = 0; row < 7; row++)
                Box(wheel, "Stepped tyre", new Vector3(0, (row - 3) * .12f, 0), new Vector3(.27f, .12f, widths[row] * .12f), rubber);
            int[] hubs = { 3, 5, 5, 5, 3 };
            for (int row = 0; row < 5; row++)
                Box(wheel, "Silver alloy rim", new Vector3(side * .15f, (row - 2) * .105f, 0), new Vector3(.035f, .105f, hubs[row] * .105f), chrome);
            for (int vent = 0; vent < 8; vent++)
            {
                var rotation = Quaternion.Euler(vent * 45f, 0, 0);
                var part = Box(wheel, "Alloy wheel vent", new Vector3(side * .174f, 0, 0) + rotation * Vector3.up * .195f,
                    new Vector3(.014f, .055f, .075f), rubber);
                part.localRotation = rotation;
            }
            Box(wheel, "Silver hubcap", new Vector3(side * .18f, 0, 0), new Vector3(.025f, .14f, .14f), chrome);
        }

        [MenuItem("Tools/Voxel Racer/Render Civilian Mercedes Wagon")]
        public static void Render()
        {
            Directory.CreateDirectory("Temp/MercedesWagon");
            string[] names = { "Front", "Rear", "Left", "Right", "FrontStraight", "RearStraight", "Top" };
            Vector3[] views = { new(5, 3.2f, 7), new(-5, 3.2f, -7), new(-8, 1.10f, 0), new(8, 1.10f, 0), new(0, 1.10f, 8), new(0, 1.10f, -8), new(0, 8, .001f) };
            for (int i = 0; i < views.Length; i++)
            {
                var preview = new PreviewRenderUtility();
                try
                {
                    preview.AddSingleGO(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
                    preview.camera.transform.position = views[i]; preview.camera.transform.LookAt(new Vector3(0, .95f, 0));
                    preview.camera.fieldOfView = 36; preview.camera.orthographic = i >= 2;
                    preview.camera.orthographicSize = i == 6 ? 2.85f : 1.85f;
                    preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 50;
                    preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.17f, .20f, .23f);
                    preview.lights[0].intensity = 1.8f; preview.lights[0].transform.rotation = Quaternion.Euler(40, 25, 0);
                    preview.lights[1].intensity = 1.3f; preview.lights[1].transform.rotation = Quaternion.Euler(30, 210, 0);
                    preview.ambientColor = new Color(.45f, .45f, .45f);
                    preview.BeginStaticPreview(new Rect(0, 0, 1100, 750)); preview.Render(true);
                    var image = preview.EndStaticPreview(); File.WriteAllBytes("Temp/MercedesWagon/" + names[i] + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
                }
                finally { preview.Cleanup(); }
            }
        }
    }
}
