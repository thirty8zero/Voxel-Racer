using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelExtendedArmorBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Upgrades/ExtendedDoorArmorPanel.prefab";
        public const string MeshPath = "Assets/Prefabs/Upgrades/ExtendedDoorArmorPanelMeshes.asset";
        public const string TuningPath = "Assets/Resources/Armor/ExtendedDoorArmorTuning.asset";
        public const string MaterialPath = "Assets/Resources/CarMaterials/ExtendedArmorGrey.mat";

        [MenuItem("Tools/Voxel Racer/Build Extended Door Armour Upgrade")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Build armour in Edit Mode.");
            var original = VoxelArmorTuning.Load();
            if (original == null || original.panelPrefab == null) throw new InvalidOperationException("Original door armour is missing.");
            var tuning = AssetDatabase.LoadAssetAtPath<VoxelArmorTuning>(TuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<VoxelArmorTuning>();
                tuning.displayName = "EXTENDED DOOR ARMOUR";
                tuning.panelPurchasePrice = Mathf.RoundToInt(original.panelPurchasePrice * 1.5f);
                tuning.voxelHitPoints = original.voxelHitPoints;
                tuning.upgradeLevel = 2;
                tuning.compatibleCarPrefab = original.compatibleCarPrefab;
                tuning.rightMountPosition = original.rightMountPosition;
                AssetDatabase.CreateAsset(tuning, TuningPath);
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Extended Armour Mid Grey" };
                material.color = new Color(.46f, .46f, .46f);
                material.SetFloat("_Metallic", .3f);
                material.SetFloat("_Smoothness", .25f);
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            var root = new GameObject("Extended Door Armor Panel");
            var usedMeshes = new HashSet<Mesh>();
            try
            {
                // Nine columns instead of six: same 0.14m cells, four face rows and one
                // return flange. Scale the authored outline/ribs/fastener spacing along Z
                // by 1.5, giving exactly 50% more projected door coverage.
                for (int row = 0; row < 5; row++)
                    for (int column = 0; column < 9; column++)
                    {
                        bool flange = row == 4;
                        var piece = new GameObject((flange ? "Return Flange " : "Plate Row " + row + " Cell ") + column);
                        piece.transform.SetParent(root.transform, false);
                        piece.transform.localPosition = new Vector3(flange ? -.07f : .07f, flange ? .07f : .07f + row * .14f, -.56f + column * .14f);
                        piece.transform.localScale = Vector3.one * .14f;
                        var generated = VoxelDoorArmorDetailBuilder.CreatePanelCellMesh(root.transform, piece.transform, 1.5f);
                        try
                        {
                            string meshName = piece.name + " Detailed Plate";
                            // Unity renames the main asset to the file name on import. Locate
                            // that first cell by main-asset identity, not its transient name.
                            var mesh = row == 0 && column == 0 ? AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) :
                                AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().FirstOrDefault(m => m.name == meshName);
                            if (mesh == null)
                            {
                                mesh = new Mesh { name = meshName };
                                if (AssetDatabase.LoadMainAssetAtPath(MeshPath) == null) AssetDatabase.CreateAsset(mesh, MeshPath);
                                else AssetDatabase.AddObjectToAsset(mesh, MeshPath);
                            }
                            mesh.Clear(); mesh.vertices = generated.vertices; mesh.triangles = generated.triangles;
                            mesh.normals = generated.normals; mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
                            mesh.name = AssetDatabase.IsMainAsset(mesh) ? "ExtendedDoorArmorPanelMeshes" : meshName;
                            usedMeshes.Add(mesh);
                            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                            piece.AddComponent<MeshRenderer>().sharedMaterial = material;
                            piece.AddComponent<VoxelArmorVoxel>().tuning = tuning;
                        }
                        finally { Object.DestroyImmediate(generated); }
                    }
                tuning.panelPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                foreach (var obsolete in AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().Where(m => !usedMeshes.Contains(m) && !AssetDatabase.IsMainAsset(m)).ToArray())
                    Object.DestroyImmediate(obsolete, true);
                EditorUtility.SetDirty(tuning);
                AssetDatabase.SaveAssets();
            }
            finally { Object.DestroyImmediate(root); }
            Debug.Log("Built Extended Door Armour: 1.5x door coverage, 45 damage pieces, mid grey; preserved existing tuning/material edits.");
        }
    }
}
