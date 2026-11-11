using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelMinigunBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Weapons/Minigun.prefab";
        private const string TuningPath = "Assets/Resources/Weapons/MinigunTuning.asset";
        private const string MeshFolder = "Assets/Prefabs/Weapons/Meshes/";

        [MenuItem("Tools/Voxel Racer/Build Minigun Upgrade")]
        public static void Build()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<VoxelGunTuning>(TuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<VoxelGunTuning>();
                tuning.displayName = "Minigun";
                tuning.compatibleCarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cars/SpyCar2PlayerCar.prefab");
                tuning.upgradeLevel = 2;
                tuning.purchasePrice = 750;
                tuning.maximumPurchases = 2;
                tuning.shotsPerSecond = 14f;
                tuning.barrelSpinDegreesPerSecond = 1080f;
                tuning.tracerLength = .42f;
                tuning.spreadDegrees = .8f;
                tuning.projectileSpeed = 90f;
                tuning.maximumRange = 108f;
                AssetDatabase.CreateAsset(tuning, TuningPath);
            }

            var source = VoxelGunUpgradeState.LongGunTuning.visualPrefab;
            var dark = Resources.Load<Material>("CarMaterials/V6DarkMetal");
            var gunmetal = Resources.Load<Material>("CarMaterials/WheelSpikeGunmetal");
            var silver = Resources.Load<Material>("CarMaterials/CarMetalDetails");
            var blue = Resources.Load<Material>("CarMaterials/BoostBottleBlue");
            var root = new GameObject("Minigun");
            var tube = BuildTube();
            try
            {
                // Match the Thump Cannon seat and reach the same protected chassis anchor.
                root.transform.localScale = Vector3.one * .5f;
                root.AddComponent<VoxelIndestructiblePart>();
                var brace = Object.Instantiate(source.transform.Find("Protected Chassis Brace").gameObject, root.transform, false);
                brace.name = "Protected Chassis Brace";
                var housing = new MeshGroup(root.transform, "Minigun Housing", tube);
                housing.Box(dark, 0, .051f, 0, .59f, .102f, .58f);
                housing.Box(silver, 0, .112f, .035f, .48f, .047f, .50f);
                housing.Box(dark, 0, .246f, .091f, .46f, .28f, .57f);
                housing.Box(gunmetal, 0, .366f, .081f, .374f, .13f, .52f);
                housing.Box(dark, 0, .31f, -.226f, .366f, .209f, .095f);
                housing.Box(silver, 0, .31f, -.284f, .266f, .133f, .027f);
                housing.Tube(gunmetal, 0, .31f, .364f, .529f, .136f);
                housing.Tube(silver, 0, .31f, .435f, .468f, .054f);
                for (int side = -1; side <= 1; side += 2)
                {
                    housing.Box(gunmetal, side * .227f, .265f, .091f, .063f, .171f, .456f);
                    housing.Box(blue, side * .259f, .309f, .066f, .012f, .036f, .306f);
                    for (int index = 0; index < 4; index++)
                        housing.Box(silver, side * .263f, .227f, -.060f + .089f * index, .023f, .029f, .037f);
                    for (int end = -1; end <= 1; end += 2)
                        housing.Box(silver, side * .223f, .134f, end * .186f, .043f, .026f, .043f);
                }
                for (int index = 0; index < 4; index++)
                    housing.Box(silver, 0, .444f, -.067f + .091f * index, .239f, .026f, .044f);
                housing.Bake();

                var rotor = new GameObject("Rotating Barrel Cluster").transform;
                rotor.SetParent(root.transform, false);
                rotor.localPosition = new Vector3(0f, .31f, .374f);
                var barrels = new MeshGroup(rotor, "Minigun Rotor", tube);
                barrels.Tube(dark, 0, 0, .11f, .439f, .123f);
                barrels.Tube(gunmetal, 0, 0, .52f, .459f, .064f);
                barrels.Tube(dark, 0, 0, 1.112f, .455f, .066f);
                barrels.Tube(silver, 0, 0, 1.148f, .441f, .028f);
                var muzzles = new Transform[6];
                for (int index = 0; index < muzzles.Length; index++)
                {
                    float angle = index * Mathf.PI / 3f;
                    float x = Mathf.Sin(angle) * .146f;
                    float y = Mathf.Cos(angle) * .146f;
                    barrels.Tube(gunmetal, x, y, .678f, .103f, 1.256f);
                    barrels.Tube(silver, x, y, .29f, .112f, .062f);
                    barrels.Tube(silver, x, y, 1.344f, .115f, .155f);
                    barrels.Tube(dark, x, y, 1.417f, .109f, .030f);
                    var muzzle = new GameObject("Muzzle " + (index + 1)).transform;
                    muzzle.SetParent(rotor, false);
                    muzzle.localPosition = new Vector3(x, y, 1.438f);
                    muzzles[index] = muzzle;
                }
                barrels.Bake();
                var mount = root.AddComponent<VoxelGunMount>();
                mount.tuning = tuning;
                mount.muzzle = muzzles[0];
                mount.ConfigureRotor(rotor, muzzles);

                tuning.visualPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                EditorUtility.SetDirty(tuning);
                AssetDatabase.SaveAssetIfDirty(tuning);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tube);
            }
        }

        private static Mesh BuildTube()
        {
            const int sides = 8;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 Point(int side, float radius, float z)
            {
                float angle = (side + .5f) * Mathf.PI * 2f / sides;
                return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
            }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
            }
            for (int side = 0; side < sides; side++)
            {
                int next = side + 1;
                Vector3 a = Point(side, .5f, -.5f), b = Point(next, .5f, -.5f);
                Vector3 c = Point(side, .5f, .5f), d = Point(next, .5f, .5f);
                Vector3 e = Point(side, .378f, -.5f), f = Point(next, .378f, -.5f);
                Vector3 g = Point(side, .378f, .5f), h = Point(next, .378f, .5f);
                Quad(a, b, d, c);
                Quad(e, g, h, f);
                Quad(c, d, h, g);
                Quad(a, e, f, b);
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private sealed class MeshGroup
        {
            private readonly Transform parent;
            private readonly string name;
            private readonly Mesh tube;
            private readonly Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            private readonly Dictionary<Material, List<CombineInstance>> batches = new();

            public MeshGroup(Transform parent, string name, Mesh tube)
            {
                this.parent = parent;
                this.name = name;
                this.tube = tube;
            }

            public void Box(Material material, float x, float y, float z, float w, float h, float d)
                => Add(material, cube, new Vector3(x, y, z), new Vector3(w, h, d));

            public void Tube(Material material, float x, float y, float z, float diameter, float depth)
                => Add(material, tube, new Vector3(x, y, z), new Vector3(diameter, diameter, depth));

            private void Add(Material material, Mesh mesh, Vector3 position, Vector3 scale)
            {
                if (!batches.TryGetValue(material, out var parts))
                    batches.Add(material, parts = new List<CombineInstance>());
                parts.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, Quaternion.identity, scale) });
            }

            public void Bake()
            {
                foreach (var batch in batches)
                {
                    string path = MeshFolder + (name + "_" + batch.Key.name).Replace(' ', '_') + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    bool created = mesh == null;
                    if (created) mesh = new Mesh { name = name + " " + batch.Key.name };
                    else mesh.Clear();
                    mesh.CombineMeshes(batch.Value.ToArray());
                    if (created) AssetDatabase.CreateAsset(mesh, path);
                    else
                    {
                        EditorUtility.SetDirty(mesh);
                        AssetDatabase.SaveAssetIfDirty(mesh);
                    }
                    var part = new GameObject(batch.Key.name);
                    part.transform.SetParent(parent, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    part.AddComponent<MeshRenderer>().sharedMaterial = batch.Key;
                }
            }
        }
    }
}
