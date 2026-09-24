using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    internal static class VoxelTuningInspector
    {
        public static void DrawScript(SerializedObject serializedObject)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }

        public static void DrawRange(string label, SerializedProperty minimum, SerializedProperty maximum)
        {
            // Use one calculated control rect. Mixing GUILayout fields with PrefixLabel
            // causes the second label to drift at narrower Inspector widths.
            Rect row = EditorGUILayout.GetControlRect();
            float prefixWidth = EditorGUIUtility.labelWidth;
            // Reserve enough space for full Min/Max labels even when Unity applies
            // Inspector DPI scaling. GUI.Label avoids the prefix indentation that was
            // clipping the final character of each mini label.
            const float miniLabelWidth = 38f;
            const float gap = 6f;
            float contentX = row.x + prefixWidth;
            float availableWidth = row.xMax - contentX;
            float valueWidth = Mathf.Max(35f, (availableWidth - miniLabelWidth * 2f - gap * 2f) * 0.5f);

            var prefixRect = new Rect(row.x, row.y, prefixWidth, row.height);
            var minLabelRect = new Rect(contentX, row.y, miniLabelWidth, row.height);
            var minValueRect = new Rect(minLabelRect.xMax, row.y, valueWidth, row.height);
            var maxLabelRect = new Rect(minValueRect.xMax + gap, row.y, miniLabelWidth, row.height);
            var maxValueRect = new Rect(maxLabelRect.xMax, row.y, Mathf.Max(0f, row.xMax - maxLabelRect.xMax), row.height);

            EditorGUI.LabelField(prefixRect, label);
            GUI.Label(minLabelRect, "Min", EditorStyles.miniLabel);
            DrawNumericValue(minValueRect, minimum);
            GUI.Label(maxLabelRect, "Max", EditorStyles.miniLabel);
            DrawNumericValue(maxValueRect, maximum);
        }

        // PropertyField also invokes a field's Header decorator. The range rows already
        // provide their own section labels, so draw the numeric values directly instead.
        private static void DrawNumericValue(Rect rect, SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = EditorGUI.IntField(rect, property.intValue);
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = EditorGUI.FloatField(rect, property.floatValue);
                    break;
                default:
                    EditorGUI.PropertyField(rect, property, GUIContent.none);
                    break;
            }
        }
    }

    [CustomEditor(typeof(VoxelObstacleCarTuning)), CanEditMultipleObjects]
    internal sealed class VoxelObstacleCarTuningEditor : VoxelOdinTuningEditor { }

    [CustomEditor(typeof(VoxelCarTuning)), CanEditMultipleObjects]
    internal sealed class VoxelCarTuningEditor : VoxelOdinTuningEditor { }

    [CustomEditor(typeof(VoxelRoadTuning))]
    internal sealed class VoxelRoadTuningEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            VoxelTuningInspector.DrawScript(serializedObject);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("laneCount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("roadWidth"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("groundWidth"));
            VoxelTuningInspector.DrawRange("Cacti Per Segment", serializedObject.FindProperty("minimumCactiPerSegment"), serializedObject.FindProperty("maximumCactiPerSegment"));
            VoxelTuningInspector.DrawRange("Cactus Height Scale", serializedObject.FindProperty("minimumCactusHeightScale"), serializedObject.FindProperty("maximumCactusHeightScale"));
            VoxelTuningInspector.DrawRange("Cactus Width Scale", serializedObject.FindProperty("minimumCactusWidthScale"), serializedObject.FindProperty("maximumCactusWidthScale"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("segmentLength"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("segmentCount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("recycleBehindDistance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("finishRoadBehindDistance"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Turning Road Pieces", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("turnChancePerSegment"));
            VoxelTuningInspector.DrawRange("Turn Angle", serializedObject.FindProperty("minimumTurnAngle"), serializedObject.FindProperty("maximumTurnAngle"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minimumStraightSegmentsBetweenTurns"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maximumTrackHeading"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("curveDegreesPerSlice"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("turnSeed"));
            serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>Keeps core mission setup visible while tucking optional scoring and spawning rules into sections.</summary>
    [CustomEditor(typeof(VoxelMissionTuning))]
    [CanEditMultipleObjects]
    internal sealed class VoxelMissionTuningEditor : VoxelOdinTuningEditor { }

    /// <summary>Organises all enemy vehicle tuning assets into focused collapsible groups.</summary>
    [CustomEditor(typeof(VoxelEnemyVehicleTuning))]
    [CanEditMultipleObjects]
    internal sealed class VoxelEnemyVehicleTuningEditor : VoxelOdinTuningEditor { }

    [CustomEditor(typeof(VoxelCameraTuning)), CanEditMultipleObjects]
    internal sealed class VoxelCameraTuningEditor : VoxelContentOdinEditor { }
}
