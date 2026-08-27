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

    [CustomEditor(typeof(VoxelObstacleCarTuning))]
    internal sealed class VoxelObstacleCarTuningEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            VoxelTuningInspector.DrawScript(serializedObject);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Spawning", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnDistanceAhead"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("obstacleCarSpawnChance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("oppositeDirectionChance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enemyCarSpawnChance"));
            VoxelTuningInspector.DrawRange("Objects Per Wave", serializedObject.FindProperty("minimumObjectsPerWave"), serializedObject.FindProperty("maximumObjectsPerWave"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sameLaneCivilianSpeedTolerance"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Paint Colours", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("paintColours"), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Traffic Models", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("semiTrailerSpawnChance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("trafficCarEnemyTuning"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("semiTrailerEnemyTuning"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Traffic Speed", EditorStyles.boldLabel);
            VoxelTuningInspector.DrawRange("Same Direction Speed", serializedObject.FindProperty("sameDirectionSpeedMin"), serializedObject.FindProperty("sameDirectionSpeedMax"));
            VoxelTuningInspector.DrawRange("Oncoming Speed", serializedObject.FindProperty("oppositeDirectionSpeedMin"), serializedObject.FindProperty("oppositeDirectionSpeedMax"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wheelSpinDegreesPerUnit"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Impact", EditorStyles.boldLabel);
            VoxelTuningInspector.DrawRange("Player Damage Voxels", serializedObject.FindProperty("playerDamageVoxelsMin"), serializedObject.FindProperty("playerDamageVoxelsMax"));
            VoxelTuningInspector.DrawRange("Obstacle Damage Voxels", serializedObject.FindProperty("obstacleDamageVoxelsMin"), serializedObject.FindProperty("obstacleDamageVoxelsMax"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("impactVoxelDamageSurfaceOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("semiImpactVoxelDamageSurfaceOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("collisionCooldown"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("launchForce"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("launchUpwardForce"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("destroyedLifetime"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Obstacle Voxel Debris", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("debrisVoxelsPerDamagedVoxel"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionSpawnOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionUpwardBias"));
            VoxelTuningInspector.DrawRange("Forward Force", serializedObject.FindProperty("explosionForwardForceMin"), serializedObject.FindProperty("explosionForwardForceMax"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionUpwardForce"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionSpreadForce"));
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(VoxelCarTuning))]
    internal sealed class VoxelCarTuningEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            VoxelTuningInspector.DrawScript(serializedObject);
            DrawRemainingPropertiesExcept("explosionForwardForceMin", "explosionForwardForceMax");
            VoxelTuningInspector.DrawRange("Forward Force", serializedObject.FindProperty("explosionForwardForceMin"), serializedObject.FindProperty("explosionForwardForceMax"));
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRemainingPropertiesExcept(params string[] excluded)
        {
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "m_Script" || System.Array.IndexOf(excluded, property.name) >= 0)
                    continue;
                EditorGUILayout.PropertyField(property, true);
            }
        }
    }

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
}
