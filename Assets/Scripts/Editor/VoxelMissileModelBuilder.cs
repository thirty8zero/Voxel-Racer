using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Compact faceted models shared by full builds and visual-only prefab updates.</summary>
    public static class VoxelMissileModelBuilder
    {
        private const string MeshFolder = "Assets/Prefabs/Weapons/Meshes";

        [MenuItem("Tools/Voxel Racer/Update Missile and Launcher Models")]
        public static void UpdateModels()
        {
            Material M(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CarMaterials/" + name + ".mat")
                ?? throw new InvalidOperationException("Missing missile material: " + name);
            var metal = M("MissileLauncherSteel"); var dark = M("MissileLauncherDark");
            var silver = M("MissileBody"); var red = M("MissileNose");
            const string missilePath = "Assets/Prefabs/Weapons/Missile.prefab";
            var missile = PrefabUtility.LoadPrefabContents(missilePath);
            try
            {
                RemoveVisuals(missile);
                BuildMissile(missile, metal, dark, silver, red);
                // Preserve particle tuning; only move the outlets to the detailed rear nozzle.
                foreach (var ps in missile.GetComponentsInChildren<ParticleSystem>())
                    ps.transform.localPosition = new Vector3(0, 0, -.425f);
                PrefabUtility.SaveAsPrefabAsset(missile, missilePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(missile); }
            UpdateLauncherModel(metal, dark, silver, red);
            var tuning = VoxelMissileLauncherTuning.Load();
            Undo.RecordObject(tuning, "Centre missile launchers on roof");
            var position = tuning.mountPosition; position.z = -.56f; tuning.mountPosition = position;
            EditorUtility.SetDirty(tuning); AssetDatabase.SaveAssetIfDirty(tuning);
            Debug.Log("Updated slim roof-centred launchers and detailed faceted missiles; retained weapon and particle tuning.");
        }

        [MenuItem("Tools/Voxel Racer/Update Missile Launcher Brackets")]
        public static void UpdateLauncherBrackets()
        {
            Material M(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CarMaterials/" + name + ".mat")
                ?? throw new InvalidOperationException("Missing missile material: " + name);
            const string launcherPath = "Assets/Prefabs/Weapons/MissileLauncher.prefab";
            var launcher = PrefabUtility.LoadPrefabContents(launcherPath);
            try
            {
                var existing = launcher.transform.Find(VoxelMissileUpgradeState.RoofBracketName);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);
                BuildRoofBrackets(launcher, M("MissileLauncherSteel"), M("MissileLauncherDark"), M("MissileBody"));
                PrefabUtility.SaveAsPrefabAsset(launcher, launcherPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(launcher); }
            Debug.Log("Updated roof brackets and chassis support bars; retained casing, muzzle, weapon and particle tuning.");
        }

        private static void UpdateLauncherModel(Material metal, Material dark, Material silver, Material red)
        {
            const string launcherPath = "Assets/Prefabs/Weapons/MissileLauncher.prefab";
            var launcher = PrefabUtility.LoadPrefabContents(launcherPath);
            try
            {
                RemoveVisuals(launcher);
                BuildLauncher(launcher, metal, dark, silver, red);
                var mount = launcher.GetComponent<VoxelGunMount>();
                mount.muzzle.localPosition = new Vector3(0, .17f, 1f);
                VoxelMissileBuilder.EnsureLauncherLaunchBurst(launcher);
                PrefabUtility.SaveAsPrefabAsset(launcher, launcherPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(launcher); }
        }

        private static void RemoveVisuals(GameObject root)
        {
            var bracket = root.transform.Find(VoxelMissileUpgradeState.RoofBracketName);
            if (bracket != null) Object.DestroyImmediate(bracket.gameObject);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) Object.DestroyImmediate(r.gameObject);
        }

        public static void BuildMissile(GameObject root, Material metal, Material dark, Material silver, Material red)
        {
            var cylinder = Solid(.5f); var shoulder = Solid(.32f); var nose = Solid(.12f);
            var tube = Tube(); var fin = Fin();
            Part(root.transform, "Faceted fuselage", cylinder, new Vector3(0, 0, -.03f), new Vector3(.15f, .15f, .56f), silver);
            Part(root.transform, "Engine collar", cylinder, new Vector3(0, 0, -.30f), new Vector3(.17f, .17f, .07f), metal);
            Part(root.transform, "Open exhaust nozzle", tube, new Vector3(0, 0, -.37f), new Vector3(.115f, .115f, .09f), dark);
            Part(root.transform, "Warhead separation band", cylinder, new Vector3(0, 0, .18f), new Vector3(.157f, .157f, .028f), dark);
            Part(root.transform, "Tapered warhead shoulder", shoulder, new Vector3(0, 0, .29f), new Vector3(.15f, .15f, .10f), red);
            Part(root.transform, "Faceted nose", nose, new Vector3(0, 0, .43f), new Vector3(.10f, .10f, .18f), red);
            Part(root.transform, "Nose cap", cylinder, new Vector3(0, 0, .532f), new Vector3(.026f, .026f, .025f), red);
            for (int i = 0; i < 4; i++)
            {
                var rotation = Quaternion.AngleAxis(i * 90, Vector3.forward);
                var part = Part(root.transform, "Swept tail fin", fin, rotation * new Vector3(.117f, 0, -.255f), new Vector3(.10f, .018f, .23f), metal);
                part.transform.localRotation = rotation;
            }
            Combine(root, "Missile");
        }

        public static void BuildLauncher(GameObject root, Material metal, Material dark, Material silver, Material red)
        {
            var tube = Tube(); var cylinder = Solid(.5f); var nose = Solid(.12f);
            Part(root.transform, "Hollow octagonal casing", tube, new Vector3(0, .17f, 0), new Vector3(.25f, .25f, 1.08f), metal);
            foreach (int end in new[] { -1, 1 })
            {
                Part(root.transform, "Reinforced barrel collar", tube, new Vector3(0, .17f, end * .555f), new Vector3(.28f, .28f, .055f), silver);
            }
            Box(root.transform, "Upper guide rib", new Vector3(0, .294f, -.04f), new Vector3(.05f, .02f, .70f), silver);
            foreach (int side in new[] { -1, 1 })
                for (int i = 0; i < 3; i++)
                    Box(root.transform, "Casing vent", new Vector3(side * .119f, .17f, -.25f + i * .19f), new Vector3(.018f, .06f, .085f), dark);
            // A recessed rounded warhead makes the hollow muzzle readable at game scale.
            Part(root.transform, "Loaded missile shoulder", cylinder, new Vector3(0, .17f, .34f), new Vector3(.14f, .14f, .16f), silver);
            Part(root.transform, "Loaded tapered warhead", nose, new Vector3(0, .17f, .475f), new Vector3(.14f, .14f, .11f), red);
            Part(root.transform, "Loaded nose cap", cylinder, new Vector3(0, .17f, .54f), new Vector3(.035f, .035f, .025f), red);
            Combine(root, "MissileLauncher");
            BuildRoofBrackets(root, metal, dark, silver);
        }

        private static void BuildRoofBrackets(GameObject launcher, Material metal, Material dark, Material silver)
        {
            var bracket = new GameObject(VoxelMissileUpgradeState.RoofBracketName);
            bracket.transform.SetParent(launcher.transform, false);
            var bolt = Solid(.5f); var gusset = Fin();
            foreach (int end in new[] { -1, 1 })
            {
                float z = end * .34f;
                Box(bracket.transform, "Roof anchor foot", new Vector3(-.11f, -.005f, z), new Vector3(.25f, .026f, .20f), metal);
                Box(bracket.transform, "Upright angle plate", new Vector3(.018f, .064f, z), new Vector3(.028f, .155f, .20f), metal);
                Box(bracket.transform, "Casing contact pad", new Vector3(.004f, .106f, z), new Vector3(.010f, .072f, .13f), dark);
                foreach (int edge in new[] { -1, 1 })
                {
                    // Fin's triangular prism is reused as a gusset in the bracket's XY plane.
                    var brace = Part(bracket.transform, "Triangular angle gusset", gusset,
                        new Vector3(-.0905f, .068f, z + edge * .076f), new Vector3(.189f, .018f, .12f), metal);
                    brace.transform.localRotation = Quaternion.Euler(-90, 180, 0);
                }
                foreach (float x in new[] { -.18f, -.055f })
                {
                    var washer = Part(bracket.transform, "Roof bolt washer", bolt, new Vector3(x, .0105f, z), new Vector3(.048f, .048f, .005f), dark);
                    washer.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    var head = Part(bracket.transform, "Roof anchor bolt", bolt, new Vector3(x, .019f, z), new Vector3(.034f, .034f, .014f), silver);
                    head.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
                var clampWasher = Part(bracket.transform, "Upright bolt washer", bolt, new Vector3(.0015f, .106f, z), new Vector3(.046f, .046f, .005f), dark);
                clampWasher.transform.localRotation = Quaternion.Euler(0, 90, 0);
                var clampBolt = Part(bracket.transform, "Upright securing bolt", bolt, new Vector3(-.008f, .106f, z), new Vector3(.032f, .032f, .014f), silver);
                clampBolt.transform.localRotation = Quaternion.Euler(0, 90, 0);

                // Bridge the roof skin to the protected roll-cage rail below it.
                // This bracket group stays upright/inboard on either launcher side.
                Vector3 upper = new Vector3(-.18f, -.010f, z);
                Vector3 lower = new Vector3(-.39f, -.29f, z);
                var support = Box(bracket.transform, "Chassis support bar", (upper + lower) * .5f,
                    new Vector3(.06f, .06f, Vector3.Distance(upper, lower) + .035f), metal);
                support.transform.localRotation = Quaternion.LookRotation(upper - lower);
                Box(bracket.transform, "Chassis rail saddle", lower, new Vector3(.095f, .065f, .12f), metal);
            }
            Combine(bracket, "MissileLauncherBracket");
            // Canonical right-side mounting; CreateVisual levels and mirrors this for either side.
            bracket.transform.localRotation = Quaternion.Euler(0, 0, 45);
        }

        private static GameObject Part(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale, Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var part = Part(parent, name, cube.GetComponent<MeshFilter>().sharedMesh, position, scale, material);
            Object.DestroyImmediate(cube);
            return part;
        }

        private static void Combine(GameObject root, string prefix)
        {
            Directory.CreateDirectory(MeshFolder);
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            var temporary = renderers.Select(r => r.GetComponent<MeshFilter>().sharedMesh)
                .Where(m => m.name.StartsWith("Missile Model Unit")).Distinct().ToArray();
            foreach (var group in renderers.GroupBy(r => r.sharedMaterial))
            {
                var parts = group.Select(r => new CombineInstance {
                    mesh = r.GetComponent<MeshFilter>().sharedMesh,
                    transform = root.transform.worldToLocalMatrix * r.transform.localToWorldMatrix
                }).ToArray();
                string path = MeshFolder + "/" + prefix + "_" + group.Key.name + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool create = saved == null;
                if (create) saved = new Mesh();
                // Native mesh APIs invalidate the existing render buffers as well as CPU data.
                saved.Clear(); saved.CombineMeshes(parts, true, true); saved.name = prefix + "_" + group.Key.name;
                if (create) AssetDatabase.CreateAsset(saved, path);
                else { EditorUtility.SetDirty(saved); AssetDatabase.SaveAssetIfDirty(saved); }
                Part(root.transform, group.Key.name + " detail", saved, Vector3.zero, Vector3.one, group.Key);
            }
            foreach (var r in renderers) Object.DestroyImmediate(r.gameObject);
            foreach (var mesh in temporary) Object.DestroyImmediate(mesh);
        }

        private sealed class MeshWriter
        {
            private readonly List<Vector3> vertices = new();
            private readonly List<int> triangles = new();
            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            { int i = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2); }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            { Triangle(a, b, c); Triangle(a, c, d); }
            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = "Missile Model Unit " + name };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
        }
        private static Vector3 Ring(int i, float radius, float z)
        { float angle = (i + .5f) * Mathf.PI / 4; return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z); }
        private static Mesh Solid(float frontRadius)
        {
            var writer = new MeshWriter();
            for (int i = 0; i < 8; i++)
            {
                var a = Ring(i, .5f, -.5f); var b = Ring(i + 1, .5f, -.5f);
                var c = Ring(i + 1, frontRadius, .5f); var d = Ring(i, frontRadius, .5f);
                writer.Quad(a, b, c, d);
                writer.Triangle(new Vector3(0, 0, -.5f), b, a);
                writer.Triangle(new Vector3(0, 0, .5f), d, c);
            }
            return writer.Mesh("Solid");
        }
        private static Mesh Tube()
        {
            var writer = new MeshWriter();
            for (int i = 0; i < 8; i++)
            {
                var a = Ring(i, .5f, -.5f); var b = Ring(i + 1, .5f, -.5f);
                var c = Ring(i + 1, .5f, .5f); var d = Ring(i, .5f, .5f);
                var e = Ring(i, .36f, -.5f); var f = Ring(i + 1, .36f, -.5f);
                var g = Ring(i + 1, .36f, .5f); var h = Ring(i, .36f, .5f);
                writer.Quad(a, b, c, d); writer.Quad(f, e, h, g);
                writer.Quad(d, c, g, h); writer.Quad(b, a, e, f);
            }
            return writer.Mesh("Tube");
        }
        private static Mesh Fin()
        {
            var writer = new MeshWriter();
            var a = new Vector3(-.5f, -.5f, -.5f); var b = new Vector3(.5f, -.5f, -.5f); var c = new Vector3(-.5f, -.5f, .5f);
            var d = a + Vector3.up; var e = b + Vector3.up; var f = c + Vector3.up;
            writer.Triangle(a, b, c); writer.Triangle(f, e, d);
            writer.Quad(b, a, d, e); writer.Quad(c, b, e, f); writer.Quad(a, c, f, d);
            return writer.Mesh("Fin");
        }
    }
}
