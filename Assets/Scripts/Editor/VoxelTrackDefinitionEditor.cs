using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    [CustomEditor(typeof(VoxelTrackDefinition))]
    internal sealed class VoxelTrackDefinitionEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor roadEditor;
        private UnityEditor.Editor trafficEditor;

        private bool identityOpen = true;
        private bool roadOpen = true;
        private bool trafficOpen = true;
        private bool missionOpen = true;
        private bool materialsOpen;
        private bool cactiOpen;
        private bool groundNoiseOpen;
        private bool skyOpen;
        private bool sunOpen;
        private bool mountainsOpen;
        private bool sceneryOpen;

        private void OnEnable()
        {
            EnsureEmbeddedTunings();
            RebuildNestedEditors();
        }

        private void OnDisable()
        {
            DestroyNestedEditors();
        }

        public override void OnInspectorGUI()
        {
            EnsureEmbeddedTunings();
            serializedObject.Update();
            VoxelTuningInspector.DrawScript(serializedObject);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This Track asset contains its road and traffic settings. Duplicate this one asset to create a new track, then add it to VoxelTrackSequence.",
                MessageType.Info);

            DrawSection(ref identityOpen, "Identity", () =>
            {
                DrawProperty("displayName");
                DrawProperty("raceSceneName");
            });

            DrawSection(ref roadOpen, "Road, Scenery Density & Run Length", () =>
            {
                DrawNestedEditor(ref roadEditor, ((VoxelTrackDefinition)target).roadTuning);
            });

            DrawSection(ref trafficOpen, "Traffic & Obstacles", () =>
            {
                DrawNestedEditor(ref trafficEditor, ((VoxelTrackDefinition)target).obstacleCarTuning);
            });

            DrawSection(ref missionOpen, "Mission", () => DrawProperty("missionTuning"));

            DrawSection(ref materialsOpen, "Materials & Colours", () =>
            {
                EditorGUILayout.LabelField("Optional Material Overrides", EditorStyles.boldLabel);
                DrawProperty("skyboxMaterial");
                DrawProperty("roadMaterial");
                DrawProperty("groundMaterial");
                DrawProperty("shoulderMaterial");
                DrawProperty("roadLineMaterial");
                DrawProperty("cactusMaterial");
                DrawProperty("obstacleMaterial");
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Generated Material Colours", EditorStyles.boldLabel);
                DrawProperty("roadColour");
                DrawProperty("groundColour");
                DrawProperty("shoulderColour");
                DrawProperty("roadLineColour");
                DrawProperty("cactusColour");
                DrawProperty("obstacleColour");
            });

            DrawSection(ref cactiOpen, "Cactus Palette", () => DrawProperty("cactusShades", true));

            DrawSection(ref groundNoiseOpen, "Ground Pixel Noise", () =>
            {
                DrawProperty("groundPixelNoiseEnabled");
                DrawProperty("groundNoisePixelSize");
                DrawProperty("groundNoiseDensity");
                DrawProperty("groundNoiseColourVariation");
                DrawProperty("groundNoiseSeed");
            });

            DrawSection(ref skyOpen, "Sky & Fog", () =>
            {
                DrawProperty("skyTint");
                DrawProperty("skyGroundColour");
                DrawProperty("atmosphereThickness");
                DrawProperty("fogEnabled");
                DrawProperty("fogColour");
                DrawProperty("fogStartDistance");
                DrawProperty("fogEndDistance");
            });

            DrawSection(ref sunOpen, "Horizon Sun", () =>
            {
                DrawProperty("horizonSunEnabled");
                DrawProperty("sunDistanceAhead");
                DrawProperty("sunHorizontalOffset");
                DrawProperty("sunHorizonHeight");
            });

            DrawSection(ref mountainsOpen, "Horizon Mountains", () =>
            {
                DrawProperty("horizonMountainsEnabled");
                DrawProperty("mountainDistance");
                DrawProperty("mountainScale");
                DrawProperty("mountainBaseHeight");
                VoxelTuningInspector.DrawRange("Mountain Peak Height",
                    serializedObject.FindProperty("minimumMountainPeakHeight"),
                    serializedObject.FindProperty("maximumMountainPeakHeight"));
                DrawProperty("mountainColour");
                DrawProperty("mountainSeed");
            });

            DrawSection(ref sceneryOpen, "Additional Scenery", () =>
            {
                DrawProperty("sceneryPrefabs", true);
                VoxelTuningInspector.DrawRange("Scenery Per Segment",
                    serializedObject.FindProperty("minimumSceneryPerSegment"),
                    serializedObject.FindProperty("maximumSceneryPerSegment"));
                VoxelTuningInspector.DrawRange("Scenery Scale",
                    serializedObject.FindProperty("minimumSceneryScale"),
                    serializedObject.FindProperty("maximumSceneryScale"));
            });

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProperty(string propertyName, bool includeChildren = false)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), includeChildren);
        }

        private static void DrawSection(ref bool isOpen, string title, System.Action contents)
        {
            // Header groups cannot contain controls that internally create another
            // header group (arrays such as Paint Colours can do this). A styled
            // standard foldout has the same presentation without that restriction.
            isOpen = EditorGUILayout.Foldout(isOpen, title, true, EditorStyles.foldoutHeader);
            if (isOpen)
            {
                using (new EditorGUI.IndentLevelScope())
                    contents();
                EditorGUILayout.Space(2f);
            }
        }

        private void DrawNestedEditor(ref UnityEditor.Editor nestedEditor, Object nestedTarget)
        {
            if (nestedTarget == null)
            {
                EditorGUILayout.HelpBox("Embedded settings could not be created for this track.", MessageType.Error);
                return;
            }

            if (nestedEditor == null || nestedEditor.target != nestedTarget)
            {
                if (nestedEditor != null)
                    DestroyImmediate(nestedEditor);
                nestedEditor = CreateEditor(nestedTarget);
            }

            nestedEditor.OnInspectorGUI();
        }

        private void EnsureEmbeddedTunings()
        {
            var track = (VoxelTrackDefinition)target;
            string assetPath = AssetDatabase.GetAssetPath(track);
            if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsSubAsset(track))
                return;

            bool changed = false;
            if (track.roadTuning == null)
            {
                track.roadTuning = CreateInstance<VoxelRoadTuning>();
                track.roadTuning.name = "Road Tuning";
                track.roadTuning.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(track.roadTuning, track);
                changed = true;
            }

            if (track.obstacleCarTuning == null)
            {
                track.obstacleCarTuning = CreateInstance<VoxelObstacleCarTuning>();
                track.obstacleCarTuning.name = "Traffic Tuning";
                track.obstacleCarTuning.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(track.obstacleCarTuning, track);
                changed = true;
            }

            if (track.obstacleCarTuning != null &&
                (track.obstacleCarTuning.staticObstacleSpawns == null || track.obstacleCarTuning.staticObstacleSpawns.Length == 0) &&
                track.staticObstacleSpawns != null && track.staticObstacleSpawns.Length > 0)
            {
                track.obstacleCarTuning.staticObstacleSpawns = track.staticObstacleSpawns;
                changed = true;
            }

            if (!changed)
                return;

            EditorUtility.SetDirty(track);
            EditorUtility.SetDirty(track.roadTuning);
            EditorUtility.SetDirty(track.obstacleCarTuning);
            AssetDatabase.SaveAssets();
            serializedObject.Update();
            RebuildNestedEditors();
        }

        private void RebuildNestedEditors()
        {
            DestroyNestedEditors();
            var track = (VoxelTrackDefinition)target;
            if (track.roadTuning != null)
                roadEditor = CreateEditor(track.roadTuning);
            if (track.obstacleCarTuning != null)
                trafficEditor = CreateEditor(track.obstacleCarTuning);
        }

        private void DestroyNestedEditors()
        {
            if (roadEditor != null)
                DestroyImmediate(roadEditor);
            if (trafficEditor != null)
                DestroyImmediate(trafficEditor);
            roadEditor = null;
            trafficEditor = null;
        }
    }
}
