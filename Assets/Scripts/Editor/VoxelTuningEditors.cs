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
        private bool movementExpanded = true;

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
            VoxelTuningInspector.DrawRange("Object Distance Offset", serializedObject.FindProperty("minimumWaveObjectDistanceOffset"), serializedObject.FindProperty("maximumWaveObjectDistanceOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sameLaneCivilianSpeedTolerance"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Static Obstacle Spawns", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Only obstacles in this list can spawn on this track. Spawn Weight is relative to the other entries.",
                MessageType.None);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("staticObstacleSpawns"), true);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("paintColours"), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Traffic Models", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("semiTrailerSpawnChance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("trafficCarEnemyTuning"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("semiTrailerEnemyTuning"));

            EditorGUILayout.Space();
            movementExpanded = EditorGUILayout.Foldout(movementExpanded, "Civilian Movement", true);
            if (movementExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("approachSpeedDistance"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("engageSpeedDistance"));
                    EditorGUILayout.Space(2f);
                    EditorGUILayout.LabelField("Same Direction", EditorStyles.miniBoldLabel);
                    VoxelTuningInspector.DrawRange("Spawn Speed Multiplier", serializedObject.FindProperty("sameDirectionSpawnSpeedMultiplierMin"), serializedObject.FindProperty("sameDirectionSpawnSpeedMultiplierMax"));
                    VoxelTuningInspector.DrawRange("Approach Speed Multiplier", serializedObject.FindProperty("sameDirectionApproachSpeedMultiplierMin"), serializedObject.FindProperty("sameDirectionApproachSpeedMultiplierMax"));
                    VoxelTuningInspector.DrawRange("Engage Speed Multiplier", serializedObject.FindProperty("sameDirectionEngageSpeedMultiplierMin"), serializedObject.FindProperty("sameDirectionEngageSpeedMultiplierMax"));
                    EditorGUILayout.Space(2f);
                    EditorGUILayout.LabelField("Oncoming", EditorStyles.miniBoldLabel);
                    VoxelTuningInspector.DrawRange("Spawn Speed Multiplier", serializedObject.FindProperty("oncomingSpawnSpeedMultiplierMin"), serializedObject.FindProperty("oncomingSpawnSpeedMultiplierMax"));
                    VoxelTuningInspector.DrawRange("Approach Speed Multiplier", serializedObject.FindProperty("oncomingApproachSpeedMultiplierMin"), serializedObject.FindProperty("oncomingApproachSpeedMultiplierMax"));
                    VoxelTuningInspector.DrawRange("Engage Speed Multiplier", serializedObject.FindProperty("oncomingEngageSpeedMultiplierMin"), serializedObject.FindProperty("oncomingEngageSpeedMultiplierMax"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("wheelSpinDegreesPerUnit"));
                }
            }

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

    /// <summary>Organises all enemy vehicle tuning assets into focused collapsible groups.</summary>
    [CustomEditor(typeof(VoxelEnemyVehicleTuning))]
    [CanEditMultipleObjects]
    internal sealed class VoxelEnemyVehicleTuningEditor : UnityEditor.Editor
    {
        private bool durabilityExpanded = true;
        private bool movementExpanded = true;
        private bool collisionExpanded = true;
        private bool playerImpactExpanded = true;
        private bool weaponDamageExpanded = true;
        private bool healthBarExpanded = true;
        private bool explosionExpanded = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            VoxelTuningInspector.DrawScript(serializedObject);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));

            durabilityExpanded = DrawSection(durabilityExpanded, "Durability",
                "voxelHealth", "vehicleHealth");
            playerImpactExpanded = DrawSection(playerImpactExpanded, "Player Impact",
                "playerDamageVoxelsMin", "playerDamageVoxelsMax");
            weaponDamageExpanded = DrawSection(weaponDamageExpanded, "Weapon Damage",
                "rearSurfaceHitRandomness");
            movementExpanded = DrawSection(movementExpanded, "Movement",
                "minimumSpawnSpeedMultiplier", "maximumSpawnSpeedMultiplier", "approachSpeedDistance",
                "minimumApproachSpeedMultiplier", "maximumApproachSpeedMultiplier", "engageSpeedDistance",
                "minimumEngageSpeedMultiplier", "maximumEngageSpeedMultiplier");
            collisionExpanded = DrawSection(collisionExpanded, "Collision",
                "collisionHalfWidth", "collisionHalfLength");
            healthBarExpanded = DrawSection(healthBarExpanded, "Health Bar",
                "healthBarWidth", "healthBarHeight", "healthBarHeightOffset",
                "healthBarFullColour", "healthBarEmptyColour", "criticalHealthPercent",
                "criticalPulseSpeed", "criticalPulseScale");
            explosionExpanded = DrawSection(explosionExpanded, "Explosion",
                "explosionVoxelCount", "maximumExplosionVoxelRemovalPercent",
                "explosionForwardForceMin", "explosionForwardForceMax", "explosionUpwardForce",
                "explosionSpreadForce", "explosionDebrisScale", "explosionDebrisLifetime",
                "destroyedLifetime");

            serializedObject.ApplyModifiedProperties();
        }

        private bool DrawSection(bool isExpanded, string title, params string[] propertyNames)
        {
            EditorGUILayout.Space(3f);
            isExpanded = EditorGUILayout.Foldout(isExpanded, title, true);
            if (!isExpanded)
                return false;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (string propertyName in propertyNames)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName));
            }
            return true;
        }
    }
}
