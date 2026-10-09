using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelKillChainValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
        private static void Equal(float actual, float expected, string reason) =>
            Check(Mathf.Abs(actual - expected) < .001f, reason + ": " + actual + " != " + expected);
        private static IList Flights(VoxelMissionProgress mission) =>
            (IList)typeof(VoxelMissionProgress).GetField("multiplierFlights", Private).GetValue(mission);
        private static float ShakeStrength(VoxelMissionProgress mission) =>
            (float)typeof(VoxelMissionProgress).GetMethod("GetKillChainShakeStrength", Private).Invoke(mission, null);
        public static int ComboFlights(VoxelMissionProgress mission)
        {
            int count = 0;
            foreach (object flight in Flights(mission))
                if ((string)flight.GetType().GetField("reason").GetValue(flight) == "KILL CHAIN COMBO") count++;
            return count;
        }

        [MenuItem("Tools/Voxel Racer/Validate Kill Chain Combo")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Start in Edit Mode.");
            var active = typeof(VoxelMissionProgress).GetField("<Active>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var oldActive = active.GetValue(null);
            int cash = VoxelCurrencyState.Balance;
            var root = new GameObject("Temporary kill chain validation");
            var tuning = ScriptableObject.CreateInstance<VoxelMissionTuning>();
            try
            {
                var mission = root.AddComponent<VoxelMissionProgress>(); mission.enabled = false; active.SetValue(null, mission);
                tuning.requiredPoints = 100000; tuning.timeBonusCurrencyMultiplier = 1; tuning.timeLimitSeconds = 120;
                tuning.enemyDestroyedMultiplier = 0; tuning.civilianDestroyedMultiplierPenalty = 0;
                tuning.enemyVoxelMultiplier = 0; tuning.civilianVoxelMultiplierPenalty = 0;
                mission.Configure(tuning);
                VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                Equal(mission.KillChainCount, 1, "First kill starts chain");
                Check(!mission.IsKillChainAnnouncementShowing, "Single kill is not a combo");
                mission.AdvanceBonusClock(2.9f); VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                Equal(mission.KillChainCount, 2, "Second kill within window"); Equal(mission.KillChainSecondsRemaining, 3, "Window refreshed");
                Check(mission.IsKillChainAnnouncementShowing, "Second kill must immediately display combo");
                Equal(mission.KillChainDisplayCount, 2, "Live x2 count");
                Equal(mission.KillChainAnnouncementRemaining, 3, "Display shares refreshed countdown");
                float initialShake = ShakeStrength(mission);
                Equal(mission.EffectiveTimeBonusMultiplier, 1, "No premature combo multiplier");
                mission.AdvanceBonusClock(2.9f);
                Check(mission.IsKillChainAnnouncementShowing, "UI stays visible throughout countdown");
                Equal(mission.KillChainDisplayCount, 2, "Count stays stable without kills");
                Check(ShakeStrength(mission) < initialShake, "Kill kick should decay to a smaller tremor");
                VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                Equal(mission.KillChainCount, 3, "Rolling window extends beyond first kill");
                Equal(mission.KillChainDisplayCount, 3, "New kill immediately increments live count");
                Equal(mission.KillChainSecondsRemaining, 3, "Third kill refreshes window");
                Check(ShakeStrength(mission) > initialShake, "Third kill renews a stronger shake");
                float thirdShake = ShakeStrength(mission);
                VoxelMissionProgress.ReportEnemyVehicleDestroyed(); Equal(mission.KillChainCount, 4, "Simultaneous kills count");
                Equal(mission.KillChainDisplayCount, 4, "Simultaneous kill immediately updates display");
                Check(ShakeStrength(mission) > thirdShake, "Fourth kill adds stronger shake");
                Equal(ComboFlights(mission), 0, "No multiplier flight during collection");
                mission.AdvanceBonusClock(2.99f); Check(mission.IsKillChainAnnouncementShowing, "Combo ended early");
                Equal(mission.EffectiveTimeBonusMultiplier, 1, "Reward held before disappearance");
                mission.AdvanceBonusClock(.01f); Check(!mission.IsKillChainAnnouncementShowing, "Announcement didn't disappear");
                Equal(mission.KillChainCount, 0, "Expiry clears chain"); Equal(mission.KillChainDisplayCount, 0, "Expiry clears display");
                Equal(mission.EffectiveTimeBonusMultiplier, 1.75f, "One total .75 bonus for x4"); Equal(ComboFlights(mission), 1, "Exactly one flight in disappearance tick");
                object flight = Flights(mission)[0];
                Equal((float)flight.GetType().GetField("startedAt").GetValue(flight), Time.unscaledTime, "Flight starts immediately");
                Equal((float)flight.GetType().GetField("amount").GetValue(flight), .75f, "Flight carries full bonus");
                Equal((float)typeof(VoxelMissionProgress).GetField("displayedMultiplierBonus", Private).GetValue(mission), 1, "Counter waits for usual flight arrival");
                Equal(mission.Breakdown.Total(VoxelMissionBreakdown.Group.MultiplierGained), .75f, "Breakdown records one total reward");
                mission.AdvanceBonusClock(10); Equal(ComboFlights(mission), 1, "Expired chain cannot pay twice");
                VoxelMissionProgress.ReportEnemyVehicleDestroyed(); Equal(mission.KillChainCount, 1, "Next chain starts fresh");
                Check(!mission.IsKillChainAnnouncementShowing, "Next single kill remains hidden");
                VoxelMissionProgress.ReportEnemyVehicleDestroyed(); Equal(mission.KillChainDisplayCount, 2, "Next combo immediately shows x2");
                Equal(ShakeStrength(mission), initialShake, "New combo starts with initial shake");
                mission.AdvanceBonusClock(3); Equal(mission.EffectiveTimeBonusMultiplier, 2, "Next chain releases .25"); Equal(ComboFlights(mission), 2, "Two separate flights");

                mission.Configure(tuning); VoxelMissionProgress.ReportEnemyVehicleDestroyed(); mission.AdvanceBonusClock(5);
                Check(!mission.IsKillChainAnnouncementShowing, "Single kill announced a combo"); Equal(mission.EffectiveTimeBonusMultiplier, 1, "Single kill has no bonus");
                VoxelMissionProgress.ReportEnemyVehicleDestroyed(); VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                VoxelMissionProgress.ReportCivilianVoxelDestroyed(20, Vector3.zero); Equal(mission.KillChainCount, 2, "Civilian damage doesn't cancel");
                VoxelMissionProgress.ReportCivilianVehicleDestroyed();
                Check(!mission.IsKillChainAnnouncementShowing, "Civilian immediately clears active UI");
                mission.AdvanceBonusClock(5);
                Equal(mission.KillChainCount, 0, "Civilian cancels unfinished chain"); Equal(mission.EffectiveTimeBonusMultiplier, 1, "Cancelled chain has no bonus"); Equal(ComboFlights(mission), 0, "Cancelled chain has no flight");
                VoxelMissionProgress.ReportEnemyVehicleDestroyed(); VoxelMissionProgress.ReportEnemyVehicleDestroyed(); mission.AdvanceBonusClock(3);
                VoxelMissionProgress.ReportEnemyVehicleDestroyed(); VoxelMissionProgress.ReportCivilianVehicleDestroyed();
                Equal(mission.KillChainCount, 0, "Civilian cancels new window"); Equal(mission.KillChainDisplayCount, 0, "No stale display after cancellation");
                mission.AdvanceBonusClock(2); Equal(mission.EffectiveTimeBonusMultiplier, 1.25f, "Previously banked reward retained");
                mission.Configure(tuning); VoxelMissionProgress.ReportEnemyVehicleDestroyed(); VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                mission.AdvanceBonusClock(5); Equal(mission.EffectiveTimeBonusMultiplier, 1.25f, "Hitch expires countdown once"); Equal(ComboFlights(mission), 1, "Hitch releases one flight");
                mission.Configure(tuning); VoxelMissionProgress.ReportEnemyVehicleDestroyed(); VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                mission.Configure(tuning); Check(!mission.IsKillChainAnnouncementShowing, "Configure clears display"); mission.AdvanceBonusClock(10); Equal(mission.EffectiveTimeBonusMultiplier, 1, "Reset clears pending bonus");

                var gate = new GameObject("Opening gate"); gate.transform.SetParent(root.transform); gate.SetActive(false);
                mission.SetStartCountdown(gate.AddComponent<VoxelStartCountdown>()); VoxelMissionProgress.ReportEnemyVehicleDestroyed(); mission.AdvanceBonusClock(5);
                Equal(mission.KillChainCount, 0, "Opening blocks combo"); mission.SetStartCountdown(null);
                tuning.enemyDestroyedMultiplier = .1f; mission.Configure(tuning);
                VoxelMissionProgress.ReportEnemyVehicleDestroyed(); VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                Equal(mission.EffectiveTimeBonusMultiplier, 1.2f, "Ordinary rewards remain immediate"); mission.AdvanceBonusClock(5); Equal(mission.EffectiveTimeBonusMultiplier, 1.45f, "Delayed combo stacks normally");
                tuning.maximumTimeMultiplier = 1.5f; mission.Configure(tuning); for (int i = 0; i < 4; i++) VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                mission.AdvanceBonusClock(5); Equal(mission.EffectiveTimeBonusMultiplier, 1.5f, "Existing cap honoured");

                tuning.maximumTimeMultiplier = 5; tuning.enemyDestroyedMultiplier = 0; tuning.requiredPoints = tuning.enemyVehicleDestroyedPoints * 2;
                mission.Configure(tuning); VoxelMissionProgress.ReportEnemyVehicleDestroyed(); VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                Check(mission.IsComplete, "Completing kill didn't finish mission"); Equal(mission.EffectiveTimeBonusMultiplier, 1.25f, "Final kill banks pending combo");
                Equal(mission.TotalCurrencyEarned, Mathf.RoundToInt(tuning.completionCurrencyAward * 2.45f), "Payout includes combo/time bonus");
                Equal(mission.KillChainCount, 0, "Completion clears chain"); VoxelMissionProgress.ReportEnemyVehicleDestroyed(); mission.AdvanceBonusClock(10); Equal(mission.EffectiveTimeBonusMultiplier, 1.25f, "No duplicate completion reward");
                tuning.requiredPoints = 100000; mission.Configure(tuning); VoxelMissionProgress.ReportEnemyVehicleDestroyed(); VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                tuning.requiredPoints = mission.Points + 1; VoxelMissionProgress.ReportEnemyVoxelDamage(); Equal(mission.EffectiveTimeBonusMultiplier, 1.25f, "Completion banks active displayed combo once");
                mission.Configure(tuning); mission.SetBossEncounter(true); VoxelMissionProgress.ReportEnemyVehicleDestroyed(); VoxelMissionProgress.ReportEnemyVehicleDestroyed(); mission.FailBossEncounter();
                Check(!mission.IsKillChainAnnouncementShowing, "Failure clears display"); mission.AdvanceBonusClock(10); Equal(mission.EffectiveTimeBonusMultiplier, 1, "Failure discards pending reward");

                tuning.requiredPoints = 100000; mission.Configure(tuning);
                for (int i = 0; i < 50; i++) VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                Equal(mission.KillChainDisplayCount, 50, "Long combo keeps incrementing count");
                Check(ShakeStrength(mission) <= 8f, "Shake stays bounded for long chains");

                tuning.requiredPoints = 100000; mission.Configure(tuning);
                var traffic = new GameObject("Temporary traffic").AddComponent<VoxelObstacleCar>(); traffic.transform.SetParent(root.transform);
                var report = typeof(VoxelObstacleCar).GetMethod("ReportDestroyed", Private);
                typeof(VoxelObstacleCar).GetField("<IsEnemyTraffic>k__BackingField", Private).SetValue(traffic, true);
                report.Invoke(traffic, new object[] { true }); Equal(mission.KillChainCount, 1, "Radar death counts"); report.Invoke(traffic, new object[] { false }); Equal(mission.KillChainCount, 1, "No-award death doesn't count");
                typeof(VoxelObstacleCar).GetField("<IsEnemyTraffic>k__BackingField", Private).SetValue(traffic, false); report.Invoke(traffic, new object[] { true }); Equal(mission.KillChainCount, 0, "Civilian reporting cancels");
                Directory.CreateDirectory("Temp/KillChain");
                string result = "PASS: second kill immediately shows live x2; each rolling/simultaneous kill increments count and refreshes the three-second window; UI persists until expiry; stronger renewed shake per kill with bounded long-chain amplitude; one total multiplier flight in the expiry tick; usual counter arrival; fresh subsequent chains; single/cancelled chains excluded; banked rewards retained; hitch/reset/opening/cap/normal reward stacking/breakdown; finishing-kill/active-combo payout; failure cleanup; actual Radar/civilian/no-award reporting. Cash and active mission restored.";
                File.WriteAllText("Temp/KillChain/Validation.txt", result); Debug.Log(result);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tuning); active.SetValue(null, oldActive); VoxelCurrencyState.Reset(); VoxelCurrencyState.Add(cash); }
        }
    }
}
