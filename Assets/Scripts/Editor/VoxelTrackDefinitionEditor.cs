using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector.Editor;

namespace VoxelRacer.Editor
{
    [CustomEditor(typeof(VoxelTrackDefinition)), CanEditMultipleObjects]
    internal sealed class VoxelTrackDefinitionEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            foreach(var item in targets) EnsureEmbeddedTunings((VoxelTrackDefinition)item);
            EditorGUILayout.HelpBox("Each Track owns its road and traffic settings. Duplicate the Track, then add it to VoxelTrackSequence. Mission and other referenced assets may be shared.", MessageType.Info);
            base.OnInspectorGUI();
        }
        private void EnsureEmbeddedTunings(VoxelTrackDefinition track)
        {
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

        }

    }
}
