using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Hollow four-door, short-boot civilian saloon based on the Mk2 reference.</summary>
    public static class VoxelCivilianJettaBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Cars/CivilianJetta.prefab";
        public const string TuningPath = "Assets/Resources/EnemyVehicles/CivilianJettaTuning.asset";
        public const int PieceBudget = 650;
        private static Material paint, glass, rubber, rim, lamp, red, amber, accent;

        [MenuItem("Tools/Voxel Racer/Build Civilian Jetta")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Build outside Play Mode.");
            Material Shared(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CarMaterials/CivilianHatch" + name + ".mat");
            paint = MaterialAsset("JettaRedPaint", new Color(.76f, .035f, .045f));
            accent = MaterialAsset("JettaGrilleAccent", new Color(.62f, .018f, .022f));
            glass = Shared("Glass"); rubber = Shared("Trim"); rim = Shared("Rim");
            lamp = Shared("Lamp"); red = Shared("TailLamp"); amber = Shared("Indicator");
            var root = new GameObject("Civilian Jetta");
            try
            {
                root.AddComponent<VoxelTrafficPaint>().bodyMaterial = paint;
                // Outer strips only. Wheel openings and the hollow cabin avoid hidden pieces.
                foreach (int side in new[] { -1, 1 })
                {
                    for (int row = 0; row < 4; row++)
                        for (int col = 0; col < 16; col++)
                        {
                            float z = -2.03f + (col + .5f) * 4.06f / 16f;
                            float y = .50f + row * .18f;
                            if (new[] { -1.28f, 1.25f }.Any(w => Mathf.Abs(z - w) < .48f &&
                                y < .42f + Mathf.Sqrt(Mathf.Max(0, .52f * .52f - (z - w) * (z - w))))) continue;
                            Box(root.transform, "Side body voxel", new Vector3(side * .96f, y, z), new Vector3(.16f, .18f, 4.06f / 16), paint);
                        }
                    for (int row = 0; row < 5; row++)
                    {
                        float front = Front(row), back = Back(row), step = (front - back) / 12f;
                        for (int col = 0; col < 12; col++)
                        {
                            float z = back + (col + .5f) * step;
                            bool pillar = col == 0 || col == 11 || Mathf.Abs(z + .25f) < step * .65f;
                            Box(root.transform, pillar ? "Cabin pillar voxel" : "Side window voxel",
                                new Vector3(side * (.96f - row * .015f), 1.10f + row * .10f, z),
                                new Vector3(.12f, .10f, step), pillar ? paint : glass);
                        }
                    }
                    foreach (float z in new[] { -.23f, -1.22f, .70f })
                        Box(root.transform, "Door seam", new Vector3(side * 1.044f, .79f, z), new Vector3(.012f, .48f, .014f), rubber);
                    foreach (float z in new[] { -.06f, -1.03f })
                        Box(root.transform, "Black door handle", new Vector3(side * 1.06f, 1.0f, z), new Vector3(.035f, .04f, .17f), rubber);
                    for (int col = 0; col < 16; col++)
                        Box(root.transform, "Side protection strip", new Vector3(side * 1.048f, .67f, -2f + (col + .5f) * .25f), new Vector3(.03f, .055f, .25f), rubber);
                    Box(root.transform, "Side indicator", new Vector3(side * 1.068f, .98f, 1.66f), new Vector3(.02f, .06f, .10f), amber);
                    Box(root.transform, "Mirror mount", new Vector3(side * 1.02f, 1.16f, .64f), new Vector3(.16f, .055f, .12f), rubber);
                    Box(root.transform, "Black mirror", new Vector3(side * 1.14f, 1.21f, .63f), new Vector3(.20f, .14f, .20f), rubber);
                    Box(root.transform, "Mirror glass", new Vector3(side * 1.14f, 1.21f, .52f), new Vector3(.15f, .09f, .02f), rim);
                    foreach (float wheelZ in new[] { -1.28f, 1.25f })
                    {
                        Wheel(root.transform, side, wheelZ);
                        foreach (float dz in new[] { -.45f, -.25f, 0f, .25f, .45f })
                            Box(root.transform, "Black wheel arch trim", new Vector3(side * 1.052f, .42f + Mathf.Sqrt(.53f * .53f - dz * dz), wheelZ + dz), new Vector3(.045f, .07f, .20f), rubber);
                    }
                }
                foreach (int end in new[] { -1, 1 })
                    for (int row = 0; row < 4; row++)
                        for (int col = 0; col < 8; col++)
                            Box(root.transform, "End body voxel", new Vector3((col - 3.5f) * .26f, .50f + row * .18f, end * 2.03f), new Vector3(.26f, .18f, .16f), paint);
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 8; col++)
                    {
                        float step = (2.04f - row * .03f) / 8f;
                        foreach (int end in new[] { -1, 1 })
                            Box(root.transform, end == 1 ? "Windscreen voxel" : "Rear screen voxel",
                                new Vector3((col - 3.5f) * step, 1.10f + row * .10f, end == 1 ? Front(row) : Back(row)),
                                new Vector3(step, .10f, .18f), col == 0 || col == 7 ? paint : glass);
                    }
                for (int x = 0; x < 8; x++)
                {
                    for (int z = 0; z < 4; z++)
                        Box(root.transform, "Bonnet voxel", new Vector3((x - 3.5f) * .26f, 1.105f, .65f + (z + .5f) * 1.46f / 4), new Vector3(.26f, .05f, 1.46f / 4), paint);
                    for (int z = 0; z < 2; z++)
                        Box(root.transform, "Boot lid voxel", new Vector3((x - 3.5f) * .26f, 1.105f, -2.11f + (z + .5f) * .83f / 2), new Vector3(.26f, .05f, .83f / 2), paint);
                    for (int z = 0; z < 4; z++)
                        Box(root.transform, "Roof voxel", new Vector3((x - 3.5f) * .245f, 1.59f, -1.16f + (z + .5f) * 1.56f / 4), new Vector3(.245f, .08f, 1.56f / 4), paint);
                }
                foreach (int end in new[] { -1, 1 })
                {
                    for (int x = 0; x < 8; x++)
                        Box(root.transform, "Black bumper voxel", new Vector3((x - 3.5f) * .27f, .59f, end * 2.16f), new Vector3(.27f, .18f, .16f), rubber);
                    Box(root.transform, "Number plate", new Vector3(0, .61f, end * 2.25f), new Vector3(.54f, .14f, .025f), lamp);
                    Box(root.transform, "Plate inset", new Vector3(0, .61f, end * 2.265f), new Vector3(.35f, .04f, .015f), rubber);
                }
                // Rectangular lamps, slatted grille and thin fixed red GTI-style outline.
                Box(root.transform, "Front grille recess", new Vector3(0, .91f, 2.123f), new Vector3(1.14f, .26f, .03f), rubber);
                for (int row = 0; row < 4; row++)
                    Box(root.transform, "Grille slat", new Vector3(0, .83f + row * .05f, 2.145f), new Vector3(1.10f, .013f, .02f), rim);
                foreach (int sign in new[] { -1, 1 })
                {
                    Box(root.transform, "Grille red horizontal", new Vector3(0, .91f + sign * .137f, 2.151f), new Vector3(1.15f, .018f, .02f), accent);
                    Box(root.transform, "Grille red vertical", new Vector3(sign * .575f, .91f, 2.151f), new Vector3(.018f, .27f, .02f), accent);
                    Box(root.transform, "Headlamp surround", new Vector3(sign * .81f, .92f, 2.13f), new Vector3(.46f, .29f, .04f), rubber);
                    for (int col = 0; col < 2; col++)
                        Box(root.transform, "Rectangular headlamp", new Vector3(sign * (.71f + col * .20f), .92f, 2.159f), new Vector3(.19f, .22f, .024f), lamp);
                    Box(root.transform, "Bumper indicator", new Vector3(sign * .81f, .61f, 2.253f), new Vector3(.25f, .08f, .028f), amber);
                    for (int col = 0; col < 3; col++)
                        Box(root.transform, "Rear lamp voxel", new Vector3(sign * (.56f + col * .18f), .94f, -2.137f), new Vector3(.18f, .20f, .04f), col == 2 ? amber : col == 0 ? lamp : red);
                }
                Box(root.transform, "Grille round badge", new Vector3(0, .91f, 2.167f), new Vector3(.105f, .105f, .025f), rim);
                Box(root.transform, "Sport grille badge", new Vector3(.39f, .90f, 2.163f), new Vector3(.105f, .044f, .024f), accent);
                Box(root.transform, "Rear hatch latch", new Vector3(0, 1.0f, -2.137f), new Vector3(.11f, .04f, .035f), rubber);
                Box(root.transform, "Tailpipe", new Vector3(.69f, .38f, -2.22f), new Vector3(.10f, .09f, .20f), rubber);
                VoxelCivilianDrivelineBuilder.AddStructure(root);
                int count = root.GetComponentsInChildren<MeshRenderer>().Length;
                if (count > PieceBudget) throw new InvalidOperationException("Jetta exceeds mobile piece budget: " + count);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var tuning = AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>(TuningPath);
                if (tuning == null)
                {
                    tuning = Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning"));
                    tuning.name = "CivilianJettaTuning"; tuning.displayName = "Civilian Jetta";
                    tuning.collisionHalfWidth = 1.35f; tuning.collisionHalfLength = 2.45f;
                    AssetDatabase.CreateAsset(tuning, TuningPath);
                }
                tuning.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                EditorUtility.SetDirty(tuning); Register(tuning); AssetDatabase.SaveAssets();
                Debug.Log("Civilian Jetta built: " + count + " pieces including three protected driveline pieces.");
            }
            finally { Object.DestroyImmediate(root); }
            Render();
        }

        private static float Front(int row) => .74f - row * .11f;
        private static float Back(int row) => -1.36f + row * .08f;

        private static void Register(VoxelEnemyVehicleTuning vehicle)
        {
            var defaults = new[] { "TrafficCarTuning", "CivilianVanTuning", "CivilianBeetleTuning", "CivilianKombiTuning" }
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
        private static Material MaterialAsset(string name, Color colour)
        {
            string path = "Assets/Resources/CarMaterials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", colour); material.SetFloat("_Smoothness", .28f);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        private static void Wheel(Transform parent, int side, float z)
        {
            var wheel = new GameObject("Obstacle Voxel Wheel").transform;
            wheel.SetParent(parent, false); wheel.localPosition = new Vector3(side * 1.0f, .42f, z);
            int[] widths = { 3, 5, 7, 7, 7, 5, 3 };
            for (int row = 0; row < 7; row++)
                Box(wheel, "Stepped tyre", new Vector3(0, (row - 3) * .115f, 0), new Vector3(.26f, .115f, widths[row] * .115f), rubber);
            int[] hubs = { 3, 5, 5, 5, 3 };
            for (int row = 0; row < 5; row++)
                Box(wheel, "Silver wheel rim", new Vector3(side * .145f, (row - 2) * .10f, 0), new Vector3(.035f, .10f, hubs[row] * .10f), rim);
            Box(wheel, "Wheel hub", new Vector3(side * .171f, 0, 0), new Vector3(.022f, .13f, .13f), rubber);
        }

        [MenuItem("Tools/Voxel Racer/Render Civilian Jetta")]
        public static void Render()
        {
            Directory.CreateDirectory("Temp/Jetta");
            string[] names = { "Front", "Rear", "Left", "Right", "FrontStraight", "RearStraight", "Top" };
            Vector3[] views = { new(5, 3, 7), new(-5, 3, -7), new(-8, 1.0f, 0), new(8, 1.0f, 0), new(0, 1.0f, 8), new(0, 1.0f, -8), new(0, 8, .001f) };
            for (int i = 0; i < views.Length; i++)
            {
                var preview = new PreviewRenderUtility();
                try
                {
                    preview.AddSingleGO(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
                    preview.camera.transform.position = views[i]; preview.camera.transform.LookAt(new Vector3(0, .85f, 0));
                    preview.camera.fieldOfView = 34; preview.camera.orthographic = i >= 2;
                    preview.camera.orthographicSize = i == 6 ? 2.65f : 1.70f;
                    preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 50;
                    preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.17f, .20f, .23f);
                    preview.lights[0].intensity = 1.8f; preview.lights[0].transform.rotation = Quaternion.Euler(40, 25, 0);
                    preview.lights[1].intensity = 1.3f; preview.lights[1].transform.rotation = Quaternion.Euler(30, 210, 0);
                    preview.ambientColor = new Color(.45f, .45f, .45f);
                    preview.BeginStaticPreview(new Rect(0, 0, 1100, 750)); preview.Render(true);
                    var image = preview.EndStaticPreview(); File.WriteAllBytes("Temp/Jetta/" + names[i] + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
                }
                finally { preview.Cleanup(); }
            }
        }
    }
}
