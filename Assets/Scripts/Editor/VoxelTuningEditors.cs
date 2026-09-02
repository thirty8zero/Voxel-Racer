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
    internal sealed class VoxelMissionTuningEditor : UnityEditor.Editor
    {
        private bool enemyScoreExpanded = true;
        private bool staticObstacleScoreExpanded = true;
        private bool roadsideTurretExpanded = true;
        private bool civilianNearMissExpanded = true;
        private bool civilianPenaltiesExpanded = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            VoxelTuningInspector.DrawScript(serializedObject);

            DrawAlwaysVisibleSection("Identity", "displayName");
            DrawAlwaysVisibleSection("Completion", "requiredPoints");
            DrawAlwaysVisibleSection("Rewards", "completionCurrencyAward");
            DrawAlwaysVisibleSection("Time Bonus", "timeLimitSeconds", "timeBonusCurrencyMultiplier");

            enemyScoreExpanded = DrawFoldout(enemyScoreExpanded, "Enemy Score",
                "enemyVoxelDamagePoints", "enemyVehicleDestroyedPoints");
            staticObstacleScoreExpanded = DrawFoldout(staticObstacleScoreExpanded, "Static Obstacle Score",
                "fuelDrumDestroyedPoints", "fuelDrumDestroyedPopupDuration");
            roadsideTurretExpanded = DrawFoldout(roadsideTurretExpanded, "Roadside Turret Spawning",
                "roadsideTurretTuning", "roadsideTurretSpawnCheckInterval", "roadsideTurretSpawnChance",
                "roadsideTurretSpawnDistanceAhead", "maximumActiveRoadsideTurrets");
            civilianNearMissExpanded = DrawFoldout(civilianNearMissExpanded, "Civilian Near Miss Score",
                "civilianNearMissDistance", "civilianNearMissMinPoints", "civilianNearMissMaxPoints",
                "civilianNearMissScoreStepPercent", "civilianNearMissPassClearance", "civilianNearMissPlayerHalfWidth",
                "civilianNearMissPlayerHalfLength", "civilianNearMissPopupDuration");
            civilianPenaltiesExpanded = DrawFoldout(civilianPenaltiesExpanded, "Civilian Penalties",
                "civilianVoxelDamagePoints", "civilianVehicleDestroyedPoints");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAlwaysVisibleSection(string title, params string[] properties)
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
                foreach (string property in properties)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(property));
        }

        private bool DrawFoldout(bool isExpanded, string title, params string[] properties)
        {
            EditorGUILayout.Space(3f);
            isExpanded = EditorGUILayout.Foldout(isExpanded, title, true);
            if (!isExpanded)
                return false;

            using (new EditorGUI.IndentLevelScope())
                foreach (string property in properties)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(property));
            return true;
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
        private bool playerRamResponseExpanded = true;
        private bool weaponDamageExpanded = true;
        private bool weaponDamageDebrisExpanded = true;
        private bool playerRamDebrisExpanded = true;
        private bool healthBarExpanded = true;
        private bool evasiveLaneChangeExpanded = true;
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
                "playerDamageVoxelsMin", "playerDamageVoxelsMax", "playerRamDamage");
            playerRamResponseExpanded = DrawSection(playerRamResponseExpanded, "Player Ram Response",
                "playerRamSpeedMatchDuration", "rearRamEnemyForwardPushDistance", "rearRamEnemyForwardPushDuration",
                "rearRamEnemyForwardPushEasing", "playerRearRamRecoilDistance", "playerRearRamRecoilDuration",
                "playerRearRamRecoilEasing", "sideRamEnemyLaneShiftDistance", "sideRamEnemyLaneShiftDuration",
                "sideRamEnemyLaneShiftEasing", "playerSideRamBounceDistance", "playerSideRamBounceDuration",
                "playerSideRamBounceEasing");
            weaponDamageExpanded = DrawSection(weaponDamageExpanded, "Weapon Damage",
                "rearSurfaceHitRandomness");
            weaponDamageDebrisExpanded = DrawSection(weaponDamageDebrisExpanded, "Weapon Damage Debris",
                "weaponDebrisScale", "weaponDebrisForwardForceMin", "weaponDebrisForwardForceMax",
                "weaponDebrisUpwardForce", "weaponDebrisSpreadForce", "weaponDebrisLifetime");
            playerRamDebrisExpanded = DrawSection(playerRamDebrisExpanded, "Player Ram Debris",
                "ramDebrisScale", "ramDebrisForwardForceMin", "ramDebrisForwardForceMax",
                "ramDebrisUpwardForce", "ramDebrisSpreadForce", "ramDebrisLifetime");
            movementExpanded = DrawSection(movementExpanded, "Movement",
                "minimumSpawnSpeedMultiplier", "maximumSpawnSpeedMultiplier", "approachSpeedDistance",
                "minimumApproachSpeedMultiplier", "maximumApproachSpeedMultiplier", "engageSpeedDistance",
                "minimumEngageSpeedMultiplier", "maximumEngageSpeedMultiplier");
            evasiveLaneChangeExpanded = DrawSection(evasiveLaneChangeExpanded, "Evasive Lane Change",
                "laneChangeDamagePercent", "laneChangeChance", "laneChangeSpeed", "laneChangeSpeedBoostChance",
                "laneChangeSpeedBoostMultiplier", "laneChangeSpeedBoostDuration");
            collisionExpanded = DrawSection(collisionExpanded, "Collision",
                "collisionHalfWidth", "collisionHalfLength");
            healthBarExpanded = DrawSection(healthBarExpanded, "Health Bar",
                "healthBarWidth", "healthBarHeight", "healthBarHeightOffset",
                "healthBarFullColour", "healthBarEmptyColour", "criticalHealthPercent",
                "criticalPulseSpeed", "criticalPulseScale");
            explosionExpanded = DrawSection(explosionExpanded, "Explosion",
                "explosionEffectScale", "explosionVoxelCount", "maximumExplosionVoxelRemovalPercent",
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

    [CustomEditor(typeof(VoxelCameraTuning))]
    internal sealed class VoxelCameraTuningEditor : UnityEditor.Editor
    {
        private bool screenShakeExpanded = true;
        private bool playerImpactShakeExpanded = true;
        private bool objectExplosionShakeExpanded = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            VoxelTuningInspector.DrawScript(serializedObject);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Chase View", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("chaseOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("chaseLookAhead"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Finish View", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("finishOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("finishLookHeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("finishLookSideOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("finishFieldOfView"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("finishSequenceDuration"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Lane Change Camera", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("laneChangeCameraDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("laneChangeCameraEasing"));

            EditorGUILayout.Space(3f);
            screenShakeExpanded = EditorGUILayout.Foldout(screenShakeExpanded, "Screen Shake", true);
            if (screenShakeExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    playerImpactShakeExpanded = DrawShakeSection(playerImpactShakeExpanded,
                        "Player Damage", "playerVehicleImpactShake");
                    objectExplosionShakeExpanded = DrawShakeSection(objectExplosionShakeExpanded,
                        "Object Explosion", "objectExplosionShake");
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private bool DrawShakeSection(bool isExpanded, string title, string propertyPrefix)
        {
            isExpanded = EditorGUILayout.Foldout(isExpanded, title, true);
            if (!isExpanded)
                return false;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyPrefix + "Duration"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyPrefix + "PositionStrength"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyPrefix + "RotationDegrees"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyPrefix + "Frequency"));
            }
            return true;
        }
    }
}
