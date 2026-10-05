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
            if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
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
                Equal(mission.EffectiveTimeBonusMultiplier, 1, "Civilian penalties stop at 1x");
                mission.ChangeMultiplier(-10, "TEST");
                Equal(mission.EffectiveTimeBonusMultiplier, 1, "1x floor");
                mission.AddMultiplierBonus(.1f);
                Equal(mission.EffectiveTimeBonusMultiplier, 1.1f, "Recovery from minimum");
                mission.ChangeMultiplier(20, "TEST");
                Equal(mission.EffectiveTimeBonusMultiplier, 5, "Maximum");
                mission.AdvanceBonusClock(5);
                Equal(mission.EffectiveTimeBonusMultiplier, 5, "No passive decay");
                mission.AdvanceBonusClock(115);
                Equal(mission.EffectiveTimeBonusMultiplier, 5, "Expiry retains multiplier until first overtime second");
                mission.AdvanceBonusClock(.4f);
                Equal(mission.EffectiveTimeBonusMultiplier, 5, "Fractional overtime waits for full second");
                mission.AdvanceBonusClock(.6f);
                Equal(mission.EffectiveTimeBonusMultiplier, 4.9f, "First second decays .1");
                Equal((float)typeof(VoxelMissionProgress).GetField("displayedMultiplierBonus", flags).GetValue(mission), 4.9f, "Decay updates HUD immediately");
                Equal((float)typeof(VoxelMissionProgress).GetField("multiplierPulseUntil", flags).GetValue(mission), Time.unscaledTime + .32f, "Decay pulses immediately");
                if (!(bool)typeof(VoxelMissionProgress).GetField("lastMultiplierWasNegative", flags).GetValue(mission))
                    throw new InvalidOperationException("Decay pulse must use loss colour");
                if (mission.AddBonusTime(15)) throw new InvalidOperationException("Expired countdown revived by time prize");
                VoxelMissionProgress.ReportEnemyVoxelDestroyed(100, Vector3.zero);
                Equal(mission.EffectiveTimeBonusMultiplier, 5, "Enemy gains still work after expiry");
                mission.AdvanceBonusClock(3.25f);
                Equal(mission.EffectiveTimeBonusMultiplier, 4.7f, "Hitch catches up three decay ticks");
                Equal(mission.Breakdown.Total(VoxelMissionBreakdown.Group.MultiplierLost), 1.25f, "Breakdown records actual penalties and decay");
                mission.AddBonusCash(30);
                VoxelMissionProgress.ReportEnemyVoxelDamage(100000);
                Equal(mission.RemainingTimeMultiplierBonus, 0, "Late finish has no remaining-time bonus");
                Equal(mission.TotalCurrencyEarned, 500, "Late finish pays surviving multiplier plus cash");
                Equal(VoxelCurrencyState.Balance, cash + 500, "Late cash credited once");
                VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                mission.AdvanceBonusClock(100);
                Equal(mission.EffectiveTimeBonusMultiplier, 4.7f, "Completion freezes decay");
                Equal(VoxelCurrencyState.Balance, cash + 500, "Duplicate finish blocked");

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
                Equal(mission.RemainingTimeBonusSeconds, 120, "All remaining seconds captured");
                Equal(mission.RemainingTimeMultiplierBonus, 1.2f, "Each remaining second adds .01");
                Equal(mission.EffectiveTimeBonusMultiplier, 2.25f, "Final action included in banked mission multiplier");
                Equal(mission.FinalRewardMultiplier, 3.45f, "Remaining time included only in payout multiplier");
                Equal((float)typeof(VoxelMissionProgress).GetField("displayedMultiplierBonus", flags).GetValue(mission), 2.25f, "Time bonus changed the original HUD");
                Equal(mission.TotalCurrencyEarned, 385, "100*3.45 + 40 cash");
                Equal(mission.Breakdown.Total(VoxelMissionBreakdown.Group.MultiplierGained), 2.45f, "Time bonus included in breakdown");

                mission.Configure(tuning);
                mission.ChangeMultiplier(-10, "TEST");
                mission.AdvanceBonusClock(120);
                tuning.requiredPoints = 1;
                VoxelMissionProgress.ReportEnemyVoxelDamage();
                Equal(mission.TotalCurrencyEarned, 100, "Late minimum multiplier pays base reward");

                tuning.timeBonusCurrencyMultiplier = .2f;
                mission.Configure(tuning);
                Equal(mission.EffectiveTimeBonusMultiplier, 1, "Old tuning values below 1 normalize at runtime");
                mission.AddMultiplierBonus(.05f);
                mission.AdvanceBonusClock(120.5f);
                Equal(mission.EffectiveTimeBonusMultiplier, 1.05f, "Expiry crossing retains fractional overtime");
                mission.AdvanceBonusClock(.5f);
                Equal(mission.EffectiveTimeBonusMultiplier, 1, "Partial final drop clamps at 1");
                Equal(mission.Breakdown.Total(VoxelMissionBreakdown.Group.MultiplierLost), .05f, "Floor losses use actual delta");
                mission.AdvanceBonusClock(60.25f);
                mission.AddMultiplierBonus(.2f);
                mission.AdvanceBonusClock(.5f);
                Equal(mission.EffectiveTimeBonusMultiplier, 1.2f, "No backlogged decay after gaining from floor");
                mission.AdvanceBonusClock(.25f);
                Equal(mission.EffectiveTimeBonusMultiplier, 1.1f, "Decay cadence resumes after floor");
                mission.SetBossEncounter(true);
                mission.FailBossEncounter();
                mission.AdvanceBonusClock(5);
                mission.AddMultiplierBonus(1);
                Equal(mission.EffectiveTimeBonusMultiplier, 1.1f, "Failure freezes multiplier and decay");

                tuning.timeBonusCurrencyMultiplier = 5;
                mission.Configure(tuning);
                Equal(mission.RemainingTimeMultiplierBonus, 0, "New mission resets completion bonus");
                mission.AdvanceBonusClock(89.25f);
                VoxelMissionProgress.ReportEnemyVoxelDamage();
                Equal(mission.RemainingTimeBonusSeconds, 31, "Bonus seconds match visible countdown rounding");
                Equal(mission.RemainingTimeMultiplierBonus, .31f, "31 visible seconds add .31");
                Equal(mission.EffectiveTimeBonusMultiplier, 5, "Time bonus preserves live multiplier cap/HUD");
                Equal(mission.FinalRewardMultiplier, 5.31f, "Time bonus is added beyond live cap for payout");
                Equal((float)typeof(VoxelMissionProgress).GetField("displayedMultiplierBonus", flags).GetValue(mission), 5, "Capped HUD changed at completion");
                Equal(mission.TotalCurrencyEarned, 531, "Capped multiplier still earns early-finish bonus");
                int completedBalance = VoxelCurrencyState.Balance;
                VoxelMissionProgress.ReportEnemyVoxelDamage();
                Equal(VoxelCurrencyState.Balance, completedBalance, "Time bonus credited once");

                tuning.timeBonusCurrencyMultiplier = 2;
                tuning.requiredPoints = 100000;
                mission.Configure(tuning);
                mission.AdvanceBonusClock(120);
                for (int i = 0; i < 100; i++) mission.AdvanceBonusClock(.01f);
                Equal(mission.EffectiveTimeBonusMultiplier, 1.9f, "Small frame deltas accumulate into one tick");
                mission.AdvanceBonusClock(float.MaxValue);
                Equal(mission.EffectiveTimeBonusMultiplier, 1, "Long overtime is bounded and respects floor");
                Directory.CreateDirectory("Temp");
                File.WriteAllText("Temp/LiveMultiplierValidation.txt", "PASS: actual-voxel rewards, event values, 1-5 live clamp, no pre-expiry decay, exact/fractional expiry, .1 per overtime second, immediate loss pulse/HUD, hitch catch-up, post-expiry gains, late multiplier payout, .01 per displayed second, final-action/time-bonus payout above live cap, actual breakdown deltas, completion/failure freeze, mission reset, no duplicate cash, floor cadence, long overtime.");
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
