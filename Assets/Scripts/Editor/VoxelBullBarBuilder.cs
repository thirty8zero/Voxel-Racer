using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelBullBarBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Upgrades/BullBar.prefab";
        private const string MeshPath = "Assets/Prefabs/Upgrades/BullBarMesh.asset";
        private const string TuningPath = "Assets/Resources/Upgrades/BullBarTuning.asset";

        [MenuItem("Tools/Voxel Racer/Build Bull Bar Upgrade")]
        public static void Build()
        {
            var root = new GameObject("BullBar");
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cube = primitive.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(primitive);
            var sections = new[] { new List<CombineInstance>(), new List<CombineInstance>(), new List<CombineInstance>() };
            var materialMeshes = new Mesh[sections.Length];
            try
            {
                // Two open rectangular rails leave the driving lamps visible through the frame.
                Box(0, new Vector3(0f, .390f, .011f), new Vector3(1.50f, .104f, .106f));
                Box(0, new Vector3(0f, .018f, .061f), new Vector3(1.76f, .128f, .127f));
                foreach (int side in new[] { -1, 1 })
                {
                    Box(0, new Vector3(side * .944f, -.005f, .016f), new Vector3(.255f, .113f, .122f), side * 24f);
                    Box(1, new Vector3(side * 1.046f, -.005f, -.030f), new Vector3(.047f, .112f, .121f), side * 24f);
                    Box(1, new Vector3(side * .755f, .390f, .011f), new Vector3(.037f, .103f, .105f));
                    Box(1, new Vector3(side * .379f, .195f, .105f), new Vector3(.120f, .498f, .108f));
                    Box(0, new Vector3(side * .379f, .194f, .165f), new Vector3(.079f, .315f, .024f));
                    Box(1, new Vector3(side * .379f, .112f, -.137f), new Vector3(.134f, .125f, .445f));
                    Box(0, new Vector3(side * .379f, .127f, -.304f), new Vector3(.205f, .194f, .105f));
                    Box(1, new Vector3(side * .29f, .012f, -.36f), new Vector3(.138f, .105f, .282f));
                    Box(2, new Vector3(side * .379f, .362f, .165f), Vector3.one * .043f);
                    Box(2, new Vector3(side * .379f, .053f, .180f), Vector3.one * .043f);
                }

                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                bool isNew = mesh == null;
                if (isNew) mesh = new Mesh { name = "BullBarMesh" };
                var combined = new CombineInstance[sections.Length];
                for (int i = 0; i < sections.Length; i++)
                {
                    materialMeshes[i] = new Mesh();
                    materialMeshes[i].CombineMeshes(sections[i].ToArray());
                    combined[i] = new CombineInstance { mesh = materialMeshes[i], transform = Matrix4x4.identity };
                }
                mesh.Clear();
                mesh.CombineMeshes(combined, false);
                if (isNew) AssetDatabase.CreateAsset(mesh, MeshPath);
                else EditorUtility.SetDirty(mesh);

                root.AddComponent<VoxelIndestructiblePart>();
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterials = new[] { Material("PloughSteel"), Material("PloughEdge"), Material("PloughBolts") };
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var tuning = AssetDatabase.LoadAssetAtPath<VoxelPloughTuning>(TuningPath);
                if (tuning == null)
                {
                    tuning = ScriptableObject.CreateInstance<VoxelPloughTuning>();
                    tuning.displayName = "BULL BAR";
                    tuning.description = "A compact steel guard for front rams.";
                    tuning.compatibleCarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cars/SpyCar2PlayerCar.prefab");
                    tuning.upgradeLevel = 1;
                    tuning.purchasePrice = 150;
                    tuning.impactDamageBonusPercent = 10f;
                    tuning.playerDamageReductionPercent = 10f;
                    tuning.mountPosition = new Vector3(0f, .37f, 2.73f);
                    AssetDatabase.CreateAsset(tuning, TuningPath);
                }
                tuning.ploughPrefab = prefab;
                EditorUtility.SetDirty(tuning);
                AssetDatabase.SaveAssetIfDirty(mesh);
                AssetDatabase.SaveAssetIfDirty(tuning);
            }
            finally
            {
                foreach (var mesh in materialMeshes) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }

            void Box(int material, Vector3 position, Vector3 size, float yaw = 0f) =>
                sections[material].Add(new CombineInstance { mesh = cube,
                    transform = Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), size) });
        }

        private static Material Material(string name)
        {
            var material = Resources.Load<Material>("CarMaterials/" + name);
            if (material == null) throw new InvalidOperationException("Missing front-ram material " + name);
            return material;
        }
    }
}
