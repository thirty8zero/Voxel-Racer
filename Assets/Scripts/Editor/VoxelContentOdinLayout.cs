using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public class VoxelContentOdinEditor : VoxelOdinTuningEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (targets.Length != 1) return;
            if (target is VoxelScenerySet scenery)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Selection probabilities", EditorStyles.boldLabel);
                float total = scenery.entries?.Where(e => e != null && e.prefab != null).Sum(e => Mathf.Max(0, e.weight)) ?? 0;
                if (total <= 0) EditorGUILayout.HelpBox("No scenery can spawn: assign a prefab with a positive weight.", MessageType.Warning);
                if (scenery.entries != null)
                    foreach (var entry in scenery.entries)
                        if (entry != null) EditorGUILayout.LabelField(entry.prefab ? entry.prefab.name : "Missing prefab",
                            (entry.prefab && total > 0 ? Mathf.Max(0, entry.weight) / total : 0).ToString("P1"));
            }
            if (target is VoxelTrackSequence sequence && sequence.tracks != null)
            {
                EditorGUILayout.LabelField("Mission order", EditorStyles.boldLabel);
                for (int i = 0; i < sequence.tracks.Length; i++)
                {
                    var track = sequence.tracks[i];
                    EditorGUILayout.LabelField($"{i + 1}. " + (track ? track.displayName + (track.isBossLevel ? " [BOSS]" : "") : "MISSING TRACK"));
                }
                if (sequence.tracks.Length == 0 || sequence.tracks.Any(t => t == null))
                    EditorGUILayout.HelpBox("The sequence is empty or contains missing track references.", MessageType.Warning);
            }
            if (target is VoxelCameraTuning camera)
            {
                var followers = UnityEngine.Object.FindObjectsByType<VoxelCameraFollow>(FindObjectsSortMode.None).Where(c => c.tuning == camera).ToArray();
                EditorGUILayout.HelpBox("Shake previews run on active cameras using this tuning during Play Mode.", MessageType.Info);
                using (new EditorGUI.DisabledScope(!Application.isPlaying || followers.Length == 0))
                {
                    if (GUILayout.Button("Preview Player Damage Shake")) foreach (var follower in followers) follower.ShakeFromPlayerDamage();
                    if (GUILayout.Button("Preview Explosion Shake")) foreach (var follower in followers) follower.ShakeFromObjectExplosion();
                }
            }
            if (target is VoxelRepairUpgradeTuning garage && GUILayout.Button("Refresh Garage Camera"))
                foreach (var controller in UnityEngine.Object.FindObjectsByType<VoxelRepairUpgradeSceneController>(FindObjectsSortMode.None))
                    if (controller.cameraTuning == garage) controller.ApplyCameraTuning();
            if (target is VoxelMainMenuTuning menu)
            {
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    if (GUILayout.Button("Refresh Live Main Menu"))
                        foreach (var controller in UnityEngine.Object.FindObjectsByType<VoxelMainMenuController>(FindObjectsSortMode.None))
                            if (controller.tuning == menu) { controller.BuildFeaturedCars(); controller.BuildDesertScenery(); }
            }
        }
    }
    [CustomEditor(typeof(VoxelScenerySet)), CanEditMultipleObjects] internal sealed class VoxelSceneryOdinEditor : VoxelContentOdinEditor { }
    [CustomEditor(typeof(VoxelTrackSequence)), CanEditMultipleObjects] internal sealed class VoxelSequenceOdinEditor : VoxelContentOdinEditor { }
    [CustomEditor(typeof(VoxelStaticObstacleDefinition)), CanEditMultipleObjects] internal sealed class VoxelStaticObstacleOdinEditor : VoxelContentOdinEditor { }
    [CustomEditor(typeof(VoxelRoadsideTurretTuning)), CanEditMultipleObjects] internal sealed class VoxelTurretOdinEditor : VoxelContentOdinEditor { }
    [CustomEditor(typeof(VoxelCarDefinition)), CanEditMultipleObjects] internal sealed class VoxelCarDefinitionOdinEditor : VoxelContentOdinEditor { }
    [CustomEditor(typeof(VoxelMainMenuTuning)), CanEditMultipleObjects] internal sealed class VoxelMenuOdinEditor : VoxelContentOdinEditor { }
    [CustomEditor(typeof(VoxelRepairUpgradeTuning)), CanEditMultipleObjects] internal sealed class VoxelGarageOdinEditor : VoxelContentOdinEditor { }

    internal static class VoxelContentOdinLayout
    {
        internal static bool Supports(Type type) => type == typeof(VoxelScenerySet) || type == typeof(VoxelTrackSequence) ||
            type == typeof(VoxelCameraTuning) || type == typeof(VoxelStaticObstacleDefinition) || type == typeof(VoxelRoadsideTurretTuning) ||
            type == typeof(VoxelCarDefinition) || type == typeof(VoxelMainMenuTuning) || type == typeof(VoxelRepairUpgradeTuning);

        internal static string Configure(Type type, string name, string group, List<Attribute> attributes)
        {
            if (type == typeof(VoxelScenerySet))
            {
                group = name == "entries" ? "Scenery Models" : name.StartsWith("distant") ? "Distant Coverage" : "Roadside Density";
                if (name.StartsWith("distant") && name != "distantSceneryEnabled") attributes.Add(new ShowIfAttribute("distantSceneryEnabled"));
            }
            if (type == typeof(VoxelTrackSequence))
            {
                group = "Campaign Order";
                if (name == "tracks") attributes.Add(new InlineEditorAttribute());
            }
            if (type == typeof(VoxelCarDefinition))
            {
                group = name == "visualPrefab" || name == "previewImage" ? "Model & Preview" : name == "tuning" ? "Driving Tuning" : "Identity & Selection";
                if (name == "visualPrefab" || name == "previewImage") attributes.Add(new PreviewFieldAttribute(96));
                if (name == "visualPrefab" || name == "tuning") attributes.Add(new RequiredAttribute());
                if (name == "tuning") attributes.Add(new InlineEditorAttribute());
            }
            if (type == typeof(VoxelMainMenuTuning))
            {
                if (name == "featuredCars") { group = "Featured Cars"; attributes.Add(new InlineEditorAttribute()); }
                if (name == "mountainScale") attributes.Add(new ShowIfAttribute("showHorizonMountains"));
            }
            if (type == typeof(VoxelStaticObstacleDefinition))
            {
                if (group == "Settings") group = "Identity & Model";
                if (name == "modelPrefab") { attributes.Add(new PreviewFieldAttribute(80)); attributes.Add(new ShowIfAttribute("@obstacleType == VoxelRacer.VoxelStaticObstacleType.VoxelBox")); }
                if (group == "Weapon Damage" || group == "Explosion") attributes.Add(new HideIfAttribute("@obstacleType == VoxelRacer.VoxelStaticObstacleType.Pothole"));
            }
            if (type == typeof(VoxelCameraTuning) && group.Contains("Shake")) group = "Screen Shake/" + group;
            if (name.Contains("Degrees") || name.Contains("Angle") || name.EndsWith("FieldOfView")) attributes.Add(new SuffixLabelAttribute("degrees"));
            if (name.EndsWith("Frequency")) attributes.Add(new SuffixLabelAttribute("Hz"));
            if (name.EndsWith("Offset") || name.EndsWith("LookAhead") || name.EndsWith("LookHeight") || name.EndsWith("PositionStrength") || name == "cameraPosition" || name == "cameraLookAt") attributes.Add(new SuffixLabelAttribute("m"));
            if (type == typeof(VoxelRoadsideTurretTuning) && name == "fireRate") attributes.Add(new LabelTextAttribute("Volley Interval (s)"));
            return group;
        }
    }
    public sealed class VoxelSceneryEntryOdinLayout : OdinAttributeProcessor<VoxelScenerySet.Entry>
    {
        public override void ProcessChildMemberAttributes(InspectorProperty parent, MemberInfo member, List<Attribute> attributes)
        {
            if (!(member is FieldInfo)) return;
            string name = member.Name;
            attributes.Add(new FoldoutGroupAttribute(name == "prefab" || name == "weight" ? "Model & Selection" : "Placement & Scale"));
            if (name == "prefab") { attributes.Add(new PreviewFieldAttribute(80)); attributes.Add(new RequiredAttribute()); }
            if (name == "scaleRange") attributes.Add(new InfoBoxAttribute("Scale must be positive, with maximum at least minimum.", InfoMessageType.Warning, "@scaleRange.x <= 0 || scaleRange.y < scaleRange.x"));
            if (name == "maximumRoadDistance") attributes.Add(new InfoBoxAttribute("Maximum road distance must exceed road clearance plus the scaled model radius.", InfoMessageType.Warning, "@maximumRoadDistance <= roadClearance + radius * UnityEngine.Mathf.Max(scaleRange.x, scaleRange.y)"));
            if (name == "radius" || name == "spacing" || name.Contains("Distance") || name == "roadClearance") attributes.Add(new SuffixLabelAttribute("m"));
        }
    }
}
