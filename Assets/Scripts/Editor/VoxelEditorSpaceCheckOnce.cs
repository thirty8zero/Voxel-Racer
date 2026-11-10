using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    internal static class VoxelEditorSpaceCheckOnce
    {
        [InitializeOnLoadMethod]
        private static void Schedule()
        {
            const string key = "VoxelEditorSpaceCheckOnce.20261108.4";
            if (SessionState.GetBool(key, false)) return;
            SessionState.SetBool(key, true);
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            try { VoxelMissionBriefingPlayValidation.Run(); }
            catch (Exception exception)
            {
                File.WriteAllText("Temp/EditorSpaceCheckOnce.txt", exception.ToString());
                Debug.LogException(exception);
            }
        }
    }
}
