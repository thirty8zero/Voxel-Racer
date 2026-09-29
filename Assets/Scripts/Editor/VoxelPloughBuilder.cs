using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelPloughBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Upgrades/Plough.prefab";
        [MenuItem("Tools/Voxel Racer/Build Plough Upgrade")]
        public static void Build()
        {
            var root = new GameObject("Plough");
            root.AddComponent<VoxelIndestructiblePart>();
            try
            {
                var steel = Material("PloughSteel", new Color(.43f, .47f, .50f), .45f);
                var edge = Material("PloughEdge", new Color(.13f, .15f, .17f), .35f);
                var bolts = Material("PloughBolts", new Color(.67f, .70f, .72f), .6f);
                // Two shallow swept wings, stepped cross-section and dark replaceable cutting edge.
                foreach (int side in new[] { -1, 1 })
                {
                    var wing = new GameObject(side < 0 ? "Left blade" : "Right blade").transform;
                    wing.SetParent(root.transform, false);
                    wing.localPosition = new Vector3(side * .71f, 0, .16f);
                    wing.localRotation = Quaternion.Euler(0, side * 14f, 0);
                    for (int row = 0; row < 4; row++)
                        Block(wing, "Steel blade row " + row, new Vector3(0, .13f + row * .17f, -.025f * row * row),
                            new Vector3(1.46f, .175f, .14f), steel);
                    Block(wing, "Cutting edge", new Vector3(0, .035f, .025f), new Vector3(1.49f, .085f, .18f), edge);
                    Block(wing, "Top rim", new Vector3(0, .74f, -.22f), new Vector3(1.48f, .075f, .17f), bolts);
                    for (int i = 0; i < 7; i++)
                        Block(wing, "Edge bolt " + i, new Vector3(-.61f + i * .20f, .075f, .122f), Vector3.one * .042f, bolts);
                    for (int i = 0; i < 3; i++)
                        Block(wing, "Rear rib " + i, new Vector3(-.5f + i * .5f, .40f, -.25f), new Vector3(.075f, .62f, .12f), edge);
                    Block(root.transform, "Mount arm " + side, new Vector3(side * .60f, .23f, -.33f), new Vector3(.14f, .16f, .84f), edge);
                }
                Block(root.transform, "Centre spine", new Vector3(0, .38f, .30f), new Vector3(.085f, .74f, .13f), steel);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            const string path = "Assets/Resources/Upgrades/PloughTuning.asset";
            var tuning = AssetDatabase.LoadAssetAtPath<VoxelPloughTuning>(path);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<VoxelPloughTuning>();
                tuning.compatibleCarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cars/SpyCar2PlayerCar.prefab");
                AssetDatabase.CreateAsset(tuning, path);
            }
            tuning.ploughPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            EditorUtility.SetDirty(tuning);
            AssetDatabase.SaveAssets();
        }
        private static Material Material(string name, Color colour, float metallic)
        {
            string path = "Assets/Resources/CarMaterials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", .28f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        private static void Block(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            Object.DestroyImmediate(block.GetComponent<Collider>());
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = size;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
