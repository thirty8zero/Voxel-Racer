using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    [CustomEditor(typeof(VoxelLoadingScreenSettings))]
    internal sealed class VoxelLoadingScreenSettingsEditor : VoxelOdinTuningEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var settings = (VoxelLoadingScreenSettings)target;
            EditorGUILayout.HelpBox("Turn Show Images off for black with LOADING while keeping the list. The list is editable; main-menu artwork is always excluded. An empty list also gives a plain black loading screen.", MessageType.Info);
            if (GUILayout.Button("Replace List With Images Folder"))
            {
                Undo.RecordObject(settings, "Populate Loading Images");
                settings.images = FindImages(settings);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
            }
            if (GUILayout.Button("Render Loading Screen Example")) VoxelLoadingScreenPreview.RenderExample();
        }

        internal static Texture2D[] FindImages(VoxelLoadingScreenSettings settings) =>
            AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Images" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path)
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>).Where(settings.IsEligible).Distinct().ToArray();

        [MenuItem("Tools/Voxel Racer/Loading Screen/Select Settings")]
        private static void SelectSettings() => Selection.activeObject = VoxelLoadingScreenSettings.Load();
    }
}
