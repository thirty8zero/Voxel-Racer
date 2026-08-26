using UnityEngine;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

namespace VoxelRacer
{
    /// <summary>Defers editor asset saves so validation and import callbacks stay deterministic.</summary>
    public static class VoxelAssetSaveQueue
    {
#if UNITY_EDITOR
        private static readonly HashSet<Object> pendingAssets = new();
        private static bool saveQueued;
#endif

        public static void Request(Object asset)
        {
#if UNITY_EDITOR
            if (asset == null)
                return;

            pendingAssets.Add(asset);
            if (saveQueued)
                return;

            saveQueued = true;
            EditorApplication.delayCall += SaveWhenEditorIsIdle;
#endif
        }

#if UNITY_EDITOR
        private static void SaveWhenEditorIsIdle()
        {
            saveQueued = false;
            if (pendingAssets.Count == 0)
                return;

            foreach (Object asset in pendingAssets)
                if (asset != null)
                    EditorUtility.SetDirty(asset);
            pendingAssets.Clear();
            AssetDatabase.SaveAssets();
        }
#endif
    }
}
