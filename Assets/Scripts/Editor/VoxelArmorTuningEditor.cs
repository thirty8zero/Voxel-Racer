using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    [CustomEditor(typeof(VoxelArmorTuning)), CanEditMultipleObjects]
    public sealed class VoxelArmorTuningEditor : UnityEditor.Editor
    {
        private bool shop = true, durability = true, fit = true;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            shop = EditorGUILayout.Foldout(shop, "Shop", true);
            if (shop) Fields("displayName", "panelPrefab", "panelPurchasePrice");
            durability = EditorGUILayout.Foldout(durability, "Durability", true);
            if (durability) Fields("voxelHitPoints");
            fit = EditorGUILayout.Foldout(fit, "Car Fit", true);
            if (fit) Fields("compatibleCarPrefab", "rightMountPosition");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Each door is purchased separately. Each panel voxel adds one integrity unit; health controls how much damage it absorbs.", MessageType.Info);
            if (GUILayout.Button("Preview Fit on Car")) VoxelArmorFitPreview.Open((VoxelArmorTuning)target);
        }
        private void Fields(params string[] names)
        {
            foreach (string name in names) EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
        }
    }

    // Preserve existing Inspector and menu entry points.
    public static class VoxelArmorFitPreview
    {
        [MenuItem("Tools/Voxel Racer/Preview Door Armour Fit")]
        public static void OpenDefault() => Open(VoxelArmorTuning.Load());
        public static void Open(VoxelArmorTuning value) => VoxelUpgradeFitPreview.Open(value);
    }
}