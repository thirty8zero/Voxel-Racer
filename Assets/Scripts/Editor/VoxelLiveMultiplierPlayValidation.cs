using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Checks the real frame clock, pause, decay pulse and paused reward reveal.</summary>
    public static class VoxelLiveMultiplierPlayValidation
    {
        private const string Pending = "VoxelRacer.LiveMultiplierPlayCheck";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameObject root;
        private static VoxelMissionTuning tuning;
        private static VoxelMissionProgress mission, previousMission;
        private static VoxelPostRaceContinue rewards;
        private static VoxelMissionTimerDisplay timer;
        private static float previousScale, stageStarted, started;
        private static int cash, phase;
        private static bool ownsState;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Validate Live Multiplier in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start from Edit Mode.");
            SessionState.SetBool(Pending + ".Background", Application.runInBackground);
            SessionState.SetBool(Pending + ".RestoreBackground", true);
            Application.runInBackground = true;
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending + ".RestoreBackground", false))
            {
                Application.runInBackground = SessionState.GetBool(Pending + ".Background", false);
                SessionState.SetBool(Pending + ".RestoreBackground", false);
            }
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(Pending, false);
                Cleanup();
            }
        }

        private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        private static void Equal(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .001f, message + ": " + actual);
        private static void Setup()
        {
            try
            {
                previousScale = Time.timeScale; previousMission = VoxelMissionProgress.Active;
                cash = VoxelCurrencyState.Balance; ownsState = true;
                root = new GameObject("Temporary live multiplier QA");
                tuning = ScriptableObject.CreateInstance<VoxelMissionTuning>();
                tuning.timeBonusCurrencyMultiplier = 2; tuning.timeLimitSeconds = 120;
                tuning.requiredPoints = 100000; tuning.completionCurrencyAward = 100;
                mission = root.AddComponent<VoxelMissionProgress>(); mission.Configure(tuning);
                var gate = new GameObject("Inactive opening gate"); gate.transform.SetParent(root.transform); gate.SetActive(false);
                mission.SetStartCountdown(gate.AddComponent<VoxelStartCountdown>());
                mission.AddMultiplierBonus(1); mission.AdvanceBonusClock(200);
                Time.timeScale = 1; phase = 0; started = stageStarted = Time.realtimeSinceStartup;
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void Next(int next) { phase = next; stageStarted = Time.realtimeSinceStartup; }
        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            if (Time.realtimeSinceStartup - started > 20) { Finish("FAIL: frame validation timed out."); return; }
            try
            {
                switch (phase)
                {
                    case 0:
                        if (Time.realtimeSinceStartup - stageStarted < .3f) return;
                        Equal(mission.RemainingTime, 120, "Opening gate must freeze timer");
                        Equal(mission.EffectiveTimeBonusMultiplier, 2, "Opening gate must block gains/decay");
                        mission.SetStartCountdown(null); mission.AdvanceBonusClock(119.75f); Next(1); break;
                    case 1:
                        if (mission.TimeBonusAvailable) return;
                        Equal(mission.EffectiveTimeBonusMultiplier, 2, "Expiry must retain multiplier");
                        Time.timeScale = 0; Next(2); break;
                    case 2:
                        if (Time.realtimeSinceStartup - stageStarted < .35f) return;
                        Equal(mission.EffectiveTimeBonusMultiplier, 2, "Pause must freeze overtime decay");
                        Time.timeScale = 1; Next(3); break;
                    case 3:
                        if (mission.EffectiveTimeBonusMultiplier >= 2) return;
                        Equal(mission.EffectiveTimeBonusMultiplier, 1.9f, "Real Update must decay .1 per second");
                        CheckPulse(); Next(4); break;
                    case 4:
                        if (mission.EffectiveTimeBonusMultiplier >= 1.9f) return;
                        Equal(mission.EffectiveTimeBonusMultiplier, 1.8f, "Second overtime tick");
                        CheckPulse();
                        Time.timeScale = 0; tuning.requiredPoints = 1; mission.Configure(tuning);
                        mission.AdvanceBonusClock(90); mission.AddBonusCash(40);
                        VoxelMissionProgress.ReportEnemyVoxelDamage();
                        Equal(mission.EffectiveTimeBonusMultiplier, 2, "Completion time bonus changed mission multiplier");
                        Equal(mission.FinalRewardMultiplier, 2.3f, "Completion adds .01 per remaining second for payout");
                        Equal((float)typeof(VoxelMissionProgress).GetField("displayedMultiplierBonus", Private).GetValue(mission), 2, "Completion bonus changed original HUD");
                        Equal(VoxelCurrencyState.Balance, cash + 270, "Completion cash credited correctly");
                        rewards = root.AddComponent<VoxelPostRaceContinue>(); rewards.missionProgress = mission;
                        typeof(VoxelPostRaceContinue).GetMethod("BuildRewardSequence", Private).Invoke(rewards, null);
                        Next(5); break;
                    case 5:
                        typeof(VoxelPostRaceContinue).GetMethod("UpdateRewardSequence", Private).Invoke(rewards, null);
                        if (Time.realtimeSinceStartup - stageStarted < 1.65f) return;
                        var bonus = root.GetComponentsInChildren<Text>(true).First(t => t.name == "Time Bonus Reward");
                        Check(bonus.gameObject.activeSelf && bonus.text.Contains("MISSION MULTIPLIER 2.00x") && bonus.text.Contains("+0.30x") && bonus.text.Contains("FINAL MULTIPLIER 2.30x") && !bonus.text.Contains("SEC LEFT"), "Results do not explain mission multiplier, time addition and final multiplier");
                        Check(root.GetComponentsInChildren<Text>(true).First(t => t.name == "Total Mission Reward").gameObject.activeSelf, "Reward reveal must use unscaled time");
                        Equal(mission.EffectiveTimeBonusMultiplier, 2, "Completed mission multiplier must freeze");
                        Equal(mission.FinalRewardMultiplier, 2.3f, "Completed payout multiplier must freeze");
                        VoxelMissionProgress.ReportEnemyVoxelDamage();
                        Equal(VoxelCurrencyState.Balance, cash + 270, "Duplicate completion paid twice");
                        root.transform.Find("Mission Reward UI").gameObject.SetActive(false);
                        mission.Configure(tuning); mission.AdvanceBonusClock(114); mission.AddBonusTime(1);
                        timer=root.AddComponent<VoxelMissionTimerDisplay>();timer.Configure(mission);
                        Next(6); break;
                    case 6:
                        if(Time.realtimeSinceStartup-stageStarted<.1f)return;
                        Check(mission.TimeExtensionPulse>0,"Timer extension feedback must be live for the completion test");
                        VoxelMissionProgress.ReportEnemyVoxelDamage();
                        Equal(mission.RemainingTime,7,"Short-time completion clock changed");
                        var noCashRoot=new GameObject("Zero bonus cash results QA");noCashRoot.transform.SetParent(root.transform);
                        rewards=noCashRoot.AddComponent<VoxelPostRaceContinue>();rewards.missionProgress=mission;
                        typeof(VoxelPostRaceContinue).GetMethod("BuildRewardSequence",Private).Invoke(rewards,null);
                        Next(7); break;
                    case 7:
                        typeof(VoxelPostRaceContinue).GetMethod("UpdateRewardSequence",Private).Invoke(rewards,null);
                        if(Time.realtimeSinceStartup-stageStarted<1.1f)return;
                        var ring=(Image)typeof(VoxelMissionTimerDisplay).GetField("timerRing",Private).GetValue(timer);
                        Equal(ring.transform.localScale.x,1,"Completed timer still pulses");
                        var background=(Image)typeof(VoxelMissionTimerDisplay).GetField("backgroundRing",Private).GetValue(timer);
                        Equal(background.transform.localScale.x,1,"Completed timer background still pulses");
                        Check(!rewards.transform.Find("Mission Reward UI/Mission Reward Panel/Bonus Cash Panel").gameObject.activeSelf,"Zero cash bonus panel visible");
                        Check(rewards.GetComponentsInChildren<Text>(true).First(t=>t.name=="Total Mission Reward").gameObject.activeSelf,"Zero cash bonus left an empty reveal delay");
                        Finish("PASS: real Update expiry and consecutive .1 ticks, immediate red pulses/HUD changes, pause/opening gates, early completion preserves original HUD and mission multiplier, separate final multiplier/payout, results explain mission multiplier + time bonus = final multiplier, completed clock freeze, paused results reveal, no duplicate payout, no remaining-seconds caption, zero bonus cash hidden/skipped, completed timer stops warning and extension pulses.");break;
                }
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void CheckPulse()
        {
            Equal((float)typeof(VoxelMissionProgress).GetField("displayedMultiplierBonus", Private).GetValue(mission), mission.EffectiveTimeBonusMultiplier, "Decay HUD lagged");
            Check((float)typeof(VoxelMissionProgress).GetField("multiplierPulseUntil", Private).GetValue(mission) > Time.unscaledTime, "Decay did not pulse");
            Check((bool)typeof(VoxelMissionProgress).GetField("lastMultiplierWasNegative", Private).GetValue(mission), "Decay pulse must be red");
        }

        private static void Finish(string report)
        {
            EditorApplication.update -= Tick; Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/LiveMultiplierPlayValidation.txt", report);
            if (report.StartsWith("PASS")) Debug.Log(report); else Debug.LogError(report);
            Cleanup(); EditorApplication.isPlaying = false;
        }

        private static void Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (tuning != null) Object.DestroyImmediate(tuning);
            root = null; tuning = null; mission = null; rewards = null; timer = null;
            if (!ownsState) return;
            Time.timeScale = previousScale;
            typeof(VoxelMissionProgress).GetField("<Active>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousMission);
            VoxelCurrencyState.Reset(); VoxelCurrencyState.Add(cash); ownsState = false;
        }
    }
}
