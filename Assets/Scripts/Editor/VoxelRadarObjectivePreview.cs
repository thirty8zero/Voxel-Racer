using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public sealed class VoxelRadarObjectivePreview : EditorWindow
    {
        private bool destroyed;

        [MenuItem("Tools/Voxel Racer/Radar Objective Preview %&r")]
        private static void Open()
        {
            var window = GetWindow<VoxelRadarObjectivePreview>(true, "Radar Objective Preview", true);
            window.minSize = new Vector2(720, 250);
            window.position = new Rect(160, 160, 760, 270);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(.12f, .15f, .18f));
            VoxelRadarObjectiveHud.Draw(VoxelRadarObjectiveHud.Area(position.width), 1f, destroyed, 1f);
            GUILayout.Space(185);
            destroyed = GUILayout.Toggle(destroyed, "Radar destroyed", GUILayout.Width(180));
            GUILayout.Label("Shared runtime HUD renderer — explosion persists until the boss arrives.", EditorStyles.miniLabel);
        }
    }
}
