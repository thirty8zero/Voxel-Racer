using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelEnemyScoreValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Enemy Type Scores")]
        public static void Run()
        {
            var previous = VoxelMissionProgress.Active;
            var active = typeof(VoxelMissionProgress).GetField("<Active>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var root = new GameObject("Temporary enemy score validation");
            var missionTuning = ScriptableObject.CreateInstance<VoxelMissionTuning>();
            var first = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>();
            var second = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>();
            try
            {
                var mission = root.AddComponent<VoxelMissionProgress>(); mission.enabled = false;
                active.SetValue(null, mission);
                missionTuning.requiredPoints = 10000; mission.Configure(missionTuning);
                first.displayName = "Interceptor"; first.destructionScore = 25;
                second.displayName = "Mine Layer"; second.destructionScore = 70;
                VoxelMissionProgress.ReportEnemyVehicleDestroyed(null, first);
                VoxelMissionProgress.ReportEnemyVehicleDestroyed(null, second);
                if(mission.Points != 95 || VoxelMissionProgress.GetEnemyVehicleDestroyedPoints(second) != 70)
                    throw new Exception("Per-type score or popup value incorrect");
                var entries = mission.Breakdown.Entries(VoxelMissionBreakdown.Group.ProgressGained);
                if(entries.Count != 2 || entries[0].amount != 25 || entries[1].amount != 70)
                    throw new Exception("Enemy breakdown does not separate types");
                second.destructionScore = 0; VoxelMissionProgress.ReportEnemyVehicleDestroyed(null, second);
                if(mission.Points != 95) throw new Exception("Zero score not honoured");
                second.destructionScore = -10;
                if(VoxelMissionProgress.GetEnemyVehicleDestroyedPoints(second) != 0) throw new Exception("Negative score not clamped");
                Debug.Log("Enemy score validation passed: distinct type scores, popup value, per-type breakdown, zero reward and nonnegative clamp.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(missionTuning);
                UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second);
                active.SetValue(null, previous);
            }
        }
    }
}
