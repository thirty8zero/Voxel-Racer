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
                ValidateScorePenaltyFlash(mission, tuning);
                ValidatePopupTiming(mission, tuning);
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
                File.WriteAllText("Temp/LiveMultiplierValidation.txt", "PASS: score-penalty red flash, repeated-loss restart, fade timing, zero-floor and mission reset/failure gates; fixed non-voxel HUD origin, incoming-number handoff/early flight and held-slot cleanup, tunable damage/multiplier holds, separate voxel/destruction holds on actual positive and negative reporting paths, independent overlapping arrivals and signed counter pulses, unchanged travel duration and counter feedback on arrival; actual-voxel rewards, event values, 1-5 live clamp, no pre-expiry decay, exact/fractional expiry, .1 per overtime second, immediate loss pulse/HUD, hitch catch-up, post-expiry gains, late multiplier payout, .01 per displayed second, final-action/time-bonus payout above live cap, actual breakdown deltas, completion/failure freeze, mission reset, no duplicate cash, floor cadence, long overtime.");
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
        private static void ValidateScorePenaltyFlash(VoxelMissionProgress mission, VoxelMissionTuning tuning)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var add = typeof(VoxelMissionProgress).GetMethod("AddPoints", flags);
            var colour = typeof(VoxelMissionProgress).GetMethod("GetMissionScoreColour", flags);
            var timestamp = typeof(VoxelMissionProgress).GetField("lastScorePenaltyAt", flags);
            Color Current() => (Color)colour.Invoke(mission, new object[] { Color.white });
            void CheckColour(Color expected, string label)
            {
                Color value = Current();
                Equal(value.r, expected.r, label + " red");
                Equal(value.g, expected.g, label + " green");
                Equal(value.b, expected.b, label + " blue");
            }
            CheckColour(Color.white, "New mission has no penalty flash");
            add.Invoke(mission, new object[] { 20, "Flash fixture", 1 });
            CheckColour(Color.white, "Positive points do not flash red");
            add.Invoke(mission, new object[] { -1, "Civilian hit fixture", 1 });
            var red = new Color(1f, .15f, .1f);
            CheckColour(red, "A score loss flashes both numeric readouts red");
            timestamp.SetValue(mission, Time.unscaledTime - .325f);
            CheckColour(Color.Lerp(red, Color.white, .5f), "Penalty colour fades back smoothly");
            add.Invoke(mission, new object[] { -5, "Civilian destruction fixture", 1 });
            CheckColour(red, "Repeated penalties restart the flash");
            timestamp.SetValue(mission, Time.unscaledTime - .51f);
            CheckColour(Color.white, "Penalty flash ends after half a second");
            add.Invoke(mission, new object[] { -100, "Clamped loss fixture", 1 });
            Equal(mission.Points, 0, "Penalty still clamps points at zero");
            timestamp.SetValue(mission, float.NegativeInfinity);
            add.Invoke(mission, new object[] { -1, "Floor fixture", 1 });
            CheckColour(Color.white, "No lost points at zero means no new flash");
            mission.SetBossEncounter(true); mission.FailBossEncounter();
            add.Invoke(mission, new object[] { -1, "Failed mission fixture", 1 });
            CheckColour(Color.white, "Failed missions ignore penalties");
            mission.Configure(tuning);
            CheckColour(Color.white, "Reconfiguration clears the flash");
        }

        private static void ValidatePopupTiming(VoxelMissionProgress mission, VoxelMissionTuning tuning)
        {
            Equal(VoxelScorePopup.MissionFlightProgress(.12f, .8f), 0, "Default score hold");
            Equal(VoxelScorePopup.MissionFlightProgress(.8f, .8f), 1, "Default score arrival");
            Equal(VoxelScorePopup.MissionFlightProgress(2f, 2.68f, 2f), 0, "Tuned score hold");
            Equal(VoxelScorePopup.MissionFlightProgress(2.34f, 2.68f, 2f), .5f, "Hold preserves score travel speed");
            Equal(VoxelScorePopup.MissionFlightProgress(.68f, .68f, 0), 1, "Zero score hold");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var flights = (System.Collections.IList)typeof(VoxelMissionProgress).GetField("multiplierFlights", flags).GetValue(mission);
            var update = typeof(VoxelMissionProgress).GetMethod("Update", flags);
            var displayed = typeof(VoxelMissionProgress).GetField("displayedMultiplierBonus", flags);
            var popupObject = new GameObject("Temporary score feedback timing");
            var random = UnityEngine.Random.state;
            try
            {
                var popup = popupObject.AddComponent<VoxelScorePopup>();
                var begin = typeof(VoxelScorePopup).GetMethod("Begin", flags);
                var lifetime = typeof(VoxelScorePopup).GetField("lifetime", flags);
                tuning.damagePopupHoldSeconds = 2f;
                tuning.voxelDamagePopupHoldSeconds = .5f;
                foreach (int sign in new[] { 1, -1 })
                {
                    begin.Invoke(popup, new object[] { Vector3.zero, sign, VoxelScorePopup.Style.WeaponDamage, -1f });
                    Equal((float)lifetime.GetValue(popup), 1.18f, "Signed voxel popup uses its own hold plus normal travel");
                    foreach (var style in new[] { VoxelScorePopup.Style.RamDamage, VoxelScorePopup.Style.EnemyDestroyed,
                        VoxelScorePopup.Style.FuelDrumDestroyed, VoxelScorePopup.Style.NearMiss })
                    {
                        begin.Invoke(popup, new object[] { Vector3.zero, sign * 30, style, 2f });
                        Equal((float)lifetime.GetValue(popup), 3.88f, "Signed non-voxel popup uses its own hold plus normal travel");
                    }
                }
                ValidateNonVoxelPopupHandoff(mission, popup, begin, lifetime, flags);
                tuning.voxelDamagePopupHoldSeconds = 0f;
                begin.Invoke(popup, new object[] { Vector3.zero, 1, VoxelScorePopup.Style.WeaponDamage, -1f });
                Equal((float)lifetime.GetValue(popup), .68f, "Reused popup accepts zero voxel hold without changing travel");
                begin.Invoke(popup, new object[] { Vector3.zero, 30, VoxelScorePopup.Style.BonusCash, -1f });
                Equal((float)lifetime.GetValue(popup), 1.2f, "Damage hold must not change cash feedback");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject); UnityEngine.Random.state = random;
                tuning.damagePopupHoldSeconds = .12f;
                tuning.voxelDamagePopupHoldSeconds = .12f;
            }
            tuning.multiplierPopupHoldSeconds = 2f;
            mission.ChangeMultiplier(.25f, "BOX PRIZE");
            object flight = flights[0];
            flight.GetType().GetField("startedAt").SetValue(flight, Time.unscaledTime - 1f); flights[0] = flight;
            update.Invoke(mission, null);
            Equal((float)displayed.GetValue(mission), 1, "Long hold must delay the displayed counter");
            flight.GetType().GetField("startedAt").SetValue(flight, Time.unscaledTime - 2.69f); flights[0] = flight;
            update.Invoke(mission, null);
            Equal((float)displayed.GetValue(mission), 1.25f, "Counter updates at tuned arrival");
            Equal(flights.Count, 0, "Arrived flight removed");
            tuning.multiplierPopupHoldSeconds = .17f; mission.Configure(tuning);
            ValidateSeparateMultiplierHolds(mission, tuning, flights, update, displayed);
        }

        private static void ValidateNonVoxelPopupHandoff(VoxelMissionProgress mission, VoxelScorePopup first,
            MethodInfo begin, FieldInfo lifetime, BindingFlags flags)
        {
            var secondObject = new GameObject("Temporary incoming non-voxel popup");
            secondObject.transform.SetParent(first.transform);
            try
            {
                var second = secondObject.AddComponent<VoxelScorePopup>();
                var elapsed = typeof(VoxelScorePopup).GetField("elapsed", flags);
                var hold = typeof(VoxelScorePopup).GetField("holdDuration", flags);
                var held = typeof(VoxelScorePopup).GetField("heldHudPopup", BindingFlags.Static | BindingFlags.NonPublic);
                begin.Invoke(first, new object[] { new Vector3(10000, -500, -10000), 125, VoxelScorePopup.Style.EnemyDestroyed, 2f });
                var start = (Vector3)typeof(VoxelScorePopup).GetField("lockedScreenPosition", flags).GetValue(first);
                float topExtent = (float)typeof(VoxelScorePopup).GetMethod("GetHudPopupTopExtent", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { VoxelScorePopup.Style.EnemyDestroyed, false });
                Vector2 anchor = mission.GetNonVoxelDamagePopupOrigin(topExtent);
                Equal(start.x, anchor.x, "Non-voxel popup aligns below the mission percentage");
                Equal(Screen.height - start.y, anchor.y, "Non-voxel popup ignores the source world position");
                if (start.z <= 0) throw new InvalidOperationException("HUD popup inherited a behind-camera origin");
                elapsed.SetValue(first, .4f);
                begin.Invoke(second, new object[] { Vector3.zero, -30, VoxelScorePopup.Style.RamDamage, -1f });
                Equal((float)elapsed.GetValue(first), (float)hold.GetValue(first), "New signed number ends the previous hold immediately");
                Equal((float)lifetime.GetValue(first) - (float)elapsed.GetValue(first), .3f, "Interrupted number leaves promptly toward the tally");
                Equal((float)elapsed.GetValue(second), 0, "Incoming number receives its full hold");
                begin.Invoke(first, new object[] { Vector3.zero, 1, VoxelScorePopup.Style.WeaponDamage, -1f });
                if (held.GetValue(null) != second) throw new InvalidOperationException("Voxel hit displaced the held non-voxel number");
                Equal((float)elapsed.GetValue(second), 0, "Voxel hits must not interrupt the non-voxel hold");
                elapsed.SetValue(second, (float)hold.GetValue(second) + .1f);
                float movingLifetime = (float)lifetime.GetValue(second);
                begin.Invoke(first, new object[] { Vector3.zero, 10, VoxelScorePopup.Style.NearMiss, 2f });
                Equal((float)lifetime.GetValue(second), movingLifetime, "Already-moving number keeps its flight timing");
                first.gameObject.SetActive(false);
                // This non-ExecuteAlways fixture has no runtime lifecycle in Edit Mode.
                typeof(VoxelScorePopup).GetMethod("OnDisable", flags).Invoke(first, null);
                if (held.GetValue(null) != null) throw new InvalidOperationException("Disabled popup retained the shared hold slot");
                first.gameObject.SetActive(true);
                begin.Invoke(first, new object[] { Vector3.zero, 125, VoxelScorePopup.Style.EnemyDestroyed, 2f });
                begin.Invoke(second, new object[] { Vector3.zero, -2, VoxelScorePopup.Style.WeaponDamage, -1f });
                if (held.GetValue(null) != second) throw new InvalidOperationException("Civilian voxel penalty did not take the shared HUD slot");
                Equal((float)elapsed.GetValue(first), (float)hold.GetValue(first), "Civilian penalty sends the previous held score off early");
                var penaltyStart = (Vector3)typeof(VoxelScorePopup).GetField("lockedScreenPosition", flags).GetValue(second);
                float penaltyExtent = (float)typeof(VoxelScorePopup).GetMethod("GetHudPopupTopExtent", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { VoxelScorePopup.Style.WeaponDamage, true });
                Vector2 penaltyAnchor = mission.GetNonVoxelDamagePopupOrigin(penaltyExtent);
                Equal(Screen.height - penaltyStart.y, penaltyAnchor.y, "Negative voxel score uses the raised HUD origin");
                Equal((float)hold.GetValue(second), mission.Tuning.voxelDamagePopupHoldSeconds, "Civilian voxel score retains its own hold tuning");
                typeof(VoxelScorePopup).GetMethod("OnDisable", flags).Invoke(second, null);
            }
            finally { UnityEngine.Object.DestroyImmediate(secondObject); }
        }

        private static void ValidateSeparateMultiplierHolds(VoxelMissionProgress mission, VoxelMissionTuning tuning,
            System.Collections.IList flights, MethodInfo update, FieldInfo displayed)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            tuning.timeBonusCurrencyMultiplier = 3;
            tuning.voxelMultiplierPopupHoldSeconds = 2;
            tuning.destroyedVehicleMultiplierPopupHoldSeconds = 3;
            tuning.civilianVoxelPopupQuietSeconds = 0;
            for (int kind = 0; kind < 4; kind++)
            {
                mission.Configure(tuning);
                switch (kind)
                {
                    case 0: VoxelMissionProgress.ReportEnemyVoxelDestroyed(10, Vector3.zero); break;
                    case 1:
                        VoxelMissionProgress.ReportCivilianVoxelDestroyed(1, Vector3.zero);
                        typeof(VoxelMissionProgress).GetMethod("FlushVoxelPopup", flags).Invoke(mission, null); break;
                    case 2: VoxelMissionProgress.ReportEnemyVehicleDestroyed(); break;
                    default: VoxelMissionProgress.ReportCivilianVehicleDestroyed(); break;
                }
                Equal(flights.Count, 1, "Each signed event creates one multiplier flight");
                float hold = kind < 2 ? 2 : 3;
                object flight = flights[0]; var started = flight.GetType().GetField("startedAt");
                started.SetValue(flight, Time.unscaledTime - hold - .6f); flights[0] = flight;
                update.Invoke(mission, null);
                Equal(flights.Count, 1, "Signed event retains its category hold plus travel");
                Equal((float)displayed.GetValue(mission), 3, "Counter waits until category arrival");
                started.SetValue(flight, Time.unscaledTime - hold - .69f); flights[0] = flight;
                update.Invoke(mission, null);
                Equal(flights.Count, 0, "Signed event arrives on its category schedule");
                Equal((float)displayed.GetValue(mission), mission.EffectiveTimeBonusMultiplier, "Signed arrival updates counter");
                bool negative = (bool)typeof(VoxelMissionProgress).GetField("lastMultiplierWasNegative", flags).GetValue(mission);
                if (negative != (kind == 1 || kind == 3)) throw new InvalidOperationException("Signed arrival pulse lost its colour");
            }
            mission.Configure(tuning);
            VoxelMissionProgress.ReportEnemyVehicleDestroyed(); // Longer hold created first.
            VoxelMissionProgress.ReportEnemyVoxelDestroyed(10, Vector3.zero);
            for (int i = 0; i < flights.Count; i++)
            {
                object flight = flights[i]; flight.GetType().GetField("startedAt").SetValue(flight, Time.unscaledTime - 2.8f); flights[i] = flight;
            }
            update.Invoke(mission, null);
            Equal(flights.Count, 1, "Long vehicle hold must not block a later voxel arrival");
            if ((string)flights[0].GetType().GetField("reason").GetValue(flights[0]) != "ENEMY DESTROYED")
                throw new InvalidOperationException("Wrong category arrived first");
            Equal((float)displayed.GetValue(mission), 3.01f, "Short arrival must not display the longer-held reward early");
            object remaining = flights[0]; remaining.GetType().GetField("startedAt").SetValue(remaining, Time.unscaledTime - 3.69f);
            flights[0] = remaining; update.Invoke(mission, null);
            Equal((float)displayed.GetValue(mission), mission.EffectiveTimeBonusMultiplier, "Last arrival displays the combined multiplier");
            tuning.timeBonusCurrencyMultiplier = 1; tuning.voxelMultiplierPopupHoldSeconds = .17f;
            tuning.destroyedVehicleMultiplierPopupHoldSeconds = .17f; tuning.civilianVoxelPopupQuietSeconds = 1;
            mission.Configure(tuning);
        }

        private static void Equal(float actual, float expected, string label)
        {
            if (Mathf.Abs(actual - expected) > .001f) throw new InvalidOperationException(label + ": " + actual + " != " + expected);
        }
    }
}
