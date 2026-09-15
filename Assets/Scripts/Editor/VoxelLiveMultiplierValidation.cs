using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelLiveMultiplierValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Live Multiplier")]
        public static void Run()
        {
            var oldActive = VoxelMissionProgress.Active;
            int cash = VoxelCurrencyState.Balance;
            var root = new GameObject("Temporary Live Multiplier Test");
            var tuning = ScriptableObject.CreateInstance<VoxelMissionTuning>();
            try
            {
                tuning.timeBonusCurrencyMultiplier = 1;
                tuning.requiredPoints = 100000;
                tuning.completionCurrencyAward = 100;
                tuning.timeLimitSeconds = 120;
                var mission = root.AddComponent<VoxelMissionProgress>();
                typeof(VoxelMissionProgress).GetField("<Active>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, mission);
                mission.Configure(tuning);
                VoxelMissionProgress.ReportEnemyVoxelDamage(10);
                Equal(mission.EffectiveTimeBonusMultiplier, 1, "Hits must not farm multiplier");
                VoxelMissionProgress.ReportEnemyVoxelDestroyed(9, Vector3.zero);
                Equal(mission.EffectiveTimeBonusMultiplier, 1, "Nine voxels do not award a bonus");
                VoxelMissionProgress.ReportEnemyVoxelDestroyed(1, Vector3.zero);
                Equal(mission.EffectiveTimeBonusMultiplier, 1.01f, "Tenth voxel awards .01 immediately");
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var flights = (System.Collections.ICollection)typeof(VoxelMissionProgress).GetField("multiplierFlights", flags).GetValue(mission);
                Equal(flights.Count, 1, "Threshold creates popup without inactivity delay");
                VoxelMissionProgress.ReportEnemyVoxelDestroyed(25, Vector3.zero);
                Equal(mission.EffectiveTimeBonusMultiplier, 1.03f, "Bulk damage awards complete groups");
                VoxelMissionProgress.ReportEnemyVoxelDestroyed(5, Vector3.zero);
                Equal(mission.EffectiveTimeBonusMultiplier, 1.04f, "Remainder carries between hits");
                VoxelMissionProgress.ReportEnemyVoxelDestroyed(60, Vector3.zero);
                Equal(mission.EffectiveTimeBonusMultiplier, 1.1f, "One hundred voxels award .1");
                var pending = typeof(VoxelMissionProgress).GetField("pendingVoxelChange", flags);
                var lastHit = typeof(VoxelMissionProgress).GetField("lastVoxelDamageAt", flags);
                var flush = typeof(VoxelMissionProgress).GetMethod("FlushVoxelPopup", flags);
                Equal((float)pending.GetValue(mission), 0, "Enemy gains bypass inactivity batching");
                VoxelMissionProgress.ReportCivilianVoxelDestroyed(1, Vector3.zero);
                flush.Invoke(mission, null);
                Equal((float)pending.GetValue(mission), -.1f, "Civilian popup still waits for inactivity");
                lastHit.SetValue(mission, Time.unscaledTime - 1.01f);
                flush.Invoke(mission, null);
                Equal((float)pending.GetValue(mission), 0, "One second flushes accumulated popup");
                mission.ChangeMultiplier(.1f, "TEST RESTORE");
                VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                VoxelMissionProgress.ReportFuelDrumDestroyed(3);
                VoxelMissionProgress.ReportCivilianNearMiss(1);
                Equal(mission.EffectiveTimeBonusMultiplier, 1.75f, "Kill, barrels and close call");
                VoxelMissionProgress.ReportCivilianVoxelDestroyed(2, Vector3.zero);
                VoxelMissionProgress.ReportCivilianVehicleDestroyed();
                Equal(mission.EffectiveTimeBonusMultiplier, .55f, "Civilian penalties stack");
                mission.ChangeMultiplier(-10, "TEST");
                Equal(mission.EffectiveTimeBonusMultiplier, 0, "Zero floor");
                mission.AddMultiplierBonus(.1f);
                Equal(mission.EffectiveTimeBonusMultiplier, .1f, "Recovery from zero");
                mission.ChangeMultiplier(20, "TEST");
                Equal(mission.EffectiveTimeBonusMultiplier, 5, "Maximum");
                mission.AdvanceBonusClock(5);
                Equal(mission.EffectiveTimeBonusMultiplier, 5, "No passive decay");
                mission.AdvanceBonusClock(115);
                Equal(mission.EffectiveTimeBonusMultiplier, 0, "Expiry loses bonus");
                mission.AddMultiplierBonus(1);
                Equal(mission.EffectiveTimeBonusMultiplier, 0, "Cannot rebuild after expiry");
                mission.AddBonusCash(30);
                VoxelMissionProgress.ReportEnemyVoxelDamage(100000);
                Equal(mission.TotalCurrencyEarned, 130, "Late finish keeps base and cash");
                Equal(VoxelCurrencyState.Balance, cash + 130, "Late cash credited once");
                VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                Equal(VoxelCurrencyState.Balance, cash + 130, "Duplicate finish blocked");

                mission.Configure(tuning);
                VoxelMissionProgress.ReportEnemyVoxelDestroyed(9, Vector3.zero);
                mission.Configure(tuning);
                VoxelMissionProgress.ReportEnemyVoxelDestroyed(1, Vector3.zero);
                Equal(mission.EffectiveTimeBonusMultiplier, 1, "New mission resets partial voxel groups");
                Equal(mission.EffectiveTimeBonusMultiplier, 1, "New mission resets multiplier");
                Equal(mission.BonusCashEarned, 0, "New mission resets cash");
                tuning.requiredPoints = tuning.enemyVehicleDestroyedPoints;
                mission.AddMultiplierBonus(1);
                mission.AddBonusCash(40);
                VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                Equal(mission.EffectiveTimeBonusMultiplier, 2.25f, "Final action included");
                Equal(mission.TotalCurrencyEarned, 265, "100*2.25 + 40 cash");

                mission.Configure(tuning);
                mission.ChangeMultiplier(-10, "TEST");
                tuning.requiredPoints = 1;
                VoxelMissionProgress.ReportEnemyVoxelDamage();
                Equal(mission.TotalCurrencyEarned, 100, "Zero bonus preserves base reward");
                Directory.CreateDirectory("Temp");
                File.WriteAllText("Temp/LiveMultiplierValidation.txt", "PASS: actual-voxel rewards, event values, stacked civilian penalties, 0-5 clamp, recovery, no decay, timeout loss/freeze, base and cash retained, final-action payout, no duplicate payout, mission reset.");
                Debug.Log(File.ReadAllText("Temp/LiveMultiplierValidation.txt"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(tuning);
                typeof(VoxelMissionProgress).GetField("<Active>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, oldActive);
                VoxelCurrencyState.Reset(); VoxelCurrencyState.Add(cash);
            }
        }
        private static void Equal(float actual, float expected, string label)
        {
            if (Mathf.Abs(actual - expected) > .001f) throw new InvalidOperationException(label + ": " + actual + " != " + expected);
        }
    }
}
