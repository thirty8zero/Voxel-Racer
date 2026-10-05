using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    /// <summary>Exercises the real multiplier HUD without touching a run, purchases or cash.</summary>
    public sealed class VoxelMultiplierHudPreview : EditorWindow
    {
        private GameObject temporary;
        private VoxelMissionProgress mission;
        private VoxelMissionTuning tuning;
        private readonly MethodInfo draw = typeof(VoxelMissionProgress).GetMethod("DrawMultiplier", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly MethodInfo header = typeof(VoxelMissionProgress).GetMethod("DrawMissionHeader", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly MethodInfo tick = typeof(VoxelMissionProgress).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

        [MenuItem("Tools/Voxel Racer/Preview Live Multiplier HUD")]
        public static void Open() => GetWindow<VoxelMultiplierHudPreview>("Live Multiplier Preview").Show();
        private void OnEnable()
        {
            minSize = new Vector2(1400, 550);
            temporary = new GameObject("Temporary Multiplier HUD Preview") { hideFlags = HideFlags.HideAndDontSave };
            temporary.SetActive(false);
            mission = temporary.AddComponent<VoxelMissionProgress>();
            mission.enabled = false;
            tuning = CreateInstance<VoxelMissionTuning>();
            tuning.hideFlags = HideFlags.HideAndDontSave;
            ResetPreview();
            EditorApplication.update += Repaint;
        }
        public void ResetPreview()
        {
            tuning.requiredPoints = 1200;
            mission.Configure(tuning);
            typeof(VoxelMissionProgress).GetProperty("Points").SetValue(mission, 986);
            mission.ChangeMultiplier(1.35f, "PREVIEW");
        }
        public void ShowExpiry() => mission.AdvanceBonusClock(mission.RemainingTime);
        private void OnGUI()
        {
            if (mission == null) return;
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(.07f, .09f, .12f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Enemy voxels +0.10")) mission.ChangeMultiplier(.1f, "ENEMY VOXELS");
            if (GUILayout.Button("Civilian hit -0.10")) mission.ChangeMultiplier(-.1f, "CIVILIAN DAMAGE");
            if (GUILayout.Button("Civilian destroyed -1.00")) mission.ChangeMultiplier(-1, "CIVILIAN DESTROYED");
            if (GUILayout.Button("Last 8 seconds")) mission.AdvanceBonusClock(Mathf.Max(0, mission.RemainingTime - 8));
            if (GUILayout.Button("Expire")) ShowExpiry();
            if (GUILayout.Button("Overtime +1 second")) { ShowExpiry(); mission.AdvanceBonusClock(1); }
            if (GUILayout.Button("+15 seconds")) mission.AddBonusTime(15);
            if (GUILayout.Button("Reset")) ResetPreview();
            GUILayout.EndHorizontal();
            tick.Invoke(mission, null);
            var area = new Rect(60, 55, Mathf.Min(990, position.width - 360), 105);
            header.Invoke(mission, new object[] { area, false, 1f });
            draw.Invoke(mission, new object[] { area, 1f });
        }
        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
            if (temporary != null) DestroyImmediate(temporary);
            if (tuning != null) DestroyImmediate(tuning);
        }
    }
}
