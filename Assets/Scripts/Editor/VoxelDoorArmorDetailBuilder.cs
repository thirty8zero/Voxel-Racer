using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Shapes the existing thirty damage pieces without changing their paths or tuning.</summary>
    public static class VoxelDoorArmorDetailBuilder
    {
        public const string MeshPath = "Assets/Prefabs/Upgrades/DoorArmorPanelMeshes.asset";
        public const string Output = "Temp/DoorArmorDetails";
        private const float Size = .14f;
        private const float Height = .56f, HalfWidth = .42f;
        private const float Epsilon = .00001f;

        [MenuItem("Tools/Voxel Racer/Update Door Armour Details")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Update door armour in Edit Mode.");
            var tuning = VoxelArmorTuning.Load();
            if (tuning == null || tuning.panelPrefab == null) throw new InvalidOperationException("Door armour tuning/prefab missing.");
            string path = AssetDatabase.GetAssetPath(tuning.panelPrefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pieces = root.GetComponentsInChildren<VoxelArmorVoxel>(true);
                Check(pieces.Length == 30, "Expected the existing thirty armour pieces.");
                Check(pieces.Count(p => p.transform.localPosition.x > 0) == 24 && pieces.Count(p => p.transform.localPosition.x < 0) == 6,
                    "Expected twenty-four face cells and six lower return-flange cells.");
                var meshes = AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().ToList();
                var front = FrontFacets();
                foreach (var piece in pieces)
                {
                    Check(Vector3.Distance(piece.transform.localScale, Vector3.one * Size) < .0001f &&
                        Quaternion.Angle(piece.transform.localRotation, Quaternion.identity) < .001f, "Unexpected armour cell transform: " + piece.name);
                    var filter = piece.GetComponent<MeshFilter>();
                    Check(filter != null && piece.GetComponent<MeshRenderer>() != null, "Missing original damage renderer.");
                    var generated = piece.transform.localPosition.x < 0
                        ? ReturnFootMesh(root.transform, piece.transform)
                        : CellMesh(root.transform, piece.transform, front);
                    try
                    {
                        string meshName = piece.name + " Detailed Plate";
                        var saved = AssetDatabase.GetAssetPath(filter.sharedMesh) == MeshPath
                            ? filter.sharedMesh : meshes.FirstOrDefault(m => m.name == meshName);
                        if (saved == null)
                        {
                            saved = new Mesh { name = meshName };
                            if (AssetDatabase.LoadMainAssetAtPath(MeshPath) == null)
                            { saved.name = "DoorArmorPanelMeshes"; AssetDatabase.CreateAsset(saved, MeshPath); }
                            else AssetDatabase.AddObjectToAsset(saved, MeshPath);
                            meshes.Add(saved);
                        }
                        // Assign mesh buffers through the Mesh API so Unity uploads live GPU
                        // data immediately; CopySerialized alone can leave a blank preview.
                        saved.Clear(); saved.vertices = generated.vertices; saved.triangles = generated.triangles;
                        saved.normals = generated.normals; saved.RecalculateBounds();
                        saved.name = AssetDatabase.IsMainAsset(saved) ? "DoorArmorPanelMeshes" : meshName;
                        EditorUtility.SetDirty(saved);
                        filter.sharedMesh = saved;
                    }
                    finally { Object.DestroyImmediate(generated); }
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                foreach (var mesh in meshes) AssetDatabase.SaveAssetIfDirty(mesh);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("Updated door armour bevels, raised rim, pressed ribs and four corner fasteners; retained thirty damage pieces, materials, mounts and tuning.");
        }

        private static List<Vector3[]> FrontFacets()
        {
            var result = new List<Vector3[]>();
            // The original contact plane stays at X=0. The perimeter tapers to a thin edge,
            // rises through a broad chamfer and narrow rim, then steps into a recessed face.
            var rings = new[] { Ring(0, .026f), Ring(.045f, .116f), Ring(.061f, .140f),
                Ring(.079f, .140f), Ring(.105f, .108f) };
            for (int ring = 0; ring < rings.Length - 1; ring++)
                for (int i = 0; i < 8; i++)
                {
                    int next = (i + 1) % 8;
                    AddFacet(result, new[] { rings[ring][i], rings[ring][next], rings[ring + 1][next], rings[ring + 1][i] });
                }
            // Two shallow chevron ribs are pressed into the face. Flat shoulders and centres
            // give deliberate creases rather than a smoothed/triangulated noisy surface.
            float[] levels = { -1, -.105f, -.083f, -.061f, -.039f, .021f, .043f, .065f, .087f, 1 };
            float[] depths = { .108f, .108f, .132f, .132f, .108f, .108f, .132f, .132f, .108f, .108f };
            for (int side = 0; side < 2; side++)
            {
                float z0 = side == 0 ? -1 : 0, z1 = side == 0 ? 0 : 1;
                for (int band = 0; band < levels.Length - 1; band++)
                {
                    Vector3 P(int row, float z) => new Vector3(depths[row], .28f + levels[row] + .10f * Mathf.Abs(z), z);
                    var polygon = new List<Vector3> { P(band, z0), P(band + 1, z0), P(band + 1, z1), P(band, z1) };
                    polygon = ClipOutline(polygon, rings[rings.Length - 1]);
                    AddFacet(result, polygon);
                }
            }
            return result;
        }

        private static Vector3[] Ring(float inset, float x)
        {
            float y0 = inset, y1 = Height - inset, z0 = -HalfWidth + inset, z1 = HalfWidth - inset;
            float corner = Mathf.Max(.012f, .065f - inset * .35f);
            return new[] { new Vector3(x, y0, z0 + corner), new Vector3(x, y0 + corner, z0),
                new Vector3(x, y1 - corner, z0), new Vector3(x, y1, z0 + corner),
                new Vector3(x, y1, z1 - corner), new Vector3(x, y1 - corner, z1),
                new Vector3(x, y0 + corner, z1), new Vector3(x, y0, z1 - corner) };
        }

        internal static Mesh CreatePanelCellMesh(Transform root, Transform piece, float widthScale)
        {
            var front = FrontFacets();
            foreach (var face in front)
                for (int i = 0; i < face.Length; i++) face[i].z *= widthScale;
            return root.InverseTransformPoint(piece.position).x < 0
                ? ReturnFootMesh(root, piece) : CellMesh(root, piece, front, widthScale);
        }

        private static Mesh CellMesh(Transform root, Transform piece, List<Vector3[]> front, float widthScale = 1f)
        {
            var centre = root.InverseTransformPoint(piece.position);
            float y0 = centre.y - Size / 2, y1 = centre.y + Size / 2;
            float z0 = centre.z - Size / 2, z1 = centre.z + Size / 2;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var matrix = piece.worldToLocalMatrix * root.localToWorldMatrix;
            var outline = Ring(0, 0);
            for (int i = 0; i < outline.Length; i++) outline[i].z *= widthScale;
            foreach (var face in front)
            {
                var polygon = new List<Vector3>(face);
                polygon = ClipCell(polygon, y0, y1, z0, z1);
                Emit(polygon);
                for (int i = 0; i < polygon.Count; i++)
                {
                    Vector3 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                    if (Vector3.Distance(a, b) < Epsilon) continue;
                    bool wall = Same(a.y, b.y, y0) || Same(a.y, b.y, y1) || Same(a.z, b.z, z0) || Same(a.z, b.z, z1);
                    for (int edge = 0; edge < outline.Length && !wall; edge++)
                        wall = Mathf.Abs(EdgeDistance(outline[edge], outline[(edge + 1) % outline.Length], a)) < Epsilon &&
                            Mathf.Abs(EdgeDistance(outline[edge], outline[(edge + 1) % outline.Length], b)) < Epsilon;
                    if (wall)
                        Emit(new[] { a, new Vector3(0, a.y, a.z), new Vector3(0, b.y, b.z), b });
                }
            }
            var back = ClipCell(new List<Vector3>(outline), y0, y1, z0, z1);
            back.Reverse();
            Emit(back);
            // Small chamfered hex fasteners belong to their original voxel renderer. They
            // disappear with that piece and add no integrity, colliders or draw calls.
            foreach (float y in new[] { .088f, .472f })
                foreach (float z in new[] { -.315f * widthScale, .315f * widthScale })
                    if (y > y0 && y < y1 && z > z0 && z < z1)
                    {
                        var seat = Hex(.120f, .024f, y, z);
                        var shoulder = Hex(.153f, .024f, y, z);
                        var head = Hex(.162f, .017f, y, z);
                        for (int i = 0; i < 6; i++)
                        {
                            int next = (i + 1) % 6;
                            Emit(new[] { seat[i], seat[next], shoulder[next], shoulder[i] });
                            Emit(new[] { shoulder[i], shoulder[next], head[next], head[i] });
                        }
                        Emit(head);
                    }
            var mesh = new Mesh { name = piece.name + " Detailed Plate" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;

            void Emit(IList<Vector3> polygon)
            {
                for (int i = 1; i + 1 < polygon.Count; i++)
                {
                    var a = matrix.MultiplyPoint3x4(polygon[0]);
                    var b = matrix.MultiplyPoint3x4(polygon[i]);
                    var c = matrix.MultiplyPoint3x4(polygon[i + 1]);
                    if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-12f) continue;
                    int start = vertices.Count;
                    vertices.Add(a); vertices.Add(b); vertices.Add(c);
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                }
            }
        }

        private static Mesh ReturnFootMesh(Transform root, Transform piece)
        {
            // The six rearward lower blocks are the existing L-shaped return flange,
            // not a fifth row of face cells. Keep their body contact and damage paths.
            var centre = root.InverseTransformPoint(piece.position);
            var profile = new[] { new Vector2(-.14f, .020f), new Vector2(-.12f, 0), new Vector2(0, 0),
                new Vector2(0, .14f), new Vector2(-.12f, .14f), new Vector2(-.14f, .12f) };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            var matrix = piece.worldToLocalMatrix * root.localToWorldMatrix;
            var back = profile.Select(p => new Vector3(p.x, p.y, centre.z - Size / 2)).ToArray();
            var front = profile.Select(p => new Vector3(p.x, p.y, centre.z + Size / 2)).ToArray();
            Add(front); Add(back.Reverse().ToArray());
            for (int i = 0; i < profile.Length; i++)
            {
                int next = (i + 1) % profile.Length;
                Add(new[] { back[i], back[next], front[next], front[i] });
            }
            var mesh = new Mesh { name = piece.name + " Detailed Return Flange" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
            void Add(Vector3[] face)
            {
                for (int i = 1; i + 1 < face.Length; i++)
                {
                    int start = vertices.Count;
                    vertices.Add(matrix.MultiplyPoint3x4(face[0])); vertices.Add(matrix.MultiplyPoint3x4(face[i])); vertices.Add(matrix.MultiplyPoint3x4(face[i + 1]));
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                }
            }
        }

        private static Vector3[] Hex(float x, float radius, float y, float z) => Enumerable.Range(0, 6)
            .Select(i => new Vector3(x, y + Mathf.Cos(i * Mathf.PI / 3) * radius, z + Mathf.Sin(i * Mathf.PI / 3) * radius)).ToArray();
        private static bool Same(float a, float b, float edge) => Mathf.Abs(a - edge) < Epsilon && Mathf.Abs(b - edge) < Epsilon;
        private static float EdgeDistance(Vector3 a, Vector3 b, Vector3 p) => (b.y - a.y) * (p.z - a.z) - (b.z - a.z) * (p.y - a.y);
        private static List<Vector3> ClipOutline(List<Vector3> polygon, Vector3[] outline)
        {
            for (int i = 0; i < outline.Length; i++)
            {
                Vector3 a = outline[i], b = outline[(i + 1) % outline.Length];
                polygon = Clip(polygon, p => EdgeDistance(a, b, p));
            }
            return polygon;
        }
        private static List<Vector3> ClipCell(List<Vector3> polygon, float y0, float y1, float z0, float z1)
        {
            polygon = Clip(polygon, p => p.y - y0); polygon = Clip(polygon, p => y1 - p.y);
            polygon = Clip(polygon, p => p.z - z0); return Clip(polygon, p => z1 - p.z);
        }
        private static List<Vector3> Clip(List<Vector3> source, Func<Vector3, float> distance)
        {
            var output = new List<Vector3>();
            for (int i = 0; i < source.Count; i++)
            {
                Vector3 a = source[i], b = source[(i + 1) % source.Count];
                float da = distance(a), db = distance(b);
                bool insideA = da >= -Epsilon, insideB = db >= -Epsilon;
                if (insideA) output.Add(a);
                if (insideA != insideB) output.Add(Vector3.LerpUnclamped(a, b, da / (da - db)));
            }
            return output;
        }
        private static void AddFacet(List<Vector3[]> output, IList<Vector3> polygon)
        {
            for (int i = 1; i + 1 < polygon.Count; i++) output.Add(new[] { polygon[0], polygon[i], polygon[i + 1] });
        }

        public static void RenderBefore() => Render(VoxelArmorTuning.Load().panelPrefab, "BeforePanel", new Vector3(2, .9f, -1), new Vector3(.07f, .28f, 0), .53f);

        [MenuItem("Tools/Voxel Racer/Validate and Render Door Armour Details")]
        public static void ValidateAndRender()
        {
            Directory.CreateDirectory(Output);
            var tuning = VoxelArmorTuning.Load();
            var definition = VoxelUpgradeFitCatalog.Assets<VoxelCarDefinition>().First(c => tuning.Fits(c));
            string tuningBefore = EditorJsonUtility.ToJson(tuning);
            int cash = VoxelCurrencyState.Balance, damage = VoxelCarRunState.MissingVoxelCount;
            bool left = VoxelArmorUpgradeState.IsLeftPurchased, right = VoxelArmorUpgradeState.IsRightPurchased;
            var prefabPieces = tuning.panelPrefab.GetComponentsInChildren<VoxelArmorVoxel>(true);
            Check(prefabPieces.Length == 30 && tuning.panelPrefab.GetComponentsInChildren<MeshRenderer>(true).Length == 30, "Armour integrity/draw count changed.");
            Check(tuning.panelPrefab.GetComponentsInChildren<Collider>(true).Length == 0, "Armour gained colliders.");
            int triangles = prefabPieces.Sum(p => p.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
            Check(triangles < 3000, "Armour geometry exceeds mobile budget.");
            Check(prefabPieces.All(p => AssetDatabase.GetAssetPath(p.GetComponent<MeshFilter>().sharedMesh) == MeshPath), "Missing detailed damage-piece mesh.");
            var entries = VoxelUpgradeFitCatalog.Discover();
            var armour = entries.Where(e => e.Asset == tuning).ToArray();
            Check(armour.Length == 2 && armour.All(e => e.Fits(definition)), "Both armour sides must appear in fit preview.");
            var car = Object.Instantiate(definition.visualPrefab);
            try
            {
                var original = car.GetComponentsInChildren<Renderer>(true).ToArray();
                int baseline = VoxelCarSelectionState.CountIntegrityVoxels(car);
                armour[0].Build(car.transform);
                Check(car.GetComponentsInChildren<VoxelArmorVoxel>(true).Length == 30 && VoxelCarSelectionState.CountIntegrityVoxels(car) == baseline + 30, "Individual armour fit/integrity failed.");
                armour[1].Build(car.transform);
                foreach (var entry in armour) entry.Build(car.transform);
                var mounted = car.GetComponentsInChildren<VoxelArmorVoxel>(true);
                Check(mounted.Length == 60 && VoxelCarSelectionState.CountIntegrityVoxels(car) == baseline + 60, "Combined/idempotent armour integrity failed.");
                Check(mounted.All(p => p.RemainingHealth == tuning.voxelHitPoints), "Mounted armour health changed.");
                foreach (var mount in new[] { VoxelArmorUpgradeState.RightMountName, VoxelArmorUpgradeState.LeftMountName })
                {
                    var side = car.transform.Find(VoxelArmorUpgradeState.UpgradeRootName + "/" + mount);
                    var panel = side.GetChild(0);
                    bool isRight = mount == VoxelArmorUpgradeState.RightMountName;
                    Check(Vector3.Dot(panel.right, isRight ? car.transform.right : -car.transform.right) > .999f, "Armour faces inward.");
                    Check(Mathf.Abs(panel.localPosition.x - (isRight ? tuning.rightMountPosition.x : -tuning.rightMountPosition.x)) < .0001f, "Armour mount moved.");
                }
                Render(car, "MountedRight", new Vector3(5, 2.1f, -4), new Vector3(0, .8f, 0), 2.5f);
                Render(car, "MountedLeft", new Vector3(-5, 2.1f, -4), new Vector3(0, .8f, 0), 2.5f);
                Render(car, "MountedCloseup", new Vector3(4.5f, 1.8f, -1.7f), new Vector3(1.1f, .78f, -.17f), .87f);
                foreach (var entry in entries.Where(e => e.Asset != tuning && e.Fits(definition))) entry.Build(car.transform);
                Render(car, "CombinedRight", new Vector3(5, 2.1f, -4), new Vector3(0, .8f, 0), 2.5f);
                Render(car, "CombinedLeft", new Vector3(-5, 2.1f, -4), new Vector3(0, .8f, 0), 2.5f);
                foreach (var renderer in original.Where(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null)) renderer.enabled = false;
                Render(car, "BodyHiddenRight", new Vector3(5, 2.1f, -4), new Vector3(0, .8f, 0), 2.5f);
                Render(car, "BodyHiddenLeft", new Vector3(-5, 2.1f, -4), new Vector3(0, .8f, 0), 2.5f);
                foreach (var renderer in original) renderer.enabled = false;
                Render(car, "UpgradesOnlyRight", new Vector3(5, 2.1f, -4), new Vector3(0, .8f, 0), 2.5f);
                Render(car, "UpgradesOnlyLeft", new Vector3(-5, 2.1f, -4), new Vector3(0, .8f, 0), 2.5f);
            }
            finally { Object.DestroyImmediate(car); }
            Render(tuning.panelPrefab, "DetailedPanel", new Vector3(2, .9f, -1), new Vector3(.07f, .28f, 0), .53f);
            Check(tuningBefore == EditorJsonUtility.ToJson(tuning) && cash == VoxelCurrencyState.Balance && damage == VoxelCarRunState.MissingVoxelCount &&
                left == VoxelArmorUpgradeState.IsLeftPurchased && right == VoxelArmorUpgradeState.IsRightPurchased, "Preview altered tuning/currency/damage/ownership.");
            File.WriteAllText(Output + "/Validation.txt", "PASS: thirty original damage pieces/renderers per panel; " + triangles + " triangles; no colliders; current HP retained; both automatic fit entries; individual/combined/idempotent mounts; mirrored outward orientation; +30/+60 integrity; compatible combined upgrades; both-side mounted/body-hidden/upgrades-only renders; unchanged tuning, currency, damage and ownership.");
            Debug.Log(File.ReadAllText(Output + "/Validation.txt"));
            VoxelUpgradeFitPreview.Open(tuning);
        }

        private static void Render(GameObject source, string name, Vector3 cameraPosition, Vector3 target, float zoom)
        {
            Directory.CreateDirectory(Output);
            var preview = new PreviewRenderUtility();
            try
            {
                var copy = Object.Instantiate(source);
                foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                preview.AddSingleGO(copy);
                preview.camera.orthographic = true; preview.camera.orthographicSize = zoom;
                preview.camera.transform.position = cameraPosition; preview.camera.transform.LookAt(target);
                preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 50;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.13f, .16f, .19f);
                preview.ambientColor = new Color(.45f, .45f, .45f);
                preview.lights[0].intensity = 1.6f; preview.lights[0].transform.rotation = Quaternion.Euler(35, -60, 0);
                preview.lights[1].intensity = 1.2f; preview.lights[1].transform.rotation = Quaternion.Euler(25, 120, 0);
                preview.BeginStaticPreview(new Rect(0, 0, 1200, 800)); preview.Render(true);
                var image = preview.EndStaticPreview();
                try { File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); }
            }
            finally { preview.Cleanup(); }
        }
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("Door armour details: " + message); }
    }
}
