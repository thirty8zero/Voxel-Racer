using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelMineLayerLaneChangeValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Mine Layer Lane Change")]
        public static void Run()
        {
            var root = new GameObject("Temporary mine layer scheduling test");
            var tuning = ScriptableObject.CreateInstance<VoxelMineLayerTuning>();
            try
            {
                var enemy = root.AddComponent<VoxelEnemyCar>(); enemy.enabled = false;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var schedule = typeof(VoxelEnemyCar).GetMethod("SchedulePostDropLaneChange", flags);
                var pending = typeof(VoxelEnemyCar).GetField("postDropLaneChangeAt", flags);
                void Roll(float roll, float now) => schedule.Invoke(enemy, new object[] { tuning, roll, now });
                void Check(float expected)
                {
                    float actual = (float)pending.GetValue(enemy);
                    if (actual != expected) throw new Exception($"Expected deadline {expected}, got {actual}");
                }
                tuning.postDropLaneChangeChance = 0; Roll(0, 10); Check(float.PositiveInfinity);
                tuning.postDropLaneChangeChance = .5f; Roll(.7f, 10); Check(float.PositiveInfinity);
                tuning.postDropLaneChangeDelay = 2; Roll(.2f, 10); Check(12);
                Roll(.2f, 11); Check(12);
                pending.SetValue(enemy, float.PositiveInfinity);
                tuning.postDropLaneChangeChance = 1; tuning.postDropLaneChangeDelay = 0;
                Roll(1, 20); Check(20);
                if ((bool)typeof(VoxelEnemyCar).GetField("evasiveChanceRolled", flags).GetValue(enemy))
                    throw new Exception("Mine drop consumed damage-based evasion roll");
                Debug.Log("Mine layer lane-change checks passed: disabled/failed/successful rolls, delay, pending deadline preservation, 100% chance, zero delay, independent damage evasion.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(tuning); }
        }
    }
}
