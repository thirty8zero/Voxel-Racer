using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelVanditoBossAppearanceBuilder
    {
        private const string PaintPath = "Assets/Resources/Bosses/VanditoBossPaint.mat";
        private const string RedPath = "Assets/Resources/Bosses/VanditoBossRed.mat";
        private const string LogoFolder = "Assets/Resources/Bosses/VanditoSideLogo";
        private const string LogoRedPath = LogoFolder + "/VanditoBossLogoRed.mat";
        private const string LogoWord = "VANDITO";
        // Use visible gaps between the hand-set pixels and between letters so the
        // name reads clearly at gameplay scale.
        private const float LogoCellWidth = .052f;
        private const float LogoCellHeight = .068f;
        private const float LogoCellGap = .014f;
        private const float LogoGlyphAdvance = .35f;
        private const float LogoItalicShear = .007f;
        private const float LogoCentreY = 1.72f;
        private const float LogoCentreZ = -1.1f;
        private static readonly Color BodyColour = new Color(.018f, .021f, .028f);
        private static readonly Color AccentColour = new Color(1f, .012f, .025f);
        // Hand-set 5x7 pixel glyphs, slanted row by row. They stay legible at play
        // distance and have the blocky, custom-built look of the van's rear eyes.
        private static readonly Dictionary<char, string[]> LogoGlyphs = new()
        {
            { 'V', new[] { "10001", "10001", "10001", "01010", "01010", "00100", "00100" } },
            { 'A', new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" } },
            { 'N', new[] { "10001", "11001", "11001", "10101", "10011", "10011", "10001" } },
            { 'D', new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" } },
            { 'I', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" } },
            { 'T', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" } },
            { 'O', new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" } }
        };

        [MenuItem("Tools/Voxel Racer/Update Vandito Boss Appearance")]
        public static void UpdatePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(VoxelRedVanBossBuilder.PrefabPath);
            try
            {
                Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, VoxelRedVanBossBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Voxel Racer/Update Vandito Side Lettering")]
        public static void UpdateSideLettering()
        {
            var root = PrefabUtility.LoadPrefabContents(VoxelRedVanBossBuilder.PrefabPath);
            try
            {
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Vandito Side Lettering") Object.DestroyImmediate(child.gameObject);
                AddSideLogo(root.transform, AssetDatabase.LoadAssetAtPath<Material>(PaintPath),
                    AssetDatabase.LoadAssetAtPath<Material>(RedPath));
                PrefabUtility.SaveAsPrefabAsset(root, VoxelRedVanBossBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject root, Material originalBodyMaterial = null)
        {
            var marker = root.GetComponent<VoxelTrafficPaint>();
            if (originalBodyMaterial == null && marker != null) originalBodyMaterial = marker.bodyMaterial;

            var paint = AssetDatabase.LoadAssetAtPath<Material>(PaintPath);
            if (paint == null)
            {
                if (originalBodyMaterial == null)
                    throw new System.InvalidOperationException("Vandito Boss paint material is missing and no source paint was supplied.");
                paint = new Material(originalBodyMaterial) { name = "VanditoBossPaint" };
                AssetDatabase.CreateAsset(paint, PaintPath);
            }
            paint.SetColor("_BaseColor", BodyColour);
            EditorUtility.SetDirty(paint);

            var accent = AssetDatabase.LoadAssetAtPath<Material>(RedPath);
            if (accent == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CarMaterials/TransitRed.mat");
                accent = new Material(source) { name = "VanditoBossRed" };
                AssetDatabase.CreateAsset(accent, RedPath);
            }
            accent.SetColor("_BaseColor", AccentColour);
            EditorUtility.SetDirty(accent);

            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (originalBodyMaterial != null && renderer.sharedMaterial == originalBodyMaterial)
                    renderer.sharedMaterial = paint;
                if (renderer.sharedMaterial == paint)
                    renderer.sharedMaterial = paint;
                if (renderer.gameObject.name == "Steel wheel row" ||
                    renderer.gameObject.name == "Side belt trim" ||
                    renderer.gameObject.name == "Stepped round headlamp")
                    renderer.sharedMaterial = accent;
            }

            RemoveExistingTrim(root.transform);
            AddFrontWindowTrim(root.transform, paint);
            AddRearWindowTrim(root, paint);
            AddWheelRivets(root.transform, paint);
            AddSideLogo(root.transform, paint, accent);
        }

        private static void RemoveExistingTrim(Transform root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var child in transforms)
            {
                if (child.name == "Vandito Front Window Trim" ||
                    child.name == "Vandito Rear Window Trim Left" ||
                    child.name == "Vandito Rear Window Trim Right" ||
                    child.name == "Vandito Wheel Rivet" ||
                    child.name == "Vandito Side Lettering")
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void AddFrontWindowTrim(Transform root, Material material)
        {
            const float angle = -18.4f;
            var rotation = Quaternion.Euler(angle, 0, 0);
            float centreY = 1.66f;
            float centreZ = 1.46f;
            float WindowZ(float y) => centreZ - (y - centreY) * .3333f;
            const float width = 1.58f;
            const float sideX = .79f;

            CreateTrimBox(root, root, "Vandito Front Window Trim", new Vector3(-sideX, centreY, centreZ), new Vector3(.07f, .86f, .07f), rotation, material);
            CreateTrimBox(root, root, "Vandito Front Window Trim", new Vector3(sideX, centreY, centreZ), new Vector3(.07f, .86f, .07f), rotation, material);
            float bottomY = 1.25f, topY = 2.07f;
            CreateTrimBox(root, root, "Vandito Front Window Trim", new Vector3(0, bottomY, WindowZ(bottomY)), new Vector3(width, .07f, .07f), rotation, material);
            CreateTrimBox(root, root, "Vandito Front Window Trim", new Vector3(0, topY, WindowZ(topY)), new Vector3(width, .07f, .07f), rotation, material);
        }

        private static void AddRearWindowTrim(GameObject root, Material material)
        {
            var rig = root.GetComponent<VoxelBossSpikeRig>();
            if (rig == null || rig.leftDoor == null || rig.rightDoor == null) return;
            AddRearWindowHalf(root.transform, rig.leftDoor, "Vandito Rear Window Trim Left", -1, material);
            AddRearWindowHalf(root.transform, rig.rightDoor, "Vandito Rear Window Trim Right", 1, material);
        }

        private static void AddRearWindowHalf(Transform root, Transform hinge, string name, int side, Material material)
        {
            float outsideX = side * .79f;
            float middleX = side * .025f;
            float centreX = side * .4075f;
            CreateTrimBox(root, hinge, name, new Vector3(outsideX, 1.58f, -2.39f), new Vector3(.07f, .56f, .07f), Quaternion.identity, material);
            CreateTrimBox(root, hinge, name, new Vector3(centreX, 1.84f, -2.39f), new Vector3(.77f, .07f, .07f), Quaternion.identity, material);
            CreateTrimBox(root, hinge, name, new Vector3(centreX, 1.32f, -2.39f), new Vector3(.77f, .07f, .07f), Quaternion.identity, material);
            // The center ends meet beside the existing rear-door seam without covering the eyes.
            CreateTrimBox(root, hinge, name, new Vector3(middleX, 1.58f, -2.39f), new Vector3(.05f, .56f, .07f), Quaternion.identity, material);
        }

        private static void CreateTrimBox(Transform root, Transform parent, string objectName,
            Vector3 rootLocalPosition, Vector3 size, Quaternion rootLocalRotation, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(root, false);
            box.transform.localPosition = rootLocalPosition;
            box.transform.localRotation = rootLocalRotation;
            box.transform.localScale = size;
            box.transform.SetParent(parent, true);
            box.AddComponent<VoxelIndestructiblePart>();
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void AddWheelRivets(Transform root, Material black)
        {
            const float radius = .16f;
            foreach (var wheel in root.GetComponentsInChildren<Transform>(true))
            {
                if (wheel.name != "Obstacle Voxel Wheel") continue;
                float outside = Mathf.Sign(wheel.localPosition.x);
                float face = outside * .205f;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    var position = wheel.localPosition + new Vector3(face,
                        Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);
                    CreateTrimBox(root, wheel, "Vandito Wheel Rivet", position,
                        new Vector3(.035f, .045f, .045f), Quaternion.identity, black);
                }
            }
        }

        private static void AddSideLogo(Transform root, Material paint, Material accent)
        {
            Directory.CreateDirectory(LogoFolder);
            AssetDatabase.Refresh();
            AssetDatabase.DeleteAsset(LogoFolder + "/VanditoBossLogoShadow.mat");
            var logoRed = AssetDatabase.LoadAssetAtPath<Material>(LogoRedPath);
            if (logoRed == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                logoRed = new Material(shader != null ? shader : accent.shader) { name = "VanditoBossLogoRed" };
                AssetDatabase.CreateAsset(logoRed, LogoRedPath);
            }
            logoRed.SetColor("_BaseColor", AccentColour);
            EditorUtility.SetDirty(logoRed);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (cube == null) throw new System.InvalidOperationException("Built-in cube mesh is unavailable for Vandito side logo.");

            float glyphWidth = 5 * LogoCellWidth;
            float glyphHeight = 7 * LogoCellHeight;
            float wordWidth = (LogoWord.Length - 1) * LogoGlyphAdvance + glyphWidth;
            var panelsBySide = new Dictionary<int, List<MeshRenderer>> { { -1, new() }, { 1, new() } };
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.gameObject.name != "Van body voxel") continue;
                Vector3 centre = root.InverseTransformPoint(renderer.transform.position);
                if (Mathf.Abs(Mathf.Abs(centre.x) - 1.04f) > .05f) continue;
                int side = centre.x < 0 ? -1 : 1;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null) continue;
                filter.sharedMesh = cube;
                renderer.sharedMaterials = new[] { paint };
                panelsBySide[side].Add(renderer);
            }

            var usedMeshPaths = new HashSet<string>();
            // Like the rear eyes, clip artwork to each existing voxel and bake it
            // into a second submesh. No floating signs or extra damage targets.
            foreach (int side in new[] { -1, 1 })
            {
                int panelIndex = 0;
                foreach (var panel in panelsBySide[side])
                {
                    var bounds = new Bounds(root.InverseTransformPoint(panel.transform.position), Vector3.zero);
                    foreach (var vertex in cube.vertices)
                        bounds.Encapsulate(root.InverseTransformPoint(panel.transform.TransformPoint(vertex)));
                    float surfaceX = side < 0 ? bounds.min.x - .002f : bounds.max.x + .002f;
                    var vertices = new List<Vector3>();
                    var triangles = new List<int>();
                    for (int letter = 0; letter < LogoWord.Length; letter++)
                    {
                        float letterLeft = -wordWidth * .5f + letter * LogoGlyphAdvance;
                        string[] glyph = LogoGlyphs[LogoWord[letter]];
                        for (int row = 0; row < glyph.Length; row++)
                        for (int column = 0; column < glyph[row].Length; column++)
                        {
                            if (glyph[row][column] != '1') continue;
                            float x = letterLeft + (column + .5f) * LogoCellWidth + (6 - row) * LogoItalicShear;
                            float y = LogoCentreY + glyphHeight * .5f - (row + .5f) * LogoCellHeight;
                            // Outside viewers see +Z to their right on the right side.
                            float z = LogoCentreZ + side * x;
                            float halfWidth = (LogoCellWidth - LogoCellGap) * .5f;
                            float halfHeight = (LogoCellHeight - LogoCellGap) * .5f;
                            float left = Mathf.Max(z - halfWidth, bounds.min.z);
                            float right = Mathf.Min(z + halfWidth, bounds.max.z);
                            float bottom = Mathf.Max(y - halfHeight, bounds.min.y);
                            float top = Mathf.Min(y + halfHeight, bounds.max.y);
                            if (right <= left || top <= bottom) continue;
                            int first = vertices.Count;
                            foreach (var point in new[] {
                                new Vector3(surfaceX,bottom,left), new Vector3(surfaceX,top,left),
                                new Vector3(surfaceX,top,right), new Vector3(surfaceX,bottom,right) })
                                vertices.Add(panel.transform.InverseTransformPoint(root.TransformPoint(point)));
                            if(side > 0) triangles.AddRange(new[] {first,first+1,first+2,first,first+2,first+3});
                            else triangles.AddRange(new[] {first,first+2,first+1,first,first+3,first+2});
                        }
                    }
                    string meshPath = LogoFolder + "/VanditoSideLogo_" + (side < 0 ? "Left" : "Right") + "_Panel" + panelIndex++ + ".asset";
                    var decal = MakeQuadMesh("Side panel lettering", vertices, triangles);
                    if (decal == null) continue;
                    usedMeshPaths.Add(meshPath);
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    bool isNew = saved == null;
                    if (isNew) saved = new Mesh();
                    // Mesh API updates refresh native vertex buffers reliably, as for the eyes.
                    saved.Clear();
                    saved.CombineMeshes(new[] {
                        new CombineInstance { mesh=cube, transform=Matrix4x4.identity },
                        new CombineInstance { mesh=decal, transform=Matrix4x4.identity }
                    }, false, true);
                    saved.name = Path.GetFileNameWithoutExtension(meshPath);
                    if(isNew) AssetDatabase.CreateAsset(saved,meshPath);
                    else EditorUtility.SetDirty(saved);
                    panel.GetComponent<MeshFilter>().sharedMesh = saved;
                    panel.sharedMaterials = new[] { paint, logoRed };
                    Object.DestroyImmediate(decal);
                }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { LogoFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!usedMeshPaths.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        private static Mesh MakeQuadMesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

    }
}
